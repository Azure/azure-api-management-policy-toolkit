// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// Replaces numeric policy outputs without replacing the supplied header dictionary or comparer.
/// </summary>
internal static class ResponseHeaderUtilities
{
    internal static void SetNumericHeader(Dictionary<string, string[]> headers, string? name, long value)
    {
        if (name is null)
        {
            return;
        }

        RemoveCaseVariants(headers, name);
        headers[name] = [value.ToString(CultureInfo.InvariantCulture)];
    }

    internal static void RemoveCaseVariants(Dictionary<string, string[]> headers, string name)
    {
        foreach (var existing in headers.Keys.Where(key => key.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            headers.Remove(existing);
        }
    }
}