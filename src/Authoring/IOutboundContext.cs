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
    /// TODO
    /// </summary>
    /// <param name="duration"></param>
    /// <param name="cacheResponse"></param>
    void CacheStore(uint duration, bool? cacheResponse);

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
    /// The find-and-replace policy replaces occurrences of a specified string with another string in the request or response.
    /// </summary>
    /// <param name="from">The string to be replaced. Policy expressions are allowed.</param>
    /// <param name="to">The string to replace with. Policy expressions are allowed.</param>
    void FindAndReplace(string from, string to);

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
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void InvokeDarpBinding(InvokeDarpBindingConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="callbackParameterName"></param>
    void JsonP(string callbackParameterName);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void JsonToXml(JsonToXmlConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    /// <param name="section"></param>
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
    /// TODO
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
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void ReturnResponse(ReturnResponseConfig config);

    /// <summary>
    /// The retry policy executes its child policies once and then retries their execution until the retry condition
    /// becomes false or retry count is exhausted.
    /// </summary>
    /// <param name="config">Configuration of retry policy</param>
    /// <param name="section">Child policies which should be retried</param>
    void Retry(RetryConfig config, Action section);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void SendOneWayRequest(SendOneWayRequestConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void SendRequest(SendRequestConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="body"></param>
    /// <param name="config"></param>
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