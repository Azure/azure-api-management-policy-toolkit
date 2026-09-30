// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;
using Microsoft.IdentityModel.Tokens;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class ValidateJwtTests
{
    private static ValidateJwtConfig Config => new()
    {
        HeaderName = "Authorization",
        RequireScheme = "Bearer",
        IssuerSigningKeys = [new Base64KeyConfig { Value = Convert.ToBase64String(IdentityTestTokens.SigningBytes) }],
        Issuers = [IdentityTestTokens.Issuer],
        Audiences = [IdentityTestTokens.Audience],
        OutputTokenVariableName = "jwt"
    };

    private static TestDocument CreateTest(ValidateJwtConfig? config = null, string? token = null)
    {
        var test = new JwtDocument(config ?? Config).AsTestDocument();
        IdentityTestTokens.SetClock(test.Context);
        if (token is not null)
        {
            test.Context.Request.Headers["Authorization"] = [$"Bearer {token}"];
        }

        return test;
    }

    [TestMethod]
    public void ValidSignatureProducesJwtAndPreservesResponse()
    {
        var test = CreateTest(token: IdentityTestTokens.Create());
        test.Context.Response.StatusCode = 202;
        test.Context.Response.StatusReason = "Accepted";
        test.Context.Response.Headers["X-Keep"] = ["yes"];
        test.Context.Response.Body.Content = "existing";

        test.RunInbound();

        var jwt = test.Context.Variables["jwt"].Should().BeAssignableTo<Jwt>().Which;
        jwt.Issuer.Should().Be(IdentityTestTokens.Issuer);
        jwt.Audiences.Should().Equal(IdentityTestTokens.Audience);
        jwt.Subject.Should().Be("alice");
        jwt.Id.Should().Be("token-id");
        jwt.Algorithm.Should().Be(SecurityAlgorithms.HmacSha256);
        jwt.Type.Should().Be("JWT");
        jwt.Claims["roles"].Should().Equal("reader", "writer");
        jwt.ExpirationTime.Should().Be(IdentityTestTokens.Now.AddMinutes(5).UtcDateTime);
        jwt.NotBefore.Should().Be(IdentityTestTokens.Now.AddMinutes(-1).UtcDateTime);
        jwt.IssuedAt.Should().Be(IdentityTestTokens.Now.AddMinutes(-1).UtcDateTime);
        test.Context.Variables["continued"].Should().Be(true);
        test.Context.ResponseTerminated.Should().BeFalse();
        test.Context.Response.StatusCode.Should().Be(202);
        test.Context.Response.StatusReason.Should().Be("Accepted");
        test.Context.Response.Headers["X-Keep"].Should().Equal("yes");
        test.Context.Response.Body.Content.Should().Be("existing");
    }

    [TestMethod]
    [DataRow("header")]
    [DataRow("query")]
    [DataRow("value")]
    public void ExtractsOnlyTheConfiguredSource(string source)
    {
        var token = IdentityTestTokens.Create();
        var config = Config with
        {
            HeaderName = source == "header" ? "X-Jwt" : null,
            QueryParameterName = source == "query" ? "access_token" : null,
            TokenValue = source == "value" ? token : null
        };
        var test = CreateTest(config);
        test.Context.Request.Headers["Authorization"] = ["Bearer invalid"];
        test.Context.Request.Headers["X-Jwt"] = [token];
        test.Context.Request.Url.Query["access_token"] = [token];

        test.RunInbound();

        test.Context.Variables["jwt"].Should().BeAssignableTo<Jwt>();
        test.Context.Variables["continued"].Should().Be(true);
    }

    [TestMethod]
    public void AuthorizationHeaderAndSchemeAreCaseInsensitive()
    {
        var test = CreateTest();
        test.Context.Request.Headers["authorization"] = [$"bEaReR {IdentityTestTokens.Create()}"];

        test.RunInbound();

        test.Context.Variables["jwt"].Should().BeAssignableTo<Jwt>();
    }

    [TestMethod]
    public void CustomHeaderDoesNotApplyAuthorizationSchemeRequirement()
    {
        var test = CreateTest(Config with { HeaderName = "X-Jwt", RequireScheme = "Basic" });
        test.Context.Request.Headers["x-jwt"] = [IdentityTestTokens.Create()];

        test.RunInbound();

        test.Context.Variables["continued"].Should().Be(true);
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("empty")]
    [DataRow("scheme")]
    [DataRow("multiple")]
    public void RejectsMissingOrAmbiguousAuthorization(string scenario)
    {
        var test = CreateTest();
        if (scenario != "missing")
        {
            test.Context.Request.Headers["Authorization"] = scenario switch
            {
                "empty" => ["Bearer "],
                "scheme" => [$"Basic {IdentityTestTokens.Create()}"],
                _ => [$"Bearer {IdentityTestTokens.Create()}", $"Bearer {IdentityTestTokens.Create()}"]
            };
        }

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-jwt");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RejectsMissingOrMultipleQueryValues(bool multiple)
    {
        var test = CreateTest(Config with { HeaderName = null, QueryParameterName = "access_token" });
        if (multiple)
        {
            test.Context.Request.Url.Query["access_token"] = [IdentityTestTokens.Create(), IdentityTestTokens.Create()];
        }

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-jwt");
    }

    [TestMethod]
    [DataRow("not-a-jwt")]
    [DataRow("a.b.c")]
    [DataRow("e30.e30.")]
    [DataRow("Bearer not-a-jwt")]
    public void RejectsMalformedDirectTokens(string token)
    {
        var test = CreateTest(Config with { HeaderName = null, TokenValue = token });

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-jwt");
    }

    [TestMethod]
    public void RejectsInvalidSignatureAndClearsStaleOutput()
    {
        var test = CreateTest(token: IdentityTestTokens.Create(key: IdentityTestTokens.OtherSigningKey));
        test.Context.Variables["jwt"] = "stale-authentication";

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-jwt", "TokenSignatureInvalid");
    }

    [TestMethod]
    public void RejectsPayloadTampering()
    {
        var parts = IdentityTestTokens.Create().Split('.');
        parts[1] = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(
            Base64UrlEncoder.Decode(parts[1]).Replace("alice", "attacker", StringComparison.Ordinal)));
        var test = CreateTest(token: string.Join('.', parts));

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-jwt", "TokenSignatureInvalid");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void UnsignedTokensRequireAnExplicitOptOut(bool allowUnsigned)
    {
        var test = CreateTest(Config with
        {
            RequireSignedTokens = allowUnsigned ? false : null,
            IssuerSigningKeys = null
        }, IdentityTestTokens.Create(unsigned: true));

        test.RunInbound();

        if (allowUnsigned)
        {
            test.Context.Variables["continued"].Should().Be(true);
            test.Context.Variables["jwt"].Should().BeAssignableTo<Jwt>();
        }
        else
        {
            IdentityTestTokens.AssertRejected(test.Context, "validate-jwt");
        }
    }

    [TestMethod]
    public void AllowUnsignedDoesNotSkipValidationOfSignedTokens()
    {
        var test = CreateTest(Config with { RequireSignedTokens = false },
            IdentityTestTokens.Create(key: IdentityTestTokens.OtherSigningKey));

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-jwt", "TokenSignatureInvalid");
    }

    [TestMethod]
    public void AllowUnsignedDoesNotAcceptAnEmptySignatureForASignedAlgorithm()
    {
        var parts = IdentityTestTokens.Create().Split('.');
        var test = CreateTest(Config with { RequireSignedTokens = false }, $"{parts[0]}.{parts[1]}.");

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-jwt", "TokenSignatureInvalid");
    }

    [TestMethod]
    public void InjectedValidatorCannotReturnAnUnsignedTokenWhenSignaturesAreRequired()
    {
        var test = CreateTest(Config with { IssuerSigningKeys = null }, IdentityTestTokens.Create(unsigned: true));
        test.Context.Services.Register<IJwtValidator>(new RecordingJwtValidator(request =>
            IdentityTestTokens.Authenticated(request.Token)));

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-jwt");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ExpirationClaimIsRequiredUnlessExplicitlyDisabled(bool allowMissingExpiration)
    {
        var test = CreateTest(Config with { RequireExpirationTime = allowMissingExpiration ? false : null },
            IdentityTestTokens.Create(claims => claims.Remove("exp")));

        test.RunInbound();

        if (allowMissingExpiration)
        {
            test.Context.Variables["continued"].Should().Be(true);
            ((Jwt)test.Context.Variables["jwt"]).ExpirationTime.Should().BeNull();
        }
        else
        {
            IdentityTestTokens.AssertRejected(test.Context, "validate-jwt", "TokenExpirationMissing");
        }
    }

    [TestMethod]
    [DataRow(-1, 0, false)]
    [DataRow(0, 0, false)]
    [DataRow(10, 0, true)]
    [DataRow(-5, 10, true)]
    [DataRow(-10, 10, false)]
    public void EnforcesExpirationAndExactClockSkew(int expirationSeconds, int skew, bool valid)
    {
        var test = CreateTest(Config with { ClockSkew = skew, RequireExpirationTime = false },
            IdentityTestTokens.Create(claims => claims["exp"] =
                IdentityTestTokens.Now.AddSeconds(expirationSeconds).ToUnixTimeSeconds()));

        test.RunInbound();

        if (valid)
        {
            test.Context.Variables["continued"].Should().Be(true);
        }
        else
        {
            IdentityTestTokens.AssertRejected(test.Context, "validate-jwt", "TokenExpired");
        }
    }

    [TestMethod]
    [DataRow(1, 0, false)]
    [DataRow(0, 0, true)]
    [DataRow(10, 10, true)]
    [DataRow(11, 10, false)]
    public void EnforcesNotBeforeAndClockSkew(int notBeforeSeconds, int skew, bool valid)
    {
        var test = CreateTest(Config with { ClockSkew = skew }, IdentityTestTokens.Create(claims =>
            claims["nbf"] = IdentityTestTokens.Now.AddSeconds(notBeforeSeconds).ToUnixTimeSeconds()));

        test.RunInbound();

        if (valid)
        {
            test.Context.Variables["continued"].Should().Be(true);
        }
        else
        {
            IdentityTestTokens.AssertRejected(test.Context, "validate-jwt", "TokenNotYetValid");
        }
    }

    [TestMethod]
    public void ValidationClockIncludesElapsedRequestTime()
    {
        var test = CreateTest(token: IdentityTestTokens.Create(claims =>
            claims["exp"] = IdentityTestTokens.Now.AddSeconds(10).ToUnixTimeSeconds()));
        test.Context.Elapsed = TimeSpan.FromSeconds(10);

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-jwt", "TokenExpired");
    }

    [TestMethod]
    [DataRow("exp")]
    [DataRow("nbf")]
    [DataRow("iat")]
    public void RejectsMalformedNumericDates(string claim)
    {
        var test = CreateTest(token: IdentityTestTokens.Create(claims => claims[claim] = "not-a-date"));

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-jwt", "TokenMalformed");
    }

    [TestMethod]
    [DataRow("exp", "0.5", 5_000_000L)]
    [DataRow("nbf", "0.5", 5_000_000L)]
    [DataRow("iat", "0.5", 5_000_000L)]
    [DataRow("exp", "0.1234567", 1_234_567L)]
    [DataRow("nbf", "0.1234567", 1_234_567L)]
    [DataRow("iat", "0.1234567", 1_234_567L)]
    [DataRow("exp", "0.0000001", 1L)]
    [DataRow("nbf", "0.0000001", 1L)]
    [DataRow("iat", "0.0000001", 1L)]
    [DataRow("exp", "0.00000001", 1L)]
    [DataRow("nbf", "0.00000001", 1L)]
    [DataRow("iat", "0.00000001", 1L)]
    public void FractionalNumericDatesPreserveUtcClockPrecision(string claim, string fraction, long fractionalTicks)
    {
        var origin = claim == "exp" ? IdentityTestTokens.Now.AddMinutes(5) : IdentityTestTokens.Now.AddMinutes(-1);
        var seconds = (origin.ToUnixTimeSeconds() + decimal.Parse(fraction, CultureInfo.InvariantCulture))
            .ToString(CultureInfo.InvariantCulture);
        var test = CreateTest(token: IdentityTestTokens.WithNumericDate(claim, seconds));

        test.RunInbound();

        var jwt = test.Context.Variables["jwt"].Should().BeAssignableTo<Jwt>().Which;
        var actual = claim switch
        {
            "exp" => jwt.ExpirationTime,
            "nbf" => jwt.NotBefore,
            _ => jwt.IssuedAt
        };
        actual.Should().Be(origin.UtcDateTime.AddTicks(fractionalTicks));
        actual!.Value.Kind.Should().Be(DateTimeKind.Utc);
        jwt.Claims[claim].Should().Equal(seconds);
        test.Context.Variables["continued"].Should().Be(true);
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("0.5", 0, "0", true)]
    [DataRow("0.5", 0, "0.4999999", true)]
    [DataRow("0.5", 0, "0.5", false)]
    [DataRow("0.0000001", 0, "0", true)]
    [DataRow("0.00000001", 0, "0", true)]
    [DataRow("-0.5", 0, "0", false)]
    [DataRow("-0.5", 1, "0", true)]
    [DataRow("-0.5", 1, "0.5", false)]
    [DataRow("-1.0000001", 1, "0", false)]
    public void FractionalExpirationRespectsExactBoundariesAndSkew(
        string offset, int skew, string elapsed, bool valid)
    {
        var seconds = (IdentityTestTokens.Now.ToUnixTimeSeconds() + decimal.Parse(offset, CultureInfo.InvariantCulture))
            .ToString(CultureInfo.InvariantCulture);
        var test = CreateTest(Config with { ClockSkew = skew }, IdentityTestTokens.WithNumericDate("exp", seconds));
        test.Context.Elapsed = TimeSpan.FromTicks(
            (long)(decimal.Parse(elapsed, CultureInfo.InvariantCulture) * TimeSpan.TicksPerSecond));

        test.RunInbound();

        if (valid)
        {
            test.Context.Variables["continued"].Should().Be(true);
        }
        else
        {
            IdentityTestTokens.AssertRejected(test.Context, "validate-jwt", "TokenExpired");
        }
    }

    [TestMethod]
    [DataRow("0.5", 0, "0", false)]
    [DataRow("0.5", 0, "0.4999999", false)]
    [DataRow("0.5", 0, "0.5", true)]
    [DataRow("0.5", 1, "0", true)]
    [DataRow("1.0000001", 1, "0", false)]
    [DataRow("0.00000001", 0, "0", false)]
    [DataRow("-0.5", 0, "0", true)]
    public void FractionalNotBeforeRespectsExactBoundariesAndSkew(
        string offset, int skew, string elapsed, bool valid)
    {
        var seconds = (IdentityTestTokens.Now.ToUnixTimeSeconds() + decimal.Parse(offset, CultureInfo.InvariantCulture))
            .ToString(CultureInfo.InvariantCulture);
        var test = CreateTest(Config with { ClockSkew = skew }, IdentityTestTokens.WithNumericDate("nbf", seconds));
        test.Context.Elapsed = TimeSpan.FromTicks(
            (long)(decimal.Parse(elapsed, CultureInfo.InvariantCulture) * TimeSpan.TicksPerSecond));

        test.RunInbound();

        if (valid)
        {
            test.Context.Variables["continued"].Should().Be(true);
        }
        else
        {
            IdentityTestTokens.AssertRejected(test.Context, "validate-jwt", "TokenNotYetValid");
        }
    }

    [TestMethod]
    [DataRow("0.25", "0.75")]
    [DataRow("0.0000001", "0.0000002")]
    [DataRow("0.00000001", "0.00000002")]
    public void FractionalDatesDoNotBypassLifetimeOrdering(string expiration, string notBefore)
    {
        var test = CreateTest(Config with { ClockSkew = 1 }, IdentityTestTokens.Create(claims =>
        {
            claims["exp"] = IdentityTestTokens.Now.ToUnixTimeSeconds()
                + decimal.Parse(expiration, CultureInfo.InvariantCulture);
            claims["nbf"] = IdentityTestTokens.Now.ToUnixTimeSeconds()
                + decimal.Parse(notBefore, CultureInfo.InvariantCulture);
        }));

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-jwt", "TokenMalformed");
    }

    [TestMethod]
    [DataRow("0.0000000000000000000000001", "0.0000000000000000000000002", false)]
    [DataRow("0.0000000000000000000000002", "0.0000000000000000000000001", true)]
    [DataRow("1e-2147483649", "2e-2147483649", false)]
    [DataRow("2e-2147483649", "1e-2147483649", true)]
    [DataRow("1e-2147483649", "2e-2147483650", true)]
    [DataRow("2e-2147483650", "1e-2147483649", false)]
    [DataRow("1e-0002147483649", "2e-2147483650", true)]
    public void FractionalLifetimeOrderingRetainsSubTickAndExponentPrecision(
        string expiration, string notBefore, bool valid)
    {
        var claims = IdentityTestTokens.Claims();
        claims.Remove("exp");
        claims.Remove("nbf");
        var payload = JsonSerializer.Serialize(claims);
        var token = IdentityTestTokens.SignRaw("{\"alg\":\"HS256\",\"typ\":\"JWT\"}",
            $"{payload[..^1]},\"exp\":{expiration},\"nbf\":{notBefore}}}");
        var test = CreateTest(Config with { ClockSkew = 1 }, token);
        test.Context.Timestamp = DateTime.UnixEpoch;

        test.RunInbound();

        if (valid)
        {
            test.Context.Variables["continued"].Should().Be(true);
        }
        else
        {
            IdentityTestTokens.AssertRejected(test.Context, "validate-jwt", "TokenMalformed");
        }
    }

    [TestMethod]
    [DataRow("-62135596800", 0L)]
    [DataRow("253402300799.9999999", 3_155_378_975_999_999_999L)]
    [DataRow("253402300799.9999998999999999999", 3_155_378_975_999_999_999L)]
    [DataRow("-0.5", 621_355_967_995_000_000L)]
    [DataRow("0.5", 621_355_968_005_000_000L)]
    [DataRow("1.2345678e0", 621_355_968_012_345_678L)]
    [DataRow("1e-1000", 621_355_968_000_000_001L)]
    [DataRow("-1e-1000", 621_355_968_000_000_000L)]
    [DataRow("0e1000", 621_355_968_000_000_000L)]
    [DataRow("1e-2147483649", 621_355_968_000_000_001L)]
    [DataRow("-1e-2147483649", 621_355_968_000_000_000L)]
    [DataRow("0e2147483648", 621_355_968_000_000_000L)]
    public void NumericDatesSupportScientificNotationAndUtcRange(string seconds, long expectedTicks)
    {
        var test = CreateTest(token: IdentityTestTokens.WithNumericDate("iat", seconds));

        test.RunInbound();

        var jwt = test.Context.Variables["jwt"].Should().BeAssignableTo<Jwt>().Which;
        jwt.IssuedAt.Should().Be(new DateTime(expectedTicks, DateTimeKind.Utc));
        test.Context.Variables["continued"].Should().Be(true);
    }

    [TestMethod]
    [DataRow("exp")]
    [DataRow("nbf")]
    [DataRow("iat")]
    public void NonNumericNonFiniteAndOverflowingDatesFailClosed(string claim)
    {
        string[] invalid =
        [
            "\"1893553745.5\"", "true", "null", "[]", "{}", "NaN", "Infinity", "-Infinity",
            "1e1000", "-1e1000", "99999999999999999999999999999999",
            "253402300800", "253402300799.99999999", "253402300799.99999990000000000000001",
            "-62135596800.0000001", "-62135596800.00000000000000000000001"
        ];
        foreach (var seconds in invalid)
        {
            var test = CreateTest(token: IdentityTestTokens.WithNumericDate(claim, seconds));
            test.Context.Variables["jwt"] = "stale";

            test.RunInbound();

            IdentityTestTokens.AssertRejected(test.Context, "validate-jwt", "TokenMalformed");
        }
    }

    [TestMethod]
    [DataRow("issuer")]
    [DataRow("audience")]
    [DataRow("missing-audience")]
    public void RejectsWrongIssuerOrAudience(string scenario)
    {
        var token = IdentityTestTokens.Create(claims =>
        {
            if (scenario == "issuer") { claims["iss"] = "https://attacker.example"; }
            if (scenario == "audience") { claims["aud"] = "another-api"; }
            if (scenario == "missing-audience") { claims.Remove("aud"); }
        });
        var test = CreateTest(token: token);

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-jwt",
            scenario == "issuer" ? "TokenIssuerNotAllowed" : "TokenAudienceNotAllowed");
    }

    [TestMethod]
    public void AcceptsAnyConfiguredAudienceFromAnArrayClaim()
    {
        var test = CreateTest(token: IdentityTestTokens.Create(claims =>
            claims["aud"] = new[] { "other", IdentityTestTokens.Audience }));

        test.RunInbound();

        ((Jwt)test.Context.Variables["jwt"]).Audiences.Should().Equal("other", IdentityTestTokens.Audience);
    }

    [TestMethod]
    [DataRow(null, true)]
    [DataRow("all", true)]
    [DataRow("any", true)]
    [DataRow("all", false)]
    [DataRow("any", false)]
    public void EnforcesRequiredClaimMatching(string? match, bool valid)
    {
        var values = valid ? new[] { "reader", "writer" } : new[] { "administrator", "owner" };
        if (match == "any" && valid) { values = ["administrator", "writer"]; }
        var test = CreateTest(Config with
        {
            RequiredClaims = [new ClaimConfig { Name = "roles", Match = match, Values = values }]
        }, IdentityTestTokens.Create());

        test.RunInbound();

        if (valid)
        {
            test.Context.Variables["continued"].Should().Be(true);
        }
        else
        {
            IdentityTestTokens.AssertRejected(test.Context, "validate-jwt", "TokenClaimValueNotAllowed");
        }
    }

    [TestMethod]
    public void AllClaimMatchingRejectsPartialMatches()
    {
        var test = CreateTest(Config with
        {
            RequiredClaims = [new ClaimConfig { Name = "roles", Values = ["reader", "administrator"] }]
        }, IdentityTestTokens.Create());

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-jwt", "TokenClaimValueNotAllowed");
    }

    [TestMethod]
    public void ClaimSeparatorIsAStringRatherThanASetOfCharacters()
    {
        var test = CreateTest(Config with
        {
            RequiredClaims = [new ClaimConfig { Name = "scp", Separator = "::", Values = ["read", "write"] }]
        }, IdentityTestTokens.Create(claims => claims["scp"] = "read::write"));

        test.RunInbound();

        test.Context.Variables["continued"].Should().Be(true);
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("case")]
    [DataRow("null")]
    public void RequiredClaimMustActuallyBePresent(string scenario)
    {
        var test = CreateTest(Config with
        {
            RequiredClaims = [new ClaimConfig { Name = scenario == "case" ? "Roles" : "roles" }]
        }, IdentityTestTokens.Create(claims =>
        {
            if (scenario == "missing") { claims.Remove("roles"); }
            if (scenario == "null") { claims["roles"] = null!; }
        }));

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-jwt", "TokenClaimNotPresent");
    }

    [TestMethod]
    public void ClaimsWithoutExpectedValuesStillRequirePresence()
    {
        var test = CreateTest(Config with
        {
            RequiredClaims = [new ClaimConfig { Name = "sub", Values = [] }]
        }, IdentityTestTokens.Create());

        test.RunInbound();

        test.Context.Variables["continued"].Should().Be(true);
    }

    [TestMethod]
    public void ClaimValuesAreCaseSensitive()
    {
        var test = CreateTest(Config with
        {
            RequiredClaims = [new ClaimConfig { Name = "roles", Values = ["READER"] }]
        }, IdentityTestTokens.Create());

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-jwt", "TokenClaimValueNotAllowed");
    }

    [TestMethod]
    [DataRow("sources")]
    [DataRow("no-source")]
    [DataRow("skew")]
    [DataRow("match")]
    public void InvalidPolicyConfigurationIsReported(string scenario)
    {
        var config = scenario switch
        {
            "sources" => Config with { TokenValue = IdentityTestTokens.Create() },
            "no-source" => Config with { HeaderName = null },
            "skew" => Config with { ClockSkew = -1 },
            _ => Config with { RequiredClaims = [new ClaimConfig { Name = "sub", Match = "invalid" }] }
        };
        var test = CreateTest(config, IdentityTestTokens.Create());

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.Policy.Should().Be(nameof(IInboundContext.ValidateJwt));
        error.InnerException.Should().BeAssignableTo<ArgumentException>();
        test.Context.Variables.Should().NotContainKey("continued");
    }

    [TestMethod]
    public void MissingSigningTrustIsAnExplicitRejection()
    {
        var test = CreateTest(Config with { IssuerSigningKeys = null }, IdentityTestTokens.Create());

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-jwt", "TokenTrustUnavailable");
        test.Context.LastError.Message.Should().Contain("trust");
    }

    [TestMethod]
    public void MockedOpenIdKeysAndIssuerAreUsedWithoutDiscovery()
    {
        var provider = new RecordingJwtKeyProvider(_ => new JwtTrustMaterial
        {
            SigningKeys = [IdentityTestTokens.SigningKey],
            Issuers = [IdentityTestTokens.Issuer]
        });
        var test = CreateTest(Config with
        {
            IssuerSigningKeys = null,
            Issuers = null,
            OpenIdConfigs = [new OpenIdConfig { Url = "https://offline.invalid/.well-known/openid-configuration" }]
        }, IdentityTestTokens.Create());
        test.Context.Services.Register<IJwtKeyProvider>(provider);

        test.RunInbound();

        test.Context.Variables["continued"].Should().Be(true);
        provider.Requests.Should().ContainSingle().Which.Should().BeEquivalentTo(new JwtKeyRequest(
            nameof(IInboundContext.ValidateJwt), null,
            ["https://offline.invalid/.well-known/openid-configuration"]));
    }

    [TestMethod]
    public void OpenIdMetadataDoesNotPermitAnUnadvertisedIssuer()
    {
        var test = CreateTest(Config with
        {
            IssuerSigningKeys = null,
            Issuers = null,
            OpenIdConfigs = [new OpenIdConfig { Url = "https://offline.invalid/config" }]
        }, IdentityTestTokens.Create());
        test.Context.Services.Register<IJwtKeyProvider>(new RecordingJwtKeyProvider(_ => new JwtTrustMaterial
        {
            SigningKeys = [IdentityTestTokens.SigningKey],
            Issuers = ["https://different-issuer.example"]
        }));

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-jwt", "TokenIssuerNotAllowed");
    }

    [TestMethod]
    public void InlineKeysSupportRolloverAndUnmatchedKidFallback()
    {
        var test = CreateTest(Config with
        {
            IssuerSigningKeys =
            [
                new Base64KeyConfig { Id = "wrong", Value = Convert.ToBase64String(IdentityTestTokens.OtherSigningBytes) },
                new Base64KeyConfig { Id = "different-kid", Value = Convert.ToBase64String(IdentityTestTokens.SigningBytes) }
            ]
        }, IdentityTestTokens.Create());

        test.RunInbound();

        test.Context.Variables["continued"].Should().Be(true);
    }

    [TestMethod]
    public void ValidatesAnRsaModulusExponentKey()
    {
        using var rsa = RSA.Create(2048);
        var parameters = rsa.ExportParameters(false);
        var test = CreateTest(Config with
        {
            IssuerSigningKeys = [new AsymmetricKeyConfig
            {
                Modulus = Base64UrlEncoder.Encode(parameters.Modulus!),
                Exponent = Base64UrlEncoder.Encode(parameters.Exponent!)
            }]
        }, IdentityTestTokens.Create(key: new RsaSecurityKey(rsa), algorithm: SecurityAlgorithms.RsaSha256));

        test.RunInbound();

        test.Context.Variables["continued"].Should().Be(true);
    }

    [TestMethod]
    public void ValidatesAStoredCertificateSigningKey()
    {
        using var certificate = IdentityTestTokens.CreateCertificate();
        var test = CreateTest(Config with
        {
            IssuerSigningKeys = [new CertificateKeyConfig { CertificateId = "signing-cert" }]
        }, IdentityTestTokens.Create(key: new X509SecurityKey(certificate), algorithm: SecurityAlgorithms.RsaSha256));
        test.SetupCertificateStore().WithCertificateById("signing-cert", certificate);

        test.RunInbound();

        test.Context.Variables["continued"].Should().Be(true);
    }

    [TestMethod]
    public void DeploymentCertificatesCanSupplyAnExplicitSigningKey()
    {
        using var certificate = IdentityTestTokens.CreateCertificate();
        var test = CreateTest(Config with
        {
            IssuerSigningKeys = [new CertificateKeyConfig { CertificateId = "signing-cert" }]
        }, IdentityTestTokens.Create(key: new X509SecurityKey(certificate), algorithm: SecurityAlgorithms.RsaSha256));
        test.Context.Deployment.Certificates["signing-cert"] = certificate;

        test.RunInbound();

        test.Context.Variables["continued"].Should().Be(true);
    }

    [TestMethod]
    public void MissingCertificateKeyCannotEstablishTrust()
    {
        var test = CreateTest(Config with
        {
            IssuerSigningKeys = [new CertificateKeyConfig { CertificateId = "missing" }]
        }, IdentityTestTokens.Create());

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-jwt", "TokenTrustUnavailable");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void EncryptedTokensRequireBothDecryptionAndSignatureValidation(bool correctDecryptionKey)
    {
        var test = CreateTest(Config with
        {
            DecryptionKeys = [new Base64KeyConfig
            {
                Value = Convert.ToBase64String(correctDecryptionKey
                    ? IdentityTestTokens.EncryptionBytes : IdentityTestTokens.OtherEncryptionBytes)
            }]
        }, IdentityTestTokens.Encrypt());

        test.RunInbound();

        if (correctDecryptionKey)
        {
            var jwt = test.Context.Variables["jwt"].Should().BeAssignableTo<Jwt>().Which;
            jwt.Subject.Should().Be("alice");
            jwt.Algorithm.Should().Be(SecurityAlgorithms.HmacSha256);
        }
        else
        {
            IdentityTestTokens.AssertRejected(test.Context, "validate-jwt");
        }
    }

    [TestMethod]
    [DataRow("RSA-OAEP", true)]
    [DataRow("RSA-OAEP-256", true)]
    [DataRow("RSA1_5", false)]
    public void JweRsaKeyManagementMustBeExplicitlyAllowed(string algorithm, bool allowed)
    {
        using var certificate = IdentityTestTokens.CreateCertificate();
        using var rsa = certificate.GetRSAPrivateKey()!;
        var key = IdentityTestTokens.RsaEncryptionKey(rsa, algorithm);
        var token = IdentityTestTokens.Encrypt(encryption: new EncryptingCredentials(
            key, algorithm, SecurityAlgorithms.Aes256CbcHmacSha512));
        var test = CreateTest(token: token);
        test.Context.Services.Register<IJwtKeyProvider>(new RecordingJwtKeyProvider(_ => new JwtTrustMaterial
        {
            DecryptionKeys = [key]
        }));
        test.Context.Variables["jwt"] = "stale";
        using var header = JsonDocument.Parse(Base64UrlEncoder.Decode(token.Split('.')[0]));
        header.RootElement.GetProperty("alg").GetString().Should().Be(algorithm);
        header.RootElement.GetProperty("enc").GetString().Should().Be(SecurityAlgorithms.Aes256CbcHmacSha512);

        test.RunInbound();

        if (allowed)
        {
            var jwt = test.Context.Variables["jwt"].Should().BeAssignableTo<Jwt>().Which;
            jwt.Subject.Should().Be("alice");
            jwt.Algorithm.Should().Be(SecurityAlgorithms.HmacSha256);
            test.Context.Variables["continued"].Should().Be(true);
            test.Context.ResponseTerminated.Should().BeFalse();
        }
        else
        {
            IdentityTestTokens.AssertRejected(test.Context, "validate-jwt", "TokenDecryptionAlgorithmNotAllowed");
        }
    }

    [TestMethod]
    public void ForbiddenJweAlgorithmIsRejectedBeforeDecryptionKeyRequirement()
    {
        using var certificate = IdentityTestTokens.CreateCertificate();
        var token = IdentityTestTokens.Encrypt(encryption: new EncryptingCredentials(
            new X509SecurityKey(certificate), "RSA1_5", SecurityAlgorithms.Aes256CbcHmacSha512));
        var test = CreateTest(token: token);

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-jwt", "TokenDecryptionAlgorithmNotAllowed");
    }

    [TestMethod]
    [DataRow("dir", 64)]
    [DataRow("A128KW", 16)]
    [DataRow("A192KW", 24)]
    [DataRow("A256KW", 32)]
    public void AllowedSymmetricJweKeyManagementStillDecrypts(string algorithm, int keyLength)
    {
        var bytes = Enumerable.Range(1, keyLength).Select(value => (byte)value).ToArray();
        var token = IdentityTestTokens.Encrypt(encryption: new EncryptingCredentials(
            new SymmetricSecurityKey(bytes), algorithm, SecurityAlgorithms.Aes256CbcHmacSha512));
        var test = CreateTest(Config with
        {
            DecryptionKeys = [new Base64KeyConfig { Value = Convert.ToBase64String(bytes) }]
        }, token);

        test.RunInbound();

        ((Jwt)test.Context.Variables["jwt"]).Subject.Should().Be("alice");
        test.Context.Variables["continued"].Should().Be(true);
    }

    [TestMethod]
    [DataRow("alg-missing")]
    [DataRow("alg-null")]
    [DataRow("alg-number")]
    [DataRow("alg-empty")]
    [DataRow("alg-duplicate")]
    [DataRow("enc-missing")]
    [DataRow("enc-number")]
    [DataRow("enc-duplicate")]
    [DataRow("non-object")]
    [DataRow("invalid-json")]
    public void MalformedJweProtectedHeadersFailClosed(string scenario)
    {
        var parts = IdentityTestTokens.Encrypt().Split('.');
        using var header = JsonDocument.Parse(Base64UrlEncoder.Decode(parts[0]));
        var fields = header.RootElement.EnumerateObject().ToDictionary(
            property => property.Name, property => (object?)property.Value, StringComparer.Ordinal);
        switch (scenario)
        {
            case "alg-missing": fields.Remove("alg"); break;
            case "alg-null": fields["alg"] = null; break;
            case "alg-number": fields["alg"] = 123; break;
            case "alg-empty": fields["alg"] = ""; break;
            case "enc-missing": fields.Remove("enc"); break;
            case "enc-number": fields["enc"] = 123; break;
        }

        var json = JsonSerializer.Serialize(fields);
        json = scenario switch
        {
            "alg-duplicate" => json[..^1] + ",\"alg\":\"A256KW\"}",
            "enc-duplicate" => json[..^1] + ",\"enc\":\"A256CBC-HS512\"}",
            "non-object" => "[]",
            "invalid-json" => "{",
            _ => json
        };
        parts[0] = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(json));
        var test = CreateTest(Config with
        {
            DecryptionKeys = [new Base64KeyConfig { Value = Convert.ToBase64String(IdentityTestTokens.EncryptionBytes) }]
        }, string.Join('.', parts));

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-jwt", "TokenMalformed");
    }

    [TestMethod]
    [DataRow("exp")]
    [DataRow("nbf")]
    [DataRow("iat")]
    public void EncryptedJwtPreservesFractionalNumericDates(string claim)
    {
        var origin = claim == "exp" ? IdentityTestTokens.Now.AddMinutes(5) : IdentityTestTokens.Now.AddMinutes(-1);
        var token = IdentityTestTokens.Encrypt(claims => claims[claim] = origin.ToUnixTimeSeconds() + 0.5m);
        var test = CreateTest(Config with
        {
            DecryptionKeys = [new Base64KeyConfig { Value = Convert.ToBase64String(IdentityTestTokens.EncryptionBytes) }]
        }, token);

        test.RunInbound();

        var jwt = test.Context.Variables["jwt"].Should().BeAssignableTo<Jwt>().Which;
        var actual = claim switch
        {
            "exp" => jwt.ExpirationTime,
            "nbf" => jwt.NotBefore,
            _ => jwt.IssuedAt
        };
        actual.Should().Be(origin.UtcDateTime.AddTicks(5_000_000));
        test.Context.Variables["continued"].Should().Be(true);
    }

    [TestMethod]
    [DataRow("issuer")]
    [DataRow("audience")]
    [DataRow("expiry")]
    [DataRow("claim")]
    public void InjectedAuthenticationDoesNotBypassPolicyConstraints(string scenario)
    {
        var raw = IdentityTestTokens.Create(claims =>
        {
            if (scenario == "issuer") { claims["iss"] = "https://wrong.example"; }
            if (scenario == "audience") { claims["aud"] = "other"; }
            if (scenario == "expiry") { claims["exp"] = IdentityTestTokens.Now.AddSeconds(-1).ToUnixTimeSeconds(); }
            if (scenario == "claim") { claims.Remove("roles"); }
        });
        var test = CreateTest(Config with
        {
            IssuerSigningKeys = null,
            RequiredClaims = [new ClaimConfig { Name = "roles", Values = ["reader"] }]
        }, raw);
        var validator = new RecordingJwtValidator(request => IdentityTestTokens.Authenticated(request.Token));
        test.Context.Services.Register<IJwtValidator>(validator);

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-jwt");
        validator.Requests.Should().ContainSingle().Which.Token.Should().Be(raw);
    }

    [TestMethod]
    public void InjectedValidatorIsAnExplicitAuthenticationBoundary()
    {
        var raw = IdentityTestTokens.Create();
        var test = CreateTest(Config with { IssuerSigningKeys = null }, raw);
        var validator = new RecordingJwtValidator(request => IdentityTestTokens.Authenticated(request.Token));
        test.Context.Services.Register<IJwtValidator>(validator);

        test.RunInbound();

        test.Context.Variables["continued"].Should().Be(true);
        var request = validator.Requests.Should().ContainSingle().Which;
        request.Token.Should().Be(raw);
        request.RequireSignedTokens.Should().BeTrue();
        request.SigningKeys.Should().BeEmpty();
        request.ValidationTime.Should().Be(IdentityTestTokens.Now);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ValidatorRejectionsAndUnexpectedFailuresAreNotSuccess(bool unexpected)
    {
        var test = CreateTest(token: IdentityTestTokens.Create());
        test.Context.Services.Register<IJwtValidator>(new RecordingJwtValidator(_ => unexpected
            ? throw new InvalidOperationException("validator implementation failed")
            : throw new SecurityTokenException("signature rejected")));

        if (unexpected)
        {
            var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());
            error.InnerException.Should().BeOfType<InvalidOperationException>();
            test.Context.Variables.Should().NotContainKey("continued").And.NotContainKey("jwt");
        }
        else
        {
            test.RunInbound();
            IdentityTestTokens.AssertRejected(test.Context, "validate-jwt");
        }
    }

    [TestMethod]
    public void NullValidatorOutputIsAnExplicitProviderContractError()
    {
        var test = CreateTest(token: IdentityTestTokens.Create());
        test.Context.Services.Register<IJwtValidator>(new RecordingJwtValidator(_ => null!));

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeOfType<InvalidOperationException>();
        test.Context.Variables.Should().NotContainKey("continued").And.NotContainKey("jwt");
    }

    [TestMethod]
    [DataRow("header")]
    [DataRow("payload")]
    public void RejectsDuplicateJsonMembersEvenWithAValidSignature(string part)
    {
        var header = "{\"alg\":\"HS256\",\"typ\":\"JWT\"}";
        var payload = JsonSerializer.Serialize(IdentityTestTokens.Claims());
        if (part == "header") { header = header[..^1] + ",\"alg\":\"HS256\"}"; }
        else { payload = payload[..^1] + ",\"iss\":\"https://issuer.example\"}"; }
        var token = IdentityTestTokens.SignRaw(header, payload);
        var test = CreateTest(token: token);

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-jwt", "TokenMalformed");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void EvaluatesAuthoredExpressionsAndEscapesConfiguredFailures(bool valid)
    {
        var message = "Denied \"quoted\"\nwith newline";
        var test = new ExpressionJwtDocument().AsTestDocument();
        IdentityTestTokens.SetClock(test.Context);
        test.Context.Variables["token-value"] = valid ? IdentityTestTokens.Create() : "malformed";
        test.Context.Variables["expected-issuer"] = IdentityTestTokens.Issuer;
        test.Context.Variables["expected-audience"] = IdentityTestTokens.Audience;
        test.Context.Variables["failure-code"] = 403;
        test.Context.Variables["failure-message"] = message;
        test.Context.Response.StatusCode = 200;
        test.Context.Response.StatusReason = "OK";
        test.Context.Response.Headers["Content-Length"] = ["999"];
        test.Context.Response.Body.Content = "stale successful response";

        test.RunInbound();

        if (valid)
        {
            test.Context.Variables["jwt"].Should().BeAssignableTo<Jwt>();
            test.Context.Variables["continued"].Should().Be(true);
        }
        else
        {
            IdentityTestTokens.AssertRejected(test.Context, "validate-jwt", code: 403, message: message);
            test.Context.Response.StatusReason.Should().Be("Forbidden");
            test.Context.Response.Headers.Should().NotContainKey("Content-Length");
        }
    }

    [TestMethod]
    public void ConfiguredFailureCodeHasTheCorrespondingHttpReason()
    {
        var test = CreateTest(Config with { FailedValidationHttpCode = 500, FailedValidationErrorMessage = "Denied" });
        test.Context.Response.StatusReason = "OK";

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(500);
        test.Context.Response.StatusReason.Should().Be("Internal Server Error");
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables.Should().NotContainKey("continued");
        using var body = JsonDocument.Parse(test.Context.Response.Body.Content!);
        body.RootElement.GetProperty("message").GetString().Should().Be("Denied");
    }

    [TestMethod]
    public void CallbackOverridesDefaultValidationExplicitly()
    {
        var test = CreateTest();
        test.SetupInbound().ValidateJwt((_, config) => config.OutputTokenVariableName == "jwt")
            .WithCallback((context, _) => context.Variables["mocked"] = true);

        test.RunInbound();

        test.Context.Variables["mocked"].Should().Be(true);
        test.Context.Variables["continued"].Should().Be(true);
        test.Context.Variables.Should().NotContainKey("jwt");
    }

    [TestMethod]
    public void UnmatchedCallbackDoesNotConcealMissingAuthentication()
    {
        var test = CreateTest();
        test.SetupInbound().ValidateJwt((_, _) => false).WithCallback((context, _) =>
            context.Variables["mocked"] = true);

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-jwt");
        test.Context.Variables.Should().NotContainKey("mocked");
    }

    [TestMethod]
    public void CallbackFailuresAreSurfaced()
    {
        var test = CreateTest();
        var failure = new InvalidOperationException("callback failure");
        test.SetupInbound().ValidateJwt().WithCallback((_, _) => throw failure);

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeSameAs(failure);
        test.Context.Variables.Should().NotContainKey("continued");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RunsInsideInboundFragments(bool valid)
    {
        var test = new IdentityFragmentDocument().AsTestDocument().RegisterFragment("identity",
            new IdentityFragment(context => context.ValidateJwt(Config)));
        IdentityTestTokens.SetClock(test.Context);
        test.Context.Request.Headers["Authorization"] = [$"Bearer {(valid ? IdentityTestTokens.Create() : "invalid")}"];

        test.RunInbound();

        if (valid)
        {
            test.Context.Variables["jwt"].Should().BeAssignableTo<Jwt>();
            test.Context.Variables["continued"].Should().Be(true);
        }
        else
        {
            IdentityTestTokens.AssertRejected(test.Context, "validate-jwt");
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ValidationControlsAllSubsequentPipelineSections(bool valid)
    {
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Global, new JwtDocument(Config))
            .AddPolicy(PolicyScope.Operation, new IdentityMarkerDocument())
            .ConfigureContext(context =>
            {
                IdentityTestTokens.SetClock(context);
                context.Request.Headers["Authorization"] = [$"Bearer {(valid ? IdentityTestTokens.Create() : "invalid")}"];
            })
            .Build();

        pipeline.RunAll();

        if (valid)
        {
            pipeline.Context.Variables.Should().ContainKeys("continued", "inner", "backend", "outbound");
        }
        else
        {
            IdentityTestTokens.AssertRejected(pipeline.Context, "validate-jwt");
            pipeline.Context.Variables.Should().NotContainKey("inner")
                .And.NotContainKey("backend").And.NotContainKey("outbound");
        }
    }

    private sealed class JwtDocument(ValidateJwtConfig config) : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.ValidateJwt(config);
            context.SetVariable("continued", true);
        }
    }

    private sealed class ExpressionJwtDocument : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            var expression = context.ExpressionContext;
            context.ValidateJwt(Config with
            {
                HeaderName = null,
                TokenValue = (string)expression.Variables["token-value"],
                Issuers = [(string)expression.Variables["expected-issuer"]],
                Audiences = [(string)expression.Variables["expected-audience"]],
                FailedValidationHttpCode = (int)expression.Variables["failure-code"],
                FailedValidationErrorMessage = (string)expression.Variables["failure-message"],
                ClockSkew = 0,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                RequiredClaims = [new ClaimConfig { Name = "scp", Separator = " ", Values = ["read", "write"] }]
            });
            context.SetVariable("continued", true);
        }
    }
}

