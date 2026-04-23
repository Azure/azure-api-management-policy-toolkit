// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
        [Document( Type = DocumentType.Fragment )] 
        public class PolicyFragment : IFragment
        {
            [FragmentVariable(DefaultValue = "fragment-fallback-value")]
            public string XFragmentValue { get; set; };

            public void Fragment(IFragmentContext context)
            {
                context.SetHeader("X-Fragment", XFragmentValue);
            }
        }
        """,
        """
        <fragment>
            <set-header name="X-Fragment">
                <value>@(context.Variables.GetValueOrDefault<string>("policy-fragment-XFragmentValue", "fragment-fallback-value"))</value>
            </set-header>
        </fragment>
        """,
        DisplayName = "Should compile policy fragment with fragment variables using Fragment method"
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
    [DataRow(
        """
        [Document("my-fragment", Type = DocumentType.Fragment)]
        public class NamedPolicyFragment : IFragment
        {
            [FragmentVariable("my-variable")]
            public string XNamedFragmentValue { get; set; };

            public void Fragment(IFragmentContext context)
            {
                context.SetHeader("X-Named-Fragment", XNamedFragmentValue);
            }
        }
        """,
        """
        <fragment>
            <set-header name="X-Named-Fragment">
                <value>@(context.Variables.GetValueOrDefault<string>("my-fragment-my-variable", ""))</value>
            </set-header>
            <base />
        </fragment>
        """,
        DisplayName = "Should compile named policy fragment with variables using Fragment method"
    )]
    public void ShouldCompileDocumentWithCorrectType(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}
