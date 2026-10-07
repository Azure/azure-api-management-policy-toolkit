// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

public interface IUrl
{
    string Host { get; }
    string Path { get; }
    int Port { get; }
    IReadOnlyDictionary<string, string[]> Query { get; }
    string QueryString { get; }
    string Scheme { get; }

    /// <summary>
    /// The whole URL as text.
    /// </summary>
    string ToString();
}