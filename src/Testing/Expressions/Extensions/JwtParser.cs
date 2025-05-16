// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Headers;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Implementations;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

public class JwtParser : IJwtParser
{
    private const string BearerPrefix = "Bearer";
    readonly JsonWebTokenHandler _handler = new JsonWebTokenHandler();

    public Jwt? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !AuthenticationHeaderValue.TryParse(value, out var header)
            || !header.Scheme.Equals(BearerPrefix, StringComparison.OrdinalIgnoreCase)
            || !_handler.CanReadToken(header.Parameter))
        {
            return null;
        }

        try
        {
            return new JwtImpl(_handler.ReadJsonWebToken(header.Parameter));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public class JwtImpl : Jwt
    {
        readonly JsonWebToken _jwt;

        public JwtImpl(JsonWebToken jwt)
        {
            _jwt = jwt;
        }

        public string Id => _jwt.Id;
        public string Algorithm => _jwt.Alg;
        public string Issuer => _jwt.Issuer;
        public string Subject => _jwt.Subject;
        public string Type => _jwt.Typ;
        public IEnumerable<string> Audiences => _jwt.Audiences;

        private IReadOnlyDictionary<string, string[]>? _claims;

        public IReadOnlyDictionary<string, string[]> Claims => _claims ??= _jwt.Claims
            .GroupBy(c => c.Type)
            .ToDictionary(g => g.Key,
                g => g.Select(c => c.Value).ToArray()
            );

        public DateTime? ExpirationTime => _jwt.ValidTo == DateTime.MinValue ? null : _jwt.ValidTo;
        public DateTime? NotBefore => _jwt.ValidFrom == DateTime.MinValue ? null : _jwt.ValidFrom;
        public DateTime? IssuedAt => _jwt.IssuedAt == DateTime.MinValue ? null : _jwt.IssuedAt;
    }
}