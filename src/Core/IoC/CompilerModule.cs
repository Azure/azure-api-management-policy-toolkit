// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;

using Azure.ApiManagement.PolicyToolkit.Compiling;
using Azure.ApiManagement.PolicyToolkit.Compiling.Syntax;

using Microsoft.Extensions.DependencyInjection;

namespace Azure.ApiManagement.PolicyToolkit.IoC;

public static class CompilerModule
{
    public static IServiceCollection SetupCompiler(this IServiceCollection services)
    {
        return services
            .AddLazyResolution()
            .AddSingleton<CSharpPolicyCompiler>()
            .AddMethodPolicyHandlers()
            .AddSyntaxCompilers();
    }

    public static IServiceCollection AddMethodPolicyHandlers(this IServiceCollection services)
    {
        IEnumerable<Type>? policyHandlerTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(type =>
                type is
                {
                    IsClass: true,
                    IsAbstract: false,
                    IsPublic: true,
                    Namespace: "Azure.ApiManagement.PolicyToolkit.Compiling.Policy"
                }
                && typeof(IMethodPolicyHandler).IsAssignableFrom(type));

        foreach (Type handlerType in policyHandlerTypes)
        {
            services.AddSingleton(typeof(IMethodPolicyHandler), handlerType);
        }

        return services;
    }

    public static IServiceCollection AddSyntaxCompilers(this IServiceCollection services)
    {
        return services
            .AddSingleton<BlockCompiler>()
            .AddSingleton<ISyntaxCompiler, IfStatementCompiler>()
            .AddSingleton<ISyntaxCompiler, LocalDeclarationStatementCompiler>()
            .AddSingleton<ISyntaxCompiler, ExpressionStatementCompiler>();
    }
}