internal static class IdentityTestTokens
{
    internal const string Issuer = "https://issuer.example";
    internal const string Audience = "api";
    internal static readonly DateTimeOffset Now = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
    internal static byte[] SigningBytes => Enumerable.Range(1, 64).Select(value => (byte)value).ToArray();
    internal static byte[] OtherSigningBytes => Enumerable.Range(65, 64).Select(value => (byte)value).ToArray();
    internal static byte[] EncryptionBytes => Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
    internal static byte[] OtherEncryptionBytes => Enumerable.Range(33, 32).Select(value => (byte)value).ToArray();
    internal static SecurityKey SigningKey => new SymmetricSecurityKey(SigningBytes) { KeyId = "trusted" };
    internal static SecurityKey OtherSigningKey => new SymmetricSecurityKey(OtherSigningBytes) { KeyId = "other" };

    internal static Dictionary<string, object> Claims() => new(StringComparer.Ordinal)
    {
        ["iss"] = Issuer,
        ["aud"] = Audience,
        ["sub"] = "alice",
        ["jti"] = "token-id",
        ["exp"] = Now.AddMinutes(5).ToUnixTimeSeconds(),
        ["nbf"] = Now.AddMinutes(-1).ToUnixTimeSeconds(),
        ["iat"] = Now.AddMinutes(-1).ToUnixTimeSeconds(),
        ["roles"] = new[] { "reader", "writer" },
        ["scp"] = "read write"
    };

