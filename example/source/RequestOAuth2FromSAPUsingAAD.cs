using System.Text;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

using Newtonsoft.Json.Linq;

namespace Contoso.Apis;

[Document]
public class RequestOAuth2FromSAPUsingAAD : IDocument
{
    public void Inbound(IInboundContext context)
    {
        context.Base();
        context.ValidateJwt(new ValidateJwtConfig
        {
            HeaderName = "Authorization",
            FailedValidationHttpCode = 401,
            RequireScheme = "Bearer",
            OpenIdConfigs =
            [
                new OpenIdConfig
                {
                    Url = "https://login.microsoftonline.com/{{AADTenantId}}/.well-known/openid-configuration"
                }
            ],
            Audiences = ["api://{{APIMAADRegisteredAppClientId}}"],
            Issuers = ["https://login.microsoftonline.com/{{AADTenantId}}/v2.0"],
            RequiredClaims =
            [
                new ClaimConfig { Name = "scp", Match = "all", Separator = " ", Values = ["user_impersonation"] }
            ]
        });
        context.SetHeader("Accept-Encoding", "gzip, deflate");
        context.SetVariable("APIMAADRegisteredAppClientId", "{{APIMAADRegisteredAppClientId}}");
        context.SetVariable("APIMAADRegisteredAppClientSecret", "{{APIMAADRegisteredAppClientSecret}}");
        context.SetVariable("AADSAPResource", "{{AADSAPResource}}");
        context.SetVariable("SAPOAuthClientID", "{{SAPOAuthClientID}}");
        context.SetVariable("SAPOAuthClientSecret", "{{SAPOAuthClientSecret}}");
        context.SetVariable("SAPOAuthScope", "{{SAPOAuthScope}}");
        context.SetVariable("SAPOAuthRefreshExpiry", "{{SAPOAuthRefreshExpiry}}");

        context.InlinePolicy("<cache-lookup-value key=\"@(\"SAPPrincipal\" + context.Request.Headers.GetValueOrDefault(\"Authorization\",\"\").AsJwt()?.Subject)\" variable-name=\"SAPBearerToken\" />");
        context.InlinePolicy("<cache-lookup-value key=\"@(\"SAPPrincipalRefresh\" + context.Request.Headers.GetValueOrDefault(\"Authorization\",\"\").AsJwt()?.Subject)\" variable-name=\"SAPRefreshToken\" />");

        if (ContainsSapTokens(context.ExpressionContext))
        {
            context.SendRequest(new SendRequestConfig
            {
                Mode = "new",
                ResponseVariableName = "fetchSAMLAssertion",
                Timeout = 10,
                IgnoreError = false,
                Url = "https://login.microsoftonline.com/{{AADTenantId}}/oauth2/v2.0/token",
                Method = "POST",
                Headers =
                [
                    new HeaderConfig
                    {
                        Name = "Content-Type",
                        ExistsAction = "override",
                        Values = ["application/x-www-form-urlencoded"]
                    }
                ],
                Body = new BodyConfig { Content = CreateAadTokenRequestBody(context.ExpressionContext) }
            });
            context.SetVariable("accessToken", GetTokenFromAadResponse(context.ExpressionContext));
            context.SendRequest(new SendRequestConfig
            {
                Mode = "new",
                ResponseVariableName = "ferchSapBearer",
                Timeout = 10,
                IgnoreError = false,
                Url = "https://{{SAPOAuthServerAdressForTokenEndpoint}}/sap/bc/sec/oauth2/token",
                Method = "POST",
                Headers =
                [
                    new HeaderConfig
                    {
                        Name = "Content-Type",
                        ExistsAction = "override",
                        Values = ["application/x-www-form-urlencoded"]
                    },
                    new HeaderConfig
                    {
                        Name = "Authorization",
                        ExistsAction = "override",
                        Values = [CreateAuthorizationHeaderToSAP(context.ExpressionContext)],
                    },
                    new HeaderConfig { Name = "Ocp-Apim-Subscription-Key", ExistsAction = "Delete" }
                ],
                Body = new BodyConfig { Content = CreateSapTokenRequestBody(context.ExpressionContext) }
            });
            context.SetVariable("SAPResponseObject", GetSAPBearerResponseObject(context.ExpressionContext));
            context.SetVariable("SAPBearerTokenExpiry", GetSAPBearerTokenExpiry(context.ExpressionContext));
            context.SetVariable("iSAPBearerTokenExpiry", GetIntSAPBearerTokenExpiry(context.ExpressionContext));
            context.SetVariable("SAPBearerToken", GetSAPBearerToken(context.ExpressionContext));
            context.SetVariable("SAPRefreshToken", GetSAPRefreshToken(context.ExpressionContext));
            context.SetVariable("RandomBackOffDelay", GetRandomBackOffDelay(context.ExpressionContext));

            context.InlinePolicy(
                "<cache-store-value key=\"@(\"SAPPrincipal\" + context.Request.Headers.GetValueOrDefault(\"Authorization\",\"\").AsJwt()?.Subject)\" value=\"@((string)context.Variables[\"SAPBearerToken\"])\" duration=\"@((int)context.Variables[\"iSAPBearerTokenExpiry\"]  - (int)context.Variables[\"RandomBackOffDelay\"])\" />");
            context.InlinePolicy(
                "<cache-store-value key=\"@(\"SAPPrincipalRefresh\" + context.Request.Headers.GetValueOrDefault(\"Authorization\",\"\").AsJwt()?.Subject)\" value=\"@((string)context.Variables[\"SAPRefreshToken\"])\" duration=\"@(int.Parse((string)context.Variables[\"SAPOAuthRefreshExpiry\"]) - (int)context.Variables[\"RandomBackOffDelay\"])\" />");
        }
        else if (ContainsRefreshTokenOnly(context.ExpressionContext))
        {
            context.SendRequest(new SendRequestConfig
            {
                Mode = "new",
                ResponseVariableName = "fetchrefreshedSAPBearer",
                Timeout = 10,
                IgnoreError = false,
                Url = "https://{{SAPOAuthServerAdressForTokenEndpoint}}/sap/bc/sec/oauth2/token",
                Method = "POST",
                Headers =
                [
                    new HeaderConfig
                    {
                        Name = "Content-Type",
                        ExistsAction = "override",
                        Values = ["application/x-www-form-urlencoded"]
                    },
                    new HeaderConfig
                    {
                        Name = "Authorization",
                        ExistsAction = "override",
                        Values = [CreateAuthorizationHeaderToSAP(context.ExpressionContext)],
                    }
                ],
                Body = new BodyConfig { Content = CreateSapRefreshTokenRequestBody(context.ExpressionContext) }
            });
            context.SetVariable("SAPRefreshedResponseObject", GetSAPRefreshResponseObject(context.ExpressionContext));
            context.SetVariable("SAPBearerTokenExpiry", GetSAPBearerTokenExpiry(context.ExpressionContext));
            context.SetVariable("iSAPBearerTokenExpiry", GetIntSAPBearerTokenExpiry(context.ExpressionContext));
            context.SetVariable("SAPBearerToken", GetSAPBearerToken(context.ExpressionContext));
            context.SetVariable("SAPRefreshToken", GetSAPRefreshToken(context.ExpressionContext));
            context.SetVariable("RandomBackOffDelay", GetRandomBackOffDelay(context.ExpressionContext));
            
            context.InlinePolicy("<cache-store-value key=\"@(\"SAPPrincipal\" + context.Request.Headers.GetValueOrDefault(\"Authorization\",\"\").AsJwt()?.Subject)\" value=\"@((string)context.Variables[\"SAPBearerToken\"])\" duration=\"@((int)context.Variables[\"iSAPBearerTokenExpiry\"]  - (int)context.Variables[\"RandomBackOffDelay\"])\" />");
            context.InlinePolicy("<cache-store-value key=\"@(\"SAPPrincipalRefresh\" + context.Request.Headers.GetValueOrDefault(\"Authorization\",\"\").AsJwt()?.Subject)\" value=\"@((string)context.Variables[\"SAPRefreshToken\"])\" duration=\"@(int.Parse((string)context.Variables[\"SAPOAuthRefreshExpiry\"]) - (int)context.Variables[\"RandomBackOffDelay\"])\" />");
        }

        if (IsNotGetOrHeadRequest(context.ExpressionContext))
        {
            context.SendRequest(new SendRequestConfig
            {
                Mode = "new",
                ResponseVariableName = "SAPCSRFToken",
                Timeout = 10,
                IgnoreError = false,
                Url = GetRequestURL(context.ExpressionContext),
                Method = "HEAD",
                Headers =
                [
                    new HeaderConfig { Name = "X-CSRF-Token", ExistsAction = "override", Values = ["Fetch"] },
                    new HeaderConfig
                    {
                        Name = "Authorization",
                        ExistsAction = "override",
                        Values = [GetSAPAuthorizationBearerToken(context.ExpressionContext)],
                    }
                ],
            });
            if (IsCSRFRequestSuccessfull(context.ExpressionContext))
            {
                context.SetVariable("SAPCSRFToken", GetCSRFToken(context.ExpressionContext));
                context.SetVariable("SAPXSRFCookie", GetXsrfCookie(context.ExpressionContext));
            }
        }

        context.SetHeader("Authorization", GetSAPAuthorizationBearerToken(context.ExpressionContext));
        context.RemoveHeader("Ocp-Apim-Subscription-Key");
        if (IsGetNotToMetadataRequest(context.ExpressionContext))
        {
            context.SetHeader("$format", "json");
        }
    }

