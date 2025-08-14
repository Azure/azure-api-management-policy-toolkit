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
                <set-header name="X-Test" exists-action="override">
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
                <set-header name="X-Test" exists-action="override">
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
            <set-header name="X-Fragment" exists-action="override">
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
            <set-header name="X-Named-Fragment" exists-action="override">
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
}
