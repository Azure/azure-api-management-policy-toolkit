using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

using Newtonsoft.Json.Linq;

namespace Contoso.Apis;

[Document("echo-api_retrieve-resource")]
public class ApiOperationPolicy : IDocument
{
    public void Inbound(IInboundContext context)
    {
        context.Base();
        if (IsFromCompanyIp(context.ExpressionContext))
        {
            context.AuthenticationBasic("{{username}}", "{{password}}");
        }
        else
        {
            context.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig()
            {
                Resource = "https://management.azure.com/",
            });
        }
    }

    public void Outbound(IOutboundContext context)
    {
        context.Base();
        context.SetBody(FilterSecrets(context.ExpressionContext));
    }

    public bool IsFromCompanyIp(IExpressionContext context)
        => context.Request.IpAddress.StartsWith("10.0.0.");

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