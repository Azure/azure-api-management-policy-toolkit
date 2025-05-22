// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.IoC;

public static class LazilyResolutionModule
{
    public static IServiceCollection AddLazyResolution(this IServiceCollection services)
    {
        return services.AddTransient(typeof(Lazy<>), typeof(LazyRequired<>));
    }

    private class LazyRequired<T>(IServiceProvider serviceProvider) : Lazy<T>(serviceProvider.GetRequiredService<T>)
        where T : notnull;
}