// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class CrossDomainTests
{
    [TestMethod]
    [DataRow(
        """"
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context)
            {
                context.CrossDomain("""
                                    <cross-domain-policy>
                                        <site-control permitted-cross-domain-policies="master-only"/>
                                        <allow-access-from domain="*.example.com"/>
                                        <allow-access-from domain="www.example.com"/>
                                        <allow-http-request-headers-from domain="*.adobe.com" headers="SOAPAction"/>
                                    </cross-domain-policy>
                                    """);
            }
        }
        """",
        """
        <policies>
            <inbound>
                <cross-domain>
                    <cross-domain-policy>
                        <site-control permitted-cross-domain-policies="master-only" />
                        <allow-access-from domain="*.example.com" />
                        <allow-access-from domain="www.example.com" />
                        <allow-http-request-headers-from domain="*.adobe.com" headers="SOAPAction" />
                    </cross-domain-policy>
                </cross-domain>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile cross-domain policy"
    )]
    public void ShouldCompileIncludeFragmentPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}