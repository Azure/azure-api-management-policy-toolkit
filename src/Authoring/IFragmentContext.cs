// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//TODO: Update Context interface definitions to not require re-defining all policies

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Context interface for policy fragments that combines all policy capabilities.
/// Fragments can contain any policy elements that are valid in inbound, outbound, backend, or on-error sections.
/// </summary>
public interface IFragmentContext : IHaveExpressionContext
{
    /// <summary>
    /// Sets a policy ID that will be added as the 'id' attribute to the next policy element.<br />
    /// Multiple chained WithId calls use the last value (last wins).
    /// </summary>
    /// <param name="id">The policy ID to add. Must be a constant string value.</param>
    /// <returns>The same context instance to allow method chaining.</returns>
    /// <example>
    /// context.WithId("my-header-policy").SetHeader("X-Custom", "value");
    /// // Produces: &lt;set-header id="my-header-policy" name="X-Custom" exists-action="override"&gt;...&lt;/set-header&gt;
    /// </example>
    IFragmentContext WithId(string id);

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
    void AppendHeader([ExpressionAllowed] string name, [ExpressionAllowed] params string[] values);

    /// <summary>
    /// Adds specified query parameter with values or appends values if parameter already exists.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/set-query-parameter-policy">set-query-parameter</a> policy with exist-action="append".
    /// </summary>
    /// <param name="name">
    /// Specifies name of the query parameter to be added. Policy expressions are allowed.
    /// </param>
    /// <param name="values">
    /// Specifies the values of the query parameter to be appended. Policy expressions are allowed.
    /// </param>
    void AppendQueryParameter([ExpressionAllowed] string name, [ExpressionAllowed] params string[] values);

    /// <summary>
    /// Authenticates with a backend service using Basic Authentication.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/authentication-basic-policy">authentication-basic</a> policy.
    /// </summary>
    /// <param name="username">
    /// Username used for authentication. Policy expressions are allowed.
    /// </param>
    /// <param name="password">
    /// Password used for authentication. Policy expressions are allowed.
    /// </param>
    void AuthenticationBasic([ExpressionAllowed] string username, [ExpressionAllowed] string password);

    /// <summary>
    /// Authenticates to the backend service using a client certificate.<br />
    /// The client certificate is provided to the backend service during TLS handshake.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/authentication-certificate-policy">authentication-certificate</a> policy.
    /// </summary>
    /// <param name="config">Configuration specifying how to find the client certificate. The certificate can be specified using a certificate thumbprint, a resource identifier, or directly as a base64-encoded value.</param>
    void AuthenticationCertificate(CertificateAuthenticationConfig config);

    /// <summary>
    /// Authenticates with a backend service using a managed identity to obtain an access token from Azure Active Directory.<br />
    /// The policy can use either a system-assigned or user-assigned managed identity.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/authentication-managed-identity-policy">authentication-managed-identity</a> policy.
    /// </summary>
    /// <param name="config">Configuration specifying the resource for which to request a token, optional client ID for user-assigned identity, and other settings.</param>
    void AuthenticationManagedIdentity(ManagedIdentityAuthenticationConfig config);

    /// <summary>
    /// Emits metrics about token usage from Azure OpenAI service calls.<br/>
    /// This policy can be used to monitor and analyze token usage patterns.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/azure-openai-emit-token-metric-policy">azure-openai-emit-token-metric</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the dimensions to include with the metric and optionally the namespace to use.
    /// </param>
    void AzureOpenAiEmitTokenMetric(EmitTokenMetricConfig config);

    /// <summary>
    /// Searches a cache for semantically similar Azure OpenAI prompts and returns cached responses if found.<br/>
    /// Uses vector embeddings to match prompt similarity against cached items based on a threshold.<br/>
    /// When a match is found, the policy short-circuits the request and returns the cached response.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/azure-openai-semantic-cache-lookup-policy">azure-openai-semantic-cache-lookup</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the similarity threshold, embedding backend, and other cache parameters.
    /// </param>
    void AzureOpenAiSemanticCacheLookup(SemanticCacheLookupConfig config);

