// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

namespace Test.Emulator.Emulator;

/// <summary>
/// Audits authored APIs, runtime registration, and dedicated executable test metadata, not policy semantics.
/// InlinePolicy raw XML execution is excluded; CrossDomain callback/XML validation remains the responsibility
/// of its dedicated tests, not a claim that the emulator implements legacy client routes.
/// </summary>
[TestClass]
public class EmulatorCoverageGateTests
{
    private const int BaselinePolicyCount = 74;
    private const int BaselinePipelinePairCount = 190;
    private const string InlineXml =
        "<set-header name=\"X-Raw-Xml\" exists-action=\"override\"><value>raw</value></set-header>";

    private static readonly Type[] s_pipelineSections =
    [
        typeof(IInboundContext),
        typeof(IOutboundContext),
        typeof(IBackendContext),
        typeof(IOnErrorContext),
    ];

    [TestMethod]
    public void ShouldEnumerateTheFullAuthoredInventoryAndFragmentSurface()
    {
        var policyNames = GetAuthoredPolicyNames();
        var pipelinePairs = GetAuthoredPipelinePairs();

        policyNames.Length.Should().BeGreaterThanOrEqualTo(BaselinePolicyCount,
            "the audit must enumerate all five authoring interfaces, not an empty or partial inventory");
        pipelinePairs.Length.Should().BeGreaterThanOrEqualTo(BaselinePipelinePairCount,
            "the baseline exposes 190 distinct pipeline policy/section pairs");

        var pipelineMethods = s_pipelineSections.SelectMany(GetPolicyMethods).ToArray();
        var fragmentMethods = GetPolicyMethods(typeof(IFragmentContext));
        var failures = new List<string>();

        foreach (var method in pipelineMethods.Where(method => method.Name != nameof(IInboundContext.Base)))
        {
            if (!fragmentMethods.Any(fragment => HaveSameSignature(method, fragment)))
            {
                failures.Add($"IFragmentContext is missing the pipeline signature {method}");
            }
        }

        foreach (var method in fragmentMethods)
        {
            if (method.Name == nameof(IInboundContext.Base)
                || !pipelineMethods.Any(pipeline => HaveSameSignature(method, pipeline)))
            {
                failures.Add($"IFragmentContext has an unsupported fragment signature {method}");
            }
        }

        foreach (var section in s_pipelineSections.Append(typeof(IFragmentContext)))
        {
            if (GetPolicyMethods(section).Count(IsSectionTypedWait) != 1)
            {
                failures.Add($"{section.Name} must expose exactly one branch-scoped Wait overload");
            }

            var helpers = GetInterfaceMethods(section)
                .Where(method => method.Name == nameof(IInboundContext.WithId))
                .ToArray();
            if (helpers.Length != 1
                || helpers[0].ReturnType != section
                || !helpers[0].GetParameters().Select(parameter => parameter.ParameterType)
                    .SequenceEqual([typeof(string)]))
            {
                failures.Add($"{section.Name} must expose its chaining WithId(string) helper");
            }
        }

        failures.Should().BeEmpty("fragment and helper contracts must match the authored inventory:{0}",
            FormatFailures(failures));
    }

    [TestMethod]
    public void ShouldDiscoverExactlyOneRuntimeHandlerForEveryAuthoredPipelinePolicy()
    {
        var pipelinePairs = GetAuthoredPipelinePairs();
        pipelinePairs.Length.Should().BeGreaterThanOrEqualTo(BaselinePipelinePairCount);

        var duplicates = FindDuplicateRegistrations(GetAttributedRegistrations());
        duplicates.Should().BeEmpty("duplicate registrations prevent runtime discovery:{0}",
            FormatFailures(duplicates));

        var failures = new List<string>();
        foreach (var section in s_pipelineSections)
        {
            var runtimeHandlers = DiscoverRuntimeHandlers(section);
            foreach (var pair in pipelinePairs.Where(pair => pair.Section == section))
            {
                if (!runtimeHandlers.Contains(pair.PolicyName))
                {
                    failures.Add($"{section.Name}.{pair.PolicyName}: missing runtime handler");
                }
            }
        }

        failures.Should().BeEmpty("every authored pipeline policy requires exactly one runtime handler:{0}",
            FormatFailures(failures));
    }

