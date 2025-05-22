// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class InvokeDarpBindingCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.InvokeDarpBinding);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<InvokeDarpBindingConfig>(context, "invoke-darp-binding",
                out IReadOnlyDictionary<string, InitializerValue>? values))
        {
            return;
        }

        XElement element = new("invoke-darp-binding");

        // Add the required Name attribute
        if (!element.AddAttribute(values, nameof(InvokeDarpBindingConfig.Name), "name"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "invoke-darp-binding",
                nameof(InvokeDarpBindingConfig.Name)
            ));
            return;
        }

        element.AddAttribute(values, nameof(InvokeDarpBindingConfig.Operation), "operation");
        element.AddAttribute(values, nameof(InvokeDarpBindingConfig.IgnoreError), "ignore-error");
        element.AddAttribute(values, nameof(InvokeDarpBindingConfig.ResponseVariableName), "response-variable-name");
        element.AddAttribute(values, nameof(InvokeDarpBindingConfig.Timeout), "timeout");
        element.AddAttribute(values, nameof(InvokeDarpBindingConfig.Template), "template");
        element.AddAttribute(values, nameof(InvokeDarpBindingConfig.ContentType), "content-type");

        if (values.TryGetValue(nameof(InvokeDarpBindingConfig.MetaData), out InitializerValue? mataDataValue))
        {
            HandleMataData(context, mataDataValue, element);
        }

        if (values.TryGetValue(nameof(InvokeDarpBindingConfig.Data), out InitializerValue? dataValue))
        {
            element.Add(new XElement("data", dataValue.Value!));
        }

        context.AddPolicy(element);
    }

    private static void HandleMataData(ICompilationContext context, InitializerValue mataDataValue,
        XElement parentElement)
    {
        if (mataDataValue.UnnamedValues is null || mataDataValue.UnnamedValues.Count == 0)
        {
            return;
        }

        var element = new XElement("metadata");

        foreach (InitializerValue item in mataDataValue.UnnamedValues ?? [])
        {
            if (!item.TryGetValues<DarpMetaData>(out var mataDataValues))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                    item.Node.GetLocation(),
                    "invoke-darp-binding.matadata",
                    nameof(DarpMetaData)
                ));
                continue;
            }

            XElement mataDataElement = new("item");

            if (!mataDataElement.AddAttribute(mataDataValues, nameof(DarpMetaData.Key), "key"))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterNotDefined,
                    item.Node.GetLocation(),
                    "invoke-darp-binding.matadata.item",
                    nameof(DarpMetaData.Key)
                ));
                continue;
            }

            if (!mataDataValues.TryGetValue(nameof(DarpMetaData.Value), out var value))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterNotDefined,
                    item.Node.GetLocation(),
                    "invoke-darp-binding.matadata.item",
                    nameof(DarpMetaData.Value)
                ));
                continue;
            }

            mataDataElement.Value = value.Value!;

            element.Add(mataDataElement);
        }

        parentElement.Add(element);
    }

    private static void HandleData(ICompilationContext context, InitializerValue dataValue, XElement parentElement)
    {
        if (dataValue.Value is null)
        {
            return;
        }

        XElement dataElement = new("data");

        foreach (InitializerValue item in dataValue.UnnamedValues)
        {
            dataElement.Add(new XElement("item", item.Value));
        }

        parentElement.Add(dataElement);
    }
}