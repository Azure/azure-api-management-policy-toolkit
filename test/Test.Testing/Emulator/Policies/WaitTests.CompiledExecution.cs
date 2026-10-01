// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.Loader;
using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;
using Microsoft.Azure.ApiManagement.PolicyToolkit.IoC;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;

namespace Test.Emulator.Emulator.Policies;

public partial class WaitTests
{
    private static readonly MetadataReference[] s_reviewCompilationReferences = CreateReviewCompilationReferences();

    [TestMethod]
    [DataRow(nameof(IInboundContext), false)]
    [DataRow(nameof(IInboundContext), true)]
    [DataRow(nameof(IBackendContext), false)]
    [DataRow(nameof(IBackendContext), true)]
    [DataRow(nameof(IOutboundContext), false)]
    [DataRow(nameof(IOutboundContext), true)]
    [DataRow(nameof(IOnErrorContext), false)]
    [DataRow(nameof(IOnErrorContext), true)]
    public void WaitReview_CompilesAndExecutesChooseSetVariableAndCacheLookup(string section, bool callback)
    {
        using var scenario = CompileReviewScenario(section, """
            context.Wait("all", branch =>
            {
                if (Enabled(branch.ExpressionContext))
                {
                    branch.WithId("selected-id").SetVariable("selected", "branch");
                    branch.WithId("lookup-id").CacheLookupValue(new CacheLookupValueConfig
                    {
                        Key = SelectedKey(branch.ExpressionContext),
                        VariableName = "cached",
                        CachingType = "internal"
                    });
                }
            });
            """);
        var when = ReviewCompiledWhen(scenario.Policy, section);
        when.Elements().Select(element => element.Name.LocalName)
            .Should().Equal("set-variable", "cache-lookup-value");
        when.Element("set-variable")!.Attribute("id")!.Value.Should().Be("selected-id");
        when.Element("cache-lookup-value")!.Attribute("id")!.Value.Should().Be("lookup-id");
        var test = scenario.Document.AsTestDocument();
        test.Context.Variables["enabled"] = true;
        test.Context.Variables["opaque"] = new object();
        test.SetupCacheStore().WithInternalCacheValue("branch", "cached value");
        var callbacks = 0;
        if (callback)
        {
            SetupWaitCache(test, section, (_, _) => true).WithCallback((gateway, config) =>
            {
                gateway.Should().NotBeSameAs(test.Context);
                gateway.Variables["selected"].Should().Be("branch");
                config.Key.Should().Be("branch");
                callbacks++;
                gateway.Variables[config.VariableName] = "callback value";
            });
        }

        RunSection(test, section);

        callbacks.Should().Be(callback ? 1 : 0);
        test.Context.Variables["selected"].Should().Be("branch");
        test.Context.Variables["cached"].Should().Be(callback ? "callback value" : "cached value");
    }

    [TestMethod]
    [DataRow("messages")]
    [DataRow("limits")]
    [DataRow("retry-fragment")]
    [DataRow("identity")]
    [DataRow("telemetry")]
    public void WaitReview_CompilesAndExecutesOtherNestedHandlerFamilies(string family)
    {
        var body = family switch
        {
            "messages" => """
                branch.SetVariable("local", "set");
                branch.SetHeader("X-Compiled", "header");
                branch.SetBody("compiled body");
                """,
            "limits" => """
                branch.RateLimit(new RateLimitConfig { Calls = 10, RenewalPeriod = 60 });
                branch.QuotaByKey(new QuotaByKeyConfig { CounterKey = "compiled", Calls = 10, RenewalPeriod = 60 });
                """,
            "retry-fragment" => """
                branch.Retry(new RetryConfig { Condition = false, Count = 1, Interval = 1 }, () =>
                {
                    branch.IncludeFragment("compiled-fragment");
                    branch.SetVariable("retried", true);
                });
                """,
            "identity" => """
                branch.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig
                {
                    Resource = "https://resource.example", OutputTokenVariableName = "token"
                });
                """,
            "telemetry" => """
                branch.Trace(new TraceConfig { Source = "compiled", Message = "trace" });
                branch.LogToEventHub(new LogToEventHubConfig { LoggerId = "logger", Value = "event" });
                """,
            _ => throw new ArgumentException("Unknown family.", nameof(family))
        };
        using var scenario = CompileReviewScenario(nameof(IInboundContext), $$"""
            context.Wait("all", branch =>
            {
                if (Enabled(branch.ExpressionContext))
                {
                    {{body}}
                }
            });
            """, includeFragment: family == "retry-fragment");
        var names = ReviewCompiledWhen(scenario.Policy, nameof(IInboundContext)).Elements()
            .Select(element => element.Name.LocalName);
        names.Should().Equal(family switch
        {
            "messages" => ["set-variable", "set-header", "set-body"],
            "limits" => ["rate-limit", "quota-by-key"],
            "retry-fragment" => ["retry"],
            "identity" => ["authentication-managed-identity"],
            "telemetry" => ["trace", "log-to-eventhub"],
            _ => throw new ArgumentException("Unknown family.", nameof(family))
        });
        var test = scenario.Document.AsTestDocument();
        test.Context.Variables["enabled"] = true;
        if (scenario.Fragment is { } fragment)
        {
            scenario.FragmentPolicy!.Elements().Select(element => element.Name.LocalName)
                .Should().Equal("set-variable");
            test.RegisterFragment("compiled-fragment", fragment);
        }
        test.SetupInbound().AuthenticationManagedIdentity().ReturnsToken("preserved-token");
        var logger = test.SetupLoggerStore().Add("logger");

        test.RunInbound();

        switch (family)
        {
            case "messages":
                test.Context.Variables["local"].Should().Be("set");
                test.Context.Request.Headers["X-Compiled"].Should().Equal("header");
                test.Context.Request.Body.Content.Should().Be("compiled body");
                break;
            case "limits":
                test.SetupRateLimitStore().GetCount($"sub:{test.Context.Subscription.Id}").Should().Be(1);
                test.SetupRateLimitStore().GetCount("quota-by-key:compiled").Should().Be(1);
                break;
            case "retry-fragment":
                test.Context.Variables["included"].Should().Be("fragment value");
                test.Context.Variables["retried"].Should().Be(true);
                break;
            case "identity":
                test.Context.Variables["token"].Should().Be("preserved-token");
                break;
            case "telemetry":
                test.SetupLoggerStore().Traces.Should().HaveCount(1);
                logger.Events.Should().HaveCount(1);
                break;
        }
    }

