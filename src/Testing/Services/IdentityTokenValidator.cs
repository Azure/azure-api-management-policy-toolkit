// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

internal static class IdentityTokenValidator
{
    private static readonly Guid s_consumerTenant = Guid.Parse("9188040d-6c67-4c5b-b112-36a304b66dad");

    internal static Jwt Validate(GatewayContext context, ValidateJwtConfig config)
    {
        const string policy = nameof(IInboundContext.ValidateJwt);
        ValidateSource(config.HeaderName, config.QueryParameterName, config.TokenValue, allowDefault: false);
        ValidateOptions(config.OutputTokenVariableName, config.FailedValidationHttpCode, config.RequiredClaims);
        ValidateValues(config.Issuers, nameof(config.Issuers));
        ValidateValues(config.Audiences, nameof(config.Audiences));
        if (config.ClockSkew < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(config.ClockSkew), "JWT clock skew must not be negative.");
        }

        if (config.RequireScheme is not null)
        {
            ValidateValue(config.RequireScheme, nameof(config.RequireScheme));
        }

        var raw = Extract(context, config.HeaderName, config.QueryParameterName, config.TokenValue, config.RequireScheme);
        var urls = config.OpenIdConfigs?.Select(value => value.Url).ToArray() ?? [];
        ValidateValues(urls, nameof(config.OpenIdConfigs));
        var trust = GetTrust(context, new JwtKeyRequest(policy, null, urls));
        if (urls.Length != 0 && trust is null)
        {
            throw TrustUnavailable("OpenID configuration requires an offline IJwtKeyProvider; discovery is not fetched.");
        }

        if (urls.Length != 0 && config.Issuers is null && trust!.Issuers.Count == 0)
        {
            throw TrustUnavailable("The offline OpenID provider did not supply trusted issuers.");
        }

        var token = Authenticate(context, policy, raw, config.RequireSignedTokens ?? true,
            ResolveKeys(context, config.IssuerSigningKeys, decryption: false).Concat(trust?.SigningKeys ?? []).ToArray(),
            ResolveKeys(context, config.DecryptionKeys, decryption: true).Concat(trust?.DecryptionKeys ?? []).ToArray());
        ValidateLifetime(token, ValidationTime(context), config.RequireExpirationTime ?? true, config.ClockSkew ?? 0);
        ValidateIssuer(token, config.Issuers);
        if (trust is { Issuers.Count: > 0 })
        {
            ValidateIssuer(token, trust.Issuers);
        }

