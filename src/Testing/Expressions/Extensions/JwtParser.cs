// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Implementations;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class JwtParser : IJwtParser
{
    private static readonly string[] EmptyClaims = [];
    private static readonly string[] EmptyJsonArrayClaim = ["[]"];
    private const string BearerPrefix = "Bearer ";

    public Jwt? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var token = value.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? value[BearerPrefix.Length..].Trim()
            : value.Trim();

        var parts = token.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        try
        {
            var headerJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));

            using var headerDoc = JsonDocument.Parse(headerJson);
            using var payloadDoc = JsonDocument.Parse(payloadJson);

            string GetHeaderProp(string name) =>
                headerDoc.RootElement.TryGetProperty(name, out var headerValue)
                    && headerValue.ValueKind == JsonValueKind.String
                    ? headerValue.GetString() ?? string.Empty
                    : string.Empty;

            var claims = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in payloadDoc.RootElement.EnumerateObject())
            {
                claims[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.Array => prop.Value.EnumerateArray()
                        .Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : v.ToString())
                        .ToArray(),
                    JsonValueKind.String => [prop.Value.GetString() ?? string.Empty],
                    JsonValueKind.Number => [prop.Value.TryGetInt64(out var l)
                        ? l.ToString()
                        : prop.Value.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture)],
                    JsonValueKind.True => ["true"],
                    JsonValueKind.False => ["false"],
                    JsonValueKind.Null => EmptyClaims,
                    _ => [prop.Value.ToString()]
                };
            }

            DateTime? FromEpoch(string claim)
            {
                if (!claims.TryGetValue(claim, out var vals) || vals.Length == 0)
                {
                    return null;
                }

                return long.TryParse(vals[0], out var seconds)
                    ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
                    : null;
            }

            claims.TryGetValue("jti", out var jtiValues);
            claims.TryGetValue("iss", out var issuerValues);
            claims.TryGetValue("sub", out var subjectValues);

            return new MockJwt
            {
                Algorithm = GetHeaderProp("alg"),
                Type = GetHeaderProp("typ"),
                Claims = new ApimClaimsDictionary(claims),
                Audiences = claims.TryGetValue("aud", out var audienceValues) ? audienceValues : EmptyClaims,
                Id = jtiValues?.FirstOrDefault() ?? string.Empty,
                Issuer = issuerValues?.FirstOrDefault() ?? string.Empty,
                Subject = subjectValues?.FirstOrDefault() ?? string.Empty,
                ExpirationTime = FromEpoch("exp"),
                IssuedAt = FromEpoch("iat"),
                NotBefore = FromEpoch("nbf")
            };
        }
        catch
        {
            return null;
        }
    }

    private static byte[] Base64UrlDecode(string segment)
    {
        var normalized = segment.Replace('-', '+').Replace('_', '/');
        switch (normalized.Length % 4)
        {
            case 2:
                normalized += "==";
                break;
            case 3:
                normalized += "=";
                break;
        }

        return Convert.FromBase64String(normalized);
    }

    private sealed class ApimClaimsDictionary : IReadOnlyDictionary<string, string[]>
    {
        private readonly IReadOnlyDictionary<string, string[]> _claims;

        public ApimClaimsDictionary(IReadOnlyDictionary<string, string[]> claims)
        {
            _claims = claims;
        }

        public string[] this[string key]
        {
            get
            {
                if (_claims.TryGetValue(key, out var value))
                {
                    return value;
                }

                return string.Equals(key, "allowed-paths", StringComparison.OrdinalIgnoreCase)
                    ? EmptyJsonArrayClaim
                    : EmptyClaims;
            }
        }

        public IEnumerable<string> Keys => _claims.Keys;
        public IEnumerable<string[]> Values => _claims.Values;
        public int Count => _claims.Count;

        public bool ContainsKey(string key) => _claims.ContainsKey(key)
            || string.Equals(key, "allowed-paths", StringComparison.OrdinalIgnoreCase);

        public bool TryGetValue(string key, out string[] value)
        {
            if (_claims.TryGetValue(key, out value!))
            {
                return true;
            }

            value = string.Equals(key, "allowed-paths", StringComparison.OrdinalIgnoreCase)
                ? EmptyJsonArrayClaim
                : EmptyClaims;
            return true;
        }

        public IEnumerator<KeyValuePair<string, string[]>> GetEnumerator() => _claims.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