    internal static string Create(Action<Dictionary<string, object>>? configure = null,
        SecurityKey? key = null, string algorithm = SecurityAlgorithms.HmacSha256, bool unsigned = false)
    {
        var claims = Claims();
        configure?.Invoke(claims);
        var payload = new JwtPayload();
        foreach (var (name, value) in claims) { payload[name] = value; }
        var credentials = unsigned ? null : new SigningCredentials(key ?? SigningKey, algorithm);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(credentials), payload));
    }

    internal static string Encrypt(Action<Dictionary<string, object>>? configure = null,
        EncryptingCredentials? encryption = null)
    {
        var claims = Claims();
        configure?.Invoke(claims);
        return new JwtSecurityTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateEncodedJwt(new SecurityTokenDescriptor
        {
            Claims = claims,
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256),
            EncryptingCredentials = encryption ?? new EncryptingCredentials(
                new SymmetricSecurityKey(EncryptionBytes),
                SecurityAlgorithms.Aes256KW, SecurityAlgorithms.Aes256CbcHmacSha512)
        });
    }

    internal static string SignRaw(string header, string payload)
    {
        var content = $"{Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(header))}." +
            Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(payload));
        return $"{content}.{Base64UrlEncoder.Encode(HMACSHA256.HashData(SigningBytes, Encoding.ASCII.GetBytes(content)))}";
    }

    internal static string WithNumericDate(string claim, string seconds,
        Action<Dictionary<string, object>>? configure = null)
    {
        var claims = Claims();
        configure?.Invoke(claims);
        claims.Remove(claim);
        var payload = JsonSerializer.Serialize(claims);
        return SignRaw("{\"alg\":\"HS256\",\"typ\":\"JWT\"}", $"{payload[..^1]},\"{claim}\":{seconds}}}");
    }

    internal static Jwt Authenticated(string token)
    {
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);
        return new MockJwt
        {
            Algorithm = parsed.Header.Alg,
            Type = parsed.Header.Typ,
            Issuer = parsed.Issuer,
            Subject = parsed.Subject,
            Id = parsed.Id,
            Audiences = parsed.Audiences.ToArray(),
            Claims = parsed.Claims.GroupBy(claim => claim.Type, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(claim => claim.Value).ToArray(), StringComparer.Ordinal),
            ExpirationTime = parsed.ValidTo,
            NotBefore = parsed.ValidFrom,
            IssuedAt = parsed.IssuedAt
        };
    }

    internal static void SetClock(GatewayContext context) => context.Timestamp = Now.UtcDateTime;

    internal static void AssertRejected(GatewayContext context, string source, string? reason = null,
        int code = 401, string? message = null)
    {
        context.Response.StatusCode.Should().Be(code);
        context.Response.StatusReason.Should().Be(code == 401 ? "Unauthorized" : "Forbidden");
        context.ResponseTerminated.Should().BeTrue();
        context.Variables.Should().NotContainKey("continued").And.NotContainKey("jwt");
        context.LastError.Source.Should().Be(source);
        context.LastError.Section.Should().Be("inbound");
        context.LastError.HttpErrorCode.Should().Be(code);
        context.LastError.Reason.Should().NotBeNullOrWhiteSpace();
        if (reason is not null) { context.LastError.Reason.Should().Be(reason); }
        context.Response.Headers["Content-Type"].Should().Equal("application/json");
        using var body = JsonDocument.Parse(context.Response.Body.Content!);
        body.RootElement.GetProperty("statusCode").GetInt32().Should().Be(code);
        body.RootElement.GetProperty("message").GetString().Should().Be(message ?? context.LastError.Message);
    }

    internal static X509Certificate2 CreateCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=identity-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(Now.AddYears(-10), Now.AddYears(10));
    }

    internal static RsaSecurityKey RsaEncryptionKey(RSA rsa, string algorithm)
    {
        var key = new RsaSecurityKey(rsa);
        if (algorithm == "RSA-OAEP-256")
        {
            key.CryptoProviderFactory = new CryptoProviderFactory
            {
                CustomCryptoProvider = new OaepSha256CryptoProvider()
            };
        }

        return key;
    }

    private sealed class OaepSha256CryptoProvider : ICryptoProvider
    {
        public bool IsSupportedAlgorithm(string algorithm, params object[] args) =>
            algorithm == "RSA-OAEP-256" && args.Length != 0 && args[0] is RsaSecurityKey { Rsa: not null };

        public object Create(string algorithm, params object[] args)
        {
            if (algorithm == "RSA-OAEP-256" && args.Length != 0
                && args[0] is RsaSecurityKey { Rsa: { } rsa } key)
            {
                return new OaepSha256KeyWrapProvider(key, rsa);
            }

            throw new ArgumentException("The fixture does not support this key-wrap request.", nameof(algorithm));
        }

        public void Release(object cryptoInstance)
        {
            if (cryptoInstance is not OaepSha256KeyWrapProvider provider)
            {
                throw new ArgumentException("Unexpected fixture crypto provider.", nameof(cryptoInstance));
            }

            provider.Dispose();
        }
    }

    private sealed class OaepSha256KeyWrapProvider(RsaSecurityKey key, RSA rsa) : KeyWrapProvider
    {
        public override string Algorithm => "RSA-OAEP-256";
        public override string Context { get; set; } = string.Empty;
        public override SecurityKey Key => key;
        public override byte[] WrapKey(byte[] bytes) => rsa.Encrypt(bytes, RSAEncryptionPadding.OaepSHA256);
        public override byte[] UnwrapKey(byte[] bytes) => rsa.Decrypt(bytes, RSAEncryptionPadding.OaepSHA256);

        protected override void Dispose(bool disposing)
        {
            // The fixture caller owns the RSA instance.
        }
    }
}

