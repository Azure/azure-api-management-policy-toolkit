// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.ApiManagement.PolicyToolkit.Authoring;

public interface IOutboundContext : IHaveExpressionContext
{
    /// <summary>
    /// The base policy used to specify when parent scope policy should be executed
    /// </summary>
    void Base();

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
    /// Deletes header of specified name.
    /// </summary>
    /// <param name="name">
    /// Specifies name of the header to be deleted. Policy expressions are allowed.
    /// </param>
    void RemoveHeader(string name);

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
    /// <param name="duration"></param>
    /// <param name="cacheResponse"></param>
    void CacheStore(uint duration, bool? cacheResponse);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void EmitMetric(EmitMetricConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="duration"></param>
    void LlmSemanticCacheStore(uint duration);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="duration"></param>
    void AzureOpenAiSemanticCacheStore(uint duration);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="config"></param>
    void JsonToXml(JsonToXmlConfig config);

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="callbackParameterName"></param>
    void JsonP(string callbackParameterName);

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
}