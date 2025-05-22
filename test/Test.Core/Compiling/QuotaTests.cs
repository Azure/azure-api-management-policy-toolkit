// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class QuotaTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.Quota(new QuotaConfig()
                    {
                        Calls = 100,
                        RenewalPeriod = 60,
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <quota calls="100" renewal-period="60" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile quota policy with calls"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.Quota(new QuotaConfig()
                    {
                        Bandwidth = 1000,
                        RenewalPeriod = 60,
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <quota bandwidth="1000" renewal-period="60" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile quota policy with bandwidth"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.Quota(new QuotaConfig()
                    {
                        Calls = 100,
                        Bandwidth = 1000,
                        RenewalPeriod = 60,
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <quota calls="100" bandwidth="1000" renewal-period="60" />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile quota policy with calls and bandwidth"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.Quota(new QuotaConfig()
                    {
                        Calls = 100,
                        Bandwidth = 1000,
                        RenewalPeriod = 60,
                        Apis = [
                            new ApiQuota()
                            {
                                Name = "api-1",
                                Calls = 10,
                            },
                            new ApiQuota()
                            {
                                Id = "api-2",
                                Bandwidth = 100,
                            }
                        ],
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <quota calls="100" bandwidth="1000" renewal-period="60">
                    <api name="api-1" calls="10" />
                    <api id="api-2" bandwidth="100" />
                </quota>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile quota policy with apis"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.Quota(new QuotaConfig()
                    {
                        Calls = 100,
                        Bandwidth = 1000,
                        RenewalPeriod = 60,
                        Apis = [
                            new ApiQuota()
                            {
                                Name = "api-1",
                                Calls = 10,
                                Operations = [
                                    new OperationQuota()
                                    {
                                        Name = "operation-1",
                                        Calls = 5,
                                    },
                                    new OperationQuota()
                                    {
                                        Id = "operation-2",
                                        Bandwidth = 50,
                                    },
                                ],
                            },
                        ],
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <quota calls="100" bandwidth="1000" renewal-period="60">
                    <api name="api-1" calls="10">
                        <operation name="operation-1" calls="5" />
                        <operation id="operation-2" bandwidth="50" />
                    </api>
                </quota>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile quota policy with apis and operations"
    )]
    public void ShouldCompileQuotaPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}