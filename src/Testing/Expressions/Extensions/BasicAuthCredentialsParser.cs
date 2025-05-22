// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using System.Text;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Implementations;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class BasicAuthCredentialsParser : IBasicAuthCredentialsParser
{
    // Header fields can be encoded with iso-8859-1. See 
    // http://tools.ietf.org/html/rfc7230#section-3.2.4
    static readonly Encoding HeaderFieldEncoding = Encoding.GetEncoding("iso-8859-1");

    public BasicAuthCredentials? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!AuthenticationHeaderValue.TryParse(value, out var header)
            || !"Basic".Equals(header.Scheme, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(header.Parameter))
        {
            return null;
        }

        try
        {
            var decoded = HeaderFieldEncoding.GetString(Convert.FromBase64String(header.Parameter));
            var index = decoded.LastIndexOf(':');
            return index > 0
                ? new MockBasicAuthCredentials(decoded[..index], decoded[(index + 1)..])
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}