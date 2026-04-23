// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class IncludeFragmentTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { context.IncludeFragment("fragment-inbound"); }
            public void Outbound(IOutboundContext context) { context.IncludeFragment("fragment-outbound"); }
            public void Backend(IBackendContext context) { context.IncludeFragment("fragment-backend"); }
            public void OnError(IOnErrorContext context) { context.IncludeFragment("fragment-on-error"); }
        }
        """,
        """
        <policies>
            <inbound>
                <include-fragment fragment-id="fragment-inbound" />
            </inbound>
            <outbound>
                <include-fragment fragment-id="fragment-outbound" />
            </outbound>
            <backend>
                <include-fragment fragment-id="fragment-backend" />
            </backend>
            <on-error>
                <include-fragment fragment-id="fragment-on-error" />
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile include-fragment policy in sections"
    )]
    public void ShouldCompileIncludeFragmentPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { context.IncludeFragment(new InboundFragment { XFragmentValue = "inbound-value" }); }
            public void Outbound(IOutboundContext context) { context.IncludeFragment(new OutboundFragment { XFragmentValue = "outbound-value" }); }
            public void Backend(IBackendContext context) { context.IncludeFragment(new BackendFragment()); }
            public void OnError(IOnErrorContext context) { context.IncludeFragment(new OnErrorFragment { XFragmentValue = "on-error-value" }); }
        }
        """,
        """
        [Document( Type = DocumentType.Fragment )] 
        public class InboundFragment : IFragment
        {
            [FragmentVariable]
            public string XFragmentValue { get; set; };

            public void Fragment(IFragmentContext context)
            {
                context.SetHeader("X-Fragment", XFragmentValue);
            }
        }

        [Document("my-fragment", Type = DocumentType.Fragment )] 
        public class OutboundFragment : IFragment
        {
            [FragmentVariable]
            public string XFragmentValue { get; set; };

            public void Fragment(IFragmentContext context)
            {
                context.SetHeader("X-Fragment", XFragmentValue);
            }
        }

        [Document( Type = DocumentType.Fragment )] 
        public class BackendFragment : IFragment
        {
            [FragmentVariable]
            public string XFragmentValue { get; set; };

            public void Fragment(IFragmentContext context)
            {
                context.SetHeader("X-Fragment", XFragmentValue);
            }
        }

        [Document( Type = DocumentType.Fragment )] 
        public class OnErrorFragment : IFragment
        {
            [FragmentVariable("my-variable")]
            public string XFragmentValue { get; set; };

            public void Fragment(IFragmentContext context)
            {
                context.SetHeader("X-Fragment", XFragmentValue);
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-variable name="inbound-fragment-XFragmentValue" value="inbound-value" />
                <include-fragment fragment-id="inbound-fragment" />
            </inbound>
            <outbound>
                <set-variable name="my-fragment-XFragmentValue" value="outbound-value" />
                <include-fragment fragment-id="my-fragment" />
            </outbound>
            <backend>
                <include-fragment fragment-id="backend-fragment" />
            </backend>
            <on-error>
                <set-variable name="on-error-fragment-my-variable" value="on-error-value" />
                <include-fragment fragment-id="on-error-fragment" />
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile include-fragment policy using objects with variables in sections"
    )]
    public void ShouldCompileIncludeFragmentPolicyViaObject(string code, string fragment, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}