    /// <summary>
    /// Stores the current Azure OpenAI request and response in the semantic cache for future lookup.<br/>
    /// This policy must be placed in the outbound section to capture both the request and response.<br/>
    /// When stored, the entries can later be found by the azure-openai-semantic-cache-lookup policy.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/azure-openai-semantic-cache-store-policy">azure-openai-semantic-cache-store</a> policy.
    /// </summary>
    /// <param name="duration">
    /// Duration in seconds for which the cached entry is valid. Policy expressions are allowed.
    /// </param>
    void AzureOpenAiSemanticCacheStore([ExpressionAllowed] uint duration);

    /// <summary>
    /// Limits tokens used by Azure OpenAI services to prevent overconsumption.<br/>
    /// This policy can enforce rate limits (tokens per minute) and/or quotas (tokens per period).<br/>
    /// Helps protect backend services and manage costs by controlling token usage.<br/>
    /// Can estimate prompt token count and track consumption via custom headers or variables.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/azure-openai-token-limit-policy">azure-openai-token-limit</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the counter key, rate limits, quotas, and optional header/variable names for tracking token usage.
    /// </param>
    void AzureOpenAiTokenLimit(TokenLimitConfig config);

    /// <summary>
    /// Checks the gateway cache for a valid cached HTTP response for the current request.<br/>
    /// If a valid cached response is not found, requests flow through the pipeline normally and response is cached before sending to caller.<br/>
    /// When found, a cached response is immediately returned to caller, bypassing most of the pipeline processing.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/cache-lookup-policy">cache-lookup</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying caching parameters including vary-by options, caching type, 
    /// revalidation settings, and other cache control options.
    /// </param>
    void CacheLookup(CacheLookupConfig config);

    /// <summary>
    /// Retrieves a value from the cache using a key and stores it in a variable for later use.<br/>
    /// Used to efficiently access cached values in policy expressions.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/cache-lookup-value-policy">cache-lookup-value</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the cache key, variable name, optional default value, and optional caching type.
    /// </param>
    void CacheLookupValue(CacheLookupValueConfig config);

    /// <summary>
    /// Removes a value from the cache using a specified key.<br/>
    /// This policy is used to remove an item from the cache based on its key.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/cache-remove-value-policy">cache-remove-value</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the cache key and optional caching type.
    /// </param>
    void CacheRemoveValue(CacheRemoveValueConfig config);

    /// <summary>
    /// Stores responses in the specified cache. This policy can be applied in cases where response content remains static for a period of time.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/cache-store-policy">cache-store</a> policy.
    /// </summary>
    /// <param name="duration">
    /// Specifies the duration in seconds that the response should be cached. Policy expressions are allowed.
    /// </param>
    /// <param name="cacheResponse">
    /// Indicates whether the response should be cached. If set to false, the response will not be cached. Policy expressions are allowed.
    /// </param>
    void CacheStore([ExpressionAllowed] int duration, [ExpressionAllowed] bool? cacheResponse);

    /// <summary>
    /// Stores a value in the cache using a specified key.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/cache-store-value-policy">cache-store-value</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the cache key, value, duration, and optional caching type.
    /// </param>
    void CacheStoreValue(CacheStoreValueConfig config);

    /// <summary>
    /// Enforces existence and value of an HTTP header in the request.<br/>
    /// If the check fails, the policy terminates request processing and returns the specified HTTP status code and error message to the caller.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/check-header-policy">check-header</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the header name, expected values, case sensitivity, and the HTTP status code and error message to return if the check fails.
    /// </param>
    void CheckHeader(CheckHeaderConfig config);

    /// <summary>
    /// Enables cross-origin resource sharing (CORS) for an API or API Management instance.<br/>
    /// This policy adds CORS headers to responses and handles preflight OPTIONS requests.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/cors-policy">cors</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying allowed origins, headers, methods, and other CORS settings.
    /// </param>
    void Cors(CorsConfig config);

