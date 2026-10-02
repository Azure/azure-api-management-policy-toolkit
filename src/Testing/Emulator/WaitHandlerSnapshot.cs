// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Reflection;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator;

internal static class WaitHandlerSnapshot
{
    internal static Dictionary<string, IPolicyHandler> Copy(Dictionary<string, IPolicyHandler> source) =>
        source.ToDictionary(entry => entry.Key, entry => Copy(entry.Value), source.Comparer);

    private static IPolicyHandler Copy(IPolicyHandler source)
    {
        var target = Activator.CreateInstance(source.GetType()) as IPolicyHandler
            ?? throw new InvalidOperationException($"Cannot create a Wait handler snapshot for '{source.GetType().Name}'.");
        for (var type = source.GetType(); type is not null; type = type.BaseType)
        {
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public
                         | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                var value = field.GetValue(source);
                if (value is IList list && field.GetValue(target) is IList copy)
                {
                    foreach (var item in list)
                    {
                        copy.Add(item);
                    }
                }
                else if (value is null or string or Delegate || field.FieldType.IsValueType)
                {
                    field.SetValue(target, value);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Handler '{source.GetType().Name}' contains unsnapshotted mutable state '{field.Name}'.");
                }
            }
        }
        return target;
    }
}