    public void Backend(IBackendContext context)
    {
        context.Base();
    }

    public void Outbound(IOutboundContext context)
    {
        context.Base();
        context.InlinePolicy("<find-and-replace from=\"@(context.Api.ServiceUrl.Host +\":\"+ context.Api.ServiceUrl.Port)\" to=\"@(context.Request.OriginalUrl.Host + \":\" + context.Request.OriginalUrl.Port + context.Api.Path)\" />");
    }

    public void OnError(IOnErrorContext context)
    {
        context.Base();
        context.SetHeader("ErrorSource", GetErrorSource(context.ExpressionContext));
        context.SetHeader("ErrorReason", GetErrorReason(context.ExpressionContext));
        context.SetHeader("ErrorMessage", GetErrorMessage(context.ExpressionContext));
        context.SetHeader("ErrorScope", GetErrorScope(context.ExpressionContext));
        context.SetHeader("ErrorSection", GetErrorSection(context.ExpressionContext));
        context.SetHeader("ErrorPath", GetErrorPath(context.ExpressionContext));
        context.SetHeader("ErrorPolicyId", GetErrorPolicyId(context.ExpressionContext));
        context.SetHeader("ErrorStatusCode", ErrorErrorStatusCode(context.ExpressionContext));
    }

    bool ContainsSapTokens(IExpressionContext context)
        => !context.Variables.ContainsKey("SAPBearerToken") && !context.Variables.ContainsKey("SAPRefreshToken");

