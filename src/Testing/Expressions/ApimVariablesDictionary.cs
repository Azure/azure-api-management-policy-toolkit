// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

/// <summary>
/// Dictionary that matches APIM runtime behavior: returns null for missing keys
/// when accessed through <see cref="IReadOnlyDictionary{TKey,TValue}"/> (as compiled
/// policy expressions do), instead of throwing <see cref="KeyNotFoundException"/>.
/// </summary>
public class ApimVariablesDictionary : Dictionary<string, object>, IReadOnlyDictionary<string, object>
{
    object IReadOnlyDictionary<string, object>.this[string key] =>
        TryGetValue(key, out var value) ? value : null!;

    IEnumerable<string> IReadOnlyDictionary<string, object>.Keys => Keys;
    IEnumerable<object> IReadOnlyDictionary<string, object>.Values => Values;
    bool IReadOnlyDictionary<string, object>.ContainsKey(string key) => ContainsKey(key);

    bool IReadOnlyDictionary<string, object>.TryGetValue(string key, out object value) =>
        TryGetValue(key, out value!);
}
