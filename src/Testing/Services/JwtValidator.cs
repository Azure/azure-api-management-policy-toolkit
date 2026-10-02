// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

/// <summary>
/// Authenticates compact signed or encrypted JWTs using explicitly supplied keys, without network access.
/// Identity policy handlers enforce lifetime, issuer, audience, and claim requirements after authentication.
/// </summary>
/// <remarks>
/// Fractional NumericDates are converted to UTC clock ticks (100 ns), retaining exact decimal lifetime ordering.
/// JWE key-management algorithms are checked independently of signature and content-encryption algorithms.
/// </remarks>
public sealed class JwtValidator : IJwtValidator
{
    private static readonly string[] s_signingAlgorithms =
    [
        SecurityAlgorithms.HmacSha256, SecurityAlgorithms.HmacSha384, SecurityAlgorithms.HmacSha512,
        SecurityAlgorithms.RsaSha256, SecurityAlgorithms.RsaSha384, SecurityAlgorithms.RsaSha512,
        SecurityAlgorithms.RsaSsaPssSha256, SecurityAlgorithms.RsaSsaPssSha384, SecurityAlgorithms.RsaSsaPssSha512,
        SecurityAlgorithms.EcdsaSha256, SecurityAlgorithms.EcdsaSha384, SecurityAlgorithms.EcdsaSha512
    ];

    private static readonly string[] s_keyManagementAlgorithms =
    [
        "dir", "A128KW", "A192KW", "A256KW", "RSA-OAEP", "RSA-OAEP-256"
    ];

    private static readonly string[] s_contentEncryptionAlgorithms =
    [
        "A128CBC-HS256", "A192CBC-HS384", "A256CBC-HS512", "A128GCM", "A192GCM", "A256GCM"
    ];

    /// <inheritdoc />
    public Jwt Validate(JwtValidationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        if (!handler.CanReadToken(request.Token))
        {
            throw Malformed();
        }

        var parts = request.Token.Split('.');
        using var header = ReadObject(parts[0]);
        RejectCriticalHeaders(header.RootElement);
        if (parts.Length == 3)
        {
            CheckSignatureShape(header.RootElement, parts[2], request);
            using var payload = ReadObject(parts[1]);
            ReadDate(payload.RootElement, "exp");
            ReadDate(payload.RootElement, "nbf");
            ReadDate(payload.RootElement, "iat");
        }
        else if (parts.Length == 5)
        {
            CheckEncryptionHeader(header.RootElement);
            if (request.DecryptionKeys.Count == 0)
            {
                throw new IdentityValidationException("TokenTrustUnavailable",
                    "JWT decryption trust material is missing. Supply decryption keys or register an IJwtValidator.");
            }

            if (request.RequireSignedTokens && request.SigningKeys.Count == 0)
            {
                throw MissingSigningTrust();
            }
        }
        else
        {
            throw Malformed();
        }

        var parameters = new TokenValidationParameters
        {
            RequireSignedTokens = request.RequireSignedTokens,
            RequireExpirationTime = false,
            ValidateLifetime = false,
            ValidateIssuer = false,
            ValidateAudience = false,
            // Signature verification is always performed; uploaded key certificates are not client trust chains.
            ValidateIssuerSigningKey = false,
            IssuerSigningKeys = request.SigningKeys,
            TokenDecryptionKeys = request.DecryptionKeys,
            TryAllIssuerSigningKeys = true,
            ValidAlgorithms = [.. s_signingAlgorithms, .. s_contentEncryptionAlgorithms],
            ClockSkew = TimeSpan.Zero
        };
        SecurityToken authenticated;
        try
        {
            handler.ValidateToken(request.Token, parameters, out authenticated);
        }
        catch (SecurityTokenMalformedException)
        {
            throw Malformed();
        }

        if (authenticated is not JwtSecurityToken jwt)
        {
            throw new InvalidOperationException("JWT authentication did not return a JWT.");
        }

        jwt = jwt.InnerToken ?? jwt;
        var authenticatedParts = jwt.RawData.Split('.');
        if (authenticatedParts.Length != 3)
        {
            throw Malformed();
        }

        using var authenticatedHeader = ReadObject(authenticatedParts[0]);
        using var authenticatedPayload = ReadObject(authenticatedParts[1]);
        RejectCriticalHeaders(authenticatedHeader.RootElement);
        CheckSignatureShape(authenticatedHeader.RootElement, authenticatedParts[2], request);
        return CreateToken(authenticatedHeader.RootElement, authenticatedPayload.RootElement);
    }

