// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Implementations;

public class ImplementationContext
{
    public static readonly ImplementationContext Default = new ImplementationContext();

    private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

    public void SetService<T>(T service)
    {
        _services[typeof(T)] = service ?? throw new NullReferenceException("service cannot be null");
    }

    public T GetService<T>([CallerMemberName] string memberName = "")
    {
        if (_services.TryGetValue(typeof(T), out var service))
        {
            return (T)service;
        }

        var methodMessage = string.IsNullOrEmpty(memberName)
            ? ""
            : $" Please set {nameof(T)} before calling {memberName} method.";
        throw new NullReferenceException($"Implementation for {nameof(T)} is not set.{methodMessage}");
    }
}