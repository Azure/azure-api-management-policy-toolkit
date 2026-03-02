// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

public interface IBackendContext : IHaveExpressionContext
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
    IBackendContext WithId(string id);

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
    /// Stores a value in the cache using a specified key.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/cache-store-value-policy">cache-store-value</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the cache key, value, duration, and optional caching type.
    /// </param>
    void CacheStoreValue(CacheStoreValueConfig config);

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
    /// Sends messages in the specified format to an Azure Event Hub defined by a Logger entity.<br/>
    /// Used for saving selected request or response context information for online or offline analysis.<br/>
    /// Compiled to <a href="https://learn.microsoft.com/en-us/azure/api-management/log-to-eventhub-policy">log-to-eventhub</a> policy.
    /// </summary>
    /// <param name="config">
    /// Configuration specifying the logger entity, message content, and optional partition settings.
    /// </param>
    void LogToEventHub(LogToEventHubConfig config);

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
}