    private static void CheckSignatureShape(JsonElement header, string signature, JwtValidationRequest request)
    {
        var algorithm = ReadString(header, "alg", required: true);
        if (algorithm == SecurityAlgorithms.None)
        {
            if (request.RequireSignedTokens || signature.Length != 0)
            {
                throw new SecurityTokenInvalidSignatureException("An unsigned JWT is not permitted.");
            }
        }
        else
        {
            if (signature.Length == 0)
            {
                throw new SecurityTokenInvalidSignatureException("A signed JWT must contain a signature.");
            }

            if (!s_signingAlgorithms.Contains(algorithm, StringComparer.Ordinal))
            {
                throw new SecurityTokenInvalidAlgorithmException("The JWT signing algorithm is not supported.");
            }

            if (request.SigningKeys.Count == 0)
            {
                throw MissingSigningTrust();
            }
        }
    }

    private static void CheckEncryptionHeader(JsonElement header)
    {
        var algorithm = ReadString(header, "alg", required: true);
        if (!s_keyManagementAlgorithms.Contains(algorithm, StringComparer.Ordinal))
        {
            throw new IdentityValidationException("TokenDecryptionAlgorithmNotAllowed",
                "JWT key-management algorithm is not allowed.");
        }

        ReadString(header, "enc", required: true);
    }

    private static Jwt CreateToken(JsonElement header, JsonElement payload)
    {
        var claims = payload.EnumerateObject().ToDictionary(
            property => property.Name,
            property => ReadValues(property.Value),
            StringComparer.Ordinal);
        string[] audiences = [];
        if (payload.TryGetProperty("aud", out var audience))
        {
            if (audience.ValueKind != JsonValueKind.String
                && (audience.ValueKind != JsonValueKind.Array
                    || audience.EnumerateArray().Any(value => value.ValueKind != JsonValueKind.String)))
            {
                throw Malformed();
            }

            audiences = ReadValues(audience);
        }

        var expiration = ReadDate(payload, "exp");
        var notBefore = ReadDate(payload, "nbf");
        var issuedAt = ReadDate(payload, "iat");
        if (notBefore is { } start && expiration is { } end && CompareDates(start, end) > 0)
        {
            throw new IdentityValidationException("TokenMalformed", "JWT not-before time exceeds its expiration time.");
        }

        return new MockJwt
        {
            Algorithm = ReadString(header, "alg", required: true),
            Type = ReadString(header, "typ"),
            Issuer = ReadString(payload, "iss"),
            Subject = ReadString(payload, "sub"),
            Id = ReadString(payload, "jti"),
            Audiences = audiences,
            Claims = claims,
            ExpirationTime = expiration?.UtcDateTime,
            NotBefore = notBefore?.UtcDateTime,
            IssuedAt = issuedAt?.UtcDateTime
        };
    }

