// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IdentityModel.Tokens.Jwt;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.IdentityModel.Tokens;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class AuthenticationManagedIdentityHandlerTests
{
    class SimpleAmi : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig()
            {
                Resource = "https://management.azure.com/"
            });
        }
    }

    class AmiClientId : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig()
            {
                Resource = "https://management.azure.com/", ClientId = "some-client-id"
            });
        }
    }

    class AmiOutputVariable : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig()
            {
                Resource = "https://management.azure.com/", OutputTokenVariableName = "testVariable"
            });
        }
    }

    class AmiIgnoreError : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig()
            {
                Resource = "https://management.azure.com/", IgnoreError = true
            });
        }
    }

    class AmiIgnoreErrorWithVariable : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig()
            {
                Resource = "https://management.azure.com/",
                IgnoreError = true,
                OutputTokenVariableName = "testVariable"
            });
        }
    }


    class PredicateAmi : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig()
            {
                Resource = "a", OutputTokenVariableName = "a"
            });
            context.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig()
            {
                Resource = "b", OutputTokenVariableName = "b"
            });
        }
    }

    class ConfiguredAmi(ManagedIdentityAuthenticationConfig config, bool outbound) : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            if (outbound) { return; }
            context.AuthenticationManagedIdentity(config);
            context.SetVariable("continued", true);
        }

        public void Outbound(IOutboundContext context)
        {
            if (!outbound) { return; }
            context.AuthenticationManagedIdentity(config);
            context.SetVariable("continued", true);
        }
    }

    [TestMethod]
    public void AuthenticationManagedIdentity_HandleSimpleConfig()
    {
        // Arrange
        var test = new SimpleAmi().AsTestDocument();

        // Act
        test.RunInbound();

        // Assert
        var authHeader = test.Context.Request.Headers.GetValueOrDefault("Authorization");
        authHeader.Should().NotBeNullOrEmpty().And.StartWithEquivalentOf("Bearer ");
        var token = new JwtSecurityTokenHandler().ReadJwtToken(authHeader!["Bearer ".Length..]);
        token.Issuer.Should().Be("system-assigned");
        token.Audiences.Should().ContainSingle().Which.Should().Be("https://management.azure.com/");
        token.SignatureAlgorithm.Should().Be(SecurityAlgorithms.HmacSha256);
    }

    [TestMethod]
    public void AuthenticationManagedIdentity_HandleClientId()
    {
        // Arrange
        var test = new AmiClientId().AsTestDocument();

        // Act
        test.RunInbound();

        // Assert
        var authHeader = test.Context.Request.Headers.GetValueOrDefault("Authorization");
        authHeader.Should().NotBeNullOrEmpty().And.StartWithEquivalentOf("Bearer ");
        var token = new JwtSecurityTokenHandler().ReadJwtToken(authHeader!["Bearer ".Length..]);
        token.Issuer.Should().Be("some-client-id");
        token.Audiences.Should().ContainSingle().Which.Should().Be("https://management.azure.com/");
        token.SignatureAlgorithm.Should().Be(SecurityAlgorithms.HmacSha256);
    }


    [TestMethod]
    public void AuthenticationManagedIdentity_HandleOutputVariable()
    {
        // Arrange
        var test = new AmiOutputVariable().AsTestDocument();

        // Act
        test.RunInbound();

        // Assert
        test.Context.Request.Headers.Should().NotContainKey("Authorization");
        var value = test.Context.Variables.GetValueOrDefault("testVariable");
        value.Should().NotBeNull().And.BeOfType<string>().Which.Should().NotBeNullOrEmpty();
        var token = new JwtSecurityTokenHandler().ReadJwtToken(value as string);
        token.Issuer.Should().Be("system-assigned");
        token.Audiences.Should().ContainSingle().Which.Should().Be("https://management.azure.com/");
        token.SignatureAlgorithm.Should().Be(SecurityAlgorithms.HmacSha256);
    }

    [TestMethod]
    public void AuthenticationManagedIdentity_HandleCallback()
    {
        // Arrange
        var test = new SimpleAmi().AsTestDocument();
        ManagedIdentityAuthenticationConfig? config = null;
        test.SetupInbound().AuthenticationManagedIdentity().WithCallback((context, cfg) =>
        {
            context.Variables["test"] = "test";
            config = cfg;
        });

        // Act
        test.RunInbound();

        // Assert
        test.Context.Request.Headers.Should().NotContainKey("Authorization");
        var variable = test.Context.Variables.Should().ContainSingle().Which;
        variable.Key.Should().Be("test");
        variable.Value.Should().BeOfType<string>().And.Be("test");
        config.Should().NotBeNull();
        config!.Resource.Should().Be("https://management.azure.com/");
    }

    [TestMethod]
    public void AuthenticationManagedIdentity_HandleCallback_WithInvocationPredicate()
    {
        // Arrange
        var test = new PredicateAmi().AsTestDocument();
        test.SetupInbound()
            .AuthenticationManagedIdentity((_, config) => config.Resource == "c")
            .WithCallback((context, _) => context.Variables["c"] = "token-c");
        test.SetupInbound()
            .AuthenticationManagedIdentity((_, config) => config.Resource == "b")
            .WithCallback((context, _) => context.Variables["b"] = "token-b");
        test.SetupInbound()
            .AuthenticationManagedIdentity((_, config) => config.Resource == "a")
            .WithCallback((context, _) => context.Variables["a"] = "token-a");

        // Act
        test.RunInbound();

        // Assert
        test.Context.Request.Headers.Should().NotContainKey("Authorization");
        test.Context.Variables.Should().ContainKeys("a", "b");
    }

    [TestMethod]
    public void AuthenticationManagedIdentity_HandleTokenProviderHook()
    {
        // Arrange
        var test = new AmiClientId().AsTestDocument();
        test.SetupInbound()
            .AuthenticationManagedIdentity()
            .WithTokenProviderHook((resource, clientId) => $"{resource}{clientId}/token");

        // Act
        test.RunInbound();

        // Assert
        test.Context.Request.Headers.Should().ContainKey("Authorization")
            .WhoseValue.Should().ContainSingle()
            .Which.Should().Be("Bearer https://management.azure.com/some-client-id/token");
    }

    [TestMethod]
    public void AuthenticationManagedIdentity_HandleTokenProviderHook_WithInvocationPredicate()
    {
        // Arrange
        var test = new PredicateAmi().AsTestDocument();
        test.SetupInbound()
            .AuthenticationManagedIdentity((_, config) => config.Resource == "c")
            .WithTokenProviderHook((_, _) => "token-c");
        test.SetupInbound()
            .AuthenticationManagedIdentity((_, config) => config.Resource == "b")
            .WithTokenProviderHook((_, _) => "token-b");
        test.SetupInbound()
            .AuthenticationManagedIdentity((_, config) => config.Resource == "a")
            .WithTokenProviderHook((_, _) => "token-a");

        // Act
        test.RunInbound();

        // Assert
        test.Context.Request.Headers.Should().NotContainKey("Authorization");
        test.Context.Variables.Should().Contain("a", "token-a").And.Contain("b", "token-b");
    }

    [TestMethod]
    public void AuthenticationManagedIdentity_HandleReturnToken()
    {
        // Arrange
        var test = new SimpleAmi().AsTestDocument();
        test.SetupInbound().AuthenticationManagedIdentity().ReturnsToken("token");

        // Act
        test.RunInbound();

        // Assert
        test.Context.Request.Headers.Should().ContainKey("Authorization")
            .WhoseValue.Should().ContainSingle()
            .Which.Should().Be("Bearer token");
    }

    [TestMethod]
    public void AuthenticationManagedIdentity_HandleReturnToken_WithInvocationPredicate()
    {
        // Arrange
        var test = new PredicateAmi().AsTestDocument();
        test.SetupInbound()
            .AuthenticationManagedIdentity((_, config) => config.Resource == "c")
            .ReturnsToken("token-c");
        test.SetupInbound()
            .AuthenticationManagedIdentity((_, config) => config.Resource == "b")
            .ReturnsToken("token-b");
        test.SetupInbound()
            .AuthenticationManagedIdentity((_, config) => config.Resource == "a")
            .ReturnsToken("token-a");

        // Act
        test.RunInbound();

        // Assert
        test.Context.Request.Headers.Should().NotContainKey("Authorization");
        test.Context.Variables.Should().Contain("a", "token-a").And.Contain("b", "token-b");
    }

    [TestMethod]
    public void AuthenticationManagedIdentity_HandleWithError()
    {
        // Arrange
        var test = new SimpleAmi().AsTestDocument();
        test.SetupInbound().AuthenticationManagedIdentity().WithError("InternalServerError");

        // Act
        var ex = Assert.ThrowsException<PolicyException>(() => test.RunInbound());

        // Assert
        ex.Policy.Should().Be("AuthenticationManagedIdentity");
        ex.Section.Should().Be("IInboundContext");
        ex.Message.Should().Be("InternalServerError");
        var data = ex.PolicyArgs.Should().NotBeNull().And.ContainSingle()
            .Which.Should().BeOfType<ManagedIdentityAuthenticationConfig>().Which;
        data.Resource.Should().Be("https://management.azure.com/");
        ex.InnerException.Should().NotBeNull().And.BeOfType<HttpRequestException>().Which.Message.Should()
            .Be("InternalServerError");
        test.Context.Request.Headers.Should().NotContainKey("Authorization");
        test.Context.Variables.Should().BeEmpty();
    }

    [TestMethod]
    public void AuthenticationManagedIdentity_HandleWithError_WithInvocationPredicate()
    {
        // Arrange
        var test = new PredicateAmi().AsTestDocument();
        test.SetupInbound()
            .AuthenticationManagedIdentity((_, config) => config.Resource == "b")
            .WithError("InternalServerError");
        test.SetupInbound()
            .AuthenticationManagedIdentity((_, config) => config.Resource == "a")
            .ReturnsToken("token-a");

        // Act
        var ex = Assert.ThrowsException<PolicyException>(() => test.RunInbound());

        // Assert
        ex.Policy.Should().Be("AuthenticationManagedIdentity");
        ex.Section.Should().Be("IInboundContext");
        ex.Message.Should().Be("InternalServerError");
        var config = ex.PolicyArgs.Should().NotBeNull().And.ContainSingle()
            .Which.Should().BeOfType<ManagedIdentityAuthenticationConfig>().Which;
        config.Resource.Should().Be("b");
        ex.InnerException.Should().NotBeNull().And.BeOfType<HttpRequestException>().Which.Message.Should()
            .Be("InternalServerError");
        test.Context.Request.Headers.Should().NotContainKey("Authorization");
        var variable = test.Context.Variables.Should().ContainSingle().Which;
        variable.Key.Should().Be("a");
        variable.Value.Should().Be("token-a");
    }

    [TestMethod]
    public void AuthenticationManagedIdentity_IgnoreError_InAuthHeader()
    {
        // Arrange
        var test = new AmiIgnoreError().AsTestDocument();
        test.SetupInbound().AuthenticationManagedIdentity().WithError("InternalServerError");

        // Act
        test.RunInbound();

        // Assert
        var authHeader = test.Context.Request.Headers.GetValueOrDefault("Authorization");
        authHeader.Should().NotBeNullOrEmpty().And.StartWithEquivalentOf("Bearer ");
        authHeader!["Bearer ".Length..].Should().BeEmpty();
    }

    [TestMethod]
    public void AuthenticationManagedIdentity_IgnoreError_InVariable()
    {
        // Arrange
        var test = new AmiIgnoreErrorWithVariable().AsTestDocument();
        test.SetupInbound().AuthenticationManagedIdentity().WithError("InternalServerError");

        // Act
        test.RunInbound();

        // Assert
        test.Context.Request.Headers.Should().NotContainKey("Authorization");
        test.Context.Variables.Should().ContainKey("testVariable")
            .WhoseValue.Should().BeOfType<string>()
            .Which.Should().BeEmpty();
    }

    [TestMethod]
    public void AuthenticationManagedIdentity_OutboundUsesContextProvider()
    {
        var test = CreateConfiguredAmi(outbound: true);
        test.Context.ManagedIdentityTokenProvider = (resource, clientId) =>
        {
            resource.Should().Be("https://management.azure.com/");
            clientId.Should().Be("client-id");
            return "outbound-token";
        };

        test.RunOutbound();

        test.Context.Request.Headers["Authorization"].Should().Equal("Bearer outbound-token");
        test.Context.Response.Headers.Should().NotContainKey("Authorization");
        test.Context.Variables.Should().Contain("continued", true);
    }

    [TestMethod]
    public void AuthenticationManagedIdentity_OutboundSupportsOutputVariableSetup()
    {
        var test = CreateConfiguredAmi(outbound: true, outputVariable: "token");
        test.Context.Request.Headers["Authorization"] = ["existing-header"];
        test.SetupOutbound().AuthenticationManagedIdentity().ReturnsToken("outbound-token");

        test.RunOutbound();

        test.Context.Variables.Should().Contain("token", "outbound-token").And.Contain("continued", true);
        test.Context.Request.Headers["Authorization"].Should().Equal("existing-header");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AuthenticationManagedIdentity_CallbackOverridesAllProviders(bool outbound)
    {
        var test = CreateConfiguredAmi(outbound, outputVariable: "token");
        test.Context.ManagedIdentityTokenProvider = (_, _) => throw new InvalidOperationException("must not run");
        SetupAmi(test, outbound).WithTokenProviderHook((_, _) => throw new InvalidOperationException("must not run"));
        SetupAmi(test, outbound).WithCallback((context, config) =>
        {
            config.ClientId.Should().Be("client-id");
            context.Variables["token"] = "callback-token";
        });

        RunAmi(test, outbound);

        test.Context.Variables.Should().Contain("token", "callback-token");
        test.Context.Request.Headers.Should().NotContainKey("Authorization");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AuthenticationManagedIdentity_HookTakesPrecedenceOverContextProvider(bool outbound)
    {
        var test = CreateConfiguredAmi(outbound, outputVariable: "token");
        var contextProviderCalls = 0;
        test.Context.ManagedIdentityTokenProvider = (_, _) =>
        {
            contextProviderCalls++;
            throw new InvalidOperationException("must not run");
        };
        SetupAmi(test, outbound).WithTokenProviderHook((resource, clientId) =>
        {
            resource.Should().Be("https://management.azure.com/");
            clientId.Should().Be("client-id");
            return "hook-token";
        });

        RunAmi(test, outbound);

        contextProviderCalls.Should().Be(0);
        test.Context.Variables.Should().Contain("token", "hook-token");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AuthenticationManagedIdentity_UnmatchedHookFallsBackToContextProvider(bool outbound)
    {
        var test = CreateConfiguredAmi(outbound, outputVariable: "token");
        var calls = 0;
        test.Context.ManagedIdentityTokenProvider = (_, _) =>
        {
            calls++;
            return "context-token";
        };
        if (outbound)
        {
            test.SetupOutbound().AuthenticationManagedIdentity((_, config) => config.ClientId == "other-client")
                .WithError("must not run");
        }
        else
        {
            test.SetupInbound().AuthenticationManagedIdentity((_, config) => config.ClientId == "other-client")
                .WithError("must not run");
        }

        RunAmi(test, outbound);

        calls.Should().Be(1);
        test.Context.Variables.Should().Contain("token", "context-token");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AuthenticationManagedIdentity_FirstMatchingProviderWins(bool outbound)
    {
        var test = CreateConfiguredAmi(outbound, outputVariable: "token");
        SetupAmi(test, outbound).ReturnsToken("first-token");
        SetupAmi(test, outbound).WithError("later matching provider must not run");

        RunAmi(test, outbound);

        test.Context.Variables.Should().Contain("token", "first-token");
    }

    [TestMethod]
    [DataRow(false, nameof(IInboundContext))]
    [DataRow(true, nameof(IOutboundContext))]
    public void AuthenticationManagedIdentity_ContextProviderFailureIsReported(bool outbound, string section)
    {
        var test = CreateConfiguredAmi(outbound, outputVariable: "token");
        var failure = new HttpRequestException("identity provider unavailable");
        test.Context.ManagedIdentityTokenProvider = (_, _) => throw failure;
        test.Context.Request.Headers["Authorization"] = ["existing-header"];

        var error = Assert.ThrowsExactly<PolicyException>(() => RunAmi(test, outbound));

        error.Policy.Should().Be(nameof(IInboundContext.AuthenticationManagedIdentity));
        error.Section.Should().Be(section);
        error.InnerException.Should().BeSameAs(failure);
        test.Context.Request.Headers["Authorization"].Should().Equal("existing-header");
        test.Context.Variables.Should().NotContainKeys("token", "continued");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AuthenticationManagedIdentity_IgnoreContextProviderFailureContinues(bool outbound)
    {
        var test = CreateConfiguredAmi(outbound, ignoreError: true, outputVariable: "token");
        test.Context.ManagedIdentityTokenProvider = (_, _) => throw new HttpRequestException("identity provider unavailable");
        test.Context.Request.Headers["Authorization"] = ["existing-header"];

        RunAmi(test, outbound);

        test.Context.Variables.Should().Contain("token", "").And.Contain("continued", true);
        test.Context.Request.Headers["Authorization"].Should().Equal("existing-header");
    }

    [TestMethod]
    [DataRow(false, null)]
    [DataRow(false, "")]
    [DataRow(false, " ")]
    [DataRow(true, null)]
    [DataRow(true, "")]
    [DataRow(true, " ")]
    public void AuthenticationManagedIdentity_RejectsEmptyProviderToken(bool outbound, string? token)
    {
        var test = CreateConfiguredAmi(outbound, outputVariable: "token");
        test.Context.ManagedIdentityTokenProvider = (_, _) => token!;

        var error = Assert.ThrowsExactly<PolicyException>(() => RunAmi(test, outbound));

        error.InnerException.Should().BeOfType<InvalidOperationException>();
        test.Context.Request.Headers.Should().NotContainKey("Authorization");
        test.Context.Variables.Should().NotContainKeys("token", "continued");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AuthenticationManagedIdentity_IgnoreEmptyProviderTokenContinues(bool outbound)
    {
        var test = CreateConfiguredAmi(outbound, ignoreError: true, outputVariable: "token");
        SetupAmi(test, outbound).ReturnsToken(" ");

        RunAmi(test, outbound);

        test.Context.Variables.Should().Contain("token", "").And.Contain("continued", true);
    }

    [TestMethod]
    public void AuthenticationManagedIdentity_OutboundSetupFailureIsReported()
    {
        var test = CreateConfiguredAmi(outbound: true, outputVariable: "token");
        test.SetupOutbound().AuthenticationManagedIdentity().WithError("outbound identity failure");

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunOutbound());

        error.Section.Should().Be(nameof(IOutboundContext));
        error.InnerException.Should().BeOfType<HttpRequestException>()
            .Which.Message.Should().Be("outbound identity failure");
        test.Context.Variables.Should().NotContainKeys("token", "continued");
    }

    [TestMethod]
    public void AuthenticationManagedIdentity_OutboundPreservesDefaultLocalTokenBehavior()
    {
        var test = CreateConfiguredAmi(outbound: true, outputVariable: "token");

        test.RunOutbound();

        var token = new JwtSecurityTokenHandler().ReadJwtToken((string)test.Context.Variables["token"]);
        token.Issuer.Should().Be("client-id");
        token.Audiences.Should().ContainSingle().Which.Should().Be("https://management.azure.com/");
    }

    private static TestDocument CreateConfiguredAmi(
        bool outbound, bool ignoreError = false, string? outputVariable = null) =>
        new ConfiguredAmi(new ManagedIdentityAuthenticationConfig
        {
            Resource = "https://management.azure.com/",
            ClientId = "client-id",
            OutputTokenVariableName = outputVariable,
            IgnoreError = ignoreError
        }, outbound).AsTestDocument();

    private static MockAuthenticationManagedIdentityProvider.Setup SetupAmi(TestDocument test, bool outbound) =>
        outbound
            ? test.SetupOutbound().AuthenticationManagedIdentity()
            : test.SetupInbound().AuthenticationManagedIdentity();

    private static void RunAmi(TestDocument test, bool outbound)
    {
        if (outbound) { test.RunOutbound(); }
        else { test.RunInbound(); }
    }
}