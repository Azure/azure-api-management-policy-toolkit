using System.Text;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

using Contoso.Apis;

using Newtonsoft.Json.Linq;

namespace Contoso.Test.Apis;

[TestClass]
public class ApiOperationPolicyTest
{
    [TestMethod]
    public void FilterSecrets()
    {
        var context = new MockExpressionContext();
        context.Response.Body.Content =
            """
            {
                "title": "Software Engineer",
                "location": "Redmond",
                "secret": "42",
                "name": "John Doe"
            }
            """;

        var newBody = new ApiOperationPolicy().FilterSecrets(context);

        var expected = """
                       {
                           "title": "Software Engineer",
                           "name": "John Doe"
                       }
                       """;
        Assert.IsTrue(
            JObject.DeepEquals(
                JObject.Parse(newBody),
                JObject.Parse(expected)
            )
        );
    }

    [TestMethod]
    public void TestInboundInternalIp()
    {
        var policyDocument = new ApiOperationPolicy();
        var testDocument = new TestDocument(policyDocument)
        {
            Context = {
                Request = { IpAddress = "10.0.0.1" }
            }
        };

        testDocument.RunInbound();

        var headers = testDocument.Context.Request.Headers;
        var value = headers.Should().ContainKey("Authorization")
            .WhoseValue.Should().ContainSingle()
            .Subject;
        value.Should().StartWith("Basic ");
        DecodeBasicAuthorization(value).Should().Be("{{username}}:{{password}}");
    }

    [TestMethod]
    public void TestInboundExternalIp()
    {
        var policyDocument = new ApiOperationPolicy();
        var testDocument = new TestDocument(policyDocument)
        {
            Context = {
                Request = { IpAddress = "11.0.0.1" }
            }
        };
        testDocument.SetupInbound().AuthenticationManagedIdentity().ReturnsToken("myToken");

        testDocument.RunInbound();

        var headers = testDocument.Context.Request.Headers;
        var value = headers.Should().ContainKey("Authorization")
            .WhoseValue.Should().ContainSingle()
            .Subject;
        value.Should().StartWith("Bearer ");
        value["Bearer ".Length..].Should().Be("myToken");
    }

    private string DecodeBasicAuthorization(string value)
    {
        var token = value["Basic ".Length..];
        return Encoding.UTF8.GetString(Convert.FromBase64String(token));
    }
}