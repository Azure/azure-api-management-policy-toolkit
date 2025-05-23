// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class AuthenticationManagedIdentityTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) 
            { 
                context.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig { Resource = "resource" });
            }
            public void Outbound(IOutboundContext context) 
            { 
                context.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig { Resource = "resource" });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <authentication-managed-identity resource="resource" />
            </inbound>
            <outbound>
                <authentication-managed-identity resource="resource" />
            </outbound>
        </policies>
        """,
        DisplayName = "Should compile authentication manage identity policy in sections"
    )]
    public void ShouldCompileAuthenticationManagedIdentityPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}