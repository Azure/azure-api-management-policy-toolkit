// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;
using Microsoft.IdentityModel.Tokens;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class GetAuthorizationContextTests
{
    [TestMethod]
    [DataRow("inbound")]
    [DataRow("outbound")]
    [DataRow("backend")]
    public void GetAuthorizationContext_StoresTypedAuthorization(string section)
    {
        var authorization = CreateAuthorization();
        var provider = new RecordingAuthorizationProvider(_ => authorization);
        var test = CreateTest(CreateConfig(), section);
        test.Context.Services.Register<IAuthorizationProvider>(provider);
        test.Context.Request.Headers["Authorization"] = ["existing-request-header"];
        test.Context.Response.Headers["Authorization"] = ["existing-response-header"];
        test.Context.Variables["authorization"] = "old-value";

        Run(test, section);

        var result = test.Context.Variables.Should().ContainKey("authorization")
            .WhoseValue.Should().BeOfType<Authorization>().Which;
        result.Should().BeSameAs(authorization);
        result.AccessToken.Should().Be("access-token");
        result.Claims.Should().Contain("scope", "api.read");
        provider.Requests.Should().ContainSingle().Which.Should()
            .Be(new AuthorizationRequest("provider", "connection", "managed", null));
        test.Context.Request.Headers["Authorization"].Should().Equal("existing-request-header");
        test.Context.Response.Headers["Authorization"].Should().Equal("existing-response-header");
        test.Context.Variables.Should().Contain("continued", true);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("managed")]
    public void GetAuthorizationContext_ManagedIdentityIgnoresIdentityToken(string? identityType)
    {
        var provider = new RecordingAuthorizationProvider(_ => CreateAuthorization());
        var test = CreateTest(CreateConfig() with { IdentityType = identityType, Identity = "ignored-token" });
        test.Context.Services.Register<IAuthorizationProvider>(provider);

        test.RunInbound();

        provider.Requests.Should().ContainSingle().Which.Should()
            .Be(new AuthorizationRequest("provider", "connection", "managed", null));
    }

    [TestMethod]
    public void GetAuthorizationContext_PassesJwtIdentityToProvider()
    {
        var provider = new RecordingAuthorizationProvider(_ => CreateAuthorization());
        var test = CreateTest(CreateConfig() with { IdentityType = "jwt", Identity = "entra-jwt" });
        test.Context.Services.Register<IAuthorizationProvider>(provider);

        test.RunInbound();

        provider.Requests.Should().ContainSingle().Which.Should()
            .Be(new AuthorizationRequest("provider", "connection", "jwt", "entra-jwt"));
    }

    [TestMethod]
    [DataRow(null, false)]
    [DataRow("", false)]
    [DataRow(" ", false)]
    [DataRow(null, true)]
    [DataRow("", true)]
    [DataRow(" ", true)]
    public void GetAuthorizationContext_RequiresJwtIdentity(string? identity, bool ignoreError)
    {
        var provider = new RecordingAuthorizationProvider(_ => CreateAuthorization());
        var test = CreateTest(CreateConfig() with
        {
            IdentityType = "jwt", Identity = identity, IgnoreError = ignoreError
        });
        test.Context.Services.Register<IAuthorizationProvider>(provider);

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeOfType<ArgumentException>();
        provider.Requests.Should().BeEmpty();
        test.Context.Variables.Should().NotContainKeys("authorization", "continued");
    }

    [TestMethod]
    [DataRow(nameof(GetAuthorizationContextConfig.ProviderId), null)]
    [DataRow(nameof(GetAuthorizationContextConfig.ProviderId), "")]
    [DataRow(nameof(GetAuthorizationContextConfig.ProviderId), " ")]
    [DataRow(nameof(GetAuthorizationContextConfig.AuthorizationId), null)]
    [DataRow(nameof(GetAuthorizationContextConfig.AuthorizationId), "")]
    [DataRow(nameof(GetAuthorizationContextConfig.AuthorizationId), " ")]
    [DataRow(nameof(GetAuthorizationContextConfig.ContextVariableName), null)]
    [DataRow(nameof(GetAuthorizationContextConfig.ContextVariableName), "")]
    [DataRow(nameof(GetAuthorizationContextConfig.ContextVariableName), " ")]
    public void GetAuthorizationContext_RejectsInvalidRequiredInput(string property, string? value)
    {
        var config = property switch
        {
            nameof(GetAuthorizationContextConfig.ProviderId) => CreateConfig() with { ProviderId = value! },
            nameof(GetAuthorizationContextConfig.AuthorizationId) => CreateConfig() with { AuthorizationId = value! },
            _ => CreateConfig() with { ContextVariableName = value! }
        };
        var provider = new RecordingAuthorizationProvider(_ => CreateAuthorization());
        var test = CreateTest(config with { IgnoreError = true });
        test.Context.Services.Register<IAuthorizationProvider>(provider);

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeOfType<ArgumentException>()
            .Which.ParamName.Should().Be(property);
        provider.Requests.Should().BeEmpty();
        test.Context.Variables.Should().NotContainKey("continued");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("JWT")]
    [DataRow("other")]
    public void GetAuthorizationContext_RejectsUnsupportedIdentityType(string identityType)
    {
        var provider = new RecordingAuthorizationProvider(_ => CreateAuthorization());
        var test = CreateTest(CreateConfig() with { IdentityType = identityType, IgnoreError = true });
        test.Context.Services.Register<IAuthorizationProvider>(provider);

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeOfType<ArgumentException>()
            .Which.ParamName.Should().Be(nameof(GetAuthorizationContextConfig.IdentityType));
        provider.Requests.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(false)]
    public void GetAuthorizationContext_RequiresInjectedProvider(bool? ignoreError)
    {
        var test = CreateTest(CreateConfig() with { IgnoreError = ignoreError });

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.Policy.Should().Be(nameof(IInboundContext.GetAuthorizationContext));
        error.Section.Should().Be(nameof(IInboundContext));
        error.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain(nameof(IAuthorizationProvider));
        test.Context.Response.StatusCode.Should().Be(500);
        test.Context.Variables.Should().NotContainKeys("authorization", "continued");
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("outbound")]
    [DataRow("backend")]
    public void GetAuthorizationContext_IgnoreErrorWithoutProviderStoresNullAndContinues(string section)
    {
        var test = CreateTest(CreateConfig() with { IgnoreError = true }, section);
        test.Context.Variables["authorization"] = CreateAuthorization("stale-token");
        test.Context.Response.StatusCode = 202;

        Run(test, section);

        test.Context.Variables.Should().ContainKey("authorization").WhoseValue.Should().BeNull();
        test.Context.Variables.Should().Contain("continued", true);
        test.Context.Response.StatusCode.Should().Be(202);
    }

    [TestMethod]
    [DataRow("inbound", nameof(IInboundContext))]
    [DataRow("outbound", nameof(IOutboundContext))]
    [DataRow("backend", nameof(IBackendContext))]
    public void GetAuthorizationContext_ReportsProviderFailure(string section, string expectedSection)
    {
        var failure = new HttpRequestException("authorization unavailable");
        var config = CreateConfig();
        var test = CreateTest(config, section);
        test.Context.Services.Register<IAuthorizationProvider>(new FaultedAuthorizationProvider(failure));

        var error = Assert.ThrowsExactly<PolicyException>(() => Run(test, section));

        error.Policy.Should().Be(nameof(IInboundContext.GetAuthorizationContext));
        error.Section.Should().Be(expectedSection);
        error.PolicyArgs.Should().ContainSingle().Which.Should().BeSameAs(config);
        error.InnerException.Should().BeSameAs(failure);
        test.Context.Response.StatusCode.Should().Be(500);
        test.Context.Variables.Should().NotContainKeys("authorization", "continued");
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("outbound")]
    [DataRow("backend")]
    public void GetAuthorizationContext_IgnoreErrorStoresNullAndContinues(string section)
    {
        var test = CreateTest(CreateConfig() with { IgnoreError = true }, section);
        test.Context.Services.Register<IAuthorizationProvider>(
            new FaultedAuthorizationProvider(new HttpRequestException("authorization unavailable")));
        test.Context.Variables["authorization"] = CreateAuthorization("stale-token");
        test.Context.Request.Headers["Authorization"] = ["existing-header"];
        test.Context.Response.StatusCode = 202;

        Run(test, section);

        test.Context.Variables.Should().ContainKey("authorization").WhoseValue.Should().BeNull();
        test.Context.Variables.Should().Contain("continued", true);
        test.Context.Request.Headers["Authorization"].Should().Equal("existing-header");
        test.Context.Response.StatusCode.Should().Be(202);
    }

    [TestMethod]
    [DataRow(nameof(SecurityTokenException), false)]
    [DataRow(nameof(SecurityTokenException), true)]
    [DataRow(nameof(SecurityTokenInvalidAudienceException), false)]
    [DataRow(nameof(SecurityTokenInvalidAudienceException), true)]
    [DataRow(nameof(SecurityTokenExpiredException), false)]
    [DataRow(nameof(SecurityTokenExpiredException), true)]
    public void GetAuthorizationContext_SynchronousTokenValidationFailureHonorsIgnoreError(
        string exceptionType, bool ignoreError)
    {
        var failure = CreateTokenValidationFailure(exceptionType);
        var test = CreateTest(CreateConfig() with
        {
            IdentityType = "jwt", Identity = "invalid-entra-jwt", IgnoreError = ignoreError
        });
        test.SetupInbound().GetAuthorizationContext().WithAuthorizationProviderHook(_ => throw failure);

        AssertTokenValidationFailure(test, failure, ignoreError);
    }

    [TestMethod]
    [DataRow(nameof(SecurityTokenException), false)]
    [DataRow(nameof(SecurityTokenException), true)]
    [DataRow(nameof(SecurityTokenInvalidAudienceException), false)]
    [DataRow(nameof(SecurityTokenInvalidAudienceException), true)]
    [DataRow(nameof(SecurityTokenExpiredException), false)]
    [DataRow(nameof(SecurityTokenExpiredException), true)]
    public void GetAuthorizationContext_FaultedTaskTokenValidationFailureHonorsIgnoreError(
        string exceptionType, bool ignoreError)
    {
        var failure = CreateTokenValidationFailure(exceptionType);
        var test = CreateTest(CreateConfig() with
        {
            IdentityType = "jwt", Identity = "invalid-entra-jwt", IgnoreError = ignoreError
        });
        test.Context.Services.Register<IAuthorizationProvider>(new FaultedAuthorizationProvider(failure));

        AssertTokenValidationFailure(test, failure, ignoreError);
    }

    [TestMethod]
    public void GetAuthorizationContext_OnErrorReadsXmlPolicySourceAndAcquisitionFailure()
    {
        var failure = new HttpRequestException("authorization unavailable");
        var test = new AuthorizationErrorDocument().AsTestDocument();
        test.Context.Services.Register<IAuthorizationProvider>(new FaultedAuthorizationProvider(failure));

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());
        test.RunOnError();

        error.Policy.Should().Be(nameof(IInboundContext.GetAuthorizationContext));
        error.InnerException.Should().BeSameAs(failure);
        test.Context.Variables.Should().Contain("error-source", "get-authorization-context")
            .And.Contain("error-reason", "AuthorizationAcquisitionFailed")
            .And.Contain("error-message", failure.Message)
            .And.Contain("error-status-code", 500)
            .And.NotContainKey("continued");
        test.Context.Response.StatusCode.Should().Be(500);
        test.Context.LastError.HttpErrorCode.Should().Be(500);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void GetAuthorizationContext_RejectsNullAuthorization(bool ignoreError)
    {
        AssertInvalidAuthorization(null!, ignoreError);
    }

    [TestMethod]
    [DataRow(null, false)]
    [DataRow("", false)]
    [DataRow(" ", false)]
    [DataRow(null, true)]
    [DataRow("", true)]
    [DataRow(" ", true)]
    public void GetAuthorizationContext_RejectsEmptyAccessToken(string? token, bool ignoreError)
    {
        AssertInvalidAuthorization(CreateAuthorization(token!), ignoreError);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void GetAuthorizationContext_RejectsNullClaims(bool ignoreError)
    {
        AssertInvalidAuthorization(new Authorization("access-token", null!), ignoreError);
    }

    [TestMethod]
    public void GetAuthorizationContext_KeyedProviderTakesPrecedence()
    {
        var authorization = CreateAuthorization("keyed-token");
        var defaultProvider = new RecordingAuthorizationProvider(_ => throw new InvalidOperationException("wrong provider"));
        var keyedProvider = new RecordingAuthorizationProvider(_ => authorization);
        var test = CreateTest(CreateConfig());
        test.Context.Services.Register<IAuthorizationProvider>(defaultProvider)
            .Register<IAuthorizationProvider>("provider", keyedProvider);

        test.RunInbound();

        defaultProvider.Requests.Should().BeEmpty();
        keyedProvider.Requests.Should().ContainSingle();
        test.Context.Variables["authorization"].Should().BeSameAs(authorization);
    }

    [TestMethod]
    public void GetAuthorizationContext_UnrelatedKeyFallsBackToDefaultProvider()
    {
        var authorization = CreateAuthorization();
        var provider = new RecordingAuthorizationProvider(_ => authorization);
        var unrelatedProvider = new RecordingAuthorizationProvider(_ => throw new InvalidOperationException("wrong provider"));
        var test = CreateTest(CreateConfig());
        test.Context.Services.Register<IAuthorizationProvider>(provider)
            .Register<IAuthorizationProvider>("other-provider", unrelatedProvider);

        test.RunInbound();

        provider.Requests.Should().ContainSingle();
        unrelatedProvider.Requests.Should().BeEmpty();
        test.Context.Variables["authorization"].Should().BeSameAs(authorization);
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("outbound")]
    [DataRow("backend")]
    public void GetAuthorizationContext_CallbackOverridesAcquisition(string section)
    {
        var authorization = CreateAuthorization("callback-token");
        var test = CreateTest(CreateConfig(), section);
        var provider = new RecordingAuthorizationProvider(_ => throw new InvalidOperationException("must not acquire"));
        test.Context.Services.Register<IAuthorizationProvider>(provider);
        Setup(test, section).WithAuthorizationProviderHook(_ => throw new InvalidOperationException("must not run hook"));
        Setup(test, section).WithCallback((context, config) =>
            context.Variables[config.ContextVariableName] = authorization);

        Run(test, section);

        test.Context.Variables["authorization"].Should().BeSameAs(authorization);
        provider.Requests.Should().BeEmpty();
    }

    [TestMethod]
    public void GetAuthorizationContext_SelectsPredicateAndFirstMatchingHook()
    {
        var first = CreateAuthorization("first-token");
        var second = CreateAuthorization("second-token");
        var test = new MultipleAuthorizations().AsTestDocument();
        test.SetupInbound().GetAuthorizationContext((_, config) => config.AuthorizationId == "missing")
            .WithError("must not run");
        test.SetupInbound().GetAuthorizationContext((_, config) => config.AuthorizationId == "a")
            .ReturnsAuthorization(first);
        test.SetupInbound().GetAuthorizationContext((_, config) => config.AuthorizationId == "a")
            .WithError("later matching hook must not run");
        test.SetupInbound().GetAuthorizationContext((_, config) => config.AuthorizationId == "b")
            .ReturnsAuthorization(second);

        test.RunInbound();

        test.Context.Variables["a"].Should().BeSameAs(first);
        test.Context.Variables["b"].Should().BeSameAs(second);
    }

    [TestMethod]
    public void GetAuthorizationContext_CallbackPredicatesAreRespected()
    {
        var first = CreateAuthorization("first-token");
        var second = CreateAuthorization("second-token");
        var test = new MultipleAuthorizations().AsTestDocument();
        test.SetupInbound().GetAuthorizationContext((_, config) => config.AuthorizationId == "missing")
            .WithCallback((_, _) => throw new InvalidOperationException("must not run"));
        test.SetupInbound().GetAuthorizationContext((_, config) => config.AuthorizationId == "a")
            .WithCallback((context, config) => context.Variables[config.ContextVariableName] = first);
        test.SetupInbound().GetAuthorizationContext((_, config) => config.AuthorizationId == "b")
            .WithCallback((context, config) => context.Variables[config.ContextVariableName] = second);

        test.RunInbound();

        test.Context.Variables["a"].Should().BeSameAs(first);
        test.Context.Variables["b"].Should().BeSameAs(second);
    }

    [TestMethod]
    public void GetAuthorizationContext_HookOverridesRegisteredProvider()
    {
        var authorization = CreateAuthorization();
        var provider = new RecordingAuthorizationProvider(_ => throw new InvalidOperationException("must not acquire"));
        AuthorizationRequest? request = null;
        var test = CreateTest(CreateConfig() with { IdentityType = "jwt", Identity = "entra-jwt" });
        test.Context.Services.Register<IAuthorizationProvider>(provider);
        test.SetupInbound().GetAuthorizationContext().WithAuthorizationProviderHook(value =>
        {
            request = value;
            return authorization;
        });

        test.RunInbound();

        provider.Requests.Should().BeEmpty();
        request.Should().Be(new AuthorizationRequest("provider", "connection", "jwt", "entra-jwt"));
        test.Context.Variables["authorization"].Should().BeSameAs(authorization);
    }

    [TestMethod]
    public void GetAuthorizationContext_UnmatchedHookFallsBackToProvider()
    {
        var authorization = CreateAuthorization();
        var provider = new RecordingAuthorizationProvider(_ => authorization);
        var test = CreateTest(CreateConfig());
        test.Context.Services.Register<IAuthorizationProvider>(provider);
        test.SetupInbound().GetAuthorizationContext((_, config) => config.AuthorizationId == "other")
            .WithError("must not run");

        test.RunInbound();

        provider.Requests.Should().ContainSingle();
        test.Context.Variables["authorization"].Should().BeSameAs(authorization);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void GetAuthorizationContext_SetupErrorHonorsIgnoreError(bool ignoreError)
    {
        var test = CreateTest(CreateConfig() with { IgnoreError = ignoreError });
        test.SetupInbound().GetAuthorizationContext().WithError("provider failure");

        if (ignoreError)
        {
            test.RunInbound();
            test.Context.Variables.Should().ContainKey("authorization").WhoseValue.Should().BeNull();
            test.Context.Variables.Should().Contain("continued", true);
        }
        else
        {
            var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());
            error.InnerException.Should().BeOfType<HttpRequestException>().Which.Message.Should().Be("provider failure");
            test.Context.Response.StatusCode.Should().Be(500);
            test.Context.Variables.Should().NotContainKey("continued");
        }
    }

    [TestMethod]
    public void GetAuthorizationContext_CallbackFailuresAreNotAcquisitionFailures()
    {
        var test = CreateTest(CreateConfig() with { IgnoreError = true });
        var failure = new InvalidOperationException("callback failure");
        test.SetupInbound().GetAuthorizationContext().WithCallback((_, _) => throw failure);

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeSameAs(failure);
        test.Context.Variables.Should().NotContainKeys("authorization", "continued");
    }

    [TestMethod]
    [DataRow("inbound", false)]
    [DataRow("outbound", false)]
    [DataRow("backend", false)]
    [DataRow("inbound", true)]
    [DataRow("outbound", true)]
    [DataRow("backend", true)]
    public void GetAuthorizationContext_EvaluatesAllPolicyExpressions(string section, bool ignoreError)
    {
        var authorization = CreateAuthorization();
        var provider = new RecordingAuthorizationProvider(_ => ignoreError
            ? throw new HttpRequestException("expression-selected failure")
            : authorization);
        var test = new AuthorizationDocument(context => new GetAuthorizationContextConfig
        {
            ProviderId = (string)context.Variables["provider-id"],
            AuthorizationId = (string)context.Variables["authorization-id"],
            ContextVariableName = (string)context.Variables["variable-name"],
            IdentityType = (string)context.Variables["identity-type"],
            Identity = (string)context.Variables["identity-token"],
            IgnoreError = (bool)context.Variables["ignore-error"]
        }, section).AsTestDocument();
        test.Context.Services.Register<IAuthorizationProvider>(provider);
        test.Context.Variables["provider-id"] = "expression-provider";
        test.Context.Variables["authorization-id"] = "expression-connection";
        test.Context.Variables["variable-name"] = "expression-authorization";
        test.Context.Variables["identity-type"] = "jwt";
        test.Context.Variables["identity-token"] = "expression-jwt";
        test.Context.Variables["ignore-error"] = ignoreError;

        Run(test, section);

        provider.Requests.Should().ContainSingle().Which.Should()
            .Be(new AuthorizationRequest("expression-provider", "expression-connection", "jwt", "expression-jwt"));
        test.Context.Variables["expression-authorization"].Should().BeSameAs(ignoreError ? null : authorization);
        test.Context.Variables.Should().Contain("continued", true);
    }

    private static SecurityTokenException CreateTokenValidationFailure(string exceptionType) => exceptionType switch
    {
        nameof(SecurityTokenException) => new SecurityTokenException("identity validation failed"),
        nameof(SecurityTokenInvalidAudienceException) => new SecurityTokenInvalidAudienceException("identity audience is invalid"),
        nameof(SecurityTokenExpiredException) => new SecurityTokenExpiredException("identity token has expired"),
        _ => throw new ArgumentOutOfRangeException(nameof(exceptionType))
    };

    private static void AssertTokenValidationFailure(
        TestDocument test, SecurityTokenException failure, bool ignoreError)
    {
        var staleAuthorization = CreateAuthorization("stale-token");
        test.Context.Variables["authorization"] = staleAuthorization;
        test.Context.Request.Headers["Authorization"] = ["existing-header"];
        test.Context.Response.StatusCode = 202;
        test.Context.Response.StatusReason = "Accepted";

        if (ignoreError)
        {
            test.RunInbound();

            test.Context.Variables.Should().ContainKey("authorization").WhoseValue.Should().BeNull();
            test.Context.Variables.Should().Contain("continued", true);
            test.Context.Response.StatusCode.Should().Be(202);
            test.Context.Response.StatusReason.Should().Be("Accepted");
        }
        else
        {
            var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

            error.Policy.Should().Be(nameof(IInboundContext.GetAuthorizationContext));
            error.Section.Should().Be(nameof(IInboundContext));
            error.InnerException.Should().BeSameAs(failure);
            test.Context.Response.StatusCode.Should().Be(500);
            test.Context.Response.StatusReason.Should().Be("Internal Server Error");
            test.Context.LastError.Source.Should().Be("get-authorization-context");
            test.Context.LastError.Reason.Should().Be("AuthorizationAcquisitionFailed");
            test.Context.LastError.Message.Should().Be(failure.Message);
            test.Context.LastError.HttpErrorCode.Should().Be(500);
            test.Context.Variables["authorization"].Should().BeSameAs(staleAuthorization);
            test.Context.Variables.Should().NotContainKey("continued");
        }

        test.Context.Request.Headers["Authorization"].Should().Equal("existing-header");
    }

    private static void AssertInvalidAuthorization(Authorization authorization, bool ignoreError)
    {
        var test = CreateTest(CreateConfig() with { IgnoreError = ignoreError });
        test.Context.Services.Register<IAuthorizationProvider>(new RecordingAuthorizationProvider(_ => authorization));

        if (ignoreError)
        {
            test.RunInbound();
            test.Context.Variables.Should().ContainKey("authorization").WhoseValue.Should().BeNull();
            test.Context.Variables.Should().Contain("continued", true);
        }
        else
        {
            var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());
            error.InnerException.Should().BeOfType<InvalidOperationException>();
            test.Context.Response.StatusCode.Should().Be(500);
            test.Context.Variables.Should().NotContainKeys("authorization", "continued");
        }
    }

    private static GetAuthorizationContextConfig CreateConfig() => new()
    {
        ProviderId = "provider", AuthorizationId = "connection", ContextVariableName = "authorization"
    };

    private static Authorization CreateAuthorization(string token = "access-token") =>
        new(token, new Dictionary<string, object> { ["scope"] = "api.read" });

    private static TestDocument CreateTest(GetAuthorizationContextConfig config, string section = "inbound") =>
        new AuthorizationDocument(_ => config, section).AsTestDocument();

    private static MockGetAuthorizationContextProvider.Setup Setup(TestDocument test, string section) => section switch
    {
        "inbound" => test.SetupInbound().GetAuthorizationContext(),
        "outbound" => test.SetupOutbound().GetAuthorizationContext(),
        "backend" => test.SetupBackend().GetAuthorizationContext(),
        _ => throw new ArgumentOutOfRangeException(nameof(section))
    };

    private static void Run(TestDocument test, string section)
    {
        switch (section)
        {
            case "inbound": test.RunInbound(); break;
            case "outbound": test.RunOutbound(); break;
            case "backend": test.RunBackend(); break;
            default: throw new ArgumentOutOfRangeException(nameof(section));
        }
    }

    private sealed class AuthorizationDocument(
        Func<IExpressionContext, GetAuthorizationContextConfig> configure,
        string section) : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            if (section != "inbound") { return; }
            context.GetAuthorizationContext(configure(context.ExpressionContext));
            context.SetVariable("continued", true);
        }

        public void Outbound(IOutboundContext context)
        {
            if (section != "outbound") { return; }
            context.GetAuthorizationContext(configure(context.ExpressionContext));
            context.SetVariable("continued", true);
        }

        public void Backend(IBackendContext context)
        {
            if (section != "backend") { return; }
            context.GetAuthorizationContext(configure(context.ExpressionContext));
            context.SetVariable("continued", true);
        }
    }

    private sealed class AuthorizationErrorDocument : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.GetAuthorizationContext(CreateConfig() with { IgnoreError = false });
            context.SetVariable("continued", true);
        }

        public void OnError(IOnErrorContext context)
        {
            var error = context.ExpressionContext.LastError;
            context.SetVariable("error-source", error.Source);
            context.SetVariable("error-reason", error.Reason);
            context.SetVariable("error-message", error.Message);
            context.SetVariable("error-status-code", context.ExpressionContext.Response.StatusCode);
        }
    }

    private sealed class MultipleAuthorizations : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.GetAuthorizationContext(CreateConfig() with { AuthorizationId = "a", ContextVariableName = "a" });
            context.GetAuthorizationContext(CreateConfig() with { AuthorizationId = "b", ContextVariableName = "b" });
        }
    }

    private sealed class RecordingAuthorizationProvider(Func<AuthorizationRequest, Authorization> acquire)
        : IAuthorizationProvider
    {
        public List<AuthorizationRequest> Requests { get; } = [];

        public Task<Authorization> GetAuthorizationAsync(
            AuthorizationRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(acquire(request));
        }
    }

    private sealed class FaultedAuthorizationProvider(Exception error) : IAuthorizationProvider
    {
        public Task<Authorization> GetAuthorizationAsync(
            AuthorizationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromException<Authorization>(error);
    }
}
