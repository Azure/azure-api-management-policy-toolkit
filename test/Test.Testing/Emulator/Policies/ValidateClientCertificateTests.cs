// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class ValidateClientCertificateTests
{
    private static ClientCertificateValidationResult ValidEvidence => new()
    {
        IsTrusted = true,
        RevocationStatus = ClientCertificateRevocationStatus.Good
    };

    private static TestDocument CreateTest(ValidateClientCertificateConfig? config = null,
        X509Certificate2? certificate = null, ClientCertificateValidationResult? evidence = null)
    {
        var test = new CertificateDocument(config ?? new ValidateClientCertificateConfig()).AsTestDocument();
        IdentityTestTokens.SetClock(test.Context);
        test.Context.Request.Certificate = certificate;
        if (evidence is not null)
        {
            test.Context.Services.Register<IClientCertificateValidator>(new RecordingCertificateValidator(_ => evidence));
        }

        return test;
    }

    [TestMethod]
    public void AcceptsValidCertificateWithExplicitTrustAndRevocationEvidence()
    {
        using var certificate = CreateCertificate();
        var test = CreateTest(certificate: certificate, evidence: ValidEvidence);

        test.RunInbound();

        test.Context.Request.Certificate.Should().BeSameAs(certificate);
        test.Context.Variables["continued"].Should().Be(true);
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    public void RejectsMissingCertificateBeforeInvokingTheValidator()
    {
        var validator = new RecordingCertificateValidator(_ => ValidEvidence);
        var test = CreateTest();
        test.Context.Services.Register<IClientCertificateValidator>(validator);

        AssertRejected(test, "ClientCertificateNotPresent");

        validator.Requests.Should().BeEmpty();
    }

    [TestMethod]
    public void CertificatePresenceDoesNotEstablishTrust()
    {
        using var certificate = CreateCertificate();
        var test = CreateTest(certificate: certificate);
        test.SetupCertificateStore().WithCertificateById("uploaded-client", certificate);

        AssertRejected(test, "ClientCertificateTrustUnavailable");
        test.Context.LastError.Message.Should().Contain(nameof(IClientCertificateValidator));
    }

    [TestMethod]
    public void DisabledTrustAndRevocationChecksAreHonoredExplicitly()
    {
        using var certificate = CreateCertificate();
        var test = CreateTest(new ValidateClientCertificateConfig
        {
            ValidateTrust = false, ValidateRevocation = false,
            Identities = [new CertificateIdentity { Thumbprint = certificate.Thumbprint }]
        }, certificate);

        test.RunInbound();

        test.Context.Variables["continued"].Should().Be(true);
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("untrusted")]
    [DataRow("revoked")]
    [DataRow("unknown")]
    public void DefaultChecksRejectInsufficientEvidence(string scenario)
    {
        using var certificate = CreateCertificate();
        var evidence = scenario switch
        {
            "untrusted" => ValidEvidence with { IsTrusted = false },
            "revoked" => ValidEvidence with { RevocationStatus = ClientCertificateRevocationStatus.Revoked },
            _ => ValidEvidence with { RevocationStatus = ClientCertificateRevocationStatus.Unknown }
        };
        var test = CreateTest(certificate: certificate, evidence: evidence);

        AssertRejected(test);
    }

    [TestMethod]
    public void ADefaultValidatorResultIsNotSuccessfulValidation()
    {
        using var certificate = CreateCertificate();
        var test = CreateTest(certificate: certificate, evidence: new ClientCertificateValidationResult());

        AssertRejected(test, "ClientCertificateUntrusted");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CanDisableRevocationWithoutDisablingTrust(bool trusted)
    {
        using var certificate = CreateCertificate();
        var test = CreateTest(new ValidateClientCertificateConfig { ValidateRevocation = false }, certificate,
            ValidEvidence with { IsTrusted = trusted, RevocationStatus = ClientCertificateRevocationStatus.Unknown });

        if (trusted)
        {
            test.RunInbound();
            test.Context.Variables["continued"].Should().Be(true);
        }
        else
        {
            AssertRejected(test, "ClientCertificateUntrusted");
        }
    }

    [TestMethod]
    public void CanDisableTrustWithoutDisablingRevocation()
    {
        using var certificate = CreateCertificate();
        var test = CreateTest(new ValidateClientCertificateConfig { ValidateTrust = false }, certificate,
            ValidEvidence with { IsTrusted = false, RevocationStatus = ClientCertificateRevocationStatus.Revoked });

        AssertRejected(test, "ClientCertificateRevoked");
    }

    [TestMethod]
    [DataRow("future", false)]
    [DataRow("future", true)]
    [DataRow("expired", false)]
    [DataRow("expired", true)]
    public void EnforcesCertificateValidityUnlessTheSpecificCheckIsDisabled(string scenario, bool disabled)
    {
        using var certificate = CreateCertificate(
            notBefore: scenario == "future" ? IdentityTestTokens.Now.AddSeconds(1) : IdentityTestTokens.Now.AddYears(-1),
            notAfter: scenario == "expired" ? IdentityTestTokens.Now.AddSeconds(-1) : IdentityTestTokens.Now.AddYears(1));
        var config = new ValidateClientCertificateConfig
        {
            ValidateNotBefore = scenario == "future" && disabled ? false : null,
            ValidateNotAfter = scenario == "expired" && disabled ? false : null
        };
        var test = CreateTest(config, certificate, ValidEvidence);

        if (disabled)
        {
            test.RunInbound();
            test.Context.Variables["continued"].Should().Be(true);
        }
        else
        {
            AssertRejected(test, scenario == "future" ? "ClientCertificateNotYetValid" : "ClientCertificateExpired");
        }
    }

    [TestMethod]
    public void CertificateValidationUsesElapsedRequestTime()
    {
        using var certificate = CreateCertificate(notAfter: IdentityTestTokens.Now.AddSeconds(10));
        var test = CreateTest(certificate: certificate, evidence: ValidEvidence);
        test.Context.Elapsed = TimeSpan.FromSeconds(11);

        AssertRejected(test, "ClientCertificateExpired");
    }

    [TestMethod]
    public void PassesResolvedFlagsAndClockToTheTrustValidator()
    {
        using var certificate = CreateCertificate();
        var validator = new RecordingCertificateValidator(_ => ValidEvidence);
        var test = CreateTest(new ValidateClientCertificateConfig
        {
            ValidateTrust = false,
            ValidateRevocation = true,
            ValidateNotBefore = false,
            ValidateNotAfter = true
        }, certificate);
        test.Context.Elapsed = TimeSpan.FromSeconds(2);
        test.Context.Services.Register<IClientCertificateValidator>(validator);

        test.RunInbound();

        validator.Requests.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new ClientCertificateValidationRequest(certificate, IdentityTestTokens.Now.AddSeconds(2), false, true, false, true));
    }

    [TestMethod]
    [DataRow("thumbprint", true)]
    [DataRow("thumbprint", false)]
    [DataRow("serial-number", true)]
    [DataRow("serial-number", false)]
    [DataRow("common-name", true)]
    [DataRow("common-name", false)]
    [DataRow("subject", true)]
    [DataRow("subject", false)]
    [DataRow("dns-name", true)]
    [DataRow("dns-name", false)]
    [DataRow("issuer-subject", true)]
    [DataRow("issuer-subject", false)]
    public void EnforcesEachConfiguredCertificateIdentityField(string field, bool matching)
    {
        using var certificate = CreateCertificate();
        var identity = field switch
        {
            "thumbprint" => new CertificateIdentity { Thumbprint = matching ? certificate.Thumbprint.ToLowerInvariant() : "00" },
            "serial-number" => new CertificateIdentity { SerialNumber = matching ? certificate.SerialNumber : "00" },
            "common-name" => new CertificateIdentity { CommonName = matching ? "client" : "other" },
            "subject" => new CertificateIdentity { Subject = matching ? certificate.Subject : "CN=other" },
            "dns-name" => new CertificateIdentity { DnsName = matching ? "client.example" : "other.example" },
            _ => new CertificateIdentity { IssuerSubject = matching ? certificate.Issuer : "CN=other" }
        };
        var test = CreateTest(new ValidateClientCertificateConfig { Identities = [identity] }, certificate, ValidEvidence);

        if (matching)
        {
            test.RunInbound();
            test.Context.Variables["continued"].Should().Be(true);
        }
        else
        {
            AssertRejected(test, "ClientCertificateIdentityNotAllowed");
        }
    }

    [TestMethod]
    public void AllFieldsWithinOneIdentityMustMatch()
    {
        using var certificate = CreateCertificate();
        var test = CreateTest(new ValidateClientCertificateConfig
        {
            Identities = [new CertificateIdentity { Thumbprint = certificate.Thumbprint, CommonName = "other" }]
        }, certificate, ValidEvidence);

        AssertRejected(test, "ClientCertificateIdentityNotAllowed");
    }

    [TestMethod]
    public void AnyCompleteMatchingIdentityIsSufficient()
    {
        using var certificate = CreateCertificate();
        var test = CreateTest(new ValidateClientCertificateConfig
        {
            Identities =
            [
                new CertificateIdentity { CommonName = "other" },
                new CertificateIdentity { Thumbprint = certificate.Thumbprint, CommonName = "client", DnsName = "client.example" }
            ]
        }, certificate, ValidEvidence);

        test.RunInbound();

        test.Context.Variables["continued"].Should().Be(true);
    }

    [TestMethod]
    public void DnsIdentityMustComeFromSubjectAlternativeNameRatherThanCommonName()
    {
        using var certificate = CreateCertificate(subject: "CN=client.example", includeDns: false);
        var test = CreateTest(new ValidateClientCertificateConfig
        {
            Identities = [new CertificateIdentity { DnsName = "client.example" }]
        }, certificate, ValidEvidence);

        AssertRejected(test, "ClientCertificateIdentityNotAllowed");
    }

    [TestMethod]
    public void CommonNameDoesNotFallBackToAnotherSubjectAttribute()
    {
        using var certificate = CreateCertificate(subject: "O=client");
        var test = CreateTest(new ValidateClientCertificateConfig
        {
            Identities = [new CertificateIdentity { CommonName = "client" }]
        }, certificate, ValidEvidence);

        AssertRejected(test, "ClientCertificateIdentityNotAllowed");
    }

    [TestMethod]
    [DataRow("thumbprint", true)]
    [DataRow("thumbprint", false)]
    [DataRow("certificate-id", true)]
    [DataRow("certificate-id", false)]
    public void IssuerIdentityRequiresTheVerifiedImmediateIssuer(string field, bool matching)
    {
        using var issuer = CreateIssuer();
        using var certificate = CreateIssuedCertificate(issuer);
        using var other = CreateIssuer("CN=other-issuer");
        var identity = field == "thumbprint"
            ? new CertificateIdentity { IssuerThumbprint = matching ? issuer.Thumbprint : other.Thumbprint }
            : new CertificateIdentity { IssuerCertificateId = matching ? "issuer" : "other" };
        var test = CreateTest(new ValidateClientCertificateConfig { Identities = [identity] }, certificate,
            ValidEvidence with { IssuerCertificate = issuer });
        test.SetupCertificateStore().WithCertificateById("issuer", issuer).WithCertificateById("other", other);

        if (matching)
        {
            test.RunInbound();
            test.Context.Variables["continued"].Should().Be(true);
        }
        else
        {
            AssertRejected(test, "ClientCertificateIdentityNotAllowed");
        }
    }

    [TestMethod]
    public void IssuerCertificateIdDoesNotMatchBySubjectNameAlone()
    {
        using var issuer = CreateIssuer();
        using var certificate = CreateIssuedCertificate(issuer);
        using var lookalike = CreateIssuer();
        var test = CreateTest(new ValidateClientCertificateConfig
        {
            Identities = [new CertificateIdentity { IssuerCertificateId = "lookalike" }]
        }, certificate, ValidEvidence with { IssuerCertificate = issuer });
        test.SetupCertificateStore().WithCertificateById("lookalike", lookalike);

        AssertRejected(test, "ClientCertificateIdentityNotAllowed");
    }

    [TestMethod]
    public void MissingIssuerEvidenceDoesNotPassAnIssuerPin()
    {
        using var certificate = CreateCertificate();
        var test = CreateTest(new ValidateClientCertificateConfig
        {
            Identities = [new CertificateIdentity { IssuerThumbprint = certificate.Thumbprint }]
        }, certificate, ValidEvidence);

        AssertRejected(test, "ClientCertificateIdentityNotAllowed");
    }

    [TestMethod]
    public void MissingIssuerCertificateResourceIsAnExplicitRejection()
    {
        using var certificate = CreateCertificate();
        var test = CreateTest(new ValidateClientCertificateConfig
        {
            Identities = [new CertificateIdentity { IssuerCertificateId = "missing" }]
        }, certificate, ValidEvidence with { IssuerCertificate = certificate });

        AssertRejected(test);
        test.Context.LastError.Message.Should().Contain("missing");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void IgnoreErrorContinuesButRecordsTheValidationFailure(bool missingCertificate)
    {
        using var certificate = CreateCertificate();
        var test = CreateTest(new ValidateClientCertificateConfig { IgnoreError = true },
            missingCertificate ? null : certificate, ValidEvidence with { IsTrusted = false });
        var traces = new List<string>();
        test.Context.Trace = traces.Add;
        test.Context.Response.StatusCode = 202;
        test.Context.Response.StatusReason = "Accepted";
        test.Context.Response.Body.Content = "existing";

        test.RunInbound();

        test.Context.Variables["continued"].Should().Be(true);
        test.Context.LastError.Source.Should().Be("validate-client-certificate");
        test.Context.LastError.Reason.Should().NotBeNullOrWhiteSpace();
        traces.Should().ContainSingle().Which.Should().Contain("validate-client-certificate");
        test.Context.ResponseTerminated.Should().BeFalse();
        test.Context.Response.StatusCode.Should().Be(202);
        test.Context.Response.StatusReason.Should().Be("Accepted");
        test.Context.Response.Body.Content.Should().Be("existing");
    }

    [TestMethod]
    public void RejectionProvidesLastErrorToExplicitOnErrorExecution()
    {
        var test = CreateTest();

        AssertRejected(test, "ClientCertificateNotPresent");
        test.RunOnError();

        test.Context.Variables["on-error-reason"].Should().Be("ClientCertificateNotPresent");
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void CoordinatedCertificateErrorRunsOnErrorAndMayReplaceResponse(bool nested, bool replaceResponse)
    {
        var config = new ValidateClientCertificateConfig();
        var pipeline = CreateErrorPipeline(config, replaceResponse ? ReplaceErrorResponse : null);
        PolicyException? original = null;
        pipeline.Context.Response.StatusCode = 200;
        pipeline.Context.Response.StatusReason = "OK";
        pipeline.Context.Response.Headers["Content-Length"] = ["999"];
        pipeline.Context.Response.Body.Content = "stale successful response";

        pipeline.RunRequest(request =>
        {
            original = Assert.ThrowsExactly<PolicyException>(() => SettlementTest.RunAll(request, nested));
            AssertFailureResponse(request.Context);
            ExecutionTest.RunSection(request, "on-error", nested);
        });

        AssertCertificateError(original!, pipeline.Context, config);
        pipeline.Context.Variables["error-ran"].Should().Be(true);
        pipeline.Context.Variables["on-error-reason"].Should().Be("ClientCertificateNotPresent");
        pipeline.Context.Variables["outer-error-ran"].Should().Be(true);
        pipeline.Context.Variables["after-error-handler"].Should().Be(true);
        pipeline.Context.ResponseTerminated.Should().BeFalse();
        pipeline.Context.Response.Headers.Should().NotContainKey("Content-Length");
        AssertNormalSectionsSkipped(pipeline.Context);
        if (replaceResponse)
        {
            AssertReplacementResponse(pipeline.Context);
        }
        else
        {
            AssertFailureResponse(pipeline.Context);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ExplicitPipelineOnErrorRemainsAvailableAfterUnhandledCertificateFailure(bool nested)
    {
        var config = new ValidateClientCertificateConfig();
        var pipeline = CreateErrorPipeline(config);

        var error = Assert.ThrowsExactly<PolicyException>(() => SettlementTest.RunAll(pipeline, nested));
        ExecutionTest.RunSection(pipeline, "on-error", nested);

        AssertCertificateError(error, pipeline.Context, config);
        AssertFailureResponse(pipeline.Context);
        pipeline.Context.Variables["error-ran"].Should().Be(true);
        pipeline.Context.Variables["outer-error-ran"].Should().Be(true);
        pipeline.Context.ResponseTerminated.Should().BeFalse();
        AssertNormalSectionsSkipped(pipeline.Context);
    }

    [TestMethod]
    public void NestedCertificateErrorUnwindsParentBaseAndStillRunsOnError()
    {
        var config = new ValidateClientCertificateConfig();
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Global, new ExecutionTestDocument
            {
                InboundAction = section =>
                {
                    section.SetVariable("outer-before", true);
                    section.Base();
                    section.SetVariable("outer-after", true);
                },
                BackendAction = section => section.SetVariable("global-backend", true),
                OutboundAction = section => section.SetVariable("global-outbound", true),
                OnErrorAction = section => section.SetVariable("outer-error-ran", true)
            })
            .AddPolicy(PolicyScope.Operation, new ExecutionTestDocument
            {
                InboundAction = section =>
                {
                    section.ValidateClientCertificate(config);
                    section.SetVariable("continued", true);
                },
                BackendAction = section => section.SetVariable("backend", true),
                OutboundAction = section => section.SetVariable("outbound", true),
                OnErrorAction = section =>
                {
                    RecordOnError(section);
                    section.Base();
                    ReplaceErrorResponse(section);
                }
            })
            .Build();
        PolicyException? original = null;

        pipeline.RunRequest(request =>
        {
            original = Assert.ThrowsExactly<PolicyException>(request.RunAllNested);
            request.RunOnErrorNested();
        });

        AssertCertificateError(original!, pipeline.Context, config);
        pipeline.Context.Variables["outer-before"].Should().Be(true);
        pipeline.Context.Variables["error-ran"].Should().Be(true);
        pipeline.Context.Variables["outer-error-ran"].Should().Be(true);
        pipeline.Context.Variables.Should().NotContainKey("outer-after");
        AssertNormalSectionsSkipped(pipeline.Context);
        AssertReplacementResponse(pipeline.Context);
        pipeline.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void OnErrorMayRethrowTheOriginalCertificateError(bool nested)
    {
        var config = new ValidateClientCertificateConfig();
        PolicyException? original = null;
        var pipeline = CreateErrorPipeline(config, _ => throw original!);

        var propagated = Assert.ThrowsExactly<PolicyException>(() => pipeline.RunRequest(request =>
        {
            original = Assert.ThrowsExactly<PolicyException>(() => SettlementTest.RunAll(request, nested));
            ExecutionTest.RunSection(request, "on-error", nested);
        }));

        propagated.Should().BeSameAs(original);
        AssertCertificateError(propagated, pipeline.Context, config);
        AssertFailureResponse(pipeline.Context);
        pipeline.Context.Variables["error-ran"].Should().Be(true);
        pipeline.Context.Variables.Should().NotContainKey("after-error-handler");
        pipeline.Context.ResponseTerminated.Should().BeFalse();
        AssertNormalSectionsSkipped(pipeline.Context);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void OnErrorReturnResponseStillTerminatesRemainingScopesAndSections(bool nested)
    {
        var config = new ValidateClientCertificateConfig();
        var onErrorCalls = 0;
        var pipeline = CreateErrorPipeline(config, section =>
        {
            onErrorCalls++;
            CompleteErrorResponse(section);
        }, chainBeforeHandling: false);
        PolicyException? original = null;

        pipeline.RunRequest(request =>
        {
            original = Assert.ThrowsExactly<PolicyException>(() => SettlementTest.RunAll(request, nested));
            ExecutionTest.RunSection(request, "on-error", nested);
        });
        SettlementTest.RunAll(pipeline, nested);
        ExecutionTest.RunSection(pipeline, "on-error", nested);

        AssertCertificateError(original!, pipeline.Context, config);
        pipeline.Context.Response.StatusCode.Should().Be(503);
        pipeline.Context.Response.StatusReason.Should().Be("Service Unavailable");
        pipeline.Context.Response.Body.Content.Should().Be("completed certificate error");
        pipeline.Context.ResponseTerminated.Should().BeTrue();
        pipeline.Context.Variables["error-ran"].Should().Be(true);
        pipeline.Context.Variables.Should().NotContainKey("outer-error-ran").And.NotContainKey("after-error-handler");
        onErrorCalls.Should().Be(1);
        AssertNormalSectionsSkipped(pipeline.Context);
    }

    [TestMethod]
    [DataRow("preserve")]
    [DataRow("replace")]
    [DataRow("rethrow")]
    [DataRow("return")]
    public void StandaloneCertificateFailurePreservesResponseAndAllowsOnError(string behavior)
    {
        var config = new ValidateClientCertificateConfig();
        Action<IOnErrorContext>? action = null;
        var test = new CertificateDocument(config, section => action?.Invoke(section)).AsTestDocument();

        var original = Assert.ThrowsExactly<PolicyException>(test.RunAll);
        AssertCertificateError(original, test.Context, config);
        AssertFailureResponse(test.Context);
        test.Context.ResponseTerminated.Should().BeFalse();
        action = behavior switch
        {
            "replace" => ReplaceErrorResponse,
            "rethrow" => _ => throw original,
            "return" => CompleteErrorResponse,
            _ => null
        };
        if (behavior == "rethrow")
        {
            Assert.ThrowsExactly<PolicyException>(test.RunOnError).Should().BeSameAs(original);
        }
        else
        {
            test.RunOnError();
        }

        AssertCertificateError(original, test.Context, config);
        test.Context.Variables["error-ran"].Should().Be(true);
        test.Context.Variables["on-error-reason"].Should().Be("ClientCertificateNotPresent");
        test.Context.Variables.Should().NotContainKey("continued").And.NotContainKey("backend").And.NotContainKey("outbound");
        test.Context.Variables.ContainsKey("after-error-handler").Should().Be(behavior is "preserve" or "replace");
        test.Context.ResponseTerminated.Should().Be(behavior == "return");
        if (behavior == "replace") { AssertReplacementResponse(test.Context); }
        else if (behavior == "return")
        {
            test.Context.Response.StatusCode.Should().Be(503);
            test.Context.Response.Body.Content.Should().Be("completed certificate error");
        }
        else { AssertFailureResponse(test.Context); }
    }

    [TestMethod]
    public void StandaloneCertificateErrorDoesNotClearAPreexistingTerminalResponseFlag()
    {
        var test = CreateTest();
        test.Context.ResponseTerminated = true;

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        AssertCertificateError(error, test.Context);
        AssertFailureResponse(test.Context);
        test.Context.ResponseTerminated.Should().BeTrue();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CoordinatedIgnoreErrorStillContinuesNormalExecution(bool nested)
    {
        var pipeline = CreateErrorPipeline(new ValidateClientCertificateConfig { IgnoreError = true },
            _ => Assert.Fail("IgnoreError must not route normal execution into on-error."));
        var traces = new List<string>();
        pipeline.Context.Trace = traces.Add;
        pipeline.Context.Response.StatusCode = 202;
        pipeline.Context.Response.StatusReason = "Accepted";
        pipeline.Context.Response.Body.Content = "existing response";

        SettlementTest.RunAll(pipeline, nested);

        pipeline.Context.Variables.Should().ContainKeys("continued", "after-base", "inner",
            "backend", "outbound", "global-backend", "global-outbound");
        pipeline.Context.Variables.Should().NotContainKey("error-ran");
        pipeline.Context.LastError.Reason.Should().Be("ClientCertificateNotPresent");
        pipeline.Context.Response.StatusCode.Should().Be(202);
        pipeline.Context.Response.StatusReason.Should().Be("Accepted");
        pipeline.Context.Response.Body.Content.Should().Be("existing response");
        pipeline.Context.ResponseTerminated.Should().BeFalse();
        traces.Should().ContainSingle();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ExistingTerminalRejectionStillSkipsCertificateValidationAndOnError(bool nested)
    {
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Global, new ExecutionTestDocument
            {
                InboundAction = section =>
                {
                    section.ReturnResponse(new ReturnResponseConfig
                    {
                        Status = new StatusConfig { Code = 409, Reason = "Conflict" },
                        Body = new BodyConfig { Content = "already terminal" }
                    });
                    section.ValidateClientCertificate(new ValidateClientCertificateConfig());
                },
                BackendAction = _ => Assert.Fail("Terminal response must skip backend."),
                OutboundAction = _ => Assert.Fail("Terminal response must skip outbound."),
                OnErrorAction = _ => Assert.Fail("Terminal response must skip coordinated on-error.")
            })
            .Build();

        SettlementTest.RunAll(pipeline, nested);
        ExecutionTest.RunSection(pipeline, "on-error", nested);

        pipeline.Context.Response.StatusCode.Should().Be(409);
        pipeline.Context.Response.Body.Content.Should().Be("already terminal");
        pipeline.Context.ResponseTerminated.Should().BeTrue();
        pipeline.Context.LastError.Source.Should().NotBe("validate-client-certificate");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SecurityRejectionHonorsIgnoreError(bool ignoreError)
    {
        using var certificate = CreateCertificate();
        var test = CreateTest(new ValidateClientCertificateConfig { IgnoreError = ignoreError }, certificate);
        test.Context.Services.Register<IClientCertificateValidator>(new RecordingCertificateValidator(_ =>
            throw new SecurityException("mocked revocation rejection")));

        if (ignoreError)
        {
            test.RunInbound();
            test.Context.Variables["continued"].Should().Be(true);
            test.Context.LastError.Message.Should().Be("mocked revocation rejection");
        }
        else
        {
            AssertRejected(test, "ClientCertificateValidationFailed");
        }
    }

    [TestMethod]
    public void IgnoreErrorDoesNotSwallowUnexpectedValidatorFailures()
    {
        using var certificate = CreateCertificate();
        var test = CreateTest(new ValidateClientCertificateConfig { IgnoreError = true }, certificate);
        var failure = new InvalidOperationException("validator bug");
        test.Context.Services.Register<IClientCertificateValidator>(new RecordingCertificateValidator(_ => throw failure));

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeSameAs(failure);
        test.Context.Variables.Should().NotContainKey("continued");
    }

    [TestMethod]
    public void NullCertificateValidatorOutputIsNotTrustEvidence()
    {
        using var certificate = CreateCertificate();
        var test = CreateTest(certificate: certificate);
        test.Context.Services.Register<IClientCertificateValidator>(new RecordingCertificateValidator(_ => null!));

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeOfType<InvalidOperationException>();
        test.Context.Variables.Should().NotContainKey("continued");
    }

    [TestMethod]
    [DataRow("issuer-attributes")]
    [DataRow("identity-count")]
    public void InvalidConfigurationIsReportedRatherThanIgnored(string scenario)
    {
        using var certificate = CreateCertificate();
        var test = CreateTest(new ValidateClientCertificateConfig
        {
            IgnoreError = true,
            Identities = scenario == "issuer-attributes"
                ? [new CertificateIdentity { IssuerCertificateId = "issuer", IssuerSubject = "CN=issuer" }]
                : Enumerable.Range(0, 11).Select(_ => new CertificateIdentity { CommonName = "client" }).ToArray()
        }, certificate, ValidEvidence);

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeAssignableTo<ArgumentException>();
        test.Context.Variables.Should().NotContainKey("continued");
    }

    [TestMethod]
    public void CallbackExplicitlyOverridesDefaultCertificateValidation()
    {
        var test = CreateTest();
        test.SetupInbound().ValidateClientCertificate((_, config) => config.IgnoreError is null)
            .WithCallback((context, _) => context.Variables["mocked"] = true);

        test.RunInbound();

        test.Context.Variables["mocked"].Should().Be(true);
        test.Context.Variables["continued"].Should().Be(true);
    }

    [TestMethod]
    public void UnmatchedCallbackDoesNotConcealAMissingCertificate()
    {
        var test = CreateTest();
        test.SetupInbound().ValidateClientCertificate((_, _) => false)
            .WithCallback((context, _) => context.Variables["mocked"] = true);

        AssertRejected(test, "ClientCertificateNotPresent");
        test.Context.Variables.Should().NotContainKey("mocked");
    }

    [TestMethod]
    public void CallbackErrorsAreSurfaced()
    {
        var test = CreateTest();
        var failure = new InvalidOperationException("callback failure");
        test.SetupInbound().ValidateClientCertificate().WithCallback((_, _) => throw failure);

        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        error.InnerException.Should().BeSameAs(failure);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SupportsInboundFragmentsWithTheSameTrustBoundary(bool valid)
    {
        using var certificate = CreateCertificate();
        var test = new IdentityFragmentDocument().AsTestDocument().RegisterFragment("identity",
            new IdentityFragment(context => context.ValidateClientCertificate(new ValidateClientCertificateConfig())));
        IdentityTestTokens.SetClock(test.Context);
        test.Context.Request.Certificate = certificate;
        test.Context.Services.Register<IClientCertificateValidator>(new RecordingCertificateValidator(_ =>
            ValidEvidence with { IsTrusted = valid }));

        if (valid)
        {
            test.RunInbound();
            test.Context.Variables["continued"].Should().Be(true);
        }
        else
        {
            AssertRejected(test, "ClientCertificateUntrusted");
        }
    }

    [TestMethod]
    public void CertificateFailureCannotReachAnyLaterPipelineSection()
    {
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Global, new CertificateDocument(new ValidateClientCertificateConfig()))
            .AddPolicy(PolicyScope.Operation, new IdentityMarkerDocument())
            .Build();

        var error = Assert.ThrowsExactly<PolicyException>(() => pipeline.RunAll());

        error.Policy.Should().Be(nameof(IInboundContext.ValidateClientCertificate));
        pipeline.Context.ResponseTerminated.Should().BeFalse();
        pipeline.Context.Response.StatusCode.Should().Be(403);
        pipeline.Context.Variables.Should().NotContainKey("continued").And.NotContainKey("inner")
            .And.NotContainKey("backend").And.NotContainKey("outbound");
    }

    private static void AssertRejected(TestDocument test, string? reason = null)
    {
        var error = Assert.ThrowsExactly<PolicyException>(() => test.RunInbound());

        AssertCertificateError(error, test.Context);
        AssertFailureResponse(test.Context);
        test.Context.ResponseTerminated.Should().BeFalse();
        test.Context.Variables.Should().NotContainKey("continued");
        if (reason is not null) { test.Context.LastError.Reason.Should().Be(reason); }
    }

    private static void AssertCertificateError(
        PolicyException error, GatewayContext context, ValidateClientCertificateConfig? config = null)
    {
        error.Policy.Should().Be(nameof(IInboundContext.ValidateClientCertificate));
        error.Section.Should().Be(nameof(IInboundContext));
        error.InnerException.Should().BeAssignableTo<SecurityException>();
        error.Message.Should().Be(context.LastError.Message);
        var argument = error.PolicyArgs.Should().ContainSingle().Which;
        argument.Should().BeOfType<ValidateClientCertificateConfig>();
        if (config is not null) { argument.Should().BeSameAs(config); }
        context.LastError.Source.Should().Be("validate-client-certificate");
        context.LastError.Section.Should().Be("inbound");
        context.LastError.HttpErrorCode.Should().Be(403);
    }

    private static void AssertFailureResponse(GatewayContext context)
    {
        context.Response.StatusCode.Should().Be(403);
        context.Response.StatusReason.Should().Be("Forbidden");
        context.Response.Headers["Content-Type"].Should().Equal("application/json");
        using var body = JsonDocument.Parse(context.Response.Body.Content!);
        body.RootElement.GetProperty("statusCode").GetInt32().Should().Be(403);
        body.RootElement.GetProperty("message").GetString().Should().Be(context.LastError.Message);
    }

    private static void AssertNormalSectionsSkipped(GatewayContext context) =>
        context.Variables.Should().NotContainKey("continued").And.NotContainKey("after-base")
            .And.NotContainKey("inner").And.NotContainKey("backend").And.NotContainKey("outbound")
            .And.NotContainKey("global-backend").And.NotContainKey("global-outbound");

    private static void AssertReplacementResponse(GatewayContext context)
    {
        context.Response.StatusCode.Should().Be(502);
        context.Response.StatusReason.Should().Be("Bad Gateway");
        context.Response.Headers["Content-Type"].Should().Equal("text/plain");
        context.Response.Headers["X-Error-Handled"].Should().Equal("certificate");
        context.Response.Body.Content.Should().Be("certificate rejected by on-error");
    }

    private static void RecordOnError(IOnErrorContext context)
    {
        context.SetVariable("error-ran", true);
        context.SetVariable("on-error-reason", context.ExpressionContext.LastError.Reason);
    }

    private static void ReplaceErrorResponse(IOnErrorContext context)
    {
        context.SetStatus(new StatusConfig { Code = 502, Reason = "Bad Gateway" });
        context.SetHeader("Content-Type", "text/plain");
        context.SetHeader("X-Error-Handled", "certificate");
        context.SetBody("certificate rejected by on-error");
    }

    private static void CompleteErrorResponse(IOnErrorContext context) =>
        context.ReturnResponse(new ReturnResponseConfig
        {
            Status = new StatusConfig { Code = 503, Reason = "Service Unavailable" },
            Body = new BodyConfig { Content = "completed certificate error" }
        });

    private static PolicyPipeline CreateErrorPipeline(ValidateClientCertificateConfig config,
        Action<IOnErrorContext>? onError = null, bool chainBeforeHandling = true) =>
        PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Global, new ExecutionTestDocument
            {
                InboundAction = section =>
                {
                    section.ValidateClientCertificate(config);
                    section.SetVariable("continued", true);
                    section.Base();
                    section.SetVariable("after-base", true);
                },
                BackendAction = section =>
                {
                    section.SetVariable("global-backend", true);
                    section.Base();
                },
                OutboundAction = section => section.SetVariable("global-outbound", true),
                OnErrorAction = section => section.SetVariable("outer-error-ran", true)
            })
            .AddPolicy(PolicyScope.Operation, new ExecutionTestDocument
            {
                InboundAction = section => section.SetVariable("inner", true),
                BackendAction = section => section.SetVariable("backend", true),
                OutboundAction = section =>
                {
                    section.SetVariable("outbound", true);
                    section.Base();
                },
                OnErrorAction = section =>
                {
                    RecordOnError(section);
                    if (chainBeforeHandling) { section.Base(); }
                    onError?.Invoke(section);
                    section.SetVariable("after-error-handler", true);
                    if (!chainBeforeHandling) { section.Base(); }
                }
            })
            .Build();

    private static X509Certificate2 CreateCertificate(string subject = "CN=client", bool includeDns = true,
        DateTimeOffset? notBefore = null, DateTimeOffset? notAfter = null)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        if (includeDns)
        {
            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName("client.example");
            names.AddDnsName("second.example");
            request.CertificateExtensions.Add(names.Build());
        }

        return request.CreateSelfSigned(notBefore ?? IdentityTestTokens.Now.AddYears(-1),
            notAfter ?? IdentityTestTokens.Now.AddYears(1));
    }

    private static X509Certificate2 CreateIssuer(string subject = "CN=issuer")
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        return request.CreateSelfSigned(IdentityTestTokens.Now.AddYears(-10), IdentityTestTokens.Now.AddYears(10));
    }

    private static X509Certificate2 CreateIssuedCertificate(X509Certificate2 issuer)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=client", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var issued = request.Create(issuer, IdentityTestTokens.Now.AddYears(-1),
            IdentityTestTokens.Now.AddYears(1), [1, 2, 3, 4]);
        return issued.CopyWithPrivateKey(rsa);
    }

    private sealed class CertificateDocument(
        ValidateClientCertificateConfig config, Action<IOnErrorContext>? onError = null) : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.ValidateClientCertificate(config);
            context.SetVariable("continued", true);
        }

        public void Backend(IBackendContext context) => context.SetVariable("backend", true);
        public void Outbound(IOutboundContext context) => context.SetVariable("outbound", true);

        public void OnError(IOnErrorContext context)
        {
            RecordOnError(context);
            onError?.Invoke(context);
            context.SetVariable("after-error-handler", true);
        }
    }

    private sealed class RecordingCertificateValidator(
        Func<ClientCertificateValidationRequest, ClientCertificateValidationResult> validate) : IClientCertificateValidator
    {
        internal List<ClientCertificateValidationRequest> Requests { get; } = [];

        public ClientCertificateValidationResult Validate(ClientCertificateValidationRequest request)
        {
            Requests.Add(request);
            return validate(request);
        }
    }
}