    string CreateAadTokenRequestBody(IExpressionContext context)
    {
        var _AADRegisteredAppClientId = context.Variables["APIMAADRegisteredAppClientId"];
        var _AADRegisteredAppClientSecret = context.Variables["APIMAADRegisteredAppClientSecret"];
        var _AADSAPResource = context.Variables["AADSAPResource"];
        var assertion = context.Request.Headers.GetValueOrDefault("Authorization", "").Replace("Bearer ", "");
        return $"grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer&assertion={assertion}&client_id={_AADRegisteredAppClientId}&client_secret={_AADRegisteredAppClientSecret}&scope={_AADSAPResource}/.default&requested_token_use=on_behalf_of&requested_token_type=urn:ietf:params:oauth:token-type:saml2";
    }

    string GetTokenFromAadResponse(IExpressionContext context)
        => (string)((IResponse)context.Variables["fetchSAMLAssertion"]).Body.As<JObject>()["access_token"];

    string CreateAuthorizationHeaderToSAP(IExpressionContext context)
    {
        var _SAPOAuthClientID = context.Variables["SAPOAuthClientID"];
        var _SAPOAuthClientSecret = context.Variables["SAPOAuthClientSecret"];
        return "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_SAPOAuthClientID}:{_SAPOAuthClientSecret}"));
    }

    string CreateSapTokenRequestBody(IExpressionContext context)
    {
        var _SAPOAuthClientID = context.Variables["SAPOAuthClientID"];
        var _SAPOAuthScope = context.Variables["SAPOAuthScope"];
        var assertion2 = context.Variables["accessToken"];
        return $"grant_type=urn:ietf:params:oauth:grant-type:saml2-bearer&assertion={assertion2}&client_id={_SAPOAuthClientID}&scope={_SAPOAuthScope}";
    }

    string CreateSapRefreshTokenRequestBody(IExpressionContext context)
    {
        var _SAPOAuthClientID = context.Variables["SAPOAuthClientID"];
        var _SAPOAuthScope = context.Variables["SAPOAuthScope"];
        var _refreshToken = context.Variables["SAPRefreshToken"];
        return $"grant_type=refresh_token&refresh_token={_refreshToken}&client_id={_SAPOAuthClientID}&scope={_SAPOAuthScope}";
    }

    JObject GetSAPBearerResponseObject(IExpressionContext context)
        => ((IResponse)context.Variables["fetchSAPBearer"]).Body.As<JObject>();

    JObject GetSAPRefreshResponseObject(IExpressionContext context)
        => ((IResponse)context.Variables["fetchrefreshedSAPBearer"]).Body.As<JObject>();

    string GetSAPBearerTokenExpiry(IExpressionContext context)
        => ((JObject)context.Variables["SAPResponseObject"])["expires_in"].ToString();

    int GetIntSAPBearerTokenExpiry(IExpressionContext context)
        => int.Parse((string)context.Variables["SAPBearerTokenExpiry"]);

    string GetSAPBearerToken(IExpressionContext context)
        => ((JObject)context.Variables["SAPResponseObject"])["access_token"].ToString();

    string GetSAPRefreshToken(IExpressionContext context)
        => ((JObject)context.Variables["SAPResponseObject"])["refresh_token"].ToString();

    double GetRandomBackOffDelay(IExpressionContext context)
        => new Random().Next(0, (int)context.Variables["iSAPBearerTokenExpiry"] / 3);

    bool ContainsRefreshTokenOnly(IExpressionContext context)
        => !context.Variables.ContainsKey("SAPBearerToken") && context.Variables.ContainsKey("SAPRefreshToken");

    bool IsNotGetOrHeadRequest(IExpressionContext context)
        => context.Request.Method != "GET" && context.Request.Method != "HEAD";

    string GetRequestURL(IExpressionContext context) => context.Request.Url.ToString();

    string GetSAPAuthorizationBearerToken(IExpressionContext context)
        => "Bearer " + (string)context.Variables["SAPBearerToken"];

    bool IsCSRFRequestSuccessfull(IExpressionContext context)
        => ((IResponse)context.Variables["SAPCSRFToken"]).StatusCode == 200;

    string GetCSRFToken(IExpressionContext context)
        => ((IResponse)context.Variables["SAPCSRFToken"]).Headers.GetValueOrDefault("x-csrf-token");

    string GetXsrfCookie(IExpressionContext context)
    {
        string rawcookie = ((IResponse)context.Variables["SAPCSRFToken"]).Headers.GetValueOrDefault("Set-Cookie");
        string[] cookies = rawcookie.Split(';');
        string xsrftoken = cookies.FirstOrDefault(ss => ss.Contains("sap-XSRF"));
        if (xsrftoken == null)
        {
            xsrftoken = cookies.FirstOrDefault(ss => ss.Contains("SAP_SESSIONID"));
        }

        return xsrftoken.Split(',')[1];
    }

    bool IsGetNotToMetadataRequest(IExpressionContext context)
        => !context.Request.Url.Path.Contains("/$metadata") && context.Request.Method == "GET";

    string GetErrorSource(IExpressionContext context) => context.LastError.Source;
    string GetErrorReason(IExpressionContext context) => context.LastError.Reason;
    string GetErrorMessage(IExpressionContext context) => context.LastError.Message;
    string GetErrorScope(IExpressionContext context) => context.LastError.Scope;
    string GetErrorSection(IExpressionContext context) => context.LastError.Section;
    string GetErrorPath(IExpressionContext context) => context.LastError.Path;
    string GetErrorPolicyId(IExpressionContext context) => context.LastError.PolicyId;
    string ErrorErrorStatusCode(IExpressionContext context) => context.Response.StatusCode.ToString();
}