    private static XElement ReviewCompiledWhen(XElement policy, string section)
    {
        var name = ReviewSectionName(section);
        var wait = policy.Element(name)!.Elements().Should().ContainSingle().Which;
        wait.Name.LocalName.Should().Be("wait");
        wait.Attribute("for")!.Value.Should().Be("all");
        var child = wait.Elements().Should().ContainSingle().Which;
        child.Name.LocalName.Should().Be("choose");
        return child.Elements("when").Should().ContainSingle().Which;
    }

    private static string ReviewSectionName(string section) => section switch
    {
        nameof(IInboundContext) => "inbound",
        nameof(IBackendContext) => "backend",
        nameof(IOutboundContext) => "outbound",
        nameof(IOnErrorContext) => "on-error",
        _ => throw new ArgumentException("Unknown section.", nameof(section))
    };

    private static MetadataReference[] CreateReviewCompilationReferences()
    {
        var assemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string
            ?? throw new InvalidOperationException("Runtime assembly references are unavailable.");
        return assemblies.Split(Path.PathSeparator).Append(typeof(IDocument).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path)).ToArray();
    }

    private static CompiledReviewScenario CompileReviewScenario(string section, string body, bool includeFragment = false)
    {
        var methods = new[]
        {
            (Section: nameof(IInboundContext), Method: "Inbound"),
            (Section: nameof(IBackendContext), Method: "Backend"),
            (Section: nameof(IOutboundContext), Method: "Outbound"),
            (Section: nameof(IOnErrorContext), Method: "OnError")
        };
        var source = $$"""
            using System;
            using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
            using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

            [Document]
            public class CompiledWaitScenario : IDocument
            {
                {{string.Join(Environment.NewLine, methods.Select(method =>
                    $"public void {method.Method}({method.Section} context) {{ {(method.Section == section ? body : "")} }}"))}}

                bool Enabled(IExpressionContext context) => (bool)context.Variables["enabled"];
                string SelectedKey(IExpressionContext context) => (string)context.Variables["selected"];
            }
            """;
        if (includeFragment)
        {
            source += """

                [Document("compiled-fragment", Type = DocumentType.Fragment)]
                public class CompiledIncludedFragment : IFragment
                {
                    public void Fragment(IFragmentContext context)
                    {
                        context.SetVariable("included", "fragment value");
                    }
                }
                """;
        }
        var syntax = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create($"WaitReview_{Guid.NewGuid():N}", [syntax],
            s_reviewCompilationReferences, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty("the same scenario must compile to executable C# and policy XML");
        var classes = syntax.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();
        using var provider = new ServiceCollection().SetupCompiler().BuildServiceProvider();
        var compiler = provider.GetRequiredService<DocumentCompiler>();
        var result = compiler.Compile(compilation, classes.Single(type => type.Identifier.ValueText == "CompiledWaitScenario"));
        result.Errors.Should().BeEmpty("the typed Wait choose branch must be approved by the policy compiler");
        XElement? fragmentPolicy = null;
        if (includeFragment)
        {
            var fragmentResult = compiler.Compile(compilation,
                classes.Single(type => type.Identifier.ValueText == "CompiledIncludedFragment"));
            fragmentResult.Errors.Should().BeEmpty();
            fragmentPolicy = fragmentResult.Document;
        }
        using var assemblyStream = new MemoryStream();
        var emitted = compilation.Emit(assemblyStream);
        emitted.Success.Should().BeTrue("the compiler-approved scenario must also emit a runnable document: {0}",
            string.Join(Environment.NewLine, emitted.Diagnostics));
        assemblyStream.Position = 0;
        var loader = new AssemblyLoadContext(compilation.AssemblyName, isCollectible: true);
        try
        {
            var assembly = loader.LoadFromStream(assemblyStream);
            var document = Activator.CreateInstance(assembly.GetType("CompiledWaitScenario", throwOnError: true)!)
                as IDocument ?? throw new InvalidOperationException("The emitted document does not implement IDocument.");
            var fragment = includeFragment
                ? Activator.CreateInstance(assembly.GetType("CompiledIncludedFragment", throwOnError: true)!) as IFragment
                    ?? throw new InvalidOperationException("The emitted fragment does not implement IFragment.")
                : null;
            return new CompiledReviewScenario(loader, document, result.Document, fragment, fragmentPolicy);
        }
        catch
        {
            loader.Unload();
            throw;
        }
    }

    private sealed class CompiledReviewScenario(
        AssemblyLoadContext loader, IDocument document, XElement policy, IFragment? fragment, XElement? fragmentPolicy)
        : IDisposable
    {
        internal IDocument Document { get; } = document;
        internal XElement Policy { get; } = policy;
        internal IFragment? Fragment { get; } = fragment;
        internal XElement? FragmentPolicy { get; } = fragmentPolicy;
        public void Dispose() => loader.Unload();
    }
}