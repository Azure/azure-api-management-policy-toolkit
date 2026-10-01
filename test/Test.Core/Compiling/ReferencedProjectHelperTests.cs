// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.IoC;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class ReferencedProjectHelperTests
{
    private const string Usings =
        """
        using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
        using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

        """;

    private static readonly MetadataReference[] References =
    [
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(XElement).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(IDocument).Assembly.Location)
    ];

    [TestMethod]
    public void ShouldInlineHelperFromReferencedProject()
    {
        var result = Compile(
            """
            public static class Helpers
            {
                public static bool IsEnabled(IExpressionContext context) => Has(context, "x");
                private static bool Has(IExpressionContext context, string key) => context.Variables.ContainsKey(key);
            }
            """,
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("enabled", Lib.Helpers.IsEnabled(context.ExpressionContext));
                }
            }
            """);

        result.Should().BeSuccessful();
        result.Document.Descendants("set-variable").Single().Attribute("value")!.Value
            .Should().Be("@(context.Variables.ContainsKey(\"x\"))");
    }

    [TestMethod]
    public void ShouldSubstituteTypeArgumentsOfGenericHelperFromReferencedProject()
    {
        var result = Compile(
            """
            public static class Helpers
            {
                public static T Variable<T>(IExpressionContext context, string name)
                    => context.Variables.GetValueOrDefault<T>(name);
            }
            """,
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("value", Lib.Helpers.Variable<string>(context.ExpressionContext, "k"));
                }
            }
            """);

        result.Should().BeSuccessful();
        result.Document.Descendants("set-variable").Single().Attribute("value")!.Value
            .Should().Be("@(context.Variables.GetValueOrDefault<string>(\"k\"))");
    }

    [TestMethod]
    public void ShouldCompileConfigFactoryWithExpressionFromReferencedProject()
    {
        var result = Compile(
            """
            public static class Limits
            {
                public static RateLimitByKeyConfig PerProduct(IInboundContext context) => new RateLimitByKeyConfig()
                {
                    Calls = 100,
                    RenewalPeriod = 10,
                    CounterKey = Key(context.ExpressionContext)
                };

                private static string Key(IExpressionContext context) => context.Product.Name;
            }
            """,
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.RateLimitByKey(Lib.Limits.PerProduct(context));
                }
            }
            """);

        result.Should().BeSuccessful();
        result.Document.Descendants("rate-limit-by-key").Single().ToString()
            .Should().Be("<rate-limit-by-key calls=\"100\" renewal-period=\"10\" counter-key=\"@(context.Product.Name)\" />");
    }

    [TestMethod]
    public void ShouldReportNewerLanguageFeatureInReferencedProjectHelper()
    {
        var result = Compile(
            """
            public static class Helpers
            {
                public static string Kind(IExpressionContext context)
                    => context.Request.Method switch { "GET" => "read", _ => "write" };
            }
            """,
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("kind", Lib.Helpers.Kind(context.ExpressionContext));
                }
            }
            """);

        result.Errors.Should().Contain(error => error.Id == "APIM2019");
    }

    [TestMethod]
    public void ShouldReportHelperFromCompiledExpressionLibraryAndFoldItsConstants()
    {
        var result = Compile(
            """
            [Expression]
            public static class Helpers
            {
                public const string Key = "k";
                public static bool IsEnabled(IExpressionContext context) => context.Variables.ContainsKey(Key);
            }
            """,
            """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("key", Key(context.ExpressionContext));
                    context.SetVariable("enabled", Enabled(context.ExpressionContext));
                }

                private static string Key(IExpressionContext context) => Lib.Helpers.Key + "x";
                private static bool Enabled(IExpressionContext context) => Lib.Helpers.IsEnabled(context) && true;
            }
            """,
            compiledLibrary: true);

        result.Errors.Should().ContainSingle(error => error.Id == "APIM2013");
        result.Document.Descendants("set-variable").First().Attribute("value")!.Value.Should().Be("@(\"k\" + \"x\")");
    }

    [TestMethod]
    [DataRow("Lib.Helpers.ReadOnlyKey + \"x\"", "APIM2018", DisplayName = "static readonly field")]
    [DataRow("Lib.Helpers.PropertyKey + \"x\"", "APIM2018", DisplayName = "static property")]
    [DataRow("Lib.Plain.Suffix(\"x\")", "APIM2013", DisplayName = "[Expression] method of an unmarked class")]
    public void ShouldReportMembersOfCompiledExpressionLibrary(string expression, string expectedId)
    {
        var result = Compile(
            """
            [Expression]
            public static class Helpers
            {
                public static readonly string ReadOnlyKey = "k";
                public static string PropertyKey => "k";
            }

            public static class Plain
            {
                [Expression]
                public static string Suffix(string value) => value + "!";
            }
            """,
            $$"""
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.SetVariable("key", Key(context.ExpressionContext));
                }

                private static string Key(IExpressionContext context) => {{expression}};
            }
            """,
            compiledLibrary: true);

        result.Errors.Should().ContainSingle().Which.Id.Should().Be(expectedId);
    }

    private static IDocumentCompilationResult Compile(string library, string document, bool compiledLibrary = false)
    {
        var libraryCompilation = CSharpCompilation.Create(
            "Library",
            [CSharpSyntaxTree.ParseText($"{Usings}namespace Lib;\n{library}")],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        MetadataReference libraryReference = libraryCompilation.ToMetadataReference();
        if (compiledLibrary)
        {
            using var image = new MemoryStream();
            var runtime = Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "System.Runtime.dll");
            var emit = libraryCompilation.AddReferences(MetadataReference.CreateFromFile(runtime)).Emit(image);
            emit.Success.Should().BeTrue(string.Join(Environment.NewLine, emit.Diagnostics));
            libraryReference = MetadataReference.CreateFromImage(image.ToArray());
        }

        var tree = CSharpSyntaxTree.ParseText($"{Usings}namespace Test;\n{document}");
        var compilation = CSharpCompilation.Create(
            "Policies",
            [tree],
            [.. References, libraryReference]);
        var model = compilation.GetSemanticModel(tree);
        var policy = tree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(declaration => declaration.AttributeLists.ContainsAttributeOfType<DocumentAttribute>(model));

        using var serviceProvider = new ServiceCollection().SetupCompiler().BuildServiceProvider();
        return serviceProvider.GetRequiredService<DocumentCompiler>().Compile(compilation, policy);
    }
}
