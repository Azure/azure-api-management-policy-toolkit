// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;

[TestClass]
public class TestDocumentTests
{
    [TestMethod]
    public void ShouldUseBasicAuthenticationForRequestsFromInternalIp()
    {
        var document = new OperationDocument();
        var test = new TestDocument(document) { Context = { Request = { IpAddress = "10.0.0.1" } } };

        test.RunInbound();

        var authValue = test.Context.Request
            .Headers.Should().ContainKey("Authorization")
            .WhoseValue.Should().ContainSingle().Subject;
        authValue.Should().StartWith("Basic ");
        var token = authValue["Basic ".Length..];
        token = Encoding.UTF8.GetString(Convert.FromBase64String(token));
        token.Should().Be("{{username}}:{{password}}");
    }

    [TestMethod]
    public void ShouldReturnResponseForInternalIpAndMockSubscription()
    {
        var document = new OperationDocument();
        var test = new TestDocument(document)
        {
            Context = { Request = { IpAddress = "10.0.0.1" }, Subscription = { Name = "asdfgh-mock" } }
        };

        test.RunInbound();

        var response = test.Context.Response;
        response.StatusCode.Should().Be(200);
        response.StatusReason.Should().Be("Mocked OK");
        test.Context.Request
            .Headers.Should().NotContainKey("Authorization");
    }

    [TestMethod]
    public void ShouldUseManagedIdentityTokenAuthenticationForRequestsFromExternalIp()
    {
        var document = new OperationDocument();
        var test = new TestDocument(document) { Context = { Request = { IpAddress = "11.0.0.1" } } };
        test.SetupInbound().AuthenticationManagedIdentity().ReturnsToken("testTokenValue");

        test.RunInbound();

        var authValue = test.Context.Request
            .Headers.Should().ContainKey("Authorization")
            .WhoseValue.Should().HaveCount(1).And.Subject.First()!;
        authValue.Should().StartWith("Bearer ");
        var token = authValue["Bearer ".Length..];
        var variableToken = test.Context
            .Variables.Should().ContainKey("testToken")
            .WhoseValue.Should().BeOfType<string>().Subject;
        token.Should().Be(variableToken).And.Be("testTokenValue");
    }

    [TestMethod]
    public void ShouldForwardRequest()
    {
        var document = new OperationDocument();
        var test = new TestDocument(document);
        int called = 0;
        test.SetupBackend().ForwardRequest().WithCallback((_, _) => called++);

        test.RunBackend();

        called.Should().Be(1);
    }

    [TestMethod]
    public void ShouldRewriteBody()
    {
        var document = new OperationDocument();
        var initial = JObject.Parse(
            """
            {
                "title": "Software Engineer",
                "location": "Redmond",
                "secret": "42",
                "name": "John Doe"
            }
            """);
        var test = new TestDocument(document) { Context = { Response = { Body = { Content = initial.ToString() } } } };

        test.RunOutbound();

        var body = test.Context.Response.Body.Content;
        var expected = JObject.Parse(
            """
            {
                "title": "Software Engineer",
                "name": "John Doe"
            }
            """);
        Assert.IsTrue(JToken.DeepEquals(
            JObject.Parse(body),
            expected
        ));
    }

    class OperationDocument : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.Base();
            if (IsFromCompanyIp(context.ExpressionContext))
            {
                if (IsMockSubscription(context.ExpressionContext))
                {
                    context.ReturnResponse(new ReturnResponseConfig
                    {
                        Status = new StatusConfig { Code = 200, Reason = "Mocked OK" },
                    });
                }

                context.AuthenticationBasic("{{username}}", "{{password}}");
            }
            else
            {
                context.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig()
                {
                    Resource = "https://management.azure.com/", OutputTokenVariableName = "testToken",
                });
                context.SetHeader("Authorization", Bearer(context.ExpressionContext));
            }
        }

        public void Backend(IBackendContext context)
        {
            context.ForwardRequest();
        }

        public void Outbound(IOutboundContext context)
        {
            context.Base();
            context.SetBody(FilterSecrets(context.ExpressionContext));
        }

        public bool IsFromCompanyIp(IExpressionContext context)
            => context.Request.IpAddress.StartsWith("10.0.0.");

        public bool IsMockSubscription(IExpressionContext context)
            => context.Subscription.Name.EndsWith("-mock");

        public string Bearer(IExpressionContext context)
            => $"Bearer {context.Variables["testToken"]}";

        [Expression]
        public string FilterSecrets(IExpressionContext context)
        {
            var body = context.Response.Body.As<JObject>();
            foreach (var internalProperty in new string[] { "location", "secret" })
            {
                if (body.ContainsKey(internalProperty))
                {
                    body.Remove(internalProperty);
                }
            }

            return body.ToString();
        }
    }
}