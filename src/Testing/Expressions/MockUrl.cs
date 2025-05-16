// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Web;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class MockUrl : IUrl
{
    public string Scheme { get; set; } = "https";
    public string Host { get; set; } = "contoso.example";
    public string Port { get; set; } = "443";
    public string Path { get; set; } = "/v2/mock/op";

    public Dictionary<string, string[]> Query { get; set; } = new Dictionary<string, string[]> { };
    IReadOnlyDictionary<string, string[]> IUrl.Query => Query;

    public string QueryString
    {
        get
        {
            var content = UrlContentEncoder.Encode(Query);
            return string.IsNullOrEmpty(content) ? "" : $"?{content}";
        }
        set
        {
            var nameValueCollection = HttpUtility.ParseQueryString(value);
            var newQuery = new Dictionary<string, string[]>();
            foreach (var key in nameValueCollection.AllKeys)
            {
                newQuery[key!] = nameValueCollection.GetValues(key)!;
            }

            Query = newQuery;
        }
    }
}