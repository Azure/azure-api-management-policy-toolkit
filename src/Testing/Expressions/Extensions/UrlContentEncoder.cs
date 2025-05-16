// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Encodings.Web;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Implementations;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class UrlContentEncoder : IUrlContentEncoder
{
    public string? Encode(IDictionary<string, IList<string>>? dictionary)
        => UrlContentEncoder.Encode(dictionary);

    public static string? Encode<T>(IDictionary<string, T>? dictionary) where T : IEnumerable<string>
    {
        if (dictionary == null || dictionary.Count == 0)
        {
            return null;
        }

        return dictionary.SelectMany(kvp => kvp.Value.Select(v => (kvp.Key, Value: v)))
            .Aggregate(
                new StringBuilder(),
                (sb, kvp) =>
                {
                    if (sb.Length > 0)
                    {
                        sb.Append('&');
                    }

                    sb.Append(UrlEncoder.Default.Encode(kvp.Key));
                    sb.Append('=');
                    sb.Append(UrlEncoder.Default.Encode(kvp.Value));
                    return sb;
                },
                sb => sb.ToString());
    }
}