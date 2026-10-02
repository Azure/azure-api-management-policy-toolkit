// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;
using Microsoft.IdentityModel.Tokens;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class ValidateAzureAdTokenTests
{
    private const string Tenant = "11111111-2222-4333-8444-555555555555";
    private const string OtherTenant = "22222222-3333-4444-8555-666666666666";
    private const string ConsumerTenant = "9188040d-6c67-4c5b-b112-36a304b66dad";
    private const string BackendApplication = "66666666-7777-4888-9999-aaaaaaaaaaaa";
    private const string ClientApplication = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee";

    private static ValidateAzureAdTokenConfig Config => new()
    {
        TenantId = Tenant,
        BackendApplicationIds = [BackendApplication],
        ClientApplicationIds = [ClientApplication],
        Audiences = [BackendApplication],
        RequiredClaims = [new ClaimConfig { Name = "roles", Values = ["reader"] }],
        OutputTokenVariableName = "jwt"
    };

    private static void EntraClaims(Dictionary<string, object> claims, string tenant = Tenant, string version = "2.0")
    {
        claims["tid"] = tenant;
        claims["ver"] = version;
        claims["iss"] = version == "2.0"
            ? $"https://login.microsoftonline.com/{tenant}/v2.0"
            : $"https://sts.windows.net/{tenant}/";
        claims["aud"] = BackendApplication;
        claims[version == "2.0" ? "azp" : "appid"] = ClientApplication;
    }

    private static string Token(Action<Dictionary<string, object>>? configure = null,
        string tenant = Tenant, string version = "2.0", SecurityKey? key = null, bool unsigned = false) =>
        IdentityTestTokens.Create(claims =>
        {
            EntraClaims(claims, tenant, version);
            configure?.Invoke(claims);
        }, key: key, unsigned: unsigned);

    private static TestDocument CreateTest(ValidateAzureAdTokenConfig? config = null,
        string? token = null, bool supplyKeys = true)
    {
        var test = new EntraDocument(config ?? Config).AsTestDocument();
        IdentityTestTokens.SetClock(test.Context);
        if (token is not null)
        {
            test.Context.Request.Headers["Authorization"] = [$"Bearer {token}"];
        }

        if (supplyKeys)
        {
            test.Context.Services.Register<IJwtKeyProvider>(new RecordingJwtKeyProvider(_ => new JwtTrustMaterial
            {
                SigningKeys = [IdentityTestTokens.SigningKey]
            }));
        }

        return test;
    }

    [TestMethod]
    [DataRow("1.0")]
    [DataRow("2.0")]
    public void ValidatesSignatureTenantApplicationsAndOutputsJwt(string version)
    {
        var test = CreateTest(token: Token(version: version));

        test.RunInbound();

        var jwt = test.Context.Variables["jwt"].Should().BeAssignableTo<Jwt>().Which;
        jwt.Claims["tid"].Should().Equal(Tenant);
        jwt.Claims[version == "2.0" ? "azp" : "appid"].Should().Equal(ClientApplication);
        jwt.Audiences.Should().Equal(BackendApplication);
        jwt.Algorithm.Should().Be(SecurityAlgorithms.HmacSha256);
        test.Context.Variables["continued"].Should().Be(true);
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("exp")]
    [DataRow("nbf")]
    [DataRow("iat")]
    public void EntraTokensAcceptAndPreserveFractionalNumericDates(string claim)
    {
        var origin = claim == "exp" ? IdentityTestTokens.Now.AddMinutes(5) : IdentityTestTokens.Now.AddMinutes(-1);
        var seconds = (origin.ToUnixTimeSeconds() + 0.5m).ToString(CultureInfo.InvariantCulture);
        var token = IdentityTestTokens.WithNumericDate(claim, seconds, claims => EntraClaims(claims));
        var test = CreateTest(token: token);

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
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("RSA-OAEP", true)]
    [DataRow("RSA-OAEP-256", true)]
    [DataRow("RSA1_5", false)]
    public void EntraEncryptedTokensEnforceTheKeyManagementAllowlist(string algorithm, bool allowed)
    {
        using var certificate = IdentityTestTokens.CreateCertificate();
        using var rsa = certificate.GetRSAPrivateKey()!;
        var key = IdentityTestTokens.RsaEncryptionKey(rsa, algorithm);
        var token = IdentityTestTokens.Encrypt(claims => EntraClaims(claims),
            new EncryptingCredentials(key, algorithm, SecurityAlgorithms.Aes256CbcHmacSha512));
        var test = CreateTest(token: token, supplyKeys: false);
        test.Context.Services.Register<IJwtKeyProvider>(new RecordingJwtKeyProvider(_ => new JwtTrustMaterial
        {
            SigningKeys = [IdentityTestTokens.SigningKey],
            DecryptionKeys = [key]
        }));

        test.RunInbound();

        if (allowed)
        {
            ((Jwt)test.Context.Variables["jwt"]).Claims["tid"].Should().Equal(Tenant);
            test.Context.Variables["continued"].Should().Be(true);
        }
        else
        {
            IdentityTestTokens.AssertRejected(test.Context, "validate-azure-ad-token", "TokenDecryptionAlgorithmNotAllowed");
        }
    }

    [TestMethod]
    [DataRow("header")]
    [DataRow("query")]
    [DataRow("value")]
    public void ExtractsTheAuthoredTokenSource(string source)
    {
        var raw = Token();
        var test = CreateTest(Config with
        {
            HeaderName = source == "header" ? "X-Entra-Token" : null,
            QueryParameterName = source == "query" ? "token" : null,
            TokenValue = source == "value" ? raw : null
        });
        test.Context.Request.Headers["X-Entra-Token"] = [raw];
        test.Context.Request.Url.Query["token"] = [raw];
        test.Context.Request.Headers["Authorization"] = ["Bearer invalid"];

        test.RunInbound();

        test.Context.Variables["jwt"].Should().BeAssignableTo<Jwt>();
        test.Context.Variables["continued"].Should().Be(true);
    }

    [TestMethod]
    public void DefaultAuthorizationHeaderAndBearerSchemeAreCaseInsensitive()
    {
        var test = CreateTest();
        test.Context.Request.Headers["authorization"] = [$"bEaReR {Token()}"];

        test.RunInbound();

        test.Context.Variables["continued"].Should().Be(true);
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("malformed")]
    [DataRow("wrong-scheme")]
    [DataRow("unsigned")]
    [DataRow("wrong-key")]
    [DataRow("expired")]
    [DataRow("future")]
    public void RejectsUnauthenticatedTokens(string scenario)
    {
        var token = scenario switch
        {
            "missing" => null,
            "malformed" => "not-a-token",
            "unsigned" => Token(unsigned: true),
            "wrong-key" => Token(key: IdentityTestTokens.OtherSigningKey),
            "expired" => Token(claims => claims["exp"] = IdentityTestTokens.Now.AddSeconds(-1).ToUnixTimeSeconds()),
            "future" => Token(claims => claims["nbf"] = IdentityTestTokens.Now.AddSeconds(1).ToUnixTimeSeconds()),
            _ => Token()
        };
        var test = CreateTest(token: token);
        if (scenario == "wrong-scheme") { test.Context.Request.Headers["Authorization"] = [$"Basic {token}"]; }
        test.Context.Variables["jwt"] = "stale";

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-azure-ad-token");
    }

    [TestMethod]
    public void MissingEntraTrustMaterialFailsExplicitly()
    {
        var test = CreateTest(token: Token(), supplyKeys: false);

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-azure-ad-token", "TokenTrustUnavailable");
        test.Context.LastError.Message.Should().Contain("trust");
    }

    [TestMethod]
    public void EntraTokensRequireExpiration()
    {
        var test = CreateTest(token: Token(claims => claims.Remove("exp")));

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-azure-ad-token", "TokenExpirationMissing");
    }

    [TestMethod]
    public void ProviderIssuerRestrictionsRemainAuthoritative()
    {
        var test = CreateTest(token: Token(), supplyKeys: false);
        test.Context.Services.Register<IJwtKeyProvider>(new RecordingJwtKeyProvider(_ => new JwtTrustMaterial
        {
            SigningKeys = [IdentityTestTokens.SigningKey],
            Issuers = [$"https://login.microsoftonline.com/{OtherTenant}/v2.0"]
        }));

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-azure-ad-token", "TokenIssuerNotAllowed");
    }

    [TestMethod]
    [DataRow("wrong-tenant")]
    [DataRow("missing-tenant")]
    [DataRow("malformed-tenant")]
    [DataRow("wrong-issuer")]
    [DataRow("issuer-tenant-mismatch")]
    [DataRow("issuer-lookalike")]
    [DataRow("unsupported-version")]
    public void SignedTokensStillRequireTheCorrectTenantAndEntraIssuer(string scenario)
    {
        var raw = Token(claims =>
        {
            switch (scenario)
            {
                case "wrong-tenant":
                    EntraClaims(claims, OtherTenant);
                    break;
                case "missing-tenant":
                    claims.Remove("tid");
                    break;
                case "malformed-tenant":
                    claims["tid"] = "not-a-tenant-guid";
                    break;
                case "wrong-issuer":
                    claims["iss"] = "https://attacker.example";
                    break;
                case "issuer-tenant-mismatch":
                    claims["iss"] = $"https://login.microsoftonline.com/{OtherTenant}/v2.0";
                    break;
                case "issuer-lookalike":
                    claims["iss"] = $"https://login.microsoftonline.com.attacker.example/{Tenant}/v2.0";
                    break;
                case "unsupported-version":
                    claims["ver"] = "3.0";
                    break;
            }
        });
        var test = CreateTest(token: raw);

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-azure-ad-token");
    }

    [TestMethod]
    [DataRow("common", Tenant, true)]
    [DataRow("organizations", Tenant, true)]
    [DataRow("common", ConsumerTenant, true)]
    [DataRow("organizations", ConsumerTenant, false)]
    [DataRow("https://login.microsoftonline.com/common", ConsumerTenant, true)]
    [DataRow("https://login.microsoftonline.com/organizations", ConsumerTenant, false)]
    public void WellKnownTenantsRetainTheirDirectoryConstraints(string configuredTenant, string tokenTenant, bool valid)
    {
        var test = CreateTest(Config with { TenantId = configuredTenant }, Token(tenant: tokenTenant));

        test.RunInbound();

        if (valid)
        {
            test.Context.Variables["continued"].Should().Be(true);
        }
        else
        {
            IdentityTestTokens.AssertRejected(test.Context, "validate-azure-ad-token", "TokenTenantNotAllowed");
        }
    }

    [TestMethod]
    [DataRow("contoso.onmicrosoft.com")]
    [DataRow("https://contoso.onmicrosoft.com")]
    public void TenantAliasesRequireExplicitCanonicalMapping(string alias)
    {
        var provider = new RecordingJwtKeyProvider(_ => new JwtTrustMaterial
        {
            SigningKeys = [IdentityTestTokens.SigningKey],
            TenantId = Tenant
        });
        var test = CreateTest(Config with { TenantId = alias }, Token(), supplyKeys: false);
        test.Context.Services.Register<IJwtKeyProvider>(provider);

        test.RunInbound();

        test.Context.Variables["continued"].Should().Be(true);
        var request = provider.Requests.Should().ContainSingle().Which;
        request.PolicyName.Should().Be(nameof(IInboundContext.ValidateAzureAdToken));
        request.TenantId.Should().Be(alias);
        request.OpenIdConfigurationUrls.Should().BeEmpty();
    }

    [TestMethod]
    public void UnresolvedTenantAliasIsNotAnAnyTenantFallback()
    {
        var test = CreateTest(Config with { TenantId = "contoso.onmicrosoft.com" }, Token());

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-azure-ad-token", "TokenTrustUnavailable");
    }

    [TestMethod]
    public void ProviderCanonicalTenantCannotOverrideAnAuthoredTenant()
    {
        var test = CreateTest(token: Token(), supplyKeys: false);
        test.Context.Services.Register<IJwtKeyProvider>(new RecordingJwtKeyProvider(_ => new JwtTrustMaterial
        {
            SigningKeys = [IdentityTestTokens.SigningKey],
            TenantId = OtherTenant
        }));

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-azure-ad-token", "TokenTenantNotAllowed");
    }

    [TestMethod]
    [DataRow("1.0", "appid")]
    [DataRow("2.0", "azp")]
    public void ClientApplicationConstraintUsesTheVersionSpecificClaim(string version, string expectedClaim)
    {
        var raw = Token(claims =>
        {
            claims[expectedClaim] = "unauthorized-client";
            claims[expectedClaim == "appid" ? "azp" : "appid"] = ClientApplication;
        }, version: version);
        var test = CreateTest(token: raw);

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-azure-ad-token", "TokenClientApplicationNotAllowed");
    }

    [TestMethod]
    [DataRow("client")]
    [DataRow("missing-client")]
    [DataRow("backend")]
    [DataRow("audience")]
    [DataRow("claim")]
    public void EnforcesEveryAuthoredApplicationAudienceAndClaimConstraint(string scenario)
    {
        var config = scenario switch
        {
            "backend" => Config with { BackendApplicationIds = ["different-backend"] },
            "audience" => Config with { Audiences = ["different-audience"] },
            _ => Config
        };
        var raw = Token(claims =>
        {
            if (scenario == "client") { claims["azp"] = "different-client"; }
            if (scenario == "missing-client") { claims.Remove("azp"); }
            if (scenario == "claim") { claims["roles"] = new[] { "administrator" }; }
        });
        var test = CreateTest(config, raw);

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-azure-ad-token");
    }

    [TestMethod]
    public void BackendApplicationConstraintWorksWithoutAnAudienceList()
    {
        var test = CreateTest(Config with { Audiences = null }, Token(claims => claims["aud"] = "wrong-backend"));

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-azure-ad-token", "TokenBackendApplicationNotAllowed");
    }

    [TestMethod]
    public void BackendApplicationSupportsTheDefaultApiApplicationUri()
    {
        var test = CreateTest(Config with { Audiences = [$"api://{BackendApplication}"] },
            Token(claims => claims["aud"] = $"api://{BackendApplication}"));

        test.RunInbound();

        test.Context.Variables["continued"].Should().Be(true);
    }

    [TestMethod]
    public void AudienceRestrictionWorksWithoutAClientApplicationList()
    {
        var test = CreateTest(Config with { ClientApplicationIds = null }, Token(claims => claims.Remove("azp")));

        test.RunInbound();

        test.Context.Variables["continued"].Should().Be(true);
    }

    [TestMethod]
    public void RequiredClaimsSupportAnyAndSeparators()
    {
        var test = CreateTest(Config with
        {
            RequiredClaims = [new ClaimConfig
            {
                Name = "scp", Match = "any", Separator = " ", Values = ["admin", "write"]
            }]
        }, Token());

        test.RunInbound();

        test.Context.Variables["continued"].Should().Be(true);
    }

    [TestMethod]
    public void AuthenticatedOverrideCannotBypassTenantChecks()
    {
        var raw = Token(tenant: OtherTenant);
        var test = CreateTest(token: raw, supplyKeys: false);
        test.Context.Services.Register<IJwtValidator>(new RecordingJwtValidator(request =>
            IdentityTestTokens.Authenticated(request.Token)));

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-azure-ad-token", "TokenTenantNotAllowed");
    }

    [TestMethod]
    public void ConfiguredCertificateDecryptsAnAuthenticatedEntraToken()
    {
        using var certificate = IdentityTestTokens.CreateCertificate();
        var raw = IdentityTestTokens.Encrypt(claims => EntraClaims(claims),
            new EncryptingCredentials(new X509SecurityKey(certificate),
                SecurityAlgorithms.RsaOAEP, SecurityAlgorithms.Aes256CbcHmacSha512));
        var test = CreateTest(Config with
        {
            DecryptionKeys = [new DecryptionKey { CertificateId = "decryption-cert" }]
        }, raw);
        test.SetupCertificateStore().WithCertificateById("decryption-cert", certificate);

        test.RunInbound();

        ((Jwt)test.Context.Variables["jwt"]).Claims["tid"].Should().Equal(Tenant);
        test.Context.Variables["continued"].Should().Be(true);
    }

    [TestMethod]
    public void MissingConfiguredDecryptionCertificateFailsExplicitly()
    {
        var test = CreateTest(Config with
        {
            DecryptionKeys = [new DecryptionKey { CertificateId = "missing" }]
        }, Token());

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-azure-ad-token", "TokenTrustUnavailable");
    }

    [TestMethod]
    [DataRow("tenant")]
    [DataRow("sources")]
    [DataRow("claim")]
    public void InvalidAuthoredConfigurationIsNotAnAuthenticationSuccess(string scenario)
    {
        var config = scenario switch
        {
            "tenant" => Config with { TenantId = " " },
            "sources" => Config with { HeaderName = "Authorization", TokenValue = Token() },
            _ => Config with { RequiredClaims = [new ClaimConfig { Name = "roles", Match = "invalid" }] }
        };
        var test = CreateTest(config, Token());

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.Policy.Should().Be(nameof(IInboundContext.ValidateAzureAdToken));
        error.InnerException.Should().BeAssignableTo<ArgumentException>();
        test.Context.Variables.Should().NotContainKey("continued");
    }

    [TestMethod]
    public void UnexpectedKeyProviderErrorsAreSurfaced()
    {
        var test = CreateTest(token: Token(), supplyKeys: false);
        var failure = new InvalidOperationException("offline key provider failed");
        test.Context.Services.Register<IJwtKeyProvider>(new RecordingJwtKeyProvider(_ => throw failure));

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeSameAs(failure);
        test.Context.Variables.Should().NotContainKey("continued").And.NotContainKey("jwt");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AuthoredExpressionsControlTenantSourceAndFailureResponse(bool valid)
    {
        var test = new ExpressionEntraDocument().AsTestDocument();
        IdentityTestTokens.SetClock(test.Context);
        test.Context.Variables["tenant-id"] = Tenant;
        test.Context.Variables["raw-token"] = valid ? Token() : "malformed";
        test.Context.Variables["failure-code"] = 403;
        test.Context.Variables["failure-message"] = "Denied \"Entra\"\nrequest";
        test.Context.Services.Register<IJwtKeyProvider>(new RecordingJwtKeyProvider(_ => new JwtTrustMaterial
        {
            SigningKeys = [IdentityTestTokens.SigningKey]
        }));

        test.RunInbound();

        if (valid)
        {
            test.Context.Variables["continued"].Should().Be(true);
            test.Context.Variables["jwt"].Should().BeAssignableTo<Jwt>();
        }
        else
        {
            IdentityTestTokens.AssertRejected(test.Context, "validate-azure-ad-token",
                code: 403, message: "Denied \"Entra\"\nrequest");
        }
    }

    [TestMethod]
    public void CallbackExplicitlyOverridesAuthentication()
    {
        var test = CreateTest(supplyKeys: false);
        test.SetupInbound().ValidateAzureAdToken((_, config) => config.TenantId == Tenant)
            .WithCallback((context, _) => context.Variables["mocked"] = true);

        test.RunInbound();

        test.Context.Variables["mocked"].Should().Be(true);
        test.Context.Variables["continued"].Should().Be(true);
        test.Context.Variables.Should().NotContainKey("jwt");
    }

    [TestMethod]
    public void UnmatchedCallbackDoesNotAuthorizeTheRequest()
    {
        var test = CreateTest();
        test.SetupInbound().ValidateAzureAdToken((_, _) => false).WithCallback((context, _) =>
            context.Variables["mocked"] = true);

        test.RunInbound();

        IdentityTestTokens.AssertRejected(test.Context, "validate-azure-ad-token");
        test.Context.Variables.Should().NotContainKey("mocked");
    }

    [TestMethod]
    public void CallbackErrorsAreSurfaced()
    {
        var test = CreateTest();
        var failure = new InvalidOperationException("callback failure");
        test.SetupInbound().ValidateAzureAdToken().WithCallback((_, _) => throw failure);

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeSameAs(failure);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SupportsInboundFragmentsWithoutWeakeningValidation(bool valid)
    {
        var test = new IdentityFragmentDocument().AsTestDocument().RegisterFragment("identity",
            new IdentityFragment(context => context.ValidateAzureAdToken(Config)));
        IdentityTestTokens.SetClock(test.Context);
        test.Context.Request.Headers["Authorization"] = [$"Bearer {(valid ? Token() : Token(tenant: OtherTenant))}"];
        test.Context.Services.Register<IJwtKeyProvider>(new RecordingJwtKeyProvider(_ => new JwtTrustMaterial
        {
            SigningKeys = [IdentityTestTokens.SigningKey]
        }));

        test.RunInbound();

        if (valid)
        {
            test.Context.Variables["continued"].Should().Be(true);
        }
        else
        {
            IdentityTestTokens.AssertRejected(test.Context, "validate-azure-ad-token");
        }
    }

    [TestMethod]
    public void RejectionTerminatesAllPipelineScopesAndLaterSections()
    {
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Global, new EntraDocument(Config))
            .AddPolicy(PolicyScope.Operation, new IdentityMarkerDocument())
            .ConfigureContext(context =>
            {
                IdentityTestTokens.SetClock(context);
                context.Request.Headers["Authorization"] = [$"Bearer {Token(tenant: OtherTenant)}"];
                context.Services.Register<IJwtKeyProvider>(new RecordingJwtKeyProvider(_ => new JwtTrustMaterial
                {
                    SigningKeys = [IdentityTestTokens.SigningKey]
                }));
            })
            .Build();

        pipeline.RunAll();

        IdentityTestTokens.AssertRejected(pipeline.Context, "validate-azure-ad-token");
        pipeline.Context.Variables.Should().NotContainKey("inner").And.NotContainKey("backend").And.NotContainKey("outbound");
    }

    private sealed class EntraDocument(ValidateAzureAdTokenConfig config) : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.ValidateAzureAdToken(config);
            context.SetVariable("continued", true);
        }
    }

    private sealed class ExpressionEntraDocument : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            var expression = context.ExpressionContext;
            context.ValidateAzureAdToken(Config with
            {
                TenantId = (string)expression.Variables["tenant-id"],
                TokenValue = (string)expression.Variables["raw-token"],
                FailedValidationHttpCode = (int)expression.Variables["failure-code"],
                FailedValidationErrorMessage = (string)expression.Variables["failure-message"]
            });
            context.SetVariable("continued", true);
        }
    }
}