    [TestMethod]
    public void ShouldHaveNoOrphanOrDuplicateSectionRegistrations()
    {
        var registrations = GetAttributedRegistrations();
        var failures = FindDuplicateRegistrations(registrations).ToList();

        foreach (var registration in registrations)
        {
            var section = s_pipelineSections.SingleOrDefault(section => section.Name == registration.Section);
            if (section is null
                || !GetInterfaceMethods(section).Any(method =>
                    method.Name == registration.PolicyName && method.Name != nameof(IInboundContext.WithId)))
            {
                failures.Add(
                    $"{registration.Section}.{registration.PolicyName}: orphan registration " +
                    $"from {registration.HandlerType.FullName}");
            }
        }

        failures.Should().BeEmpty(
            "only authored pipeline policies and inherited property accessors may be registered; " +
            "fragments reuse caller handlers and WithId is dispatched separately:{0}",
            FormatFailures(failures));

        foreach (var section in s_pipelineSections)
        {
            var runtimeHandlers = DiscoverRuntimeHandlers(section);
            var attributed = registrations.Where(registration => registration.Section == section.Name).ToArray();
            var runtimeNames = runtimeHandlers.Keys.Cast<string>().OrderBy(name => name, StringComparer.Ordinal);

            runtimeNames.Should().Equal(
                attributed.Select(registration => registration.PolicyName)
                    .OrderBy(name => name, StringComparer.Ordinal),
                "{0} runtime discovery must agree with section attributes and instance PolicyName values",
                section.Name);

            foreach (var registration in attributed)
            {
                runtimeHandlers[registration.PolicyName].Should().BeOfType(registration.HandlerType,
                    "{0}.{1} must use the attributed concrete handler",
                    section.Name, registration.PolicyName);
            }
        }
    }

    [TestMethod]
    public void ShouldHaveDedicatedExecutableTestsForEveryAuthoredPolicy()
    {
        var policyNames = GetAuthoredPolicyNames();
        policyNames.Length.Should().BeGreaterThanOrEqualTo(BaselinePolicyCount);

        var failures = new List<string>();
        foreach (var policyName in policyNames)
        {
            var expectedNames = policyName == nameof(IInboundContext.AuthenticationManagedIdentity)
                ? new[] { $"{policyName}Tests", "AuthenticationManagedIdentityHandlerTests" }
                : new[] { $"{policyName}Tests" };
            failures.AddRange(FindMissingExecutableTestClasses(expectedNames));
        }

        failures.Should().BeEmpty(
            "each distinct authored policy, including InlinePolicy and CrossDomain, needs a dedicated " +
            "[TestClass] with an executable [TestMethod]; metadata alone does not prove semantic coverage:{0}",
            FormatFailures(failures));
    }

    [TestMethod]
    public void ShouldHaveSeparateExecutableWithIdTests()
    {
        var failures = FindMissingExecutableTestClasses(["WithIdTests"]);

        failures.Should().BeEmpty(
            "WithId is a compile-time helper excluded from policy registration, but still needs separate tests:{0}",
            FormatFailures(failures));
    }

