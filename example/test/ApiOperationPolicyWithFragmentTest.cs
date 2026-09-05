using System.Text;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

using Contoso.Apis;

using Newtonsoft.Json.Linq;

namespace Contoso.Test.Apis;

[TestClass]
public class ApiOperationPolicyWithFragmentTest
{

    [TestMethod]
    public void TestInboundInternalIp()
    {
        var policyDocument = new ApiOperationWithFragmentPolicy();
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

    private string DecodeBasicAuthorization(string value)
    {
        var token = value["Basic ".Length..];
        return Encoding.UTF8.GetString(Convert.FromBase64String(token));
    }
}