    /// <summary>
    /// Enables cross-domain calls from Adobe Flash and Microsoft Silverlight browser-based clients.<br/>
    /// This policy adds appropriate CORS headers to allow browser-based clients to make cross-domain requests.<br/>
    /// It creates a cross-domain policy XML document that is consumed by Silverlight and Flash clients.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/cross-domain-policy">cross-domain</a> policy.
    /// </summary>
    /// <param name="policy">
    /// XML-formatted cross-domain policy document that specifies allowed domains. Policy expressions are not allowed.
    /// </param>
    void CrossDomain(string policy);

    /// <summary>
    /// Emits custom metrics to Azure Monitor for tracking API usage patterns and performance.<br/>
    /// The metrics are accessible through Azure Monitor metrics explorer with the configured dimensions and namespace.<br/>
    /// This policy is useful for custom monitoring and can be used for visualization, alerting, and analysis.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/emit-metric-policy">emit-metric</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the metric name, dimensions, optional namespace, and optional value.
    /// </param>
    void EmitMetric(EmitMetricConfig config);

    /// <summary>
    /// Replaces occurrences of a specified string with another string in the request or response body.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/find-and-replace-policy">find-and-replace</a> policy.
    /// </summary>
    /// <param name="from">The string value to find. Policy expressions are allowed.</param>
    /// <param name="to">The replacement string value. Policy expressions are allowed.</param>
    void FindAndReplace([ExpressionAllowed] string from, [ExpressionAllowed] string to);

    /// <summary>
    /// Forwards the incoming request to the backend service specified in the request context.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/forward-request-policy">forward-request</a> policy.
    /// </summary>
    /// <param name="config">
    /// Optional configuration specifying timeout settings, HTTP version, redirect behavior, buffering options, and error handling.
    /// </param>
    void ForwardRequest(ForwardRequestConfig? config = null);

    /// <summary>
    /// Retrieves an authorization context from a specified provider.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/get-authorization-context-policy">get-authorization-context</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the credential provider, connection resource identifier, context variable name, identity type, identity token, and error handling behavior.
    /// </param>
    void GetAuthorizationContext(GetAuthorizationContextConfig config);

    /// <summary>
    /// The policy inserts the policy fragment as-is at the location you select in the policy definition.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/include-fragment-policy">include-fragment</a> policy.
    /// </summary>
    /// <param name="fragmentId">A string. Specifies the identifier (name) of a policy fragment created in the API Management instance. Policy expressions are not allowed.</param>
    void IncludeFragment(string fragmentId);

    /// <summary>
    /// Inlines the specified policy as is to policy document.
    /// </summary>
    /// <param name="policy">
    /// Policy in xml format.
    /// </param>
    void InlinePolicy(string policy);

    /// <summary>
    /// Invokes a Dapr binding with the specified configuration.<br/>
    /// This policy allows you to interact with Dapr bindings to trigger external resources or services.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/invoke-dapr-binding-policy">invoke-dapr-binding</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the Dapr binding name, operation, metadata, and other settings.
    /// </param>
    void InvokeDarpBinding(InvokeDarpBindingConfig config);

    /// <summary>
    /// Filters (allows/denies) calls from specific IP addresses and/or ranges.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/ip-filter-policy">ip-filter</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the action to take (allow or deny), IP addresses, and/or IP address ranges.
    /// </param>
    void IpFilter(IpFilterConfig config);

    /// <summary>
    /// Converts a response containing JSON to JSONP format.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/jsonp-policy">jsonp</a> policy.
    /// </summary>
    /// <param name="callbackParameterName">
    /// Specifies the query parameter name whose value provides the name of the callback function.
    /// </param>
    void JsonP([ExpressionAllowed] string callbackParameterName);

