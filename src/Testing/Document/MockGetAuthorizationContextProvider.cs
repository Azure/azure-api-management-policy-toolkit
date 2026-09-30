// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Document;

/// <summary>
/// Configures get-authorization-context callbacks and acquisition hooks for emulator tests.
/// </summary>
public static class MockGetAuthorizationContextProvider
{
    /// <summary>
    /// Configures every invocation in the selected policy section.
    /// </summary>
    public static Setup GetAuthorizationContext<TSection>(this MockPoliciesProvider<TSection> mock)
        where TSection : class => GetAuthorizationContext(mock, (_, _) => true);

    /// <summary>
    /// Configures invocations matching the predicate in the selected policy section.
    /// </summary>
    public static Setup GetAuthorizationContext<TSection>(
        this MockPoliciesProvider<TSection> mock,
        Func<GatewayContext, GetAuthorizationContextConfig, bool> predicate)
        where TSection : class
    {
        ArgumentNullException.ThrowIfNull(mock);
        ArgumentNullException.ThrowIfNull(predicate);
        var handler = mock.SectionContextProxy.GetHandler<GetAuthorizationContextHandler>();
        return new Setup(predicate, handler);
    }

    /// <summary>
    /// Setup for matching get-authorization-context invocations.
    /// </summary>
    public class Setup
    {
        private readonly Func<GatewayContext, GetAuthorizationContextConfig, bool> _predicate;
        private readonly GetAuthorizationContextHandler _handler;

        internal Setup(
            Func<GatewayContext, GetAuthorizationContextConfig, bool> predicate,
            GetAuthorizationContextHandler handler)
        {
            _predicate = predicate;
            _handler = handler;
        }

        /// <summary>
        /// Overrides the policy's default behavior with a callback.
        /// </summary>
        public void WithCallback(Action<GatewayContext, GetAuthorizationContextConfig> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            _handler.CallbackSetup.Add((_predicate, callback).ToTuple());
        }

        /// <summary>
        /// Provides authorization instead of calling a registered authorization provider.
        /// </summary>
        public void WithAuthorizationProviderHook(Func<AuthorizationRequest, Authorization> hook)
        {
            ArgumentNullException.ThrowIfNull(hook);
            _handler.ProvideAuthorizationHooks.Add((_predicate, hook).ToTuple());
        }

        /// <summary>
        /// Returns the specified typed authorization for matching invocations.
        /// </summary>
        public void ReturnsAuthorization(Authorization authorization)
        {
            ArgumentNullException.ThrowIfNull(authorization);
            WithAuthorizationProviderHook(_ => authorization);
        }

        /// <summary>
        /// Simulates an authorization acquisition failure.
        /// </summary>
        public void WithError(string error)
        {
            ArgumentNullException.ThrowIfNull(error);
            WithAuthorizationProviderHook(_ => throw new HttpRequestException(error));
        }
    }
}
