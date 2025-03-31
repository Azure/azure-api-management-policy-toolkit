// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public interface IOutboundContext : IHaveExpressionContext
{
    /// <summary>
    /// Adds header of specified name with values or appends values if header already exists.
    /// </summary>
    /// <param name="name">
    /// Specifies name of the header to be added. Policy expressions are allowed.
    /// </param>
    /// <param name="values">
    /// Specifies the values of the header to be set or appended. Policy expressions are allowed.
    /// </param>
    void AppendHeader(string name, params string[] values);

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
    /// The base policy used to specify when parent scope policy should be executed
    /// </summary>
    void Base();

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
    void CacheStore([ExpressionAllowed] uint duration, [ExpressionAllowed] bool? cacheResponse);

    /// <summary>
    /// Stores a value in the cache using a specified key.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/cache-store-value-policy">cache-store-value</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the cache key, value, duration, and optional caching type.
    /// </param>
    void CacheStoreValue(CacheStoreValueConfig config);

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
    /// Invokes a Dapr binding with the specified configuration.<br/>
    /// This policy allows you to interact with Dapr bindings to trigger external resources or services.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/invoke-dapr-binding-policy">invoke-dapr-binding</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the Dapr binding name, operation, metadata, and other settings.
    /// </param>
    void InvokeDarpBinding(InvokeDarpBindingConfig config);

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
    /// Redirects URLs in the response content to a specified hostname and scheme.<br/>
    /// This policy rewrites URLs in the response body to point to the gateway URL instead of the backend service URL.<br/>
    /// Useful when backend services return absolute URLs that need to be redirected through the API Management gateway.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/redirect-content-urls-policy">redirect-content-urls</a> policy.
    /// </summary>
    void RedirectContentUrls();

    /// <summary>
    /// Deletes header of specified name.
    /// </summary>
    /// <param name="name">
    /// Specifies name of the header to be deleted. Policy expressions are allowed.
    /// </param>
    void RemoveHeader(string name);

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
    /// Sets or replaces the request or response body with the specified value.<br />
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/set-body-policy">set-body</a> policy.
    /// </summary>
    /// <param name="body">
    /// The value to set as the body. Policy expressions are allowed.
    /// </param>
    /// <param name="config">
    /// Optional configuration specifying template, xsi:nil, and parse date settings.
    /// </param>
    void SetBody(string body, SetBodyConfig? config = null);

    /// <summary>
    /// Adds header of specified name with values or overrides values if header already exists.
    /// </summary>
    /// <param name="name">
    /// Specifies name of the header to be added. Policy expressions are allowed.
    /// </param>
    /// <param name="values">
    /// Specifies the values of the header to be set. Policy expressions are allowed.
    /// </param>
    void SetHeader(string name, params string[] values);

    /// <summary>
    /// Sets header of specified name and values if header not already exist.
    /// </summary>
    /// <param name="name">
    /// Specifies name of the header to be added. Policy expressions are allowed.
    /// </param>
    /// <param name="values">
    /// Specifies the values of the header to be set. Policy expressions are allowed.
    /// </param>
    void SetHeaderIfNotExist(string name, params string[] values);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void SetStatus(StatusConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="name"></param>
    /// <param name="value"></param>
    void SetVariable(string name, object value);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void Trace(TraceConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void ValidateClientCertificate(ValidateClientCertificateConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void ValidateContent(ValidateContentConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void ValidateHeaders(ValidateHeadersConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void ValidateStatusCode(ValidateStatusCodeConfig config);

    /// <summary>
    /// The wait policy executes its immediate child policies in parallel, and waits for either all or one of its immediate
    /// child policies to complete before it completes.
    /// The wait policy can have as its immediate child policies one or more of the following: send-request,
    /// cache-lookup-value, and choose policies.
    /// </summary>
    /// <param name="section">Child policies which should be awaited</param>
    /// <param name="waitFor">
    /// Determines whether the wait policy waits for all immediate child policies to be completed or just one.
    /// Policy expressions are allowed.
    /// </param>
    void Wait(Action section, string? waitFor = null);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void XslTransform(XslTransformConfig config);
}