    /// <summary>
    /// Converts JSON content to XML format.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/json-to-xml-policy">json-to-xml</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying how to convert JSON to XML, including options for applying the policy, considering the Accept header, parsing dates, and more.
    /// </param>
    void JsonToXml(JsonToXmlConfig config);

    /// <summary>
    /// Limits the number of concurrent calls to a backend service.<br />
    /// This policy helps protect the backend service from being overwhelmed by too many concurrent requests.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/limit-concurrency-policy">limit-concurrency</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the maximum number of concurrent calls allowed and the behavior when the limit is reached.
    /// </param>
    /// <param name="section">
    /// The policy section to be executed when the concurrency limit is not reached.
    /// </param>
    void LimitConcurrency(LimitConcurrencyConfig config, Action section);

    /// <summary>
    /// Evaluates the content safety of a prompt using a Language Model (LLM) service.<br/>
    /// This policy can be used to ensure that prompts adhere to specified safety standards.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/llm-content-safety-policy">llm-content-safety</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the backend service ID, optional shield prompt setting, content safety categories, and block lists.
    /// </param>
    void LlmContentSafety(LlmContentSafetyConfig config);

    /// <summary>
    /// Emits metrics about token usage from Language Model service calls.<br/>
    /// This policy captures and records token usage information for monitoring and analysis purposes.<br/>
    /// Use this policy to track token consumption across LLM operations with custom dimensions.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/azure-openai-emit-token-metric-policy">llm-emit-token-metric</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the dimensions to include with the metric and optionally the namespace to use.
    /// </param>
    void LlmEmitTokenMetric(EmitTokenMetricConfig config);

    /// <summary>
    /// Searches a cache for semantically similar LLM prompts and returns cached responses if found.<br/>
    /// Uses vector embeddings to match prompt similarity against cached items based on a threshold.<br/>
    /// When a match is found, the policy short-circuits the request and returns the cached response.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/azure-openai-semantic-cache-lookup-policy">llm-semantic-cache-lookup</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the similarity threshold, embedding backend, and other cache parameters.
    /// </param>
    void LlmSemanticCacheLookup(SemanticCacheLookupConfig config);

    /// <summary>
    /// Stores the current LLM request and response in the semantic cache for future lookup.<br/>
    /// This policy must be placed in the outbound section to capture both the request and response.<br/>
    /// When stored, the entries can later be found by the llm-semantic-cache-lookup policy.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/llm-semantic-cache-store-policy">llm-semantic-cache-store</a> policy.
    /// </summary>
    /// <param name="duration">
    /// Duration in seconds for which the cached entry is valid. Policy expressions are allowed.
    /// </param>
    void LlmSemanticCacheStore([ExpressionAllowed] uint duration);

    /// <summary>
    /// Limits tokens used by Language Model services to prevent overconsumption.<br/>
    /// This policy can enforce rate limits (tokens per minute) and/or quotas (tokens per period).<br/>
    /// Helps protect backend services and manage costs by controlling token usage.<br/>
    /// Can estimate prompt token count and track consumption via custom headers or variables.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/azure-openai-token-limit-policy">llm-token-limit</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the counter key, rate limits, quotas, and optional header/variable names for tracking token usage.
    /// </param>
    void LlmTokenLimit(TokenLimitConfig config);

    /// <summary>
    /// Sends messages in the specified format to an Azure Event Hub defined by a Logger entity.<br/>
    /// Used for saving selected request or response context information for online or offline analysis.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/log-to-eventhub-policy">log-to-eventhub</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the logger entity, message content, and optional partition settings.
    /// </param>
    void LogToEventHub(LogToEventHubConfig config);

    /// <summary>
    /// Returns a mocked response to the caller of the API.<br/>
    /// This policy terminates the pipeline processing and returns the specified mocked response directly to the caller.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/mock-response-policy">mock-response</a> policy.
    /// </summary>
    /// <param name="config">
    /// Optional configuration specifying status code, content type, headers, and other response characteristics.
    /// When null, returns a default empty 200 OK response.
    /// </param>
    void MockResponse(MockResponseConfig? config = null);