internal sealed class RecordingJwtKeyProvider(Func<JwtKeyRequest, JwtTrustMaterial> provide) : IJwtKeyProvider
{
    internal List<JwtKeyRequest> Requests { get; } = [];

    public JwtTrustMaterial GetKeys(JwtKeyRequest request)
    {
        Requests.Add(request);
        return provide(request);
    }
}

internal sealed class RecordingJwtValidator(Func<JwtValidationRequest, Jwt> validate) : IJwtValidator
{
    internal List<JwtValidationRequest> Requests { get; } = [];

    public Jwt Validate(JwtValidationRequest request)
    {
        Requests.Add(request);
        return validate(request);
    }
}

internal sealed class IdentityFragment(Action<IFragmentContext> execute) : IFragment
{
    public void Fragment(IFragmentContext context) => execute(context);
}

internal sealed class IdentityFragmentDocument : IDocument
{
    public void Inbound(IInboundContext context)
    {
        context.IncludeFragment("identity");
        context.SetVariable("continued", true);
    }
}

internal sealed class IdentityMarkerDocument : IDocument
{
    public void Inbound(IInboundContext context) => context.SetVariable("inner", true);
    public void Backend(IBackendContext context) => context.SetVariable("backend", true);
    public void Outbound(IOutboundContext context) => context.SetVariable("outbound", true);
}