    [TestMethod]
    public void ShouldReuseCallerHandlersForFragmentContexts()
    {
        var context = new GatewayContext();
        var fragmentProxyType = GetProxyType(typeof(IFragmentContext));
        var factory = fragmentProxyType.GetMethod("CreateWithHandlers", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("SectionContextProxy.CreateWithHandlers was not found.");
        var handlersField = fragmentProxyType.GetField("_handlers", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("SectionContextProxy._handlers was not found.");

        DiscoverRuntimeHandlers(typeof(IFragmentContext)).Keys.Cast<string>().Should().BeEmpty(
            "fragments have no standalone section registration");

        foreach (var section in s_pipelineSections)
        {
            var callerHandlers = DiscoverRuntimeHandlers(section);
            var proxy = factory.MakeGenericMethod(typeof(IFragmentContext))
                .Invoke(null, [context, callerHandlers, section.Name])
                ?? throw new InvalidOperationException($"Could not create a fragment proxy for {section.Name}.");

            handlersField.GetValue(proxy).Should().BeSameAs(callerHandlers,
                "a fragment included from {0} must share that section's actual handler instances", section.Name);
            ((IFragmentContext)proxy).ExpressionContext.Should().BeSameAs(context);
        }
    }

    [TestMethod]
    public void ShouldSupportTheExplicitInlinePolicyCallbackOnlyExecutionExclusion()
    {
        var test = new InlineCallbackDocument().AsTestDocument();
        string? receivedXml = null;
        test.SetupInbound().Inline().WithCallback((context, xml) =>
        {
            receivedXml = xml;
            context.Variables["inline-callback"] = true;
        });

        test.RunInbound();

        receivedXml.Should().Be(InlineXml);
        test.Context.Variables.Should().Contain("inline-callback", true);
        test.Context.Request.Headers.Should().NotContainKey("X-Raw-Xml",
            "this verifies explicit callback execution, not interpretation of raw policy XML");
    }

    private static MethodInfo[] GetInterfaceMethods(Type section) =>
        section.GetInterfaces().Append(section)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Distinct()
            .ToArray();

    private static MethodInfo[] GetPolicyMethods(Type section) =>
        GetInterfaceMethods(section)
            .Where(method => !method.IsSpecialName && method.Name != nameof(IInboundContext.WithId))
            .ToArray();

    private static string[] GetAuthoredPolicyNames() =>
        s_pipelineSections.Append(typeof(IFragmentContext))
            .SelectMany(GetPolicyMethods)
            .Select(method => method.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    private static PolicySection[] GetAuthoredPipelinePairs() =>
        s_pipelineSections
            .SelectMany(section => GetPolicyMethods(section).Select(method => new PolicySection(section, method.Name)))
            .Distinct()
            .ToArray();

    private static bool HaveSameSignature(MethodInfo first, MethodInfo second)
    {
        if (first.Name != second.Name || first.ReturnType != second.ReturnType)
        {
            return false;
        }

        if (IsSectionTypedWait(first) || IsSectionTypedWait(second))
        {
            return IsSectionTypedWait(first) && IsSectionTypedWait(second);
        }

        return first.GetParameters().Select(parameter => parameter.ParameterType)
            .SequenceEqual(second.GetParameters().Select(parameter => parameter.ParameterType));
    }

    private static bool IsSectionTypedWait(MethodInfo method)
    {
        var parameters = method.GetParameters();
        return method.Name == nameof(IInboundContext.Wait)
            && method.DeclaringType is { } section
            && parameters.Length == 2
            && parameters[0].ParameterType == typeof(string)
            && parameters[1].IsDefined(typeof(ParamArrayAttribute))
            && parameters[1].ParameterType == typeof(Action<>).MakeGenericType(section).MakeArrayType();
    }

    private static Type GetProxyType(Type section) =>
        typeof(GatewayContext).Assembly
            .GetType($"{typeof(SectionAttribute).Namespace}.SectionContextProxy`1", throwOnError: true)!
            .MakeGenericType(section);

    private static IDictionary DiscoverRuntimeHandlers(Type section)
    {
        var method = GetProxyType(section).GetMethod("DiscoverHandlers", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("SectionContextProxy.DiscoverHandlers was not found.");
        var result = method.MakeGenericMethod(section).Invoke(null, null);

        return result as IDictionary
            ?? throw new InvalidOperationException($"Runtime discovery for {section.Name} did not return a handler map.");
    }

    private static HandlerRegistration[] GetAttributedRegistrations()
    {
        var assembly = typeof(GatewayContext).Assembly;
        var handlerInterface = assembly
            .GetType($"{typeof(SectionAttribute).Namespace}.IPolicyHandler", throwOnError: true)!;
        var policyNameProperty = handlerInterface.GetProperty("PolicyName")
            ?? throw new InvalidOperationException("IPolicyHandler.PolicyName was not found.");
        var registrations = new List<HandlerRegistration>();

        foreach (var type in assembly.GetTypes().Where(type =>
                     type.IsClass && !type.IsAbstract
                     && type.Namespace == $"{typeof(SectionAttribute).Namespace}.Policies"
                     && handlerInterface.IsAssignableFrom(type)))
        {
            var scopes = type.GetCustomAttributes<SectionAttribute>(inherit: false)
                .Select(attribute => attribute.Scope)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (scopes.Length == 0)
            {
                continue;
            }

            var handler = Activator.CreateInstance(type)
                ?? throw new InvalidOperationException($"Could not instantiate handler {type.FullName}.");
            var policyName = policyNameProperty.GetValue(handler) as string;
            policyName.Should().NotBeNullOrWhiteSpace("{0} must supply a runtime PolicyName", type.FullName);

            registrations.AddRange(scopes.Select(scope => new HandlerRegistration(scope, policyName!, type)));
        }

        return registrations.ToArray();
    }

    private static string[] FindDuplicateRegistrations(IEnumerable<HandlerRegistration> registrations) =>
        registrations.GroupBy(registration => (registration.Section, registration.PolicyName))
            .Where(group => group.Count() != 1)
            .Select(group =>
                $"{group.Key.Section}.{group.Key.PolicyName}: duplicate handlers " +
                string.Join(", ", group.Select(registration => registration.HandlerType.FullName)))
            .ToArray();

    private static string[] FindMissingExecutableTestClasses(string[] expectedNames)
    {
        var candidates = typeof(EmulatorCoverageGateTests).Assembly.GetTypes()
            .Where(type => expectedNames.Contains(type.Name, StringComparer.Ordinal))
            .ToArray();
        if (candidates.Any(HasExecutableTests))
        {
            return [];
        }

        var expected = string.Join(" or ", expectedNames);
        return candidates.Length == 0
            ? [$"{expected}: missing dedicated test class"]
            : [$"{expected}: no non-ignored, instantiable [TestClass] with an executable [TestMethod]"];
    }

    private static bool HasExecutableTests(Type type) =>
        type.IsClass && type.IsVisible && !type.IsAbstract && !type.ContainsGenericParameters
        && type.IsDefined(typeof(TestClassAttribute), inherit: false)
        && !type.IsDefined(typeof(IgnoreAttribute), inherit: true)
        && type.GetConstructors().Any(constructor =>
            constructor.GetParameters().Length == 0
            || constructor.GetParameters().Select(parameter => parameter.ParameterType)
                .SequenceEqual([typeof(TestContext)]))
        && type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Any(IsExecutableTestMethod);

    private static bool IsExecutableTestMethod(MethodInfo method)
    {
        if (method.IsAbstract || method.ContainsGenericParameters || method.GetMethodBody() is null
            || !method.IsDefined(typeof(TestMethodAttribute), inherit: true)
            || method.IsDefined(typeof(IgnoreAttribute), inherit: true))
        {
            return false;
        }

        var returnType = method.ReturnType;
        if (returnType != typeof(void) && returnType != typeof(Task) && returnType != typeof(ValueTask))
        {
            return false;
        }

        if (returnType == typeof(void) && method.IsDefined(typeof(AsyncStateMachineAttribute), inherit: false))
        {
            return false;
        }

        return method.GetParameters().Length == 0
            || method.GetCustomAttributes(inherit: true).OfType<ITestDataSource>().Any();
    }

    private static string FormatFailures(IEnumerable<string> failures) =>
        Environment.NewLine + string.Join(Environment.NewLine,
            failures.OrderBy(failure => failure, StringComparer.Ordinal).Select(failure => $"  {failure}"));

    private sealed record PolicySection(Type Section, string PolicyName);

    private sealed record HandlerRegistration(string Section, string PolicyName, Type HandlerType);

    private sealed class InlineCallbackDocument : IDocument
    {
        public void Inbound(IInboundContext context) => context.InlinePolicy(InlineXml);
    }
}