        ValidateAudience(token, config.Audiences);
        ValidateClaims(token, config.RequiredClaims);
        return token;
    }

    internal static Jwt Validate(GatewayContext context, ValidateAzureAdTokenConfig config)
    {
        const string policy = nameof(IInboundContext.ValidateAzureAdToken);
        ValidateValue(config.TenantId, nameof(config.TenantId));
        ValidateSource(config.HeaderName, config.QueryParameterName, config.TokenValue, allowDefault: true);
        ValidateOptions(config.OutputTokenVariableName, config.FailedValidationHttpCode, config.RequiredClaims);
        ValidateValues(config.Audiences, nameof(config.Audiences));
        ValidateValues(config.BackendApplicationIds, nameof(config.BackendApplicationIds));
        ValidateValues(config.ClientApplicationIds, nameof(config.ClientApplicationIds));

        var raw = Extract(context, config.HeaderName, config.QueryParameterName, config.TokenValue, "Bearer");
        var trust = GetTrust(context, new JwtKeyRequest(policy, config.TenantId, []));
        var decryptionKeys = config.DecryptionKeys?.Select(key =>
            (KeyConfig)new CertificateKeyConfig { CertificateId = key.CertificateId }).ToArray();
        var token = Authenticate(context, policy, raw, requireSigned: true, trust?.SigningKeys ?? [],
            ResolveKeys(context, decryptionKeys, decryption: true).Concat(trust?.DecryptionKeys ?? []).ToArray());
        ValidateLifetime(token, ValidationTime(context), requireExpiration: true, clockSkew: 0);
        ValidateTenant(token, config.TenantId, trust?.TenantId);
        var version = SingleClaim(token, "ver");
        if (version is not ("1.0" or "2.0"))
        {
            throw new IdentityValidationException("TokenMalformed", "The Entra token version must be 1.0 or 2.0.");
        }

        var tenant = SingleClaim(token, "tid");
        var expectedIssuer = version == "2.0"
            ? $"https://login.microsoftonline.com/{tenant}/v2.0"
            : $"https://sts.windows.net/{tenant}/";
        if (!string.Equals(token.Issuer, expectedIssuer, StringComparison.OrdinalIgnoreCase))
        {
            throw new IdentityValidationException("TokenIssuerNotAllowed", "The token issuer does not match its Entra tenant and version.");
        }

        if (trust is { Issuers.Count: > 0 })
        {
            ValidateIssuer(token, trust.Issuers);
        }

        if (config.BackendApplicationIds is not null
            && !token.Audiences.Any(audience => config.BackendApplicationIds.Any(application =>
                ApplicationMatches(application, audience)
                || string.Equals($"api://{application}", audience, StringComparison.OrdinalIgnoreCase))))
        {
            throw new IdentityValidationException("TokenBackendApplicationNotAllowed", "The token backend application is not allowed.");
        }

        if (config.ClientApplicationIds is not null)
        {
            var application = SingleClaim(token, version == "2.0" ? "azp" : "appid");
            if (application is null || !config.ClientApplicationIds.Any(allowed => ApplicationMatches(allowed, application)))
            {
                throw new IdentityValidationException("TokenClientApplicationNotAllowed", "The token client application is not allowed.");
            }
        }

        ValidateAudience(token, config.Audiences);
        ValidateClaims(token, config.RequiredClaims);
        return token;
    }

    internal static DateTimeOffset ValidationTime(GatewayContext context) =>
        new DateTimeOffset(context.Timestamp.ToUniversalTime()).Add(context.Elapsed);

    private static Jwt Authenticate(GatewayContext context, string policy, string raw, bool requireSigned,
        IReadOnlyList<SecurityKey> signingKeys, IReadOnlyList<SecurityKey> decryptionKeys)
    {
        var validator = context.Services.Resolve<IJwtValidator>(policy)
            ?? context.Services.Resolve<IJwtValidator>()
            ?? new JwtValidator();
        var token = validator.Validate(new JwtValidationRequest(
            policy, raw, requireSigned, signingKeys, decryptionKeys, ValidationTime(context)));
        if (token is null || token.Claims is null || token.Audiences is null
            || string.IsNullOrWhiteSpace(token.Algorithm)
            || token.Claims.Any(claim => claim.Value is null))
        {
            throw new InvalidOperationException("The JWT validator must return an authenticated Jwt with nonnull claims and audiences.");
        }

        if (requireSigned && token.Algorithm == SecurityAlgorithms.None)
        {
            throw new SecurityTokenInvalidSignatureException("An unsigned JWT is not permitted.");
        }

        return token;
    }

    private static JwtTrustMaterial? GetTrust(GatewayContext context, JwtKeyRequest request)
    {
        var provider = context.Services.Resolve<IJwtKeyProvider>(request.PolicyName)
            ?? context.Services.Resolve<IJwtKeyProvider>();
        if (provider is null)
        {
            return null;
        }

        var trust = provider.GetKeys(request);
        if (trust is null || trust.SigningKeys is null || trust.DecryptionKeys is null || trust.Issuers is null
            || trust.SigningKeys.Any(key => key is null) || trust.DecryptionKeys.Any(key => key is null)
            || trust.Issuers.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("The JWT key provider returned invalid trust material.");
        }

        return trust;
    }

    private static IEnumerable<SecurityKey> ResolveKeys(GatewayContext context, KeyConfig[]? configs, bool decryption)
    {
        if (configs is null) { yield break; }
        foreach (var config in configs)
        {
            SecurityKey key;
            switch (config)
            {
                case Base64KeyConfig symmetric:
                    ValidateValue(symmetric.Value, nameof(symmetric.Value));
                    try
                    {
                        key = new SymmetricSecurityKey(Convert.FromBase64String(symmetric.Value));
                    }
                    catch (FormatException error)
                    {
                        throw new ArgumentException("The configured JWT key is not Base64.", nameof(configs), error);
                    }

                    break;
                case AsymmetricKeyConfig asymmetric when !decryption:
                    ValidateValue(asymmetric.Modulus, nameof(asymmetric.Modulus));
                    ValidateValue(asymmetric.Exponent, nameof(asymmetric.Exponent));
                    try
                    {
                        key = new RsaSecurityKey(new RSAParameters
                        {
                            Modulus = Base64UrlEncoder.DecodeBytes(asymmetric.Modulus),
                            Exponent = Base64UrlEncoder.DecodeBytes(asymmetric.Exponent)
                        });
                    }
                    catch (FormatException error)
                    {
                        throw new ArgumentException("The configured RSA key is not Base64url.", nameof(configs), error);
                    }

                    break;
                case CertificateKeyConfig certificateKey:
                    ValidateValue(certificateKey.CertificateId, nameof(certificateKey.CertificateId));
                    var certificate = FindCertificate(context, certificateKey.CertificateId)
                        ?? throw TrustUnavailable($"The JWT key certificate '{certificateKey.CertificateId}' is missing.");
                    if (decryption && !certificate.HasPrivateKey)
                    {
                        throw TrustUnavailable($"The JWT decryption certificate '{certificateKey.CertificateId}' has no private key.");
                    }

                    key = new X509SecurityKey(certificate);
                    break;
                default:
                    throw new ArgumentException("The configured JWT key type cannot be used for this operation.", nameof(configs));
            }

            if (config.Id is not null) { key.KeyId = config.Id; }
            yield return key;
        }
    }

    internal static X509Certificate2? FindCertificate(GatewayContext context, string id) =>
        context.CertificateStore.ById.GetValueOrDefault(id) ?? context.Deployment.Certificates.GetValueOrDefault(id);

    private static void ValidateSource(string? header, string? query, string? token, bool allowDefault)
    {
        if (header is not null) { ValidateValue(header, nameof(header)); }
        if (query is not null) { ValidateValue(query, nameof(query)); }
        var count = (header is not null ? 1 : 0) + (query is not null ? 1 : 0) + (token is not null ? 1 : 0);
        if (count > 1 || (count == 0 && !allowDefault))
        {
            throw new ArgumentException("The identity policy requires exactly one token source.");
        }
    }

    private static string Extract(GatewayContext context, string? header, string? query, string? token, string? scheme)
    {
        if (token is not null) { return RequireToken(token); }
        if (query is not null)
        {
            context.Request.Url.Query.TryGetValue(query, out var values);
            return SingleToken(values);
        }

        header ??= "Authorization";
        var entries = context.Request.Headers
            .Where(entry => string.Equals(entry.Key, header, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (entries.Length == 0) { throw MissingToken(); }
        if (entries.Length != 1)
        {
            throw new IdentityValidationException("TokenMalformed", "The JWT header is ambiguous.");
        }

        var value = SingleToken(entries[0].Value).Trim();
        if (!string.Equals(header, "Authorization", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var separator = value.IndexOfAny([' ', '\t']);
        if (separator < 0)
        {
            if (scheme is not null) { throw InvalidScheme(); }
            return value;
        }

        if (scheme is not null && !string.Equals(value[..separator], scheme, StringComparison.OrdinalIgnoreCase))
        {
            throw InvalidScheme();
        }

        return RequireToken(value[(separator + 1)..].Trim());
    }

    private static string SingleToken(string[]? values)
    {
        if (values is null || values.Length == 0) { throw MissingToken(); }
        if (values.Length != 1)
        {
            throw new IdentityValidationException("TokenMalformed", "The JWT token source must contain exactly one value.");
        }

        return RequireToken(values[0]);
    }

    private static string RequireToken(string? token) => !string.IsNullOrWhiteSpace(token) ? token : throw MissingToken();

    private static void ValidateOptions(string? output, int? failureCode, ClaimConfig[]? claims)
    {
        if (output is not null) { ValidateValue(output, nameof(output)); }
        if (failureCode is < 100 or > 599)
        {
            throw new ArgumentOutOfRangeException(nameof(failureCode), "The failed-validation HTTP code must be between 100 and 599.");
        }

        if (claims is null) { return; }
        foreach (var claim in claims)
        {
            ValidateValue(claim.Name, nameof(claim.Name));
            if (claim.Match is not (null or "all" or "any"))
            {
                throw new ArgumentException("Required-claim match must be all or any.", nameof(claim.Match));
            }

            if (claim.Separator == string.Empty)
            {
                throw new ArgumentException("A claim separator must not be empty.", nameof(claim.Separator));
            }

            if (claim.Values?.Any(value => value is null) == true)
            {
                throw new ArgumentException("Required-claim values must not be null.", nameof(claim.Values));
            }
        }
    }

    private static void ValidateLifetime(Jwt token, DateTimeOffset now, bool requireExpiration, int clockSkew)
    {
        if (requireExpiration && token.ExpirationTime is null)
        {
            throw new IdentityValidationException("TokenExpirationMissing", "JWT expiration time is required.");
        }

        if (token.ExpirationTime is { } expiration && now.UtcDateTime - expiration.ToUniversalTime() >= TimeSpan.FromSeconds(clockSkew))
        {
            throw new IdentityValidationException("TokenExpired", "JWT has expired.");
        }

        if (token.NotBefore is { } notBefore && notBefore.ToUniversalTime() - now.UtcDateTime > TimeSpan.FromSeconds(clockSkew))
        {
            throw new IdentityValidationException("TokenNotYetValid", "JWT is not yet valid.");
        }

        if (token.NotBefore > token.ExpirationTime)
        {
            throw new IdentityValidationException("TokenMalformed", "JWT not-before time exceeds its expiration time.");
        }
    }

    private static void ValidateIssuer(Jwt token, IReadOnlyList<string>? issuers)
    {
        if (issuers is not null && !issuers.Contains(token.Issuer, StringComparer.Ordinal))
        {
            throw new IdentityValidationException("TokenIssuerNotAllowed", "JWT issuer is not allowed.");
        }
    }

    private static void ValidateAudience(Jwt token, string[]? audiences)
    {
        if (audiences is not null && !token.Audiences.Any(audience => audiences.Contains(audience, StringComparer.Ordinal)))
        {
            throw new IdentityValidationException("TokenAudienceNotAllowed", "JWT audience is not allowed.");
        }
    }

    private static void ValidateClaims(Jwt token, ClaimConfig[]? claims)
    {
        if (claims is null) { return; }
        foreach (var claim in claims)
        {
            var actual = ClaimValues(token, claim.Name);
            if (actual is null || actual.Length == 0)
            {
                throw new IdentityValidationException("TokenClaimNotPresent", $"JWT required claim '{claim.Name}' is not present.");
            }

            if (claim.Values is null || claim.Values.Length == 0) { continue; }
            var values = claim.Separator is null
                ? actual
                : actual.SelectMany(value => value.Split(claim.Separator, StringSplitOptions.None)).ToArray();
            var matches = claim.Match == "any"
                ? claim.Values.Any(expected => values.Contains(expected, StringComparer.Ordinal))
                : claim.Values.All(expected => values.Contains(expected, StringComparer.Ordinal));
            if (!matches)
            {
                throw new IdentityValidationException("TokenClaimValueNotAllowed", $"JWT required claim '{claim.Name}' does not match.");
            }
        }
    }

    private static string[]? ClaimValues(Jwt token, string name) =>
        token.Claims.FirstOrDefault(claim => string.Equals(claim.Key, name, StringComparison.Ordinal)).Value;

    private static string? SingleClaim(Jwt token, string name)
    {
        var values = ClaimValues(token, name);
        return values is { Length: 1 } ? values[0] : null;
    }

    private static void ValidateTenant(Jwt token, string configuredTenant, string? providerTenant)
    {
        if (!Guid.TryParseExact(SingleClaim(token, "tid"), "D", out var tenant))
        {
            throw new IdentityValidationException("TokenTenantNotAllowed", "The Entra token tenant is missing or invalid.");
        }

        var alias = configuredTenant;
        if (Uri.TryCreate(configuredTenant, UriKind.Absolute, out var tenantUrl)
            && tenantUrl.Scheme == Uri.UriSchemeHttps
            && tenantUrl.IsDefaultPort && string.IsNullOrEmpty(tenantUrl.UserInfo)
            && string.Equals(tenantUrl.Host, "login.microsoftonline.com", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrEmpty(tenantUrl.Query) && string.IsNullOrEmpty(tenantUrl.Fragment))
        {
            alias = tenantUrl.AbsolutePath.Trim('/');
        }

        var common = string.Equals(alias, "common", StringComparison.OrdinalIgnoreCase);
        var organizations = string.Equals(alias, "organizations", StringComparison.OrdinalIgnoreCase);
        Guid? mappedTenant = null;
        if (providerTenant is not null)
        {
            if (!Guid.TryParseExact(providerTenant, "D", out var canonical))
            {
                throw new InvalidOperationException("The JWT key provider's canonical tenant must be a GUID.");
            }

            mappedTenant = canonical;
        }

        if (common || organizations)
        {
            if ((organizations && tenant == s_consumerTenant) || (mappedTenant is { } mapped && mapped != tenant))
            {
                throw new IdentityValidationException("TokenTenantNotAllowed", "The Entra token tenant is not allowed.");
            }

            return;
        }

        if (Guid.TryParseExact(alias, "D", out var expectedTenant))
        {
            if (tenant != expectedTenant || (mappedTenant is { } mapped && mapped != expectedTenant))
            {
                throw new IdentityValidationException("TokenTenantNotAllowed", "The Entra token tenant is not allowed.");
            }

            return;
        }

        if (mappedTenant is null)
        {
            throw TrustUnavailable("The Entra tenant alias requires a canonical tenant GUID from an offline IJwtKeyProvider.");
        }

        if (mappedTenant != tenant)
        {
            throw new IdentityValidationException("TokenTenantNotAllowed", "The Entra token tenant is not allowed.");
        }
    }

    private static bool ApplicationMatches(string expected, string actual) =>
        Guid.TryParseExact(expected, "D", out var expectedId) && Guid.TryParseExact(actual, "D", out var actualId)
            ? expectedId == actualId
            : string.Equals(expected, actual, StringComparison.Ordinal);

    private static void ValidateValues(IEnumerable<string>? values, string name)
    {
        if (values?.Any(string.IsNullOrWhiteSpace) == true)
        {
            throw new ArgumentException("Identity policy values must not be empty.", name);
        }
    }

    private static void ValidateValue(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The identity policy value must not be empty.", name);
        }
    }

    private static IdentityValidationException MissingToken() => new("TokenNotPresent", "JWT not present.");
    private static IdentityValidationException InvalidScheme() => new("TokenSchemeInvalid", "JWT authorization scheme is not allowed.");
    private static IdentityValidationException TrustUnavailable(string message) => new("TokenTrustUnavailable", message);
}
