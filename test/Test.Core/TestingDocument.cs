// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.CodeContext;

[Document("echo-api.retrieve-resource")]
public class TestingDocument : IDocument
{
    public void Inbound(IInboundContext context)
    {
        context.Base();
        if (IsFromCompanyIp(context.ExpressionContext))
        {
            context.SetHeader("X-Company", "true");
            context.AuthenticationBasic("{{username}}", "{{password}}");
            context.ReturnResponse(new ReturnResponseConfig
            {
                Status = new StatusConfig { Code = 200, Reason = "OK" }
            });
            return;
        }
        else
        {
            context.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig
            {
                Resource = "https://management.azure.com/", OutputTokenVariableName = "testToken",
            });
            context.SetHeader("Authorization", Bearer(context.ExpressionContext));
        }
    }

    public void Outbound(IOutboundContext c)
    {
        c.RemoveHeader("Backend-Statistics");
        c.SetBody(FilterRequest(c.ExpressionContext));
        c.Base();
    }

    bool IsFromCompanyIp(IExpressionContext context) => context.Request.IpAddress.StartsWith("10.0.0.");

    public string Bearer(IExpressionContext context) => $"Bearer {context.Variables["testToken"]}";

    string FilterRequest(IExpressionContext context)
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