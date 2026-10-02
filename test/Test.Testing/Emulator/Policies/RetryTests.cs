// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class RetryTests
{
    class SimpleRetry : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.Retry(new RetryConfig
            {
                Condition = true,
                Count = 3,
                Interval = 1,
            }, () =>
            {
                context.SetVariable("retried", true);
            });
        }

        public void Backend(IBackendContext context) { }
        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    class OutboundRetry : IDocument
    {
        public void Inbound(IInboundContext context) { }
        public void Backend(IBackendContext context) { }

        public void Outbound(IOutboundContext context)
        {
            context.Retry(new RetryConfig
            {
                Condition = true,
                Count = 2,
                Interval = 1,
            }, () =>
            {
                context.SetVariable("outbound-retried", true);
            });
        }

        public void OnError(IOnErrorContext context) { }
    }

    class DynamicRetry : IDocument
    {
        private int _attempts;

        public void Inbound(IInboundContext context)
        {
            context.Retry(new RetryConfig
            {
                Condition = true,
                ConditionEvaluator = () => _attempts < 2,
                Count = 3,
                Interval = 1,
            }, () =>
            {
                _attempts++;
                context.SetVariable("attempts", _attempts);
            });
        }

        public void Backend(IBackendContext context) { }
        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    class RetryWithUnhandledException : IDocument
    {
        private int _attempts;

        public void Inbound(IInboundContext context)
        {
            context.Retry(new RetryConfig
            {
                Condition = true,
                ConditionEvaluator = () => _attempts < 2,
                Count = 1,
                Interval = 1,
                FirstFastRetry = true,
            }, () =>
            {
                _attempts++;
                context.SetVariable("attempts", _attempts);
                if (_attempts == 1)
                {
                    throw new InvalidOperationException("fail first");
                }
            });
        }

        public void Backend(IBackendContext context) { }
        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    [TestMethod]
    public void Retry_Inbound_ShouldExecuteSectionOnce()
    {
        // Arrange
        var test = new TestDocument(new SimpleRetry());

        // Act
        test.RunInbound();

        // Assert - section should have been executed
        test.Context.Variables.Should().ContainKey("retried")
            .WhoseValue.Should().Be(true);
    }

    [TestMethod]
    public void Retry_Inbound_Callback()
    {
        // Arrange
        var test = new TestDocument(new SimpleRetry());
        var executedCallback = false;

        test.SetupInbound().Retry().WithCallback((context, config, section) =>
        {
            executedCallback = true;
            // Execute the section to verify we have access to it
            section();
        });

        // Act
        test.RunInbound();

        // Assert
        executedCallback.Should().BeTrue();
        test.Context.Variables.Should().ContainKey("retried")
            .WhoseValue.Should().Be(true);
    }

    [TestMethod]
    public void Retry_Inbound_CallbackCanSkipSection()
    {
        // Arrange
        var test = new TestDocument(new SimpleRetry());

        test.SetupInbound().Retry().WithCallback((context, config, section) =>
        {
            // Intentionally do NOT call section()
            context.Variables["skipped"] = true;
        });

        // Act
        test.RunInbound();

        // Assert - section should not have executed
        test.Context.Variables.Should().NotContainKey("retried");
        test.Context.Variables.Should().ContainKey("skipped")
            .WhoseValue.Should().Be(true);
    }

    [TestMethod]
    public void Retry_Outbound_ShouldExecuteSectionOnce()
    {
        // Arrange
        var test = new TestDocument(new OutboundRetry());

        // Act
        test.RunOutbound();

        // Assert
        test.Context.Variables.Should().ContainKey("outbound-retried")
            .WhoseValue.Should().Be(true);
    }

    [TestMethod]
    public void Retry_Inbound_CallbackWithPredicate()
    {
        // Arrange
        var test = new TestDocument(new SimpleRetry());
        var executedCallback = false;

        test.SetupInbound()
            .Retry((_, config, _) => config.Count == 3)
            .WithCallback((context, config, section) =>
            {
                executedCallback = true;
                section();
            });

        // Act
        test.RunInbound();

        // Assert
        executedCallback.Should().BeTrue();
    }

    [TestMethod]
    public void Retry_Inbound_ShouldReevaluateConditionBetweenAttempts()
    {
        var test = new DynamicRetry().AsTestDocument();

        test.RunInbound();

        test.Context.Variables.Should().ContainKey("attempts")
            .WhoseValue.Should().Be(2);
    }

    [TestMethod]
    public void Retry_Inbound_ShouldPropagateUnhandledExceptionWithoutRetry()
    {
        var test = new RetryWithUnhandledException().AsTestDocument();

        var act = () => test.RunInbound();

        var error = act.Should().Throw<PolicyException>().Which;

        error.Policy.Should().Be(nameof(IInboundContext.Retry));
        error.InnerException.Should().BeOfType<InvalidOperationException>();
        test.Context.Variables.Should().ContainKey("attempts")
            .WhoseValue.Should().Be(1);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(3)]
    [DataRow(50)]
    public void Retry_CountIsAdditionalExecutionsAndStopsAtExhaustion(int count)
    {
        var document = new RetryDocument(CreateConfig(count) with { FirstFastRetry = true });
        var evaluations = 0;
        document.Config = document.Config with
        {
            ConditionEvaluator = () =>
            {
                evaluations++;
                return true;
            }
        };
        var test = document.AsTestDocument();
        var scheduler = RegisterScheduler(test);

        test.RunInbound();

        document.Attempts.Should().Be(count + 1);
        evaluations.Should().Be(count);
        scheduler.Delays.Should().HaveCount(count - 1);
        test.Context.Variables.Should().ContainKey("after-retry")
            .WhoseValue.Should().Be(true);
    }

    [TestMethod]
    public void Retry_FalseConditionStillExecutesChildrenInitially()
    {
        var document = new RetryDocument(CreateConfig() with { Condition = false });
        var test = document.AsTestDocument();
        var scheduler = RegisterScheduler(test);

        test.RunInbound();

        document.Attempts.Should().Be(1);
        scheduler.Delays.Should().BeEmpty();
    }

    [TestMethod]
    public void Retry_EvaluatesConditionOnlyAfterChildrenAndStopsEarly()
    {
        var document = new RetryDocument(CreateConfig());
        var observedAttempts = new List<int>();
        document.Config = document.Config with
        {
            ConditionEvaluator = () =>
            {
                observedAttempts.Add(document.Attempts);
                return document.Attempts < 2;
            }
        };
        var test = document.AsTestDocument();
        var scheduler = RegisterScheduler(test);

        test.RunInbound();

        document.Attempts.Should().Be(2);
        observedAttempts.Should().Equal(1, 2);
        scheduler.Delays.Should().Equal(TimeSpan.FromSeconds(1));
    }

    [TestMethod]
    public void Retry_ConditionEvaluatorOverridesCapturedCondition()
    {
        var document = new RetryDocument(CreateConfig(1) with
        {
            Condition = false,
            ConditionEvaluator = () => true,
            FirstFastRetry = true
        });
        var test = document.AsTestDocument();
        var scheduler = RegisterScheduler(test);

        test.RunInbound();

        document.Attempts.Should().Be(2);
        scheduler.Delays.Should().BeEmpty();
    }

    [TestMethod]
    public void Retry_DoesNotDelayWhenInitialExecutionMakesConditionFalse()
    {
        var document = new RetryDocument(CreateConfig());
        document.Config = document.Config with
        {
            ConditionEvaluator = () => document.Attempts == 0
        };
        var test = document.AsTestDocument();
        var scheduler = RegisterScheduler(test);

        test.RunInbound();

        document.Attempts.Should().Be(1);
        scheduler.Delays.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Retry_FixedDelaysOccurBeforeEachAdditionalAttempt(bool firstFastRetry)
    {
        var events = new List<string>();
        var document = new RetryDocument(CreateConfig() with { FirstFastRetry = firstFastRetry })
        {
            Child = () => events.Add("attempt")
        };
        var test = document.AsTestDocument();
        var scheduler = RegisterScheduler(test, new RecordingRetryScheduler
        {
            OnDelay = _ => events.Add("delay")
        });

        test.RunInbound();

        var expected = firstFastRetry
            ? new[] { "attempt", "attempt", "delay", "attempt", "delay", "attempt" }
            : new[] { "attempt", "delay", "attempt", "delay", "attempt", "delay", "attempt" };
        events.Should().Equal(expected);
        scheduler.Delays.Should().OnlyContain(delay => delay == TimeSpan.FromSeconds(1));
        scheduler.JitterCalls.Should().Be(0);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Retry_LinearDelaysUseRetryOrdinalWithoutJitter(bool firstFastRetry)
    {
        var document = new RetryDocument(CreateConfig(4) with
        {
            Delta = 2,
            FirstFastRetry = firstFastRetry
        });
        var test = document.AsTestDocument();
        var scheduler = RegisterScheduler(test);

        test.RunInbound();

        int[] seconds = firstFastRetry ? [3, 5, 7] : [1, 3, 5, 7];
        scheduler.Delays.Should().Equal(seconds.Select(value => TimeSpan.FromSeconds(value)));
        scheduler.JitterCalls.Should().Be(0);
        document.Attempts.Should().Be(5);
    }

    [TestMethod]
    [DataRow(0.8, 5, 9, 17, 25)]
    [DataRow(1.0, 6, 11, 21, 25)]
    [DataRow(1.2, 7, 13, 25, 25)]
    public void Retry_ExponentialDelaysUseInjectedJitterAndMaximum(
        double jitter, int first, int second, int third, int fourth)
    {
        var document = new RetryDocument(CreateConfig(4) with
        {
            Delta = 5,
            MaxInterval = 25
        });
        var test = document.AsTestDocument();
        var scheduler = RegisterScheduler(test, new RecordingRetryScheduler(() => jitter));

        test.RunInbound();

        scheduler.Delays.Should().Equal(
            new[] { first, second, third, fourth }.Select(value => TimeSpan.FromSeconds(value)));
        scheduler.JitterCalls.Should().Be(4);
    }

    [TestMethod]
    public void Retry_FirstFastExponentialRetryDoesNotConsumeJitterOrResetOrdinal()
    {
        var document = new RetryDocument(CreateConfig(4) with
        {
            Delta = 5,
            MaxInterval = 25,
            FirstFastRetry = true
        });
        var test = document.AsTestDocument();
        var scheduler = RegisterScheduler(test);

        test.RunInbound();

        scheduler.Delays.Should().Equal(
            TimeSpan.FromSeconds(11), TimeSpan.FromSeconds(21), TimeSpan.FromSeconds(25));
        scheduler.JitterCalls.Should().Be(3);
    }

    [TestMethod]
    public void Retry_MaximumCapsEvenTheFirstExponentialDelay()
    {
        var document = new RetryDocument(CreateConfig(2) with
        {
            Interval = 2,
            Delta = 1,
            MaxInterval = 1
        });
        var test = document.AsTestDocument();
        var scheduler = RegisterScheduler(test);

        test.RunInbound();

        scheduler.Delays.Should().Equal(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    [TestMethod]
    public void Retry_LinearDelayArithmeticDoesNotOverflowInt32()
    {
        var document = new RetryDocument(CreateConfig(2) with { Delta = int.MaxValue });
        var test = document.AsTestDocument();
        var scheduler = RegisterScheduler(test);

        test.RunInbound();

        scheduler.Delays.Should().Equal(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2_147_483_648d));
    }

    [TestMethod]
    public void Retry_ExponentialMaximumPreventsOverflowAtFiftiethRetry()
    {
        var document = new RetryDocument(CreateConfig(50) with
        {
            Delta = int.MaxValue,
            MaxInterval = 2,
            FirstFastRetry = true
        });
        var test = document.AsTestDocument();
        var scheduler = RegisterScheduler(test);

        test.RunInbound();

        document.Attempts.Should().Be(51);
        scheduler.Delays.Should().HaveCount(49)
            .And.OnlyContain(delay => delay == TimeSpan.FromSeconds(2));
        scheduler.JitterCalls.Should().Be(49);
    }

    [TestMethod]
    public void Retry_DefaultSchedulerRecordsVirtualDelaysAndIsReusedAcrossSections()
    {
        var document = new RetryDocument(CreateConfig(1));
        var test = document.AsTestDocument();

        test.RunInbound();
        var scheduler = test.Context.Services.Resolve<IRetryScheduler>();
        test.RunOutbound();

        scheduler.Should().BeOfType<VirtualRetryScheduler>()
            .Which.Delays.Should().Equal(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        test.Context.Services.Resolve<IRetryScheduler>().Should().BeSameAs(scheduler);
    }

    [TestMethod]
    public void Retry_VirtualSchedulerIsDeterministicAndRecordsZeroDelay()
    {
        var scheduler = new VirtualRetryScheduler();

        scheduler.Delay(TimeSpan.Zero);
        scheduler.Delay(TimeSpan.FromSeconds(3));

        scheduler.Delays.Should().Equal(TimeSpan.Zero, TimeSpan.FromSeconds(3));
        scheduler.NextJitterFactor().Should().Be(1);
        scheduler.NextJitterFactor().Should().Be(1);
    }

    [TestMethod]
    public void Retry_VirtualSchedulerRejectsNegativeDelay()
    {
        var scheduler = new VirtualRetryScheduler();

        var act = () => scheduler.Delay(TimeSpan.FromSeconds(-1));

        act.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("delay");
        scheduler.Delays.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(0)]
    [DataRow(51)]
    [DataRow(int.MaxValue)]
    public void Retry_RejectsCountOutsideOneThroughFiftyBeforeExecuting(int count)
    {
        AssertInvalidConfig(CreateConfig(count) with { Condition = false }, nameof(RetryConfig.Count));
    }

    [TestMethod]
    public void Retry_RejectsMissingRequiredIntervalBeforeExecuting()
    {
        AssertInvalidConfig(CreateConfig() with { Condition = false, Interval = null },
            nameof(RetryConfig.Interval));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Retry_RejectsNonPositiveIntervalBeforeExecuting(int interval)
    {
        AssertInvalidConfig(CreateConfig() with { Condition = false, Interval = interval },
            nameof(RetryConfig.Interval));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Retry_RejectsNonPositiveDeltaBeforeExecuting(int delta)
    {
        AssertInvalidConfig(CreateConfig() with { Condition = false, Delta = delta },
            nameof(RetryConfig.Delta));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Retry_RejectsNonPositiveMaximumBeforeExecuting(int maximum)
    {
        AssertInvalidConfig(CreateConfig() with { Condition = false, Delta = 1, MaxInterval = maximum },
            nameof(RetryConfig.MaxInterval));
    }

    [TestMethod]
    public void Retry_RejectsMaximumWithoutExponentialDelta()
    {
        AssertInvalidConfig(CreateConfig() with { Condition = false, MaxInterval = 5 },
            nameof(RetryConfig.MaxInterval));
    }

    [TestMethod]
    public void Retry_InvalidConfigCannotBeHiddenByCallback()
    {
        var document = new RetryDocument(CreateConfig(0));
        var test = document.AsTestDocument();
        var callbacks = 0;
        test.SetupInbound().Retry().WithCallback((_, _, _) => callbacks++);

        var act = () => test.RunInbound();

        act.Should().Throw<PolicyException>().WithInnerException<ArgumentOutOfRangeException>();
        callbacks.Should().Be(0);
        document.Attempts.Should().Be(0);
    }

    [TestMethod]
    public void Retry_RejectsNullSectionBeforeCallback()
    {
        var test = new NullRetrySection().AsTestDocument();
        test.SetupInbound().Retry().WithCallback((_, _, _) => Assert.Fail("Invalid callback executed."));

        var act = () => test.RunInbound();

        act.Should().Throw<PolicyException>().WithInnerException<ArgumentNullException>()
            .Which.ParamName.Should().Be("section");
    }

    [TestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(0.79)]
    [DataRow(1.21)]
    public void Retry_RejectsJitterOutsideDocumentedRange(double jitter)
    {
        var document = new RetryDocument(CreateConfig(1) with { Delta = 1, MaxInterval = 10 });
        var test = document.AsTestDocument();
        var scheduler = RegisterScheduler(test, new RecordingRetryScheduler(() => jitter));

        var act = () => test.RunInbound();

        act.Should().Throw<PolicyException>().WithInnerException<InvalidOperationException>();
        document.Attempts.Should().Be(1);
        scheduler.Delays.Should().BeEmpty();
    }

    [TestMethod]
    public void Retry_PropagatesChildFailureWithoutEvaluatingCondition()
    {
        var expected = new InvalidOperationException("unhandled child failure");
        var evaluations = 0;
        var document = new RetryDocument(CreateConfig(1) with
        {
            FirstFastRetry = true,
            ConditionEvaluator = () =>
            {
                evaluations++;
                return true;
            }
        })
        {
            Child = () => throw expected
        };
        var test = document.AsTestDocument();
        var scheduler = RegisterScheduler(test);

        var act = () => test.RunInbound();

        var error = act.Should().Throw<PolicyException>().Which;
        error.InnerException.Should().BeSameAs(expected);
        document.Attempts.Should().Be(1);
        evaluations.Should().Be(0);
        scheduler.Delays.Should().BeEmpty();
        test.Context.Variables.Should().NotContainKey("after-retry");
    }

    [TestMethod]
    public void Retry_PropagatesFailureOnAdditionalAttemptWithoutAnotherRetry()
    {
        var expected = new InvalidOperationException("second attempt failed");
        var attempts = 0;
        var document = new RetryDocument(CreateConfig(2) with { FirstFastRetry = true })
        {
            Child = () =>
            {
                if (++attempts == 2)
                {
                    throw expected;
                }
            }
        };
        var test = document.AsTestDocument();
        var scheduler = RegisterScheduler(test);

        var act = () => test.RunInbound();

        act.Should().Throw<PolicyException>().Which.InnerException.Should().BeSameAs(expected);
        document.Attempts.Should().Be(2);
        scheduler.Delays.Should().BeEmpty();
    }

    [TestMethod]
    public void Retry_PropagatesConditionEvaluatorFailure()
    {
        var expected = new InvalidOperationException("condition failed");
        var document = new RetryDocument(CreateConfig() with { ConditionEvaluator = () => throw expected });
        var test = document.AsTestDocument();
        var scheduler = RegisterScheduler(test);

        var act = () => test.RunInbound();

        act.Should().Throw<PolicyException>().Which.InnerException.Should().BeSameAs(expected);
        document.Attempts.Should().Be(1);
        scheduler.Delays.Should().BeEmpty();
    }

    [TestMethod]
    public void Retry_PropagatesSchedulerFailureBeforeAnotherExecution()
    {
        var expected = new InvalidOperationException("scheduler failed");
        var document = new RetryDocument(CreateConfig(1));
        var test = document.AsTestDocument();
        RegisterScheduler(test, new RecordingRetryScheduler { OnDelay = _ => throw expected });

        var act = () => test.RunInbound();

        act.Should().Throw<PolicyException>().Which.InnerException.Should().BeSameAs(expected);
        document.Attempts.Should().Be(1);
        test.Context.Variables.Should().NotContainKey("after-retry");
    }

    [TestMethod]
    public void Retry_RetriesHandledSendRequestErrorsUsingResponseCondition()
    {
        var document = new RetryingSendRequest(ignoreError: true);
        var test = document.AsTestDocument();
        var calls = 0;
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(_ =>
        {
            if (++calls == 1)
            {
                throw new HttpRequestException("connection failed");
            }

            return new HttpResponseMessage(calls == 2
                ? System.Net.HttpStatusCode.InternalServerError
                : System.Net.HttpStatusCode.OK);
        }));
        var scheduler = RegisterScheduler(test);

        test.RunInbound();

        calls.Should().Be(3);
        scheduler.Delays.Should().Equal(TimeSpan.FromSeconds(1));
        test.Context.Variables["response"].Should().BeOfType<MockResponse>()
            .Which.StatusCode.Should().Be(200);
    }

    [TestMethod]
    public void Retry_PreservesUnhandledChildPolicyExceptionAndOrigin()
    {
        var document = new RetryingSendRequest(ignoreError: false);
        var test = document.AsTestDocument();
        var expected = new HttpRequestException("connection failed");
        var calls = 0;
        HttpResponseMessage Fail(HttpRequestMessage request)
        {
            calls++;
            throw expected;
        }
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(Fail));
        var scheduler = RegisterScheduler(test);

        var act = () => test.RunInbound();

        var error = act.Should().Throw<PolicyException>().Which;
        error.Policy.Should().Be(nameof(IInboundContext.SendRequest));
        error.Section.Should().Be(nameof(IInboundContext));
        error.InnerException.Should().BeSameAs(expected);
        calls.Should().Be(1);
        scheduler.Delays.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void Retry_ExecutesInEveryAuthoredSection(string section)
    {
        var document = new RetryDocument(CreateConfig(1));
        var test = document.AsTestDocument();
        var scheduler = RegisterScheduler(test);

        RunSection(test, section);

        document.Attempts.Should().Be(2);
        scheduler.Delays.Should().Equal(TimeSpan.FromSeconds(1));
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void Retry_CallbackOverridesExecutionAndReceivesOriginalArguments(string section)
    {
        var document = new RetryDocument(CreateConfig());
        var test = document.AsTestDocument();
        var scheduler = RegisterScheduler(test);
        RetryConfig? observedConfig = null;
        SetupRetry(test, section).WithCallback((gateway, config, child) =>
        {
            gateway.Should().BeSameAs(test.Context);
            observedConfig = config;
            child();
        });

        RunSection(test, section);

        observedConfig.Should().BeSameAs(document.Config);
        document.Attempts.Should().Be(1);
        scheduler.Delays.Should().BeEmpty();
    }

    [TestMethod]
    public void Retry_UnmatchedCallbackFallsThroughToDefaultBehavior()
    {
        var document = new RetryDocument(CreateConfig(1) with { Condition = false });
        var test = document.AsTestDocument();
        test.SetupInbound().Retry((_, config, _) => config.Count == 2)
            .WithCallback((_, _, _) => Assert.Fail("Unmatched callback executed."));

        test.RunInbound();

        document.Attempts.Should().Be(1);
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void Retry_ReturnResponseTerminatesWithoutRetryOrConditionEvaluation(string section)
    {
        var evaluations = 0;
        var document = new RetryDocument(CreateConfig() with
        {
            ConditionEvaluator = () =>
            {
                evaluations++;
                return true;
            }
        })
        {
            ReturnResponseOnAttempt = true
        };
        var test = document.AsTestDocument();
        var scheduler = RegisterScheduler(test);

        RunSection(test, section);

        document.Attempts.Should().Be(1);
        evaluations.Should().Be(0);
        scheduler.Delays.Should().BeEmpty();
        test.Context.Response.StatusCode.Should().Be(202);
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables.Should().NotContainKey("after-retry");
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void Retry_InvokeRequestPreservesSectionOnlyTermination(string section)
    {
        var document = new RetryDocument(CreateConfig()) { InvokeRequestOnAttempt = true };
        var test = document.AsTestDocument();
        test.Context.Services.Register<IHttpClient>(StubHttpClient.Ok("invoked"));
        var scheduler = RegisterScheduler(test);

        RunSection(test, section);

        document.Attempts.Should().Be(1);
        scheduler.Delays.Should().BeEmpty();
        test.Context.Response.Body.Content.Should().Be("invoked");
        test.Context.ResponseTerminated.Should().BeFalse();
        test.Context.Variables.Should().NotContainKey("after-retry");
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void Retry_ExecutesInFragmentsUsingCallingSectionHandlers(string section)
    {
        var fragment = new RetryFragment();
        var test = new FragmentHost().AsTestDocument().RegisterFragment("retry-fragment", fragment);
        var scheduler = RegisterScheduler(test);

        RunSection(test, section);

        fragment.Attempts.Should().Be(2);
        scheduler.Delays.Should().Equal(TimeSpan.FromSeconds(1));
        test.Context.Variables.Should().ContainKey("fragment-after-retry")
            .WhoseValue.Should().Be(true);
    }

    private static RetryConfig CreateConfig(int count = 3) => new()
    {
        Condition = true,
        Count = count,
        Interval = 1
    };

    private static RecordingRetryScheduler RegisterScheduler(
        TestDocument test, RecordingRetryScheduler? scheduler = null)
    {
        scheduler ??= new RecordingRetryScheduler();
        test.Context.Services.Register<IRetryScheduler>(scheduler);
        return scheduler;
    }

    private static void AssertInvalidConfig(RetryConfig config, string parameter)
    {
        var document = new RetryDocument(config);
        var test = document.AsTestDocument();
        var scheduler = RegisterScheduler(test);

        var act = () => test.RunInbound();

        var error = act.Should().Throw<PolicyException>().WithInnerException<ArgumentException>().Which;
        error.ParamName.Should().Be(parameter);
        document.Attempts.Should().Be(0);
        scheduler.Delays.Should().BeEmpty();
    }

    private static MockRetryProvider.Setup SetupRetry(TestDocument test, string section) => section switch
    {
        nameof(IInboundContext) => test.SetupInbound().Retry(),
        nameof(IBackendContext) => test.SetupBackend().Retry(),
        nameof(IOutboundContext) => test.SetupOutbound().Retry(),
        nameof(IOnErrorContext) => test.SetupOnError().Retry(),
        _ => throw new ArgumentException("Unknown section.", nameof(section))
    };

    private static void RunSection(TestDocument test, string section)
    {
        switch (section)
        {
            case nameof(IInboundContext): test.RunInbound(); break;
            case nameof(IBackendContext): test.RunBackend(); break;
            case nameof(IOutboundContext): test.RunOutbound(); break;
            case nameof(IOnErrorContext): test.RunOnError(); break;
            default: throw new ArgumentException("Unknown section.", nameof(section));
        }
    }

    private class RecordingRetryScheduler(Func<double>? jitter = null) : IRetryScheduler
    {
        public List<TimeSpan> Delays { get; } = [];
        public int JitterCalls { get; private set; }
        public Action<TimeSpan>? OnDelay { get; init; }

        public void Delay(TimeSpan delay)
        {
            Delays.Add(delay);
            OnDelay?.Invoke(delay);
        }

        public double NextJitterFactor()
        {
            JitterCalls++;
            return jitter?.Invoke() ?? 1;
        }
    }

    private class RetryDocument(RetryConfig config) : IDocument
    {
        public RetryConfig Config { get; set; } = config;
        public int Attempts { get; private set; }
        public Action? Child { get; init; }
        public bool ReturnResponseOnAttempt { get; init; }
        public bool InvokeRequestOnAttempt { get; init; }

        public void Inbound(IInboundContext context) =>
            Execute(context.Retry, context.SetVariable, context.ReturnResponse, context.InvokeRequest);

        public void Backend(IBackendContext context) =>
            Execute(context.Retry, context.SetVariable, context.ReturnResponse, context.InvokeRequest);

        public void Outbound(IOutboundContext context) =>
            Execute(context.Retry, context.SetVariable, context.ReturnResponse, context.InvokeRequest);

        public void OnError(IOnErrorContext context) =>
            Execute(context.Retry, context.SetVariable, context.ReturnResponse, context.InvokeRequest);

        private void Execute(
            Action<RetryConfig, Action> retry,
            Action<string, object> setVariable,
            Action<ReturnResponseConfig> returnResponse,
            Action<InvokeRequestConfig> invokeRequest)
        {
            retry(Config, () =>
            {
                Attempts++;
                setVariable("attempts", Attempts);
                Child?.Invoke();
                if (ReturnResponseOnAttempt)
                {
                    returnResponse(new ReturnResponseConfig
                    {
                        Status = new StatusConfig { Code = 202, Reason = "Accepted" }
                    });
                }

                if (InvokeRequestOnAttempt)
                {
                    invokeRequest(new InvokeRequestConfig { Url = "https://example.com/invoke" });
                }
            });
            setVariable("after-retry", true);
        }
    }

    private class RetryingSendRequest(bool ignoreError) : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.Retry(CreateConfig() with
            {
                FirstFastRetry = true,
                ConditionEvaluator = () =>
                    !context.ExpressionContext.Variables.TryGetValue("response", out var value)
                    || value is not IResponse response
                    || response.StatusCode >= 500
            }, () => context.SendRequest(new SendRequestConfig
            {
                ResponseVariableName = "response",
                Url = "https://example.com/retry",
                IgnoreError = ignoreError
            }));
        }

        public void Backend(IBackendContext context) { }
        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    private class RetryFragment : IFragment
    {
        public int Attempts { get; private set; }

        public void Fragment(IFragmentContext context)
        {
            context.Retry(CreateConfig(1), () =>
            {
                Attempts++;
                context.SetVariable("fragment-attempts", Attempts);
            });
            context.SetVariable("fragment-after-retry", true);
        }
    }

    private class NullRetrySection : IDocument
    {
        public void Inbound(IInboundContext context) => context.Retry(CreateConfig(), null!);
        public void Backend(IBackendContext context) { }
        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    private class FragmentHost : IDocument
    {
        public void Inbound(IInboundContext context) => context.IncludeFragment("retry-fragment");
        public void Backend(IBackendContext context) => context.IncludeFragment("retry-fragment");
        public void Outbound(IOutboundContext context) => context.IncludeFragment("retry-fragment");
        public void OnError(IOnErrorContext context) => context.IncludeFragment("retry-fragment");
    }
}
