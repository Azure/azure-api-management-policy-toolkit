// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class DocumentTypeTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SetHeader("X-Test", "value");
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-header name="X-Test">
                    <value>value</value>
                </set-header>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile regular policy document with policies root"
    )]
    [DataRow(
        """
        [Document( Type = DocumentType.Policy )]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.SetHeader("X-Test", "value");
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-header name="X-Test">
                    <value>value</value>
                </set-header>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile explicit policy document with policies root"
    )]
    [DataRow(
        """
        [Document( Type = DocumentType.Fragment )] 
        public class PolicyFragment : IFragment
        {
            public void Fragment(IFragmentContext context)
            {
                context.SetHeader("X-Fragment", "fragment-value");
            }
        }
        """,
        """
        <fragment>
            <set-header name="X-Fragment">
                <value>fragment-value</value>
            </set-header>
        </fragment>
        """,
        DisplayName = "Should compile policy fragment with fragment root using Fragment method"
    )]
    [DataRow(
        """
        [Document("my-fragment", Type = DocumentType.Fragment)]
        public class NamedPolicyFragment : IFragment
        {
            public void Fragment(IFragmentContext context)
            {
                context.SetHeader("X-Named-Fragment", "named-value");
                context.Base();
            }
        }
        """,
        """
        <fragment>
            <set-header name="X-Named-Fragment">
                <value>named-value</value>
            </set-header>
            <base />
        </fragment>
        """,
        DisplayName = "Should compile named policy fragment with multiple policies using Fragment method"
    )]
    public void ShouldCompileDocumentWithCorrectType(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    [DataRow(
        """
        [Document(DocumentNames.Fragment, Type = DocumentType.Fragment)]
        public class NamedPolicyFragment : IFragment
        {
            public void Fragment(IFragmentContext context)
            {
                context.Base();
            }
        }

        public static class DocumentNames
        {
            public const string Fragment = "shared-fragment";
        }
        """,
        DisplayName = "Should resolve constant document name"
    )]
    [DataRow(
        """
        [Document(Prefix + "-fragment", Type = DocumentType.Fragment)]
        public class NamedPolicyFragment : IFragment
        {
            private const string Prefix = "shared";

            public void Fragment(IFragmentContext context)
            {
                context.Base();
            }
        }
        """,
        DisplayName = "Should resolve concatenated constant document name"
    )]
    [DataRow(
        """
        [Document(nameof(shared) + "-fragment", Type = DocumentType.Fragment)]
        public class NamedPolicyFragment : IFragment
        {
            private const string shared = "unused";

            public void Fragment(IFragmentContext context)
            {
                context.Base();
            }
        }
        """,
        DisplayName = "Should resolve nameof document name"
    )]
    public void ShouldResolveConstantDocumentName(string code)
    {
        var result = code.CompileDocument();

        result.Should().BeSuccessful();
        var tree = CSharpSyntaxTree.ParseText(
            $"using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;\nnamespace Test;\n{code}");
        var compilation = CSharpCompilation.Create(
            "Names",
            [tree],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IDocument).Assembly.Location)
            ]);
        var document = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(declaration => declaration.Identifier.ValueText == "NamedPolicyFragment");
        document.ExtractDocumentFileName(compilation.GetSemanticModel(tree)).Should().Be("shared-fragment");
    }

    [TestMethod]
    public void ShouldRejectEmptyDocumentName()
    {
        const string document =
            """
            [Document("", Type = DocumentType.Fragment)]
            public class NamedPolicyFragment : IFragment
            {
                public void Fragment(IFragmentContext context)
                {
                    context.Base();
                }
            }
            """;

        var result = document.CompileDocument();

        result.Errors.Should().ContainSingle(error => error.Id == "APIM2015");
    }

    [TestMethod]
    [DataRow("../policy", "The output path cannot contain parent directory segments")]
    [DataRow(@"..\policy.xml", "The output path cannot contain parent directory segments")]
    [DataRow("/policy", "The output path must be relative")]
    [DataRow(@"C:\policy.xml", "The output path must be relative")]
    public void ShouldRejectDocumentNameOutsideOutputFolder(string documentName, string reason)
    {
        var document =
            $$"""
              [Document(@"{{documentName}}")]
              public class PolicyDocument : IDocument
              {
                  public void Inbound(IInboundContext context)
                  {
                      context.Base();
                  }
              }
              """;

        var result = document.CompileDocument();

        result.Errors.Should().ContainSingle(error =>
            error.Id == "APIM2021" &&
            error.GetMessage() == $"Document name '{documentName}' is invalid: {reason}");
        result.Errors.Single().Location.GetLineSpan().StartLinePosition.Line.Should().Be(5);
    }

    [TestMethod]
    public void ShouldReadDocumentTypeFromTheAttributeNotTheClassName()
    {
        // The type was matched by looking for "Fragment" anywhere in the attribute's arguments.
        var result = """
                     [Document("MyFragmentThing", Type = DocumentType.Policy)]
                     public class MyFragmentThing : IDocument
                     {
                         public void Inbound(IInboundContext context) { context.Base(); }
                     }
                     """.CompileDocument();

        result.Should().BeSuccessful();
        result.Document.Name.LocalName.Should().Be("policies");
    }

    [TestMethod]
    public void ShouldReadDocumentTypeThroughAnAlias()
    {
        var result = """
                     using Kind = Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.DocumentType;

                     [Document("f", Type = Kind.Fragment)]
                     public class F : IFragment
                     {
                         public void Fragment(IFragmentContext context) { context.SetHeader("a", "b"); }
                     }
                     """.CompileDocument();

        result.Should().BeSuccessful();
        result.Document.Name.LocalName.Should().Be("fragment");
    }

    [TestMethod]
    public void ShouldReadDocumentTypeFromTheImplementedInterface()
    {
        var result = """
                     [Document("f")]
                     public class F : IFragment
                     {
                         public void Fragment(IFragmentContext context) { context.SetHeader("a", "b"); }
                     }
                     """.CompileDocument();

        result.Should().BeSuccessful();
        result.Document.Name.LocalName.Should().Be("fragment");
    }
}
