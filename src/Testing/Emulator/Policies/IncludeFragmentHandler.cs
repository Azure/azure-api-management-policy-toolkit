// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
        var fragmentId = args?.FirstOrDefault()?.ToString()
            ?? throw new InvalidOperationException("Fragment ID is required for IncludeFragment.");

        // 1. Check pre-registered fragments first
        if (!context.FragmentRegistry.TryGetValue(fragmentId, out var fragment))
        {
            // 2. Scan all loaded assemblies for [Document] classes implementing IFragment, named as the compiler
            //    names them
            var fragmentType = FindFragmentType(fragmentId)
                ?? throw new InvalidOperationException(
                    $"Fragment '{fragmentId}' not found. Register it via RegisterFragment(\"{fragmentId}\", instance) " +
                    $"or ensure a class implementing IFragment named \"{fragmentId}\" by its [Document] attribute " +
                    "or its class name is loaded.");

            fragment = (IFragment)Activator.CreateInstance(fragmentType)!;
        }

        ExecuteFragment(fragment, context);
        return null;
    }

    private static Type? FindFragmentType(string fragmentId)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (!typeof(IFragment).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
                        continue;

                    // The compiler names a document after the [Document] name, or after the class when the attribute
                    // doesn't give one, and takes the document type from the attribute or the implemented interface.
                    // Fragment ids are matched the way the fragment registry matches them.
                    if (TryGetDocumentNameAndType(type, out var name, out var documentType) &&
                        documentType == DocumentType.Fragment &&
                        string.Equals(name, fragmentId, StringComparison.OrdinalIgnoreCase))
                        return type;
                }
            }
            catch
            {
                // Skip assemblies that can't be scanned (e.g., dynamic or unloadable assemblies)
            }
        }

        return null;
    }

    private static bool TryGetDocumentNameAndType(Type type, out string name, out DocumentType documentType)
    {
        // Read the attribute as written: DocumentAttribute.Type defaults to Policy, so only the attribute data tells an
        // explicit Type = DocumentType.Policy apart from an attribute without a Type argument.
        var attribute = type.GetCustomAttributesData()
            .FirstOrDefault(data => data.AttributeType == typeof(DocumentAttribute));
        if (attribute is null)
        {
            name = type.Name;
            documentType = default;
            return false;
        }

        name = attribute.ConstructorArguments.FirstOrDefault().Value as string ?? type.Name;

        var typeArgument = attribute.NamedArguments
            .FirstOrDefault(argument => argument.MemberName == nameof(DocumentAttribute.Type));
        // As in the compiler, a defined Type argument wins over the implemented interface (only IFragment
        // implementations are looked up here).
        documentType = typeArgument.MemberInfo is not null &&
                       typeArgument.TypedValue.Value is int value &&
                       Enum.IsDefined(typeof(DocumentType), value)
            ? (DocumentType)value
            : DocumentType.Fragment;
        return true;
    }

    private static void ExecuteFragment(IFragment fragment, GatewayContext context)
    {
        var handlers = context.CurrentSectionHandlers
            ?? throw new InvalidOperationException(
                "IncludeFragment must be called from within a section context.");

        var proxy = SectionContextProxy<IFragmentContext>.CreateWithHandlers<IFragmentContext>(context, handlers);
        try
        {
            fragment.Fragment(proxy.Object);
        }
        catch (FinishSectionProcessingException)
        {
            throw;
        }
    }
}
