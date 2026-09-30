// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class IncludeFragmentTests
{
    private sealed class IncludeDocument(string? id) : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.WithId("include").IncludeFragment(id!);
            context.SetVariable("after", true);
        }

        public void Backend(IBackendContext context)
        {
            context.WithId("include").IncludeFragment(id!);
            context.SetVariable("after", true);
        }

        public void Outbound(IOutboundContext context)
        {
            context.WithId("include").IncludeFragment(id!);
            context.SetVariable("after", true);
        }

        public void OnError(IOnErrorContext context)
        {
            context.WithId("include").IncludeFragment(id!);
            context.SetVariable("after", true);
        }
    }

    internal sealed class CallbackFragment(Action<IFragmentContext> action) : IFragment
    {
        public void Fragment(IFragmentContext context) => action(context);
    }

    [Document("execution-reflection", Type = DocumentType.Fragment)]
    public sealed class ReflectedFragment : IFragment
    {
        public void Fragment(IFragmentContext context) => context.SetVariable("reflected", true);
    }

    [Document("execution-ambiguous", Type = DocumentType.Fragment)]
    public sealed class FirstAmbiguousFragment : IFragment
    {
        public void Fragment(IFragmentContext context) => context.SetVariable("first", true);
    }

    [Document("execution-ambiguous", Type = DocumentType.Fragment)]
    public sealed class SecondAmbiguousFragment : IFragment
    {
        public void Fragment(IFragmentContext context) => context.SetVariable("second", true);
    }

    [Document("execution-policy-document")]
    public sealed class WrongDocumentTypeFragment : IFragment
    {
        public void Fragment(IFragmentContext context) => context.SetVariable("wrong", true);
    }

    [Document("execution-abstract", Type = DocumentType.Fragment)]
    public abstract class AbstractFragment : IFragment
    {
        public abstract void Fragment(IFragmentContext context);
    }

    [Document("execution-not-a-fragment", Type = DocumentType.Fragment)]
    public sealed class NotAFragment : IDocument
    {
    }

    [Document("execution-needs-constructor", Type = DocumentType.Fragment)]
    public sealed class ConstructorDependencyFragment(string value) : IFragment
    {
        public void Fragment(IFragmentContext context) => context.SetVariable("dependency", value);
    }

    [Document("execution-constructor-failure", Type = DocumentType.Fragment)]
    public sealed class ThrowingConstructorFragment : IFragment
    {
        public ThrowingConstructorFragment() => throw new InvalidOperationException("constructor failure");
        public void Fragment(IFragmentContext context) => Assert.Fail("Construction failed.");
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void IncludeFragment_ExecutesRegisteredInstancesAndSharesTheExpressionContext(string section)
    {
        var test = new IncludeDocument("registered").AsTestDocument();
        var calls = 0;
        test.Context.Variables["value"] = "expression";
        test.RegisterFragment("registered", new CallbackFragment(context =>
        {
            calls++;
            context.ExpressionContext.Should().BeSameAs(test.Context);
            context.SetVariable("included", context.ExpressionContext.Variables["value"]);
        }));

        ExecutionTest.RunSection(test, section);

        calls.Should().Be(1);
        test.Context.Variables["included"].Should().Be("expression");
        test.Context.Variables["after"].Should().Be(true);
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void IncludeFragment_DiscoversAttributedFragments(string section)
    {
        var test = new IncludeDocument("execution-reflection").AsTestDocument();

        ExecutionTest.RunSection(test, section);

        test.Context.Variables["reflected"].Should().Be(true);
        test.Context.Variables["after"].Should().Be(true);
    }

    [TestMethod]
    [DataRow("execution-reflection")]
    [DataRow("execution-ambiguous")]
    public void IncludeFragment_ExplicitRegistrationTakesPriorityOverReflection(string id)
    {
        var test = new IncludeDocument(id).AsTestDocument();
        test.RegisterFragment(id, new ConstructorDependencyFragment("registered instance"));

        test.RunInbound();

        test.Context.Variables["dependency"].Should().Be("registered instance");
        test.Context.Variables.Should().NotContainKey("reflected").And.NotContainKey("first")
            .And.NotContainKey("second");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void IncludeFragment_ResolvesIdsCaseInsensitivelyForRegistrationAndReflection(bool registered)
    {
        var test = new IncludeDocument("EXECUTION-REFLECTION").AsTestDocument();
        if (registered)
        {
            test.RegisterFragment("execution-reflection", new ReflectedFragment());
        }

        test.RunInbound();

        test.Context.Variables["reflected"].Should().Be(true);
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void IncludeFragment_ReusesTheCallingSectionsMockHandlers(string section)
    {
        var test = new IncludeDocument("registered").AsTestDocument();
        test.RegisterFragment("registered",
            new CallbackFragment(context => context.WithId("variable").SetVariable("fragment-value", "value")));
        var calls = 0;
        void Callback(GatewayContext context, string name, object value, string caller)
        {
            calls++;
            name.Should().Be("fragment-value");
            value.Should().Be("value");
            context.Variables["mock-section"] = caller;
        }
        test.SetupInbound().SetVariable((_, name, _) => name == "fragment-value")
            .WithCallback((context, name, value) => Callback(context, name, value, "inbound"));
        test.SetupBackend().SetVariable((_, name, _) => name == "fragment-value")
            .WithCallback((context, name, value) => Callback(context, name, value, "backend"));
        test.SetupOutbound().SetVariable((_, name, _) => name == "fragment-value")
            .WithCallback((context, name, value) => Callback(context, name, value, "outbound"));
        test.SetupOnError().SetVariable((_, name, _) => name == "fragment-value")
            .WithCallback((context, name, value) => Callback(context, name, value, "on-error"));

        ExecutionTest.RunSection(test, section);

        calls.Should().Be(1);
        test.Context.Variables["mock-section"].Should().Be(section);
        test.Context.Variables.Should().NotContainKey("fragment-value").And.ContainKey("after");
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void IncludeFragment_NestedFragmentsPreserveExecutionOrderAndCallerSection(string section)
    {
        var calls = new List<string>();
        var test = new IncludeDocument("outer").AsTestDocument();
        test.RegisterFragment("outer", new CallbackFragment(context =>
        {
            calls.Add("outer before");
            context.WithId("inner").IncludeFragment("inner");
            calls.Add("outer after");
        }));
        test.RegisterFragment("inner", new CallbackFragment(context =>
        {
            calls.Add("inner");
            context.SetHeader("X-Fragment", "inner");
        }));

        ExecutionTest.RunSection(test, section);

        calls.Should().Equal("outer before", "inner", "outer after");
        var headers = section is "inbound" or "backend"
            ? test.Context.Request.Headers
            : test.Context.Response.Headers;
        headers["X-Fragment"].Should().Equal("inner");
        test.Context.Variables["after"].Should().Be(true);
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void IncludeFragment_AllowsPoliciesSupportedByTheCallerSection(string section)
    {
        var test = new IncludeDocument("method").AsTestDocument();
        test.RegisterFragment("method", new CallbackFragment(context => context.SetMethod("PATCH")));

        ExecutionTest.RunSection(test, section);

        test.Context.Request.Method.Should().Be("PATCH");
    }

    [TestMethod]
    [DataRow("backend", false)]
    [DataRow("outbound", false)]
    [DataRow("on-error", false)]
    [DataRow("backend", true)]
    [DataRow("outbound", true)]
    [DataRow("on-error", true)]
    public void IncludeFragment_RejectsPoliciesUnavailableInTheCallerSectionWithAccurateDiagnostics(
        string section, bool nested)
    {
        var test = new IncludeDocument(nested ? "outer" : "authentication").AsTestDocument();
        test.RegisterFragment("authentication",
            new CallbackFragment(context => context.AuthenticationBasic("user", "password")));
        test.RegisterFragment("outer", new CallbackFragment(context => context.IncludeFragment("authentication")));

        var error = Assert.ThrowsExactly<PolicyException>(() => ExecutionTest.RunSection(test, section));

        error.Policy.Should().Be(nameof(IInboundContext.AuthenticationBasic));
        error.Section.Should().Be(ExecutionTest.SectionName(section));
        error.InnerException.Should().BeOfType<NotImplementedException>()
            .Which.Message.Should().Contain(nameof(IInboundContext.AuthenticationBasic));
        test.Context.Request.Headers.Should().NotContainKey("Authorization");
        test.Context.Variables.Should().NotContainKey("after");
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void IncludeFragment_MissingIdsSurfaceAnExplicitPolicyError(string section)
    {
        var test = new IncludeDocument("execution-missing").AsTestDocument();

        var error = Assert.ThrowsExactly<PolicyException>(() => ExecutionTest.RunSection(test, section));

        error.Policy.Should().Be(nameof(IInboundContext.IncludeFragment));
        error.Section.Should().Be(ExecutionTest.SectionName(section));
        error.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("execution-missing").And.Contain("RegisterFragment");
        test.Context.Variables.Should().NotContainKey("after");
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public void IncludeFragment_RejectsInvalidIds(string? id)
    {
        var test = new IncludeDocument(id).AsTestDocument();

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeAssignableTo<ArgumentException>();
        test.Context.ResponseTerminated.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("execution-policy-document")]
    [DataRow("execution-abstract")]
    [DataRow("execution-not-a-fragment")]
    public void IncludeFragment_IgnoresTypesThatAreNotConcreteFragmentDocuments(string id)
    {
        var test = new IncludeDocument(id).AsTestDocument();

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain(id).And.Contain("not found");
        test.Context.Variables.Should().NotContainKey("wrong");
    }

    [TestMethod]
    public void IncludeFragment_RejectsAmbiguousReflectedIdsRatherThanChoosingAnArbitraryType()
    {
        var test = new IncludeDocument("execution-ambiguous").AsTestDocument();

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("execution-ambiguous").And.Contain("ambiguous");
        test.Context.Variables.Should().NotContainKey("first").And.NotContainKey("second");
    }

    [TestMethod]
    public void IncludeFragment_SurfacesMissingConstructorsWithoutFallingBack()
    {
        var test = new IncludeDocument("execution-needs-constructor").AsTestDocument();

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeOfType<MissingMethodException>();
        test.Context.Variables.Should().NotContainKey("after");
    }

    [TestMethod]
    public void IncludeFragment_SurfacesConstructorFailuresWithoutFallingBack()
    {
        var test = new IncludeDocument("execution-constructor-failure").AsTestDocument();

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeOfType<TargetInvocationException>()
            .Which.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("constructor failure");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void IncludeFragment_DetectsDirectAndIndirectCyclesWithoutUnboundedRecursion(bool indirect)
    {
        var visits = 0;
        var test = new IncludeDocument("outer").AsTestDocument();
        test.RegisterFragment("outer", new CallbackFragment(context =>
        {
            // Bound the red-phase failure so a missing cycle check cannot overflow the test process.
            if (++visits > 3)
            {
                throw new InvalidOperationException("Test cycle guard reached.");
            }
            context.IncludeFragment(indirect ? "inner" : "OUTER");
        }));
        test.RegisterFragment("inner", new CallbackFragment(context => context.IncludeFragment("OUTER")));

        var error = Assert.ThrowsExactly<PolicyException>(test.RunInbound);

        error.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("OUTER").And.Contain("already being executed");
        visits.Should().Be(1);
        test.Context.Variables.Should().NotContainKey("after");
    }

    [TestMethod]
    [DataRow("inbound", false)]
    [DataRow("backend", false)]
    [DataRow("outbound", false)]
    [DataRow("on-error", false)]
    [DataRow("inbound", true)]
    [DataRow("backend", true)]
    [DataRow("outbound", true)]
    [DataRow("on-error", true)]
    public void IncludeFragment_ClearsNestedExecutionStateAfterErrorsAndEarlyTermination(
        string section, bool terminate)
    {
        var visits = 0;
        var test = new IncludeDocument("registered").AsTestDocument();
        test.RegisterFragment("registered", new CallbackFragment(context =>
        {
            if (++visits == 1)
            {
                if (terminate)
                {
                    context.ReturnResponse(new ReturnResponseConfig());
                }
                throw new InvalidOperationException("fragment failure");
            }
            context.SetVariable("recovered", true);
        }));

        if (terminate)
        {
            ExecutionTest.RunSection(test, section);
        }
        else
        {
            Assert.ThrowsExactly<PolicyException>(() => ExecutionTest.RunSection(test, section))
                .InnerException.Should().BeOfType<InvalidOperationException>();
        }
        ExecutionTest.RunSection(test, section);

        visits.Should().Be(2);
        test.Context.Variables["recovered"].Should().Be(true);
        test.Context.Variables["after"].Should().Be(true);
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void IncludeFragment_NestedReturnResponseTerminatesTheCallerAndPipeline(string section)
    {
        var test = new IncludeDocument("outer").AsTestDocument();
        test.RegisterFragment("outer", new CallbackFragment(context =>
        {
            context.IncludeFragment("inner");
            context.SetVariable("outer-after", true);
        }));
        test.RegisterFragment("inner", new CallbackFragment(context =>
        {
            context.ReturnResponse(new ReturnResponseConfig
            {
                Status = new StatusConfig { Code = 204, Reason = "No Content" }
            });
            context.SetVariable("inner-after", true);
        }));

        ExecutionTest.RunSection(test, section);

        test.Context.Response.StatusCode.Should().Be(204);
        test.Context.ResponseTerminated.Should().BeTrue();
        test.Context.Variables.Keys.Should().NotContain(["outer-after", "inner-after", "after"]);
    }

    [TestMethod]
    [DataRow("inbound")]
    [DataRow("backend")]
    [DataRow("outbound")]
    [DataRow("on-error")]
    public void IncludeFragment_NestedInvokeRequestRemainsSectionOnly(string section)
    {
        var calls = new List<string>();
        var first = section is "inbound" or "backend" ? PolicyScope.Global : PolicyScope.Operation;
        var last = section is "inbound" or "backend" ? PolicyScope.Operation : PolicyScope.Global;
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(first, new IncludeDocument("outer"))
            .AddPolicy(last, ExecutionTest.FlowDocument("later", calls))
            .Build();
        pipeline.RegisterFragment("outer", new CallbackFragment(context =>
        {
            context.IncludeFragment("inner");
            context.SetVariable("outer-after", true);
        }));
        pipeline.RegisterFragment("inner", new CallbackFragment(context =>
        {
            context.WithId("invoke").InvokeRequest(new InvokeRequestConfig());
            context.SetVariable("inner-after", true);
        }));
        PolicyPipelineTests.SetupInvokeRequest(pipeline.Context);

        ExecutionTest.RunSection(pipeline, section);

        calls.Should().Equal($"later:{section}:before", $"later:{section}:after");
        pipeline.Context.Response.StatusCode.Should().Be(203);
        pipeline.Context.ResponseTerminated.Should().BeFalse();
        pipeline.Context.Variables.Keys.Should().NotContain(["outer-after", "inner-after", "after"]);
    }

    [TestMethod]
    public void IncludeFragment_SharedPipelineRegistrationIsUsedAcrossScopes()
    {
        var visits = 0;
        var pipeline = PolicyPipelineBuilder.Create()
            .AddPolicy(PolicyScope.Global, new IncludeDocument("registered"))
            .AddPolicy(PolicyScope.Operation, new IncludeDocument("registered"))
            .Build()
            .RegisterFragment("registered", new CallbackFragment(_ => visits++));

        pipeline.RunAll();
        pipeline.RunOnError();

        visits.Should().Be(8);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    public void IncludeFragment_RegistrationRejectsBlankIdsInBothHarnesses(string id)
    {
        var fragment = new ReflectedFragment();
        var test = new IncludeDocument("registered").AsTestDocument();
        var pipeline = PolicyPipelineBuilder.Create().Build();

        Assert.ThrowsExactly<ArgumentException>(() => test.RegisterFragment(id, fragment));
        Assert.ThrowsExactly<ArgumentException>(() => pipeline.RegisterFragment(id, fragment));
    }

    [TestMethod]
    public void IncludeFragment_RegistrationRejectsNullIdsAndInstancesInBothHarnesses()
    {
        var fragment = new ReflectedFragment();
        var test = new IncludeDocument("registered").AsTestDocument();
        var pipeline = PolicyPipelineBuilder.Create().Build();

        Assert.ThrowsExactly<ArgumentNullException>(() => test.RegisterFragment(null!, fragment));
        Assert.ThrowsExactly<ArgumentNullException>(() => pipeline.RegisterFragment(null!, fragment));
        Assert.ThrowsExactly<ArgumentNullException>(() => test.RegisterFragment("registered", null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => pipeline.RegisterFragment("registered", null!));
    }
}