// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public static class GenericCompiler
{
    public static void HandleList(
        XElement element,
        IReadOnlyDictionary<string, InitializerValue> values,
        string key,
        string listName,
        string elementName)
    {
        if (!values.TryGetValue(key, out InitializerValue? listInitializer))
        {
            return;
        }

        XElement listElement = new(listName);
        foreach (InitializerValue initializer in listInitializer.UnnamedValues ?? [])
        {
            listElement.Add(new XElement(elementName, initializer.Value!));
        }

        element.Add(listElement);
    }
}