    /// <summary>
    /// Configures a proxy server for forwarding requests.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/proxy-policy">proxy</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the proxy server URL, and optionally the username and password for authentication.
    /// </param>
    void Proxy(ProxyConfig config);

    /// <summary>
    /// Publishes a message to a Dapr topic.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/publish-to-dapr-policy">publish-to-dapr</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the topic, content, and other optional settings for the publish-to-dapr policy.
    /// </param>
    void PublishToDarp(PublishToDarpConfig config);

    /// <summary>
    /// Enforces a quota on the number of calls or bandwidth usage within a specified renewal period.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/quota-policy">quota</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the quota limits, renewal period, and optional API and operation-specific quotas.
    /// </param>
    void Quota(QuotaConfig config);

    /// <summary>
    /// Enforces a quota on the number of calls or bandwidth usage within a specified renewal period, based on a key.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/quota-by-key-policy">quota-by-key</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the counter key, renewal period, and optional calls, bandwidth, increment condition, increment count, and first period start.
    /// </param>
    void QuotaByKey(QuotaByKeyConfig config);

    /// <summary>
    /// Limits the call rate to a backend service to prevent overuse and protect backend resources.<br/>
    /// This policy enforces a limit on the number of calls allowed within a specified renewal period.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/rate-limit-policy">rate-limit</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the maximum number of calls allowed, renewal period, and optional headers or variables for tracking remaining calls and retry information.
    /// </param>
    void RateLimit(RateLimitConfig config);

    /// <summary>
    /// Limits the call rate by a specified key to prevent overuse and protect backend resources.<br/>
    /// This policy enforces a limit on the number of calls allowed within a specified renewal period, based on a key.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/rate-limit-by-key-policy">rate-limit-by-key</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the maximum number of calls allowed, renewal period, counter key, and optional increment conditions, headers, or variables for tracking remaining calls and retry information.
    /// </param>
    void RateLimitByKey(RateLimitByKeyConfig config);

    /// <summary>
    /// Redirects URLs in the response content to a specified hostname and scheme.<br/>
    /// This policy rewrites URLs in the response body to point to the gateway URL instead of the backend service URL.<br/>
    /// Useful when backend services return absolute URLs that need to be redirected through the API Management gateway.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/redirect-content-urls-policy">redirect-content-urls</a> policy.
    /// </summary>
    void RedirectContentUrls();

    /// <summary>
    /// Deletes header of specified name.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/set-header-policy">set-header</a> policy.
    /// </summary>
    /// <param name="name">
    /// Specifies name of the header to be deleted. Policy expressions are allowed.
    /// </param>
    void RemoveHeader([ExpressionAllowed] string name);

    /// <summary>
    /// Deletes query parameter of specified name.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/set-query-parameter-policy">set-query-parameter</a> policy with exist-action="delete".
    /// </summary>
    /// <param name="name">
    /// Specifies name of the query parameter to be deleted. Policy expressions are allowed.
    /// </param>
    void RemoveQueryParameter([ExpressionAllowed] string name);

    /// <summary>
    /// Aborts pipeline execution and returns the specified response directly to the caller.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/return-response-policy">return-response</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the response details including status code, headers, body, or a response variable.
    /// </param>
    void ReturnResponse(ReturnResponseConfig config);

    /// <summary>
    /// Executes its child policies once and then retries their execution until the retry condition becomes false or retry count is exhausted.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/retry-policy">retry</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying retry conditions, retry count, intervals, and other retry behavior settings.
    /// </param>
    /// <param name="section">
    /// Child policies which should be retried.
    /// </param>
    void Retry(RetryConfig config, Action section);

