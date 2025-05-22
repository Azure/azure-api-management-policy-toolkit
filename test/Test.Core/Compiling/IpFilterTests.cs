// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class IpFilterTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.IpFilter(new IpFilterConfig()
                    {
                        Action = "allow",
                        Addresses = ["13.66.201.169", "192.168.1.1"],
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <ip-filter action="allow">
                    <address>13.66.201.169</address>
                    <address>192.168.1.1</address>
                </ip-filter>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile ip-filter policy with addresses"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.IpFilter(new IpFilterConfig()
                    {
                        Action = "allow",
                        AddressRanges = [
                            new AddressRange() {
                                From = "10.0.0.1",
                                To = "10.0.0.255",
                            },
                            new AddressRange() {
                                From = "11.1.1.1",
                                To = "11.1.1.255",
                            }
                        ],
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <ip-filter action="allow">
                    <address-range from="10.0.0.1" to="10.0.0.255" />
                    <address-range from="11.1.1.1" to="11.1.1.255" />
                </ip-filter>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile ip-filter policy with address ranges"
    )]
    public void ShouldCompileIpFilterPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}