using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Example;

using Newtonsoft.Json.Linq;

namespace Contoso.Apis;

[Document("echo-api_retrieve-another-resource")]
public class ApiOperationWithFragmentPolicy : IDocument
{
    public void Inbound(IInboundContext context)
    {
        context.Base();
        if (IsFromCompanyIp(context.ExpressionContext))
        {
            context.IncludeFragment(new AuthenticationFragment { XAuthFragmentValue = "true" });
        }
        else
        {
            context.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig()
            {
                Resource = Constants.AzureManagementUrl, // Or use literals such as "https://management.azure.com/"
            });
        }
    }

    public bool IsFromCompanyIp(IExpressionContext context)
        => context.Request.IpAddress.StartsWith("10.0.0.");
}