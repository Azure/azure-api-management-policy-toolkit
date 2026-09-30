// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[
    Section(nameof(IInboundContext)),
    Section(nameof(IBackendContext)),
    Section(nameof(IOutboundContext)),
    Section(nameof(IOnErrorContext))
]
internal class IncludeFragmentHandler : IPolicyHandler
{
    public string PolicyName => nameof(IInboundContext.IncludeFragment);

    public object? Handle(GatewayContext context, object?[]? args)
    {
        var fragmentId = args.ExtractArgument<string>();
        ArgumentException.ThrowIfNullOrWhiteSpace(fragmentId);
        if (!context.ActiveFragments.Add(fragmentId))
        {
            throw new InvalidOperationException(
                $"Fragment '{fragmentId}' is already being executed. Recursive fragment inclusion is not supported.");
        }

        try
        {
            if (!context.FragmentRegistry.TryGetValue(fragmentId, out var fragment))
            {
                var fragmentType = FindFragmentType(fragmentId)
                    ?? throw new InvalidOperationException(
                        $"Fragment '{fragmentId}' not found. Register it via RegisterFragment(\"{fragmentId}\", instance) " +
                        $"or ensure a class with [Document(\"{fragmentId}\", Type = DocumentType.Fragment)] is loaded.");

                fragment = (IFragment)Activator.CreateInstance(fragmentType)!;
            }

            ExecuteFragment(fragment, context);
            return null;
        }
        finally
        {
            context.ActiveFragments.Remove(fragmentId);
        }
    }

    private static Type? FindFragmentType(string fragmentId)
    {
        var matches = new List<Type>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic)
            {
                continue;
            }

            foreach (var type in assembly.GetTypes())
            {
                if (!typeof(IFragment).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface
                    || type.ContainsGenericParameters)
                {
                    continue;
                }

                var document = type.GetCustomAttribute<DocumentAttribute>(inherit: false);
                if (document?.Type == DocumentType.Fragment
                    && string.Equals(document.Name, fragmentId, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(type);
                }
            }
        }

        return matches.Count switch
        {
            0 => null,
            1 => matches[0],
            _ => throw new InvalidOperationException(
                $"Fragment '{fragmentId}' is ambiguous. Multiple fragment documents were found: " +
                $"{string.Join(", ", matches.Select(type => type.FullName))}. Register a fragment explicitly.")
        };
    }

    private static void ExecuteFragment(IFragment fragment, GatewayContext context)
    {
        var handlers = context.CurrentSectionHandlers
            ?? throw new InvalidOperationException(
                "IncludeFragment must be called from within a section context.");
        var sectionName = context.CurrentSectionName
            ?? throw new InvalidOperationException(
                "IncludeFragment must be called from within a section context.");

        var proxy = SectionContextProxy<IFragmentContext>.CreateWithHandlers<IFragmentContext>(
            context, handlers, sectionName);
        fragment.Fragment(proxy.Object);
    }
}