    private static JsonDocument ReadObject(string encoded)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(encoded));
        }
        catch (FormatException)
        {
            throw Malformed();
        }
        catch (JsonException)
        {
            throw Malformed();
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || document.RootElement.EnumerateObject().Any(property => !names.Add(property.Name)))
        {
            document.Dispose();
            throw Malformed();
        }

        return document;
    }

    private static void RejectCriticalHeaders(JsonElement header)
    {
        if (header.TryGetProperty("crit", out var critical)
            && (critical.ValueKind != JsonValueKind.Array || critical.GetArrayLength() != 0))
        {
            throw new IdentityValidationException("TokenMalformed", "JWT critical header extensions are not supported.");
        }
    }

    private static string ReadString(JsonElement element, string name, bool required = false)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            if (required) { throw Malformed(); }
            return string.Empty;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw Malformed();
        }

        var result = value.GetString()!;
        if (required && string.IsNullOrWhiteSpace(result))
        {
            throw Malformed();
        }

        return result;
    }

    private static string[] ReadValues(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Array => value.EnumerateArray().Select(ReadValue).ToArray(),
        JsonValueKind.Null => [],
        _ => [ReadValue(value)]
    };

    private static string ReadValue(JsonElement value) => value.ValueKind == JsonValueKind.String
        ? value.GetString()!
        : value.GetRawText();

    private static NumericDate? ReadDate(JsonElement payload, string claim)
    {
        if (!payload.TryGetProperty(claim, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number)
        {
            throw Malformed();
        }

        var number = value.GetRawText().AsSpan();
        var negative = number[0] == '-';
        if (negative) { number = number[1..]; }
        var exponentIndex = number.IndexOfAny('e', 'E');
        var mantissa = exponentIndex < 0 ? number : number[..exponentIndex];
        var point = mantissa.IndexOf('.');
        var fractionalDigits = point < 0 ? 0 : mantissa.Length - point - 1;
        var digits = (point < 0 ? mantissa.ToString() : string.Concat(mantissa[..point], mantissa[(point + 1)..]))
            .TrimStart('0');
        if (digits.Length == 0)
        {
            return new NumericDate("", "0", false, DateTime.UnixEpoch);
        }

        var significant = digits.TrimEnd('0');
        var offset = digits.Length - significant.Length - fractionalDigits + 7;
        var exponentText = exponentIndex < 0 ? "0" : number[(exponentIndex + 1)..].ToString();
        var negativeExponent = exponentText[0] == '-';
        var exponentDigits = exponentText.TrimStart('+', '-').TrimStart('0');
        var normalizedExponent = exponentDigits.Length == 0 ? "0"
            : negativeExponent ? $"-{exponentDigits}" : exponentDigits;

        long? tickScale;
        string magnitude;
        if (int.TryParse(normalizedExponent, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var exponent))
        {
            tickScale = (long)exponent + offset;
            magnitude = (tickScale.Value + significant.Length).ToString(CultureInfo.InvariantCulture);
        }
        else if (negativeExponent)
        {
            // Huge negative exponents are sub-tick; keep their decimal rank for exact ordering.
            tickScale = null;
            magnitude = $"-{AdjustMagnitude(exponentDigits, -(offset + significant.Length))}";
        }
        else
        {
            throw Malformed();
        }

        return new NumericDate(significant, magnitude, negative, ToUtcDate(significant, tickScale, negative));
    }

    private static DateTime ToUtcDate(string digits, long? tickScale, bool negative)
    {
        var integerDigits = tickScale is { } scale ? digits.Length + scale : long.MinValue;
        if (integerDigits > 19) { throw Malformed(); }
        var count = integerDigits <= 0 ? 0 : (int)Math.Min(digits.Length, integerDigits);
        var wholeTicks = 0L;
        if (count != 0 && !long.TryParse(digits.AsSpan(0, count),
                NumberStyles.None, CultureInfo.InvariantCulture, out wholeTicks))
        {
            throw Malformed();
        }

        if (tickScale is > 0)
        {
            for (var index = 0L; index < tickScale.Value; index++)
            {
                if (wholeTicks > long.MaxValue / 10) { throw Malformed(); }
                wholeTicks *= 10;
            }
        }

        var fractionalTick = integerDigits < digits.Length;
        var limit = negative ? DateTime.UnixEpoch.Ticks : DateTime.MaxValue.Ticks - DateTime.UnixEpoch.Ticks;
        if (wholeTicks > limit || (wholeTicks == limit && fractionalTick))
        {
            throw Malformed();
        }

        // Ceiling preserves exp/nbf boundaries at the gateway clock's 100-ns resolution, even below one tick.
        var ticks = negative ? -wholeTicks : wholeTicks + (fractionalTick ? 1 : 0);
        return new DateTime(DateTime.UnixEpoch.Ticks + ticks, DateTimeKind.Utc);
    }

    private static int CompareDates(NumericDate left, NumericDate right)
    {
        var leftSign = left.Digits.Length == 0 ? 0 : left.IsNegative ? -1 : 1;
        var rightSign = right.Digits.Length == 0 ? 0 : right.IsNegative ? -1 : 1;
        if (leftSign != rightSign) { return leftSign.CompareTo(rightSign); }
        if (leftSign == 0) { return 0; }

        var result = CompareRanks(left.Magnitude, right.Magnitude);
        if (result == 0)
        {
            for (var index = 0; index < Math.Max(left.Digits.Length, right.Digits.Length); index++)
            {
                var leftDigit = index < left.Digits.Length ? left.Digits[index] : '0';
                var rightDigit = index < right.Digits.Length ? right.Digits[index] : '0';
                result = leftDigit.CompareTo(rightDigit);
                if (result != 0) { break; }
            }
        }

        return leftSign < 0 ? -result : result;
    }

    private static int CompareRanks(string left, string right)
    {
        var leftNegative = left[0] == '-';
        var rightNegative = right[0] == '-';
        if (leftNegative != rightNegative) { return leftNegative ? -1 : 1; }
        var leftDigits = leftNegative ? left.AsSpan(1) : left.AsSpan();
        var rightDigits = rightNegative ? right.AsSpan(1) : right.AsSpan();
        var result = leftDigits.Length.CompareTo(rightDigits.Length);
        if (result == 0) { result = leftDigits.SequenceCompareTo(rightDigits); }
        return leftNegative ? -result : result;
    }

    private static string AdjustMagnitude(string digits, int adjustment)
    {
        var result = digits.ToCharArray();
        var carry = adjustment;
        for (var index = result.Length - 1; index >= 0 && carry != 0; index--)
        {
            var digit = result[index] - '0' + carry;
            carry = Math.DivRem(digit, 10, out var remainder);
            if (remainder < 0) { remainder += 10; carry--; }
            result[index] = (char)('0' + remainder);
        }

        var prefix = carry == 0 ? string.Empty : carry.ToString(CultureInfo.InvariantCulture);
        return (prefix + new string(result)).TrimStart('0');
    }

    private readonly record struct NumericDate(string Digits, string Magnitude, bool IsNegative, DateTime UtcDateTime);

    private static IdentityValidationException Malformed() => new("TokenMalformed", "JWT is malformed.");

    private static IdentityValidationException MissingSigningTrust() => new("TokenTrustUnavailable",
        "JWT signing trust material is missing. Supply signing keys or register an IJwtKeyProvider or IJwtValidator.");
}
