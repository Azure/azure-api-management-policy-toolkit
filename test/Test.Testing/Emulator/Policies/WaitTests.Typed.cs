// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Net;
using System.Reflection;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Test.Emulator.Emulator.Policies;

public partial class WaitTests
{
    private static readonly TimeSpan s_waitTimeout = TimeSpan.FromSeconds(10);

    [TestMethod]
    [DataRow(nameof(IInboundContext), null, false)]
    [DataRow(nameof(IInboundContext), "all", false)]
    [DataRow(nameof(IBackendContext), null, false)]
    [DataRow(nameof(IBackendContext), "all", false)]
    [DataRow(nameof(IOutboundContext), null, false)]
    [DataRow(nameof(IOutboundContext), "all", false)]
    [DataRow(nameof(IOnErrorContext), null, false)]
    [DataRow(nameof(IOnErrorContext), "all", false)]
    [DataRow(nameof(IInboundContext), null, true)]
    [DataRow(nameof(IInboundContext), "all", true)]
    [DataRow(nameof(IBackendContext), null, true)]
    [DataRow(nameof(IBackendContext), "all", true)]
    [DataRow(nameof(IOutboundContext), null, true)]
    [DataRow(nameof(IOutboundContext), "all", true)]
    [DataRow(nameof(IOnErrorContext), null, true)]
    [DataRow(nameof(IOnErrorContext), "all", true)]
    public async Task TypedWait_AllStartsBothBranchesBeforeEitherCompletes(
        string section, string? waitFor, bool fragment)
    {
        var test = CreateTypedWait(waitFor, fragment);
        var client = new ControlledWaitHttpClient();
        test.Context.Services.Register<IHttpClient>(client);
        var untouched = new MockResponse { StatusCode = 418 };
        test.Context.Variables["untouched"] = untouched;
        test.Context.Request.Body.Content = "parent body";
        test.Context.Request.Headers["X-Input"] = ["parent"];
        var execution = RunWaitAsync(() => RunSection(test, section));

        try
        {
            await Task.WhenAll(client.First.Started.Task, client.Second.Started.Task).WaitAsync(s_waitTimeout);
            execution.IsCompleted.Should().BeFalse("both immediate children are still blocked");
            test.Context.Variables.Should().NotContainKey("first").And.NotContainKey("second");
            client.First.Body.Should().Be(section == nameof(IOutboundContext) ? null : "parent body");
            client.Second.Body.Should().Be(client.First.Body);
            client.First.InputHeader.Should().Be("parent");
            client.Second.InputHeader.Should().Be("parent");

            var first = client.First.Complete(HttpStatusCode.Created, "first body");
            await first.Disposed.Task.WaitAsync(s_waitTimeout);
            execution.IsCompleted.Should().BeFalse("all must still wait for the second child");
            test.Context.Variables.Should().NotContainKey("first").And.NotContainKey("second");

            client.Second.Complete(HttpStatusCode.Accepted, "second body");
            await execution.WaitAsync(s_waitTimeout);

            test.Context.Variables["first"].Should().BeOfType<MockResponse>().Which.StatusCode.Should().Be(201);
            test.Context.Variables["second"].Should().BeOfType<MockResponse>().Which.StatusCode.Should().Be(202);
            test.Context.Variables["untouched"].Should().BeSameAs(untouched);
            test.Context.Variables["after-wait"].Should().Be(true);
            test.Context.Request.Body.Content.Should().Be("parent body");
        }
        finally
        {
            client.ReleaseRemaining();
            await ObserveWaitExecution(execution);
        }
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext), false)]
    [DataRow(nameof(IBackendContext), false)]
    [DataRow(nameof(IOutboundContext), false)]
    [DataRow(nameof(IOnErrorContext), false)]
    [DataRow(nameof(IInboundContext), true)]
    [DataRow(nameof(IBackendContext), true)]
    [DataRow(nameof(IOutboundContext), true)]
    [DataRow(nameof(IOnErrorContext), true)]
    public async Task TypedWait_AnyCommitsOnlyWinnerAndCancelsUncooperativeLoser(string section, bool fragment)
    {
        var test = CreateTypedWait("any", fragment);
        var client = new ControlledWaitHttpClient();
        test.Context.Services.Register<IHttpClient>(client);
        var execution = RunWaitAsync(() => RunSection(test, section));

        try
        {
            await Task.WhenAll(client.First.Started.Task, client.Second.Started.Task).WaitAsync(s_waitTimeout);
            client.Second.Complete(HttpStatusCode.Accepted, "winner");
            await execution.WaitAsync(s_waitTimeout);

            client.First.Token.IsCancellationRequested.Should().BeTrue();
            client.First.Response.Task.IsCompleted.Should().BeFalse("the losing client deliberately ignores cancellation");
            test.Context.Variables.Should().NotContainKey("first").And.ContainKey("second");
            test.Context.Variables["second"].Should().BeOfType<MockResponse>()
                .Which.Body.Content.Should().Be("winner");
            test.Context.Variables["after-wait"].Should().Be(true);
            test.Context.Request.Body.Content = "after wait";

            var late = client.First.Complete(HttpStatusCode.Created, "late loser");
            await late.Disposed.Task.WaitAsync(s_waitTimeout);

            test.Context.Variables.Should().NotContainKey("first");
            test.Context.Variables["second"].Should().BeOfType<MockResponse>()
                .Which.Body.Content.Should().Be("winner");
            test.Context.Request.Body.Content.Should().Be("after wait");
        }
        finally
        {
            client.ReleaseRemaining();
            await ObserveWaitExecution(execution);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task TypedWait_AnyObservesFirstCompletionIncludingTransportFailure(bool ignoreError)
    {
        var document = new TypedWaitDocument("any")
        {
            InboundBranches =
            [
                branch => branch.SendRequest(WaitRequest("first") with { IgnoreError = ignoreError }),
                branch => branch.SendRequest(WaitRequest("second"))
            ]
        };
        var test = document.AsTestDocument();
        var client = new ControlledWaitHttpClient();
        var traces = new ConcurrentQueue<string>();
        test.Context.Trace = traces.Enqueue;
        test.Context.Services.Register<IHttpClient>(client);
        var failure = new HttpRequestException("first branch failed");
        var execution = RunWaitAsync(test.RunInbound);

        try
        {
            await Task.WhenAll(client.First.Started.Task, client.Second.Started.Task).WaitAsync(s_waitTimeout);
            client.First.Response.SetException(failure);
            if (ignoreError)
            {
                await execution.WaitAsync(s_waitTimeout);
                test.Context.Variables.Should().ContainKey("first").WhoseValue.Should().BeNull();
                test.Context.Variables["after-wait"].Should().Be(true);
                traces.Should().Contain(message => message.Contains("explicitly ignored"));
            }
            else
            {
                var error = await WaitFailure(execution);
                error.Policy.Should().Be(nameof(IInboundContext.SendRequest));
                error.InnerException.Should().BeSameAs(failure);
                test.Context.Variables.Should().NotContainKey("first").And.NotContainKey("after-wait");
            }

            client.Second.Token.IsCancellationRequested.Should().BeTrue();
            client.Second.Response.Task.IsCompleted.Should().BeFalse();
            var late = client.Second.Complete(HttpStatusCode.OK);
            await late.Disposed.Task.WaitAsync(s_waitTimeout);
            test.Context.Variables.Should().NotContainKey("second");
        }
        finally
        {
            client.ReleaseRemaining();
            await ObserveWaitExecution(execution);
        }
    }

    [TestMethod]
    public async Task TypedWait_AllObservesErrorsWithoutPartiallyCommittingSuccessfulChildren()
    {
        var test = new TypedWaitDocument("all").AsTestDocument();
        var client = new ControlledWaitHttpClient();
        test.Context.Services.Register<IHttpClient>(client);
        var failure = new IOException("first failed");
        var execution = RunWaitAsync(test.RunInbound);

        try
        {
            await Task.WhenAll(client.First.Started.Task, client.Second.Started.Task).WaitAsync(s_waitTimeout);
            client.First.Response.SetException(failure);
            execution.IsCompleted.Should().BeFalse();
            client.Second.Complete(HttpStatusCode.OK);

            var error = await WaitFailure(execution);

            error.InnerException.Should().BeSameAs(failure);
            test.Context.Variables.Should().NotContainKey("first")
                .And.NotContainKey("second").And.NotContainKey("after-wait");
        }
        finally
        {
            client.ReleaseRemaining();
            await ObserveWaitExecution(execution);
        }
    }

    [TestMethod]
    public async Task TypedWait_ChooseUsesItsOwnPriorLookupBeforeEvaluatingTheNestedCondition()
    {
        var document = new TypedWaitDocument("all")
        {
            InboundBranches =
            [
                branch =>
                {
                    if ((bool)branch.ExpressionContext.Variables["enabled"])
                    {
                        branch.CacheLookupValue(new CacheLookupValueConfig
                        {
                            Key = "first",
                            VariableName = "first",
                            CachingType = "external"
                        });
                        if (!branch.ExpressionContext.Variables.ContainsKey("first"))
                        {
                            branch.SendRequest(WaitRequest("first"));
                        }
                    }
                },
                branch => branch.CacheLookupValue(new CacheLookupValueConfig
                {
                    Key = "second",
                    VariableName = "second",
                    CachingType = "external"
                })
            ]
        };
        var test = document.AsTestDocument();
        var cache = new ControlledWaitCache();
        var client = new ControlledWaitHttpClient();
        test.Context.Services.Register<ICache>("external", cache);
        test.Context.Services.Register<IHttpClient>(client);
        test.Context.Variables["enabled"] = true;
        test.Context.Variables["first"] = "stale parent value";
        var execution = RunWaitAsync(test.RunInbound);

        try
        {
            await Task.WhenAll(cache.First.Started.Task, cache.Second.Started.Task).WaitAsync(s_waitTimeout);
            cache.First.Result.SetResult(null);
            await client.First.Started.Task.WaitAsync(s_waitTimeout);
            test.Context.Variables["first"].Should().Be("stale parent value");
            cache.Second.Result.SetResult("cached second");
            client.First.Complete(HttpStatusCode.Created);
            await execution.WaitAsync(s_waitTimeout);

            cache.First.Token.CanBeCanceled.Should().BeTrue();
            cache.Second.Token.CanBeCanceled.Should().BeTrue();
            test.Context.Variables["first"].Should().BeOfType<MockResponse>().Which.StatusCode.Should().Be(201);
            test.Context.Variables["second"].Should().Be("cached second");
            client.Second.Started.Task.IsCompleted.Should().BeFalse();
        }
        finally
        {
            cache.ReleaseRemaining();
            client.ReleaseRemaining();
            await ObserveWaitExecution(execution);
        }
    }

    [TestMethod]
    public void TypedWait_AllMergesOnlyChangedVariablesAndPropagatesRemovals()
    {
        var document = new InboundWaitAction(context => context.Wait("all",
            branch => branch.CacheLookupValue(new CacheLookupValueConfig { Key = "missing", VariableName = "removed" }),
            branch => branch.CacheLookupValue(new CacheLookupValueConfig { Key = "cached", VariableName = "changed" })));
        var test = document.AsTestDocument();
        test.Context.Variables["removed"] = "old";
        test.Context.Variables["changed"] = "old";
        test.Context.Variables["untouched"] = "keep";
        test.SetupCacheStore().WithInternalCacheValue("cached", "new");

        test.RunInbound();

        test.Context.Variables.Should().NotContainKey("removed");
        test.Context.Variables["changed"].Should().Be("new");
        test.Context.Variables["untouched"].Should().Be("keep");
    }

    [TestMethod]
    public void TypedWait_AnyCanCompleteAChooseWithoutProducingAnOutput()
    {
        var test = new InboundWaitAction(context => context.Wait("any", branch =>
        {
            if ((bool)branch.ExpressionContext.Variables["enabled"])
            {
                branch.SendRequest(WaitRequest("first"));
            }
        })).AsTestDocument();
        test.Context.Variables["enabled"] = false;

        test.RunInbound();

        test.Context.Variables.Should().NotContainKey("first");
        test.Context.Variables["enabled"].Should().Be(false);
    }

    [TestMethod]
    public void TypedWait_LegacyWaitMocksDoNotReplaceTypedExecution()
    {
        var test = new InboundWaitAction(context => context.Wait("all",
            branch => branch.CacheLookupValue(new CacheLookupValueConfig { Key = "first", VariableName = "first" })))
            .AsTestDocument();
        test.SetupInbound().Wait().WithCallback((_, _, _) => Assert.Fail("Legacy mock captured typed Wait."));
        test.SetupInbound().CacheLookupValue().WithValue("typed result");

        test.RunInbound();

        test.Context.Variables["first"].Should().Be("typed result");
    }

    [TestMethod]
    public void TypedWait_AllRejectsConflictingVariableOutputsWithoutChangingParent()
    {
        var document = new InboundWaitAction(context => context.Wait("all",
            branch => branch.CacheLookupValue(new CacheLookupValueConfig { Key = "one", VariableName = "same" }),
            branch => branch.CacheLookupValue(new CacheLookupValueConfig { Key = "two", VariableName = "same" })));
        var test = document.AsTestDocument();
        test.Context.Variables["same"] = "original";
        test.SetupCacheStore().WithInternalCacheValue("one", "first").WithInternalCacheValue("two", "second");

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.Policy.Should().Be(nameof(IInboundContext.Wait));
        error.InnerException.Should().BeOfType<InvalidOperationException>();
        error.Message.Should().Contain("same").And.Contain("conflict");
        test.Context.Variables["same"].Should().Be("original");
    }

    [TestMethod]
    public void TypedWait_ChildCallbacksUseSeparateContextsAndServiceRegistries()
    {
        var test = new TypedWaitDocument("all").AsTestDocument();
        var contexts = new ConcurrentDictionary<string, GatewayContext>();
        var sharedClient = StubHttpClient.Ok();
        var originalResponse = new MockResponse { StatusCode = 418 };
        originalResponse.Headers["X-Seed"] = ["original"];
        test.Context.Services.Register<IHttpClient>(sharedClient);
        test.Context.Variables["seed"] = originalResponse;
        test.Context.Request.Body.Content = "parent";
        test.Context.Api.Name = "snapshot api";
        test.Context.Operation.Name = "snapshot operation";
        test.Context.User.Note = "snapshot user";
        test.SetupInbound().SendRequest().WithCallback((branch, config) =>
        {
            branch.Should().NotBeSameAs(test.Context);
            branch.Request.Should().NotBeSameAs(test.Context.Request);
            branch.Response.Should().NotBeSameAs(test.Context.Response);
            branch.Services.Should().NotBeSameAs(test.Context.Services);
            branch.Api.Should().NotBeSameAs(test.Context.Api);
            branch.User.Should().NotBeSameAs(test.Context.User);
            branch.RequestId.Should().Be(test.Context.RequestId);
            branch.Timestamp.Should().Be(test.Context.Timestamp);
            branch.Api.Name.Should().Be("snapshot api");
            branch.Operation.Name.Should().Be("snapshot operation");
            branch.User.Note.Should().Be("snapshot user");
            branch.Services.Resolve<IHttpClient>().Should().BeSameAs(sharedClient);
            contexts.TryAdd(config.ResponseVariableName, branch).Should().BeTrue();
            branch.Request.Body.Content = config.ResponseVariableName;
            branch.Request.Headers["X-Branch"] = [config.ResponseVariableName];
            branch.Response.StatusCode = 503;
            branch.Services.Register<IHttpClient>(StubHttpClient.NotFound());
            if (config.ResponseVariableName == "first")
            {
                ((MockResponse)branch.Variables["seed"]).Headers["X-Seed"][0] = "changed";
            }
            branch.Variables[config.ResponseVariableName] = config.ResponseVariableName;
        });

        test.RunInbound();

        contexts["first"].Should().NotBeSameAs(contexts["second"]);
        contexts["first"].Variables.Should().NotBeSameAs(contexts["second"].Variables);
        test.Context.Request.Body.Content.Should().Be("parent");
        test.Context.Request.Headers.Should().NotContainKey("X-Branch");
        test.Context.Response.StatusCode.Should().Be(200);
        test.Context.Services.Resolve<IHttpClient>().Should().BeSameAs(sharedClient);
        originalResponse.Headers["X-Seed"].Should().Equal("original");
        test.Context.Variables["seed"].Should().BeOfType<MockResponse>()
            .Which.Headers["X-Seed"].Should().Equal("changed");
        test.Context.Variables["first"].Should().Be("first");
        test.Context.Variables["second"].Should().Be("second");
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void TypedWait_FragmentBranchesPreserveCacheValueAndCallbackSetups(string section)
    {
        var test = new FragmentHost().AsTestDocument()
            .RegisterFragment("wait-fragment", new TypedCacheWaitFragment());
        SetupWaitCache(test, section, (_, config) => config.Key == "first").WithValue("mocked first");
        GatewayContext? callbackContext = null;
        SetupWaitCache(test, section, (_, config) => config.Key == "second").WithCallback((branch, config) =>
        {
            callbackContext = branch;
            branch.Variables[config.VariableName] = "mocked second";
        });

        RunSection(test, section);

        callbackContext.Should().NotBeNull().And.NotBeSameAs(test.Context);
        test.Context.Variables["first"].Should().Be("mocked first");
        test.Context.Variables["second"].Should().Be("mocked second");
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void TypedWait_FragmentBranchErrorsRetainTheCallingSection(string section)
    {
        var test = CreateTypedWait("all", fragment: true);
        var expected = new InvalidOperationException("fragment child failed");
        switch (section)
        {
            case nameof(IInboundContext): test.SetupInbound().SendRequest().WithCallback((_, _) => throw expected); break;
            case nameof(IBackendContext): test.SetupBackend().SendRequest().WithCallback((_, _) => throw expected); break;
            case nameof(IOutboundContext): test.SetupOutbound().SendRequest().WithCallback((_, _) => throw expected); break;
            case nameof(IOnErrorContext): test.SetupOnError().SendRequest().WithCallback((_, _) => throw expected); break;
            default: throw new ArgumentException("Unknown section.", nameof(section));
        }

        var error = Assert.ThrowsExactly<PolicyException>(() => RunSection(test, section));

        error.Policy.Should().Be(nameof(IInboundContext.SendRequest));
        error.Section.Should().Be(section);
        error.InnerException.Should().BeSameAs(expected);
        test.Context.Variables.Should().NotContainKey("after-wait");
    }

    [TestMethod]
    public async Task TypedWait_AnyDoesNotShareCommittedObjectsOrPermitLateCallbackWrites()
    {
        var test = new TypedWaitDocument("any").AsTestDocument();
        var firstStarted = WaitSignal<bool>();
        var secondStarted = WaitSignal<bool>();
        var releaseFirst = WaitSignal<bool>();
        var releaseSecond = WaitSignal<bool>();
        var loserFinished = WaitSignal<bool>();
        GatewayContext? loser = null;
        MockResponse? winner = null;
        test.Context.Request.Body.Content = "parent";
        test.SetupInbound().SendRequest().WithCallback((branch, config) =>
        {
            if (config.ResponseVariableName == "first")
            {
                firstStarted.SetResult(true);
                releaseFirst.Task.GetAwaiter().GetResult();
                winner = new MockResponse { StatusCode = 201 };
                winner.Headers["X-Winner"] = ["before"];
                branch.Variables["first"] = winner;
            }
            else
            {
                loser = branch;
                secondStarted.SetResult(true);
                releaseSecond.Task.GetAwaiter().GetResult();
                branch.Variables["second"] = "late";
                branch.Request.Body.Content = "late";
                loserFinished.SetResult(true);
            }
        });
        var execution = RunWaitAsync(test.RunInbound);

        try
        {
            await Task.WhenAll(firstStarted.Task, secondStarted.Task).WaitAsync(s_waitTimeout);
            releaseFirst.SetResult(true);
            await execution.WaitAsync(s_waitTimeout);

            loser.Should().NotBeNull();
            loser!.Services.Resolve<HttpTransportState>()!.CancellationToken.IsCancellationRequested.Should().BeTrue();
            test.Context.Variables["first"].Should().NotBeSameAs(winner);
            winner!.Headers["X-Winner"][0] = "after";
            releaseSecond.SetResult(true);
            await loserFinished.Task.WaitAsync(s_waitTimeout);

            test.Context.Variables.Should().NotContainKey("second");
            test.Context.Variables["first"].Should().BeOfType<MockResponse>()
                .Which.Headers["X-Winner"].Should().Equal("before");
            test.Context.Request.Body.Content.Should().Be("parent");
        }
        finally
        {
            releaseFirst.TrySetResult(true);
            releaseSecond.TrySetResult(true);
            await ObserveWaitExecution(execution);
        }
    }

    [TestMethod]
    [DataRow("all", false)]
    [DataRow("any", false)]
    [DataRow("all", true)]
    [DataRow("any", true)]
    public async Task TypedWait_CallerCancellationReachesHttpAndCacheAndPreventsLateResults(
        string waitFor, bool ignoreHttpError)
    {
        using var cancellation = new CancellationTokenSource();
        var document = new InboundWaitAction(context => context.Wait(waitFor,
            branch => branch.SendRequest(WaitRequest("first") with { IgnoreError = ignoreHttpError }),
            branch => branch.CacheLookupValue(new CacheLookupValueConfig { Key = "second", VariableName = "second" })));
        var test = document.AsTestDocument();
        var client = new ControlledWaitHttpClient();
        var cache = new ControlledWaitCache();
        test.Context.Services.Register(new HttpTransportState { CancellationToken = cancellation.Token });
        test.Context.Services.Register<IHttpClient>(client);
        test.Context.Services.Register<ICache>(cache);
        var execution = RunWaitAsync(test.RunInbound);

        try
        {
            await Task.WhenAll(client.First.Started.Task, cache.Second.Started.Task).WaitAsync(s_waitTimeout);
            cancellation.Cancel();
            var error = await WaitFailure(execution);
            error.InnerException.Should().BeAssignableTo<OperationCanceledException>();
            client.First.Token.IsCancellationRequested.Should().BeTrue();
            cache.Second.Token.IsCancellationRequested.Should().BeTrue();
            test.Context.Variables.Should().NotContainKey("first").And.NotContainKey("second");

            cache.Second.Result.SetResult("late cache value");
            var late = client.First.Complete(HttpStatusCode.OK);
            await late.Disposed.Task.WaitAsync(s_waitTimeout);
            test.Context.Variables.Should().NotContainKey("first").And.NotContainKey("second");
        }
        finally
        {
            cancellation.Cancel();
            client.ReleaseRemaining();
            cache.ReleaseRemaining();
            await ObserveWaitExecution(execution);
        }
    }

    [TestMethod]
    [DataRow("all")]
    [DataRow("any")]
    public void TypedWait_PreCanceledCallerDoesNotStartBranches(string waitFor)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var executions = 0;
        var test = new InboundWaitAction(context => context.Wait(waitFor,
            _ => Interlocked.Increment(ref executions),
            _ => Interlocked.Increment(ref executions))).AsTestDocument();
        test.Context.Services.Register(new HttpTransportState { CancellationToken = cancellation.Token });

        Assert.ThrowsExactly<PolicyException>(test.RunInbound)
            .InnerException.Should().BeAssignableTo<OperationCanceledException>();

        executions.Should().Be(0);
    }

    [TestMethod]
    public async Task TypedWait_AnyTimeoutIsACompletionAndCancelsTheOtherBranch()
    {
        var document = new InboundWaitAction(context => context.Wait("any",
            branch => branch.SendRequest(WaitRequest("first") with { Timeout = 1 }),
            branch => branch.CacheLookupValue(new CacheLookupValueConfig { Key = "second", VariableName = "second" })));
        var test = document.AsTestDocument();
        var client = new ControlledWaitHttpClient();
        var cache = new ControlledWaitCache();
        test.Context.Services.Register<IHttpClient>(client);
        test.Context.Services.Register<ICache>(cache);
        var execution = RunWaitAsync(test.RunInbound);

        try
        {
            await Task.WhenAll(client.First.Started.Task, cache.Second.Started.Task).WaitAsync(s_waitTimeout);
            var error = await WaitFailure(execution);
            error.Policy.Should().Be(nameof(IInboundContext.SendRequest));
            error.InnerException.Should().BeAssignableTo<OperationCanceledException>();
            client.First.Token.IsCancellationRequested.Should().BeTrue();
            cache.Second.Token.IsCancellationRequested.Should().BeTrue();
            test.Context.Variables.Should().NotContainKey("first").And.NotContainKey("second");
        }
        finally
        {
            client.ReleaseRemaining();
            cache.ReleaseRemaining();
            await ObserveWaitExecution(execution);
        }
    }

    [TestMethod]
    public void TypedWait_ZeroTimeoutDoesNotInvokeClient()
    {
        var test = new InboundWaitAction(context => context.Wait("all",
            branch => branch.SendRequest(WaitRequest("first") with { Timeout = 0 }))).AsTestDocument();
        var client = new ControlledWaitHttpClient();
        test.Context.Services.Register<IHttpClient>(client);

        Assert.ThrowsExactly<PolicyException>(test.RunInbound)
            .InnerException.Should().BeAssignableTo<OperationCanceledException>();

        client.First.Started.Task.IsCompleted.Should().BeFalse();
        test.Context.Variables.Should().NotContainKey("first");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("ALL")]
    [DataRow("Any")]
    [DataRow("none")]
    [DataRow(" all")]
    [DataRow("all ")]
    public void TypedWait_RejectsInvalidModesBeforeCallbacksOrChildren(string waitFor)
    {
        var executions = 0;
        var test = new InboundWaitAction(context => context.Wait(waitFor,
            _ => executions++)).AsTestDocument();
        test.SetupInbound().WaitBranches().WithCallback((_, _, _) => Assert.Fail("Invalid mock executed."));

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeOfType<ArgumentException>()
            .Which.ParamName.Should().Be("waitFor");
        executions.Should().Be(0);
    }

    [TestMethod]
    [DataRow("null-array")]
    [DataRow("empty")]
    [DataRow("null-entry")]
    public void TypedWait_RejectsInvalidBranchArraysBeforeCallback(string invalid)
    {
        var branches = invalid switch
        {
            "null-array" => null!,
            "empty" => Array.Empty<Action<IInboundContext>>(),
            "null-entry" => new Action<IInboundContext>[] { null! },
            _ => throw new ArgumentException("Unknown case.", nameof(invalid))
        };
        var test = new InboundWaitAction(context => context.Wait("all", branches)).AsTestDocument();
        test.SetupInbound().WaitBranches().WithCallback((_, _, _) => Assert.Fail("Invalid mock executed."));

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeAssignableTo<ArgumentException>()
            .Which.ParamName.Should().Be("branches");
    }

    [TestMethod]
    public void TypedWait_ExplicitTypedCallbackReceivesOriginalBranchesAndCanSkipExecution()
    {
        var executions = 0;
        Action<IInboundContext>[] branches = [_ => executions++];
        var test = new InboundWaitAction(context => context.Wait("any", branches)).AsTestDocument();
        var callbacks = 0;
        test.SetupInbound().WaitBranches((_, _, mode) => mode == "all")
            .WithCallback((_, _, _) => Assert.Fail("Unmatched mock executed."));
        test.SetupInbound().WaitBranches((gateway, original, mode) =>
            ReferenceEquals(gateway, test.Context) && ReferenceEquals(original, branches) && mode == "any")
            .WithCallback((gateway, original, mode) =>
            {
                original.Should().BeSameAs(branches);
                mode.Should().Be("any");
                callbacks++;
                gateway.Variables["mocked"] = true;
            });

        test.RunInbound();

        executions.Should().Be(0);
        callbacks.Should().Be(1);
        test.Context.Variables["mocked"].Should().Be(true);
    }

    [TestMethod]
    public void TypedWait_PropagatesTypedCallbackErrors()
    {
        var test = new TypedWaitDocument("all").AsTestDocument();
        var expected = new InvalidOperationException("typed mock failed");
        test.SetupInbound().WaitBranches().WithCallback((_, _, _) => throw expected);

        Assert.ThrowsExactly<PolicyException>(test.RunInbound).InnerException.Should().BeSameAs(expected);
        test.Context.Variables.Should().NotContainKey("after-wait");
    }

    [TestMethod]
    [DataRow("set-variable")]
    [DataRow("nested-wait")]
    [DataRow("include-fragment")]
    public void TypedWait_RejectsPoliciesOutsideSupportedBranchOperations(string policy)
    {
        var test = new InboundWaitAction(context => context.Wait("all", branch =>
        {
            switch (policy)
            {
                case "set-variable": branch.SetVariable("illegal", true); break;
                case "nested-wait": branch.Wait("all", _ => { }); break;
                case "include-fragment": branch.IncludeFragment("unknown"); break;
                default: throw new ArgumentException("Unknown policy.", nameof(policy));
            }
        })).AsTestDocument();

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeOfType<NotSupportedException>();
        test.Context.Variables.Should().NotContainKey("illegal");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TypedWait_RejectsCapturedOuterProxiesIncludingExpressionAccess(bool expressionAccess)
    {
        var test = new InboundWaitAction(context => context.Wait("all", branch =>
        {
            if (expressionAccess)
            {
                _ = context.ExpressionContext.Variables;
            }
            else
            {
                context.SendRequest(WaitRequest("first"));
            }
        })).AsTestDocument();
        var client = new ControlledWaitHttpClient();
        test.Context.Services.Register<IHttpClient>(client);

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeOfType<InvalidOperationException>();
        error.Message.Should().Contain("branch");
        client.First.Started.Task.IsCompleted.Should().BeFalse();
    }

    [TestMethod]
    public void TypedWait_RejectsUnsupportedMutableInputRatherThanSharingIt()
    {
        var test = new TypedWaitDocument("all").AsTestDocument();
        var client = new ControlledWaitHttpClient();
        test.Context.Services.Register<IHttpClient>(client);
        var unsafeValue = new object();
        test.Context.Variables["unsafe"] = unsafeValue;

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeOfType<NotSupportedException>();
        error.Message.Should().Contain("isolat");
        test.Context.Variables["unsafe"].Should().BeSameAs(unsafeValue);
        client.First.Started.Task.IsCompleted.Should().BeFalse();
        client.Second.Started.Task.IsCompleted.Should().BeFalse();
    }

    [TestMethod]
    public void TypedWait_RetryNestingCannotBeHiddenByTypedCallback()
    {
        var test = new InboundWaitAction(context => context.Retry(
            new RetryConfig { Condition = false, Count = 1, Interval = 1 },
            () => context.Wait("all", _ => Assert.Fail("Nested branch executed.")))).AsTestDocument();
        test.SetupInbound().WaitBranches().WithCallback((_, _, _) => Assert.Fail("Nested callback executed."));

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.Policy.Should().Be(nameof(IInboundContext.Wait));
        error.InnerException.Should().BeOfType<InvalidOperationException>();
        error.Message.Should().Contain("retry");
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    [DataRow(nameof(IFragmentContext))]
    public void Wait_LegacyOverloadRemainsAvailableAndIsObsoleteWithoutBeingAnError(string section)
    {
        var interfaceType = section switch
        {
            nameof(IInboundContext) => typeof(IInboundContext),
            nameof(IBackendContext) => typeof(IBackendContext),
            nameof(IOutboundContext) => typeof(IOutboundContext),
            nameof(IOnErrorContext) => typeof(IOnErrorContext),
            nameof(IFragmentContext) => typeof(IFragmentContext),
            _ => throw new ArgumentException("Unknown section.", nameof(section))
        };
        var legacy = interfaceType.GetMethod(nameof(IInboundContext.Wait), [typeof(Action), typeof(string)]);

        legacy.Should().NotBeNull();
        var obsolete = legacy!.GetCustomAttribute<ObsoleteAttribute>();
        obsolete.Should().NotBeNull();
        obsolete!.IsError.Should().BeFalse();
    }

    private static TestDocument CreateTypedWait(string? waitFor, bool fragment) =>
        fragment
            ? new FragmentHost().AsTestDocument().RegisterFragment("wait-fragment", new TypedWaitFragment(waitFor))
            : new TypedWaitDocument(waitFor).AsTestDocument();

    private static SendRequestConfig WaitRequest(string name) => new()
    {
        Url = $"https://example.com/{name}",
        ResponseVariableName = name,
        Mode = "copy"
    };

    private static MockCacheLookupValueProvider.Setup SetupWaitCache(
        TestDocument test, string section, Func<GatewayContext, CacheLookupValueConfig, bool> predicate) => section switch
        {
            nameof(IInboundContext) => test.SetupInbound().CacheLookupValue(predicate),
            nameof(IBackendContext) => test.SetupBackend().CacheLookupValue(predicate),
            nameof(IOutboundContext) => test.SetupOutbound().CacheLookupValue(predicate),
            nameof(IOnErrorContext) => test.SetupOnError().CacheLookupValue(predicate),
            _ => throw new ArgumentException("Unknown section.", nameof(section))
        };

    private static TaskCompletionSource<T> WaitSignal<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static Task RunWaitAsync(Action execute) =>
        Task.Factory.StartNew(execute, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    private static async Task<PolicyException> WaitFailure(Task execution)
    {
        Func<Task> act = () => execution.WaitAsync(s_waitTimeout);
        return (await act.Should().ThrowAsync<PolicyException>()).Which;
    }

    private static Task ObserveWaitExecution(Task execution) =>
        execution.ContinueWith(completed => _ = completed.Exception, CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).WaitAsync(s_waitTimeout);

    private sealed class TypedWaitDocument(string? waitFor) : IDocument
    {
        public Action<IInboundContext>[] InboundBranches { get; init; } =
        [
            branch => branch.SendRequest(WaitRequest("first")),
            branch => branch.SendRequest(WaitRequest("second"))
        ];

        public void Inbound(IInboundContext context)
        {
            context.Wait(waitFor, InboundBranches);
            context.SetVariable("after-wait", true);
        }

        public void Backend(IBackendContext context)
        {
            context.Wait(waitFor,
                branch => branch.SendRequest(WaitRequest("first")),
                branch => branch.SendRequest(WaitRequest("second")));
            context.SetVariable("after-wait", true);
        }

        public void Outbound(IOutboundContext context)
        {
            context.Wait(waitFor,
                branch => branch.SendRequest(WaitRequest("first")),
                branch => branch.SendRequest(WaitRequest("second")));
            context.SetVariable("after-wait", true);
        }

        public void OnError(IOnErrorContext context)
        {
            context.Wait(waitFor,
                branch => branch.SendRequest(WaitRequest("first")),
                branch => branch.SendRequest(WaitRequest("second")));
            context.SetVariable("after-wait", true);
        }
    }

    private sealed class TypedWaitFragment(string? waitFor) : IFragment
    {
        public void Fragment(IFragmentContext context)
        {
            context.Wait(waitFor,
                branch => branch.SendRequest(WaitRequest("first")),
                branch => branch.SendRequest(WaitRequest("second")));
            context.SetVariable("after-wait", true);
        }
    }

    private sealed class TypedCacheWaitFragment : IFragment
    {
        public void Fragment(IFragmentContext context) => context.Wait("all",
            branch => branch.CacheLookupValue(new CacheLookupValueConfig { Key = "first", VariableName = "first" }),
            branch => branch.CacheLookupValue(new CacheLookupValueConfig { Key = "second", VariableName = "second" }));
    }

    private sealed class InboundWaitAction(Action<IInboundContext> execute) : IDocument
    {
        public void Inbound(IInboundContext context) => execute(context);
        public void Backend(IBackendContext context) { }
        public void Outbound(IOutboundContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    private sealed class ControlledWaitHttpClient : IHttpClient
    {
        public PendingWaitRequest First { get; } = new();
        public PendingWaitRequest Second { get; } = new();

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            var pending = request.RequestUri!.AbsolutePath switch
            {
                "/first" => First,
                "/second" => Second,
                _ => throw new InvalidOperationException("Unexpected wait child request.")
            };
            pending.Token = cancellationToken;
            pending.Body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            pending.InputHeader = request.Headers.TryGetValues("X-Input", out var values) ? values.Single() : null;
            pending.Started.SetResult(true);
            return pending.Response.Task;
        }

        public void ReleaseRemaining()
        {
            First.Response.TrySetCanceled();
            Second.Response.TrySetCanceled();
        }
    }

    private sealed class PendingWaitRequest
    {
        public TaskCompletionSource<bool> Started { get; } = WaitSignal<bool>();
        public TaskCompletionSource<HttpResponseMessage> Response { get; } = WaitSignal<HttpResponseMessage>();
        public CancellationToken Token { get; set; }
        public string? Body { get; set; }
        public string? InputHeader { get; set; }

        public WaitHttpResponse Complete(HttpStatusCode status, string? body = null)
        {
            var response = new WaitHttpResponse(status) { Content = body is null ? null : new StringContent(body) };
            Response.SetResult(response);
            return response;
        }
    }

    private sealed class WaitHttpResponse(HttpStatusCode status) : HttpResponseMessage(status)
    {
        public TaskCompletionSource<bool> Disposed { get; } = WaitSignal<bool>();

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Disposed.TrySetResult(true);
        }
    }

    private sealed class ControlledWaitCache : ICache
    {
        public PendingWaitCache First { get; } = new();
        public PendingWaitCache Second { get; } = new();

        public Task<object?> GetAsync(string key, CancellationToken ct = default)
        {
            var pending = key switch
            {
                "first" => First,
                "second" => Second,
                _ => throw new InvalidOperationException("Unexpected wait child cache key.")
            };
            pending.Token = ct;
            pending.Started.SetResult(true);
            return pending.Result.Task;
        }

        public Task SetAsync(string key, object value, TimeSpan ttl, CancellationToken ct = default) =>
            throw new NotSupportedException("This test cache supports only lookup.");

        public Task RemoveAsync(string key, CancellationToken ct = default) =>
            throw new NotSupportedException("This test cache supports only lookup.");

        public Task<CacheValueResult> GetOrCreateAsync(string key, TimeSpan expiresAfter, TimeSpan? refreshAfter,
            Func<object?, CancellationToken, Task<object?>> valueFactory, CancellationToken ct = default) =>
            throw new NotSupportedException("This test cache supports only lookup.");

        public Task<CacheValueResult> GetOrCreateWithDynamicTtlAsync(string key,
            Func<object?, CancellationToken, Task<CacheValueFactoryResult>> valueFactory,
            bool forceRefresh = false, CancellationToken ct = default) =>
            throw new NotSupportedException("This test cache supports only lookup.");

        public void ReleaseRemaining()
        {
            First.Result.TrySetCanceled();
            Second.Result.TrySetCanceled();
        }
    }

    private sealed class PendingWaitCache
    {
        public TaskCompletionSource<bool> Started { get; } = WaitSignal<bool>();
        public TaskCompletionSource<object?> Result { get; } = WaitSignal<object?>();
        public CancellationToken Token { get; set; }
    }
}