// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

namespace Test.Emulator.Emulator.Policies;

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
    public void WaitReview_FragmentTypedOverrideReceivesOriginalDelegatesBeforeDefaultExecution(
        string section, string? mode)
    {
        var fragment = new ReviewTypedWaitFragment(mode);
        var test = new FragmentHost().AsTestDocument().RegisterFragment("wait-fragment", fragment);
        var opaque = new object();
        test.Context.Variables["opaque"] = opaque;
        SetupWaitFragmentBranches(test, section).WithCallback((gateway, branches, authoredMode) =>
        {
            gateway.Should().BeSameAs(test.Context);
            branches.Should().BeSameAs(fragment.Branches);
            branches.Should().HaveCount(1);
            authoredMode.Should().Be(mode);
            gateway.Variables["fragment-override"] = true;
        });
        SetupPipelineWaitBranches(test, section).WithCallback((_, _, _) =>
            Assert.Fail("A pipeline-typed callback received fragment-typed delegates."));

        RunSection(test, section);

        fragment.Executions.Should().Be(0);
        test.Context.Variables["fragment-override"].Should().Be(true);
        test.Context.Variables["fragment-after-wait"].Should().Be(true);
        test.Context.Variables["opaque"].Should().BeSameAs(opaque);
        test.Context.Variables.Should().NotContainKey("fragment-default");
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void WaitReview_FragmentOverridesSelectFirstMatchingPredicate(string section)
    {
        var fragment = new ReviewTypedWaitFragment("any");
        var test = new FragmentHost().AsTestDocument().RegisterFragment("wait-fragment", fragment);
        var callbacks = 0;
        SetupWaitFragmentBranches(test, section, (_, _, mode) => mode == "all")
            .WithCallback((_, _, _) => Assert.Fail("An unmatched fragment predicate executed."));
        SetupWaitFragmentBranches(test, section, (gateway, branches, mode) =>
                ReferenceEquals(gateway, test.Context) && ReferenceEquals(branches, fragment.Branches) && mode == "any")
            .WithCallback((_, _, _) => callbacks++);
        SetupWaitFragmentBranches(test, section).WithCallback((_, _, _) => Assert.Fail("A later callback executed."));

        RunSection(test, section);

        callbacks.Should().Be(1);
        fragment.Executions.Should().Be(0);
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext))]
    [DataRow(nameof(IBackendContext))]
    [DataRow(nameof(IOutboundContext))]
    [DataRow(nameof(IOnErrorContext))]
    public void WaitReview_FragmentCallbackErrorsPreserveTheCallingSection(string section)
    {
        var fragment = new ReviewTypedWaitFragment("all");
        var test = new FragmentHost().AsTestDocument().RegisterFragment("wait-fragment", fragment);
        var expected = new InvalidOperationException("fragment override failed");
        SetupWaitFragmentBranches(test, section).WithCallback((_, _, _) => throw expected);

        var error = Assert.ThrowsExactly<PolicyException>(() => RunSection(test, section));

        error.Policy.Should().Be(nameof(IFragmentContext.Wait));
        error.Section.Should().Be(section);
        error.InnerException.Should().BeSameAs(expected);
        fragment.Executions.Should().Be(0);
        test.Context.Variables.Should().NotContainKey("fragment-after-wait");
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext), "null-array")]
    [DataRow(nameof(IInboundContext), "empty")]
    [DataRow(nameof(IInboundContext), "null-entry")]
    [DataRow(nameof(IBackendContext), "null-array")]
    [DataRow(nameof(IBackendContext), "empty")]
    [DataRow(nameof(IBackendContext), "null-entry")]
    [DataRow(nameof(IOutboundContext), "null-array")]
    [DataRow(nameof(IOutboundContext), "empty")]
    [DataRow(nameof(IOutboundContext), "null-entry")]
    [DataRow(nameof(IOnErrorContext), "null-array")]
    [DataRow(nameof(IOnErrorContext), "empty")]
    [DataRow(nameof(IOnErrorContext), "null-entry")]
    public void WaitReview_FragmentOverrideCannotHideInvalidBranchArrays(string section, string invalid)
    {
        var fragment = new ReviewTypedWaitFragment("all")
        {
            Branches = invalid switch
            {
                "null-array" => null,
                "empty" => [],
                "null-entry" => [null!],
                _ => throw new ArgumentException("Unknown case.", nameof(invalid))
            }
        };
        var test = new FragmentHost().AsTestDocument().RegisterFragment("wait-fragment", fragment);
        SetupWaitFragmentBranches(test, section).WithCallback((_, _, _) => Assert.Fail("Invalid override executed."));

        var error = Assert.ThrowsExactly<PolicyException>(() => RunSection(test, section));

        error.InnerException.Should().BeAssignableTo<ArgumentException>().Which.ParamName.Should().Be("branches");
        fragment.Executions.Should().Be(0);
    }

    [TestMethod]
    [DataRow(nameof(IInboundContext), true)]
    [DataRow(nameof(IInboundContext), false)]
    [DataRow(nameof(IBackendContext), true)]
    [DataRow(nameof(IBackendContext), false)]
    [DataRow(nameof(IOutboundContext), true)]
    [DataRow(nameof(IOutboundContext), false)]
    [DataRow(nameof(IOnErrorContext), true)]
    [DataRow(nameof(IOnErrorContext), false)]
    public void WaitReview_FragmentAndPipelineDelegateTypesCannotCaptureEachOthersOverrides(
        string section, bool executeFragment)
    {
        if (executeFragment)
        {
            var fragment = new ReviewTypedWaitFragment("all");
            var test = new FragmentHost().AsTestDocument().RegisterFragment("wait-fragment", fragment);
            SetupPipelineWaitBranches(test, section).WithCallback((_, _, _) =>
                Assert.Fail("Pipeline override must not capture a fragment invocation."));

            RunSection(test, section);

            fragment.Executions.Should().Be(1);
            test.Context.Variables["fragment-default"].Should().Be("executed");
        }
        else
        {
            var test = CreateTypedWait("all", fragment: false);
            SetupWaitFragmentBranches(test, section).WithCallback((_, _, _) =>
                Assert.Fail("Fragment override must not capture a pipeline invocation."));
            SetupFullWaitSend(test, section, (gateway, config) => gateway.Variables[config.ResponseVariableName] = true);

            RunSection(test, section);

            test.Context.Variables["first"].Should().Be(true);
            test.Context.Variables["second"].Should().Be(true);
        }
    }

    private static MockWaitProvider.BranchSetup<IFragmentContext> SetupWaitFragmentBranches(
        TestDocument test, string section,
        Func<GatewayContext, Action<IFragmentContext>[], string?, bool>? predicate = null) => section switch
        {
            nameof(IInboundContext) => test.SetupInbound().WaitFragmentBranches(predicate),
            nameof(IBackendContext) => test.SetupBackend().WaitFragmentBranches(predicate),
            nameof(IOutboundContext) => test.SetupOutbound().WaitFragmentBranches(predicate),
            nameof(IOnErrorContext) => test.SetupOnError().WaitFragmentBranches(predicate),
            _ => throw new ArgumentException("Unknown section.", nameof(section))
        };

    private static MockWaitProvider.BranchSetup<T> PipelineWaitBranches<T>(MockPoliciesProvider<T> provider)
        where T : class => provider.WaitBranches();

    private static ReviewPipelineWaitSetup SetupPipelineWaitBranches(TestDocument test, string section) => section switch
    {
        nameof(IInboundContext) => new(callback => PipelineWaitBranches(test.SetupInbound())
            .WithCallback((gateway, branches, mode) => callback(gateway, branches, mode))),
        nameof(IBackendContext) => new(callback => PipelineWaitBranches(test.SetupBackend())
            .WithCallback((gateway, branches, mode) => callback(gateway, branches, mode))),
        nameof(IOutboundContext) => new(callback => PipelineWaitBranches(test.SetupOutbound())
            .WithCallback((gateway, branches, mode) => callback(gateway, branches, mode))),
        nameof(IOnErrorContext) => new(callback => PipelineWaitBranches(test.SetupOnError())
            .WithCallback((gateway, branches, mode) => callback(gateway, branches, mode))),
        _ => throw new ArgumentException("Unknown section.", nameof(section))
    };

    private sealed class ReviewPipelineWaitSetup(Action<Action<GatewayContext, Array, string?>> configure)
    {
        internal void WithCallback(Action<GatewayContext, Array, string?> callback) => configure(callback);
    }

    private sealed class ReviewTypedWaitFragment : IFragment
    {
        private readonly string? _mode;
        public Action<IFragmentContext>[]? Branches { get; set; }
        public int Executions { get; private set; }

        internal ReviewTypedWaitFragment(string? mode)
        {
            _mode = mode;
            Branches =
            [
                branch =>
                {
                    Executions++;
                    branch.CacheLookupValue(new CacheLookupValueConfig
                    {
                        Key = "fragment-key", VariableName = "fragment-default", DefaultValue = "executed"
                    });
                }
            ];
        }

        public void Fragment(IFragmentContext context)
        {
            context.Wait(_mode, Branches!);
            context.SetVariable("fragment-after-wait", true);
        }
    }
}