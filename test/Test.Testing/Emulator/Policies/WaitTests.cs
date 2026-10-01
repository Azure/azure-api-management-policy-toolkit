// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public partial class WaitTests
{
    [TestMethod]
    [DataRow(nameof(IInboundContext), null)]
    [DataRow(nameof(IInboundContext), "all")]
    [DataRow(nameof(IInboundContext), "any")]
    [DataRow(nameof(IBackendContext), null)]
    [DataRow(nameof(IBackendContext), "all")]
    [DataRow(nameof(IBackendContext), "any")]
    [DataRow(nameof(IOutboundContext), null)]
    [DataRow(nameof(IOutboundContext), "all")]
    [DataRow(nameof(IOutboundContext), "any")]
    [DataRow(nameof(IOnErrorContext), null)]
    [DataRow(nameof(IOnErrorContext), "all")]
    [DataRow(nameof(IOnErrorContext), "any")]
    public void Wait_DefaultFailsExplicitlyWithoutExecutingSequentially(string section, string? waitFor)
    {
        var document = new WaitDocument(waitFor);
        var test = document.AsTestDocument();

        var act = () => RunSection(test, section);

        var error = act.Should().Throw<PolicyException>().Which;
        error.Policy.Should().Be(nameof(IInboundContext.Wait));
        error.Section.Should().Be(section);
        error.InnerException.Should().BeOfType<NotSupportedException>();
        error.Message.Should().Contain("immediate child").And.Contain("isolation").And.Contain("cancellation");
        document.Executions.Should().Be(0);
        test.Context.Variables.Should().NotContainKey("after-wait");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("ALL")]
    [DataRow("Any")]
    [DataRow("none")]
    [DataRow(" all")]
    [DataRow("all ")]
    public void Wait_RejectsInvalidWaitFor(string waitFor)
    {
        var document = new WaitDocument(waitFor);
        var test = document.AsTestDocument();

        var act = () => test.RunInbound();

        var error = act.Should().Throw<PolicyException>().WithInnerException<ArgumentException>().Which;
        error.ParamName.Should().Be("waitFor");
        document.Executions.Should().Be(0);
    }

    [TestMethod]
    public void Wait_InvalidModeCannotBeHiddenByCallback()
    {
        var document = new WaitDocument("invalid");
        var test = document.AsTestDocument();
        var callbackExecuted = false;
        test.SetupInbound().Wait().WithCallback((_, _, _) => callbackExecuted = true);

        var act = () => test.RunInbound();

        act.Should().Throw<PolicyException>().WithInnerException<ArgumentException>();
        callbackExecuted.Should().BeFalse();
        document.Executions.Should().Be(0);
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext), null)]
    [DataRow(nameof(IInboundContext), "all")]
    [DataRow(nameof(IInboundContext), "any")]
    [DataRow(nameof(IBackendContext), null)]
    [DataRow(nameof(IBackendContext), "all")]
    [DataRow(nameof(IBackendContext), "any")]
    [DataRow(nameof(IOutboundContext), null)]
    [DataRow(nameof(IOutboundContext), "all")]
    [DataRow(nameof(IOutboundContext), "any")]
    [DataRow(nameof(IOnErrorContext), null)]
    [DataRow(nameof(IOnErrorContext), "all")]
    [DataRow(nameof(IOnErrorContext), "any")]
    public void Wait_ExplicitCallbackReceivesArgumentsInEveryAuthoredSection(string section, string? waitFor)
    {
        var document = new WaitDocument(waitFor);
        var test = document.AsTestDocument();
        var callbacks = 0;
        SetupWait(test, section).WithCallback((gateway, child, mode) =>
        {
            gateway.Should().BeSameAs(test.Context);
            mode.Should().Be(waitFor);
            callbacks++;
            child();
        });

        RunSection(test, section);

        callbacks.Should().Be(1);
        document.Executions.Should().Be(1);
        test.Context.Variables.Should().ContainKey("after-wait")
            .WhoseValue.Should().Be(true);
    }

    [TestMethod]
    public void Wait_CallbackCanSkipChildren()
    {
        var document = new WaitDocument("all");
        var test = document.AsTestDocument();
        test.SetupInbound().Wait().WithCallback((gateway, _, _) => gateway.Variables["mocked"] = true);

        test.RunInbound();

        document.Executions.Should().Be(0);
        test.Context.Variables.Should().ContainKey("mocked")
            .WhoseValue.Should().Be(true);
    }

    [TestMethod]
    public void Wait_SelectsFirstMatchingPredicate()
    {
        var document = new WaitDocument("any");
        var test = document.AsTestDocument();
        var callbacks = 0;
        test.SetupInbound().Wait((_, _, mode) => mode == "all")
            .WithCallback((_, _, _) => Assert.Fail("Unmatched callback executed."));
        test.SetupInbound().Wait((_, _, mode) => mode == "any")
            .WithCallback((_, _, _) => callbacks++);
        test.SetupInbound().Wait().WithCallback((_, _, _) => Assert.Fail("A later callback executed."));

        test.RunInbound();

        callbacks.Should().Be(1);
        document.Executions.Should().Be(0);
    }

    [TestMethod]
    public void Wait_UnmatchedCallbackDoesNotEnableSequentialFallback()
    {
        var document = new WaitDocument("all");
        var test = document.AsTestDocument();
        test.SetupInbound().Wait((_, _, mode) => mode == "any")
            .WithCallback((_, child, _) => child());

        var act = () => test.RunInbound();

        act.Should().Throw<PolicyException>().WithInnerException<NotSupportedException>();
        document.Executions.Should().Be(0);
    }

    [TestMethod]
    public void Wait_PropagatesCallbackErrors()
    {
        var document = new WaitDocument("all");
        var test = document.AsTestDocument();
        var expected = new InvalidOperationException("wait mock failed");
        test.SetupInbound().Wait().WithCallback((_, _, _) => throw expected);

        var act = () => test.RunInbound();

        var error = act.Should().Throw<PolicyException>().Which;
        error.Policy.Should().Be(nameof(IInboundContext.Wait));
        error.InnerException.Should().BeSameAs(expected);
        document.Executions.Should().Be(0);
        test.Context.Variables.Should().NotContainKey("after-wait");
    }

    [TestMethod]
    public void Wait_PreservesCallbackTermination()
    {
        var document = new WaitDocument("all");
        var test = document.AsTestDocument();
        test.SetupInbound().Wait().WithCallback((gateway, _, _) =>
        {
            gateway.Response.StatusCode = 202;
            throw new FinishSectionProcessingException();
        });

        test.RunInbound();

        test.Context.Response.StatusCode.Should().Be(202);
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables.Should().NotContainKey("after-wait");
    }

    [TestMethod]
    public void Wait_RejectsNullSectionBeforeCallback()
    {
        var test = new NullWaitSection().AsTestDocument();
        test.SetupInbound().Wait().WithCallback((_, _, _) => Assert.Fail("Invalid callback executed."));

        var act = () => test.RunInbound();

        var error = act.Should().Throw<PolicyException>().WithInnerException<ArgumentNullException>().Which;
        error.ParamName.Should().Be("section");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Wait_CannotBeNestedInRetryEvenWithCallbackOverrides(bool overrideRetry)
    {
        var document = new RetryWithWait();
        var test = document.AsTestDocument();
        var callbackExecuted = false;
        test.SetupInbound().Wait().WithCallback((_, _, _) => callbackExecuted = true);
        if (overrideRetry)
        {
            test.SetupInbound().Retry().WithCallback((_, _, child) => child());
        }

        var act = () => test.RunInbound();

        var error = act.Should().Throw<PolicyException>().Which;
        error.Policy.Should().Be(nameof(IInboundContext.Wait));
        error.InnerException.Should().BeOfType<InvalidOperationException>();
        error.Message.Should().Contain("nested").And.Contain("retry");
        callbackExecuted.Should().BeFalse();
        document.Executions.Should().Be(0);
    }

    [TestMethod]
    public void Wait_RetryScopeIsRestoredAfterFailure()
    {
        var document = new RetryWithWait();
        var test = document.AsTestDocument();
        var callbacks = 0;
        test.SetupInbound().Wait().WithCallback((_, child, _) =>
        {
            callbacks++;
            child();
        });

        var act = () => test.RunInbound();
        act.Should().Throw<PolicyException>().WithInnerException<InvalidOperationException>();
        document.UseRetry = false;
        test.RunInbound();

        callbacks.Should().Be(1);
        document.Executions.Should().Be(1);
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void Wait_CallbackWorksInsideFragments(string section)
    {
        var fragment = new WaitFragment();
        var test = new FragmentHost().AsTestDocument().RegisterFragment("wait-fragment", fragment);
        SetupWait(test, section).WithCallback((_, child, mode) =>
        {
            mode.Should().Be("all");
            child();
        });

        RunSection(test, section);

        fragment.Executions.Should().Be(1);
    }

    [TestMethod]
    [DataRow("all")]
    [DataRow("any")]
    public void Wait_DefaultRejectsRequestsBeforeChildExecutionOrFalseCompletion(string waitFor)
    {
        var document = new WaitRequests(waitFor);
        var test = document.AsTestDocument();
        var requests = new List<string>();
        SetupHttpClient(test, requests);

        var act = () => test.RunInbound();

        var error = act.Should().Throw<PolicyException>().Which;
        error.Policy.Should().Be(nameof(IInboundContext.Wait));
        error.Section.Should().Be(nameof(IInboundContext));
        error.InnerException.Should().BeOfType<NotSupportedException>();
        requests.Should().BeEmpty();
        document.ChildDelegateExecutions.Should().Be(0);
        document.Completed.Should().BeFalse();
        test.Context.Variables.Should().NotContainKey("first")
            .And.NotContainKey("second").And.NotContainKey("after-wait");
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("all")]
    [DataRow("any")]
    public void Wait_ExplicitMockSuppliesResponsesWithoutExecutingChildPolicies(string waitFor)
    {
        var document = new WaitRequests(waitFor);
        var test = document.AsTestDocument();
        var requests = new List<string>();
        SetupHttpClient(test, requests);
        var callbacks = 0;
        test.SetupInbound().Wait().WithCallback((gateway, _, mode) =>
        {
            mode.Should().Be(waitFor);
            callbacks++;
            gateway.Variables["first"] = new MockResponse { StatusCode = 201 };
            if (mode == "all")
            {
                gateway.Variables["second"] = new MockResponse { StatusCode = 202 };
            }
        });

        test.RunInbound();

        callbacks.Should().Be(1);
        requests.Should().BeEmpty();
        document.ChildDelegateExecutions.Should().Be(0);
        document.Completed.Should().BeTrue();
        test.Context.Variables.Should().ContainKey("after-wait")
            .WhoseValue.Should().Be(true);
        test.Context.Variables["first"].Should().BeOfType<MockResponse>()
            .Which.StatusCode.Should().Be(201);
        if (waitFor == "all")
        {
            test.Context.Variables["second"].Should().BeOfType<MockResponse>()
                .Which.StatusCode.Should().Be(202);
        }
        else
        {
            test.Context.Variables.Should().NotContainKey("second");
        }
    }

    [TestMethod]
    [DataRow("all")]
    [DataRow("any")]
    public void Wait_ExplicitCallbackCanDeliberatelyInvokeChildrenSynchronously(string waitFor)
    {
        var document = new WaitRequests(waitFor);
        var test = document.AsTestDocument();
        var events = new List<string>();
        SetupHttpClient(test, events);
        test.SetupInbound().Wait().WithCallback((_, child, mode) =>
        {
            mode.Should().Be(waitFor);
            events.Add("callback");
            child();
            events.Add("callback-complete");
        });

        test.RunInbound();

        events.Should().Equal("callback", "/first", "/second", "callback-complete");
        document.ChildDelegateExecutions.Should().Be(1);
        document.Completed.Should().BeTrue();
        test.Context.Variables.Should().ContainKey("after-wait")
            .WhoseValue.Should().Be(true);
        test.Context.Variables["first"].Should().BeOfType<MockResponse>()
            .Which.StatusCode.Should().Be(201);
        test.Context.Variables["second"].Should().BeOfType<MockResponse>()
            .Which.StatusCode.Should().Be(202);
    }

    private static void SetupHttpClient(TestDocument test, List<string> requests)
    {
        test.Context.Services.Register<IHttpClient>(new StubHttpClient(request =>
        {
            var path = request.RequestUri?.AbsolutePath
                ?? throw new InvalidOperationException("Child request URI is required.");
            requests.Add(path);
            return new HttpResponseMessage(path switch
            {
                "/first" => System.Net.HttpStatusCode.Created,
                "/second" => System.Net.HttpStatusCode.Accepted,
                _ => throw new InvalidOperationException($"Unexpected child request '{path}'.")
            });
        }));
    }

    private static MockWaitProvider.Setup SetupWait(TestDocument test, string section) => section switch
    {
        nameof(IInboundContext) => test.SetupInbound().Wait(),
        nameof(IBackendContext) => test.SetupBackend().Wait(),
        nameof(IOutboundContext) => test.SetupOutbound().Wait(),
        nameof(IOnErrorContext) => test.SetupOnError().Wait(),
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

#pragma warning disable CS0618
    private class WaitDocument(string? waitFor) : IDocument
    {
        public int Executions { get; private set; }

        public void Inbound(IInboundContext context) =>
            Execute(child => context.Wait(child, waitFor), context.SetVariable);

        public void Backend(IBackendContext context) =>
            Execute(child => context.Wait(child, waitFor), context.SetVariable);

        public void Outbound(IOutboundContext context) =>
            Execute(child => context.Wait(child, waitFor), context.SetVariable);

        public void OnError(IOnErrorContext context) =>
            Execute(child => context.Wait(child, waitFor), context.SetVariable);

        private void Execute(Action<Action> wait, Action<string, object> setVariable)
        {
            wait(() => Executions++);
            setVariable("after-wait", true);
        }
    }

    private class NullWaitSection : IDocument
    {
        public void Inbound(IInboundContext context) => context.Wait(null!, "all");
        public void Backend(IBackendContext context) { }
        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    private class RetryWithWait : IDocument
    {
        public bool UseRetry { get; set; } = true;
        public int Executions { get; private set; }

        public void Inbound(IInboundContext context)
        {
            void Wait() => context.Wait(() => Executions++, "all");
            if (UseRetry)
            {
                context.Retry(new RetryConfig { Condition = false, Count = 1, Interval = 1 }, Wait);
            }
            else
            {
                Wait();
            }
        }

        public void Backend(IBackendContext context) { }
        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    private class WaitFragment : IFragment
    {
        public int Executions { get; private set; }
        public void Fragment(IFragmentContext context) => context.Wait(() => Executions++, "all");
    }

    private class FragmentHost : IDocument
    {
        public void Inbound(IInboundContext context) => context.IncludeFragment("wait-fragment");
        public void Backend(IBackendContext context) => context.IncludeFragment("wait-fragment");
        public void Outbound(IOutboundContext context) => context.IncludeFragment("wait-fragment");
        public void OnError(IOnErrorContext context) => context.IncludeFragment("wait-fragment");
    }

    private class WaitRequests(string waitFor) : IDocument
    {
        public int ChildDelegateExecutions { get; private set; }
        public bool Completed { get; private set; }

        public void Inbound(IInboundContext context)
        {
            context.Wait(() =>
            {
                ChildDelegateExecutions++;
                context.SendRequest(new SendRequestConfig
                {
                    Url = "https://example.com/first",
                    ResponseVariableName = "first"
                });
                context.SendRequest(new SendRequestConfig
                {
                    Url = "https://example.com/second",
                    ResponseVariableName = "second"
                });
            }, waitFor);
            context.SetVariable("after-wait", true);
            Completed = true;
        }

        public void Backend(IBackendContext context) { }
        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
    }
#pragma warning restore CS0618
}