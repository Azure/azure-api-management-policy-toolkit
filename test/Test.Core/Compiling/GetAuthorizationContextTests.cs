using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Compiling;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Azure.Api.Management.PolicyToolkit.Tests.Compiling
{
    [TestClass]
    public class GetAuthorizationContextTests
    {
        [DataTestMethod]
        [DataRow(
            @"
                using Azure.Api.Management.PolicyToolkit.Authoring;

                public class TestDocument : IDocument
                {
                    public void Inbound(IInboundContext context)
                    {
                        context.GetAuthorizationContext(new GetAuthorizationContextConfig
                        {
                            ProviderId = ""provider-id"",
                            AuthorizationId = ""authorization-id"",
                            ContextVariableName = ""context-variable-name""
                        });
                    }
                }
            ",
            "provider-id", "authorization-id", "context-variable-name", null, null, null)]
        [DataRow(
            @"
                using Azure.Api.Management.PolicyToolkit.Authoring;

                public class TestDocument : IDocument
                {
                    public void Inbound(IInboundContext context)
                    {
                        context.GetAuthorizationContext(new GetAuthorizationContextConfig
                        {
                            ProviderId = ""provider-id"",
                            AuthorizationId = ""authorization-id"",
                            ContextVariableName = ""context-variable-name"",
                            IdentityType = ""jwt"",
                            Identity = ""jwt-token"",
                            IgnoreError = true
                        });
                    }
                }
            ",
            "provider-id", "authorization-id", "context-variable-name", "jwt", "jwt-token", "true")]
        [DataRow(
            @"
                using Azure.Api.Management.PolicyToolkit.Authoring;

                public class TestDocument : IDocument
                {
                    public void Inbound(IInboundContext context)
                    {
                        context.GetAuthorizationContext(new GetAuthorizationContextConfig
                        {
                            ProviderId = ""@(context.Variables[""provider-id""])"",
                            AuthorizationId = ""@(context.Variables[""authorization-id""])"",
                            ContextVariableName = ""@(context.Variables[""context-variable-name""])""
                        });
                    }
                }
            ",
            "@(context.Variables[\"provider-id\"])", "@(context.Variables[\"authorization-id\"])", "@(context.Variables[\"context-variable-name\"])", null, null, null)]
        public void Compile_GetAuthorizationContextPolicy(string code, string expectedProviderId, string expectedAuthorizationId, string expectedContextVariableName, string expectedIdentityType, string expectedIdentity, string expectedIgnoreError)
        {
            // Arrange
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var document = syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
            var compiler = new CSharpPolicyCompiler(document);

            // Act
            var result = compiler.Compile();

            // Assert
            Assert.AreEqual(0, result.Diagnostics.Count());
            var policy = result.Document.Element("inbound").Element("get-authorization-context");
            Assert.IsNotNull(policy);
            Assert.AreEqual(expectedProviderId, policy.Attribute("provider-id").Value);
            Assert.AreEqual(expectedAuthorizationId, policy.Attribute("authorization-id").Value);
            Assert.AreEqual(expectedContextVariableName, policy.Attribute("context-variable-name").Value);
            if (expectedIdentityType != null)
            {
                Assert.AreEqual(expectedIdentityType, policy.Attribute("identity-type").Value);
            }
            if (expectedIdentity != null)
            {
                Assert.AreEqual(expectedIdentity, policy.Attribute("identity").Value);
            }
            if (expectedIgnoreError != null)
            {
                Assert.AreEqual(expectedIgnoreError, policy.Attribute("ignore-error").Value);
            }
        }

        [TestMethod]
        public void Compile_GetAuthorizationContextPolicy_WithMissingRequiredProperties()
        {
            // Arrange
            var code = @"
                using Azure.Api.Management.PolicyToolkit.Authoring;

                public class TestDocument : IDocument
                {
                    public void Inbound(IInboundContext context)
                    {
                        context.GetAuthorizationContext(new GetAuthorizationContextConfig
                        {
                            ProviderId = ""provider-id""
                        });
                    }
                }
            ";

            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var document = syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
            var compiler = new CSharpPolicyCompiler(document);

            // Act
            var result = compiler.Compile();

            // Assert
            Assert.AreEqual(1, result.Diagnostics.Count());
            var diagnostic = result.Diagnostics.First();
            Assert.AreEqual("APIM2008", diagnostic.Id);
            Assert.IsTrue(diagnostic.GetMessage().Contains("Required 'AuthorizationId' parameter was not defined for 'get-authorization-context' policy"));
        }

        [TestMethod]
        public void Compile_GetAuthorizationContextPolicy_WithInvalidValues()
        {
            // Arrange
            var code = @"
                using Azure.Api.Management.PolicyToolkit.Authoring;

                public class TestDocument : IDocument
                {
                    public void Inbound(IInboundContext context)
                    {
                        context.GetAuthorizationContext(new GetAuthorizationContextConfig
                        {
                            ProviderId = ""provider-id"",
                            AuthorizationId = ""authorization-id"",
                            ContextVariableName = ""context-variable-name"",
                            IdentityType = ""invalid-identity-type""
                        });
                    }
                }
            ";

            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var document = syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
            var compiler = new CSharpPolicyCompiler(document);

            // Act
            var result = compiler.Compile();

            // Assert
            Assert.AreEqual(1, result.Diagnostics.Count());
            var diagnostic = result.Diagnostics.First();
            Assert.AreEqual("APIM2009", diagnostic.Id);
            Assert.IsTrue(diagnostic.GetMessage().Contains("Invalid value 'invalid-identity-type' for parameter 'IdentityType' in 'get-authorization-context' policy"));
        }
    }
}
