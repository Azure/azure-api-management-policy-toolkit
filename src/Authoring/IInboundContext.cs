// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public interface IInboundContext : IHaveExpressionContext
{
    /// <summary>
    /// The base policy used to specify when parent scope policy should be executed
    /// </summary>
    void Base();

    /// <summary>
    /// Adds header of specified name with values or overrides values if header already exists.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/set-header-policy">set-header</a> policy.
    /// </summary>
    /// <param name="name">
    /// Specifies name of the header to be added. Policy expressions are allowed.
    /// </param>
    /// <param name="values">
    /// Specifies the values of the header to be set. Policy expressions are allowed.
    /// </param>
    void SetHeader(string name, params string[] values);

    /// <summary>
    /// Sets header of specified name and values if header not already exist.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/set-header-policy">set-header</a> policy.
    /// </summary>
    /// <param name="name">
    /// Specifies name of the header to be added. Policy expressions are allowed.
    /// </param>
    /// <param name="values">
    /// Specifies the values of the header to be set. Policy expressions are allowed.
    /// </param>
    void SetHeaderIfNotExist(string name, params string[] values);

    /// <summary>
    /// Adds header of specified name with values or appends values if header already exists.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/set-header-policy">set-header</a> policy.
    /// </summary>
    /// <param name="name">
    /// Specifies name of the header to be added. Policy expressions are allowed.
    /// </param>
    /// <param name="values">
    /// Specifies the values of the header to be set or appended. Policy expressions are allowed.
    /// </param>
    void AppendHeader(string name, params string[] values);

    /// <summary>
    /// Deletes header of specified name.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/set-header-policy">set-header</a> policy.
    /// </summary>
    /// <param name="name">
    /// Specifies name of the header to be deleted. Policy expressions are allowed.
    /// </param>
    void RemoveHeader(string name);

    /// <summary>
    /// The cors policy adds cross-origin resource sharing (CORS) support to an operation or an API to allow cross-domain calls from browser-based clients.
    /// </summary>
    /// <param name="config"></param>
    void Cors(CorsConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="body"></param>
    /// <param name="config"></param>
    void SetBody(string body, SetBodyConfig? config = null);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="name"></param>
    /// <param name="value"></param>
    void SetVariable(string name, dynamic value);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void RateLimit(RateLimitConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="template"></param>
    /// <param name="copyUnmatchedParams"></param>
    void RewriteUri(string template, bool copyUnmatchedParams = true);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void Quota(QuotaConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="method"></param>
    void SetMethod(string method);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void IpFilter(IpFilterConfig config);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="name"></param>
    /// <param name="values"></param>
    void SetQueryParameter(string name, params string[] values);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="name"></param>
    /// <param name="values"></param>
    void SetQueryParameterIfNotExist(string name, params string[] values);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="name"></param>
    /// <param name="values"></param>
    void AppendQueryParameter(string name, params string[] values);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="name"></param>
    void RemoveQueryParameter(string name);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void RateLimitByKey(RateLimitByKeyConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void CheckHeader(CheckHeaderConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void MockResponse(MockResponseConfig? config = null);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void SendRequest(SendRequestConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void ReturnResponse(ReturnResponseConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void ValidateJwt(ValidateJwtConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void SetBackendService(SetBackendServiceConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="username"></param>
    /// <param name="password"></param>
    void AuthenticationBasic(string username, string password);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void CacheLookup(CacheLookupConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void EmitMetric(EmitMetricConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void LlmEmitTokenMetric(EmitTokenMetricConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void AzureOpenAiEmitTokenMetric(EmitTokenMetricConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void LlmSemanticCacheLookup(SemanticCacheLookupConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void AzureOpenAiSemanticCacheLookup(SemanticCacheLookupConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void AuthenticationManagedIdentity(ManagedIdentityAuthenticationConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void AuthenticationCertificate(CertificateAuthenticationConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void JsonToXml(JsonToXmlConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void CacheLookupValue(CacheLookupValueConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void CacheStoreValue(CacheStoreValueConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void CacheRemoveValue(CacheRemoveValueConfig config);

    /// <summary>
    /// The policy inserts the policy fragment as-is at the location you select in the policy definition.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/include-fragment-policy">include-fragment</a> policy.
    /// </summary>
    /// <param name="fragmentId">A string. Specifies the identifier (name) of a policy fragment created in the API Management instance. Policy expressions aren't allowed.</param>
    void IncludeFragment(string fragmentId);

    /// <summary>
    /// Inlines the specified policy as is to policy document.
    /// </summary>
    /// <param name="policy">
    /// Policy in xml format.
    /// </param>
    void InlinePolicy(string policy);

    /// <summary>
    /// The find-and-replace policy replaces occurrences of a specified string with another string in the request or response.
    /// </summary>
    /// <param name="from">The string to be replaced. Policy expressions are allowed.</param>
    /// <param name="to">The string to replace with. Policy expressions are allowed.</param>
    void FindAndReplace(string from, string to);

    /// <summary>
    ///     The retry policy executes its child policies once and then retries their execution until the retry condition
    ///     becomes false or retry count is exhausted.
    /// </summary>
    /// <param name="config">Configuration of retry policy</param>
    /// <param name="section">Child policies which should be retried</param>
    void Retry(RetryConfig config, Action section);

    /// <summary>
    ///     The wait policy executes its immediate child policies in parallel, and waits for either all or one of its immediate
    ///     child policies to complete before it completes.
    ///     The wait policy can have as its immediate child policies one or more of the following: send-request,
    ///     cache-lookup-value, and choose policies.
    /// </summary>
    /// <param name="section">Child policies which should be awaited</param>
    /// <param name="waitFor">
    ///     Determines whether the wait policy waits for all immediate child policies to be completed or just one.
    ///     Policy expressions are allowed.
    /// </param>
    void Wait(Action section, string? waitFor = null);

    /// <summary>
    /// The get-authorization-context policy retrieves an authorization context from a specified provider.
    /// </summary>
    /// <param name="config">The configuration for the get-authorization-context policy.</param>
    void GetAuthorizationContext(GetAuthorizationContextConfig config);

    /// <summary>
    ///     TODO
    /// </summary>
    /// <param name="config"></param>
    /// <param name="section"></param>
    void LimitConcurrency(LimitConcurrencyConfig config, Action section);

    /// <summary>
    /// The log-to-eventhub policy sends messages in the specified format to an event hub defined by a Logger entity.
    /// As its name implies, the policy is used for saving selected request or response context information for online or
    /// offline analysis.
    /// </summary>
    /// <param name="config"></param>
    void LogToEventHub(LogToEventHubConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void QuotaByKey(QuotaByKeyConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="policy"></param>
    void CrossDomain(string policy);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void Proxy(ProxyConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    void RedirectContentUrls();

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void SendOneWayRequest(SendOneWayRequestConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void Trace(TraceConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void ValidateAzureAdToken(ValidateAzureAdTokenConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void XmlToJson(XmlToJsonConfig config);
}