    /// <summary>
    /// Rewrites the request URL to the specified template.<br/>
    /// This policy modifies the request URL by rewriting the URI path and optionally copying unmatched query parameters.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/rewrite-uri-policy">rewrite-uri</a> policy.
    /// </summary>
    /// <param name="template">
    /// Specifies the URI template to rewrite the request URL to. Policy expressions are allowed.
    /// </param>
    /// <param name="copyUnmatchedParams">
    /// Optional. Specifies whether to copy unmatched query parameters from the original request URL to the rewritten URL.
    /// Default value is true. Policy expressions are allowed.
    /// </param>
    void RewriteUri([ExpressionAllowed] string template, [ExpressionAllowed] bool copyUnmatchedParams = true);

    /// <summary>
    /// Sends a one-way HTTP request to a specified URL without waiting for a response.
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/send-one-way-request-policy">send-one-way-request</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the request mode, timeout, URL, method, headers, body, authentication, and proxy settings.
    /// </param>
    void SendOneWayRequest(SendOneWayRequestConfig config);

    /// <summary>
    /// Sends an HTTP request to a specified URL and optionally waits for a response.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/send-request-policy">send-request</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the request details including URL, method, headers, body, authentication, and proxy settings.
    /// </param>
    void SendRequest(SendRequestConfig config);

    /// <summary>
    /// Sends a message to an Azure Service Bus queue or topic.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/send-service-bus-message-policy">send-service-bus-message</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the queue or topic name, namespace, client identity, message properties, and payload.
    /// </param>
    void SendServiceBusMessage(SendServiceBusMessageConfig config);

    /// <summary>
    /// Sets the backend service for the request.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/set-backend-service-policy">set-backend-service</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the backend service details, including base URL, backend ID, Service Fabric settings, and Dapr settings.
    /// </param>
    void SetBackendService(SetBackendServiceConfig config);

    /// <summary>
    /// Sets or replaces the request or response body with the specified value.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/set-body-policy">set-body</a> policy.
    /// </summary>
    /// <param name="body">
    /// The value to set as the body. Policy expressions are allowed.
    /// </param>
    /// <param name="config">
    /// Optional configuration specifying template, xsi:nil, and parse date settings.
    /// </param>
    void SetBody([ExpressionAllowed] string body, SetBodyConfig? config = null);

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
    void SetHeader([ExpressionAllowed] string name, [ExpressionAllowed] params string[] values);

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
    void SetHeaderIfNotExist([ExpressionAllowed] string name, [ExpressionAllowed] params string[] values);

    /// <summary>
    /// Sets the HTTP method for the request.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/set-method-policy">set-method</a> policy.
    /// </summary>
    /// <param name="method">
    /// Specifies the HTTP method to set for the request.
    /// </param>
    void SetMethod(string method);

    /// <summary>
    /// Sets or replaces the value of a query parameter in the request URL.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/set-query-parameter-policy">set-query-parameter</a> policy.
    /// </summary>
    /// <param name="name">
    /// Specifies the name of the query parameter to be set. Policy expressions are allowed.
    /// </param>
    /// <param name="values">
    /// Specifies the values of the query parameter to be set. Policy expressions are allowed.
    /// </param>
    void SetQueryParameter([ExpressionAllowed] string name, [ExpressionAllowed] params string[] values);

    /// <summary>
    /// Sets the value of a query parameter in the request URL if it does not already exist.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/set-query-parameter-policy">set-query-parameter</a> policy.
    /// </summary>
    /// <param name="name">
    /// Specifies the name of the query parameter to be set. Policy expressions are allowed.
    /// </param>
    /// <param name="values">
    /// Specifies the values of the query parameter to be set. Policy expressions are allowed.
    /// </param>
    void SetQueryParameterIfNotExist([ExpressionAllowed] string name, [ExpressionAllowed] params string[] values);

    /// <summary>
    /// Sets the HTTP status code and reason phrase for the response.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/set-status-policy">set-status</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the status code and reason phrase.
    /// </param>
    void SetStatus(StatusConfig config);

    /// <summary>
    /// Sets a variable with the specified name and value.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/set-variable-policy">set-variable</a> policy.
    /// </summary>
    /// <param name="name">
    /// Specifies the name of the variable to be set. Policy expressions are allowed.
    /// </param>
    /// <param name="value">
    /// Specifies the value of the variable to be set. Policy expressions are allowed.
    /// </param>
    void SetVariable(string name, [ExpressionAllowed] object value);

    /// <summary>
    /// Adds a trace message to the trace log.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/trace-policy">trace</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the trace source, message, severity, and optional metadata.
    /// </param>
    void Trace(TraceConfig config);

    /// <summary>
    /// Validates the Azure Active Directory token in the request.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/validate-azure-ad-token-policy">validate-azure-ad-token</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the validation parameters, including tenant ID, token location, validation settings, and expected claims.
    /// </param>
    void ValidateAzureAdToken(ValidateAzureAdTokenConfig config);

    /// <summary>
    /// Validates the client certificate presented in the request.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/validate-client-certificate-policy">validate-client-certificate</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the validation parameters, including revocation, trust, validity period, and identities.
    /// </param>
    void ValidateClientCertificate(ValidateClientCertificateConfig config);

    /// <summary>
    /// Validates the content of the request or response against specified rules.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/validate-content-policy">validate-content</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the validation rules, including content type, size limits, and schema validation.
    /// </param>
    void ValidateContent(ValidateContentConfig config);

    /// <summary>
    /// Validates the headers in the request against specified rules.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/validate-headers-policy">validate-headers</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the validation rules for headers, including actions for specified and unspecified headers.
    /// </param>
    void ValidateHeaders(ValidateHeadersConfig config);

    /// <summary>
    /// Validates a JWT (JSON Web Token) in the request.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/validate-jwt-policy">validate-jwt</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the validation parameters, including token location, validation settings, and expected claims.
    /// </param>
    void ValidateJwt(ValidateJwtConfig config);

    /// <summary>
    /// Validates an OData request to ensure it conforms to OData protocol specifications.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/validate-odata-request-policy">validate-odata-request</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the validation parameters, including OData version, size limits, and error handling.
    /// </param>
    void ValidateOdataRequest(ValidateOdataRequestConfig config);

    /// <summary>
    /// Validates the parameters of the request against specified rules.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/validate-parameters-policy">validate-parameters</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the validation rules for headers, query parameters, and path parameters, including actions for specified and unspecified parameters.
    /// </param>
    void ValidateParameters(ValidateParametersConfig config);

    /// <summary>
    /// Validates the status code in the response against specified rules.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/validate-status-code-policy">validate-status-code</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the validation rules for status codes, including actions for specified and unspecified status codes.
    /// </param>
    void ValidateStatusCode(ValidateStatusCodeConfig config);

    /// <summary>
    /// Executes its immediate child policies in parallel, and waits for either all or one of its immediate
    /// child policies to complete before it completes.<br/>
    /// The wait policy can have as its immediate child policies one or more of the following: send-request,
    /// cache-lookup-value, and choose policies.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/wait-policy">wait</a> policy.
    /// </summary>
    /// <param name="section">Child policies which should be awaited.</param>
    /// <param name="waitFor">
    /// Determines whether the wait policy waits for all immediate child policies to be completed or just one.<br/>
    /// Policy expressions are allowed.
    /// </param>
    void Wait(Action section, [ExpressionAllowed] string? waitFor = null);

    /// <summary>
    /// Converts XML content to JSON format.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/xml-to-json-policy">xml-to-json</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying how to convert XML to JSON, including options for applying the policy, considering the Accept header, and more.
    /// </param>
    void XmlToJson(XmlToJsonConfig config);

    /// <summary>
    /// Transforms XML in request or response body using XSL transform (XSLT).<br/>
    /// Can transform XML in the request body, response body, or a context variable.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/xsl-transform-policy">xsl-transform</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the transformation parameters, including where to apply the transformation (request, response, or variable),
    /// the XSL stylesheet to use, content type, and error handling options.
    /// </param>
    void XslTransform(XslTransformConfig config);
}