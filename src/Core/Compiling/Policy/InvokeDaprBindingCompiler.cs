// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class InvokeDaprBindingCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.InvokeDaprBinding);

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<InvokeDaprBindingConfig>(context, "invoke-dapr-binding",
                out IReadOnlyDictionary<string, InitializerValue>? values))
        {
            return;
        }

        XElement element = new("invoke-dapr-binding");

        // Add the required Name attribute
        if (!element.AddAttribute(values, nameof(InvokeDaprBindingConfig.Name), "name"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "invoke-dapr-binding",
                nameof(InvokeDaprBindingConfig.Name)
            ));
            return;
        }

        element.AddAttribute(values, nameof(InvokeDaprBindingConfig.Operation), "operation");
        element.AddAttribute(values, nameof(InvokeDaprBindingConfig.IgnoreError), "ignore-error");
        element.AddAttribute(values, nameof(InvokeDaprBindingConfig.ResponseVariableName), "response-variable-name");
        element.AddAttribute(values, nameof(InvokeDaprBindingConfig.Timeout), "timeout");
        element.AddAttribute(values, nameof(InvokeDaprBindingConfig.Template), "template");
        element.AddAttribute(values, nameof(InvokeDaprBindingConfig.ContentType), "content-type");

        if (values.TryGetValue(nameof(InvokeDaprBindingConfig.MetaData), out InitializerValue? mataDataValue))
        {
            HandleMataData(context, mataDataValue, element);
        }

        if (values.TryGetValue(nameof(InvokeDaprBindingConfig.Data), out InitializerValue? dataValue))
        {
            element.Add(new XElement("data", dataValue.Value!));
        }

        context.AddPolicy(element);
    }

    private static void HandleMataData(IDocumentCompilationContext context, InitializerValue mataDataValue,
        XElement parentElement)
    {
        if (mataDataValue.UnnamedValues is null || mataDataValue.UnnamedValues.Count == 0)
        {
            return;
        }

        var element = new XElement("metadata");

        foreach (InitializerValue item in mataDataValue.UnnamedValues ?? [])
        {
            if (!item.TryGetValues<DaprMetaData>(out var mataDataValues))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                    item.Node.GetLocation(),
                    "invoke-dapr-binding.metadata",
                    nameof(DaprMetaData)
                ));
                continue;
            }

            XElement mataDataElement = new("item");

            if (!mataDataElement.AddAttribute(mataDataValues, nameof(DaprMetaData.Key), "key"))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterNotDefined,
                    item.Node.GetLocation(),
                    "invoke-dapr-binding.metadata.item",
                    nameof(DaprMetaData.Key)
                ));
                continue;
            }

            if (!mataDataValues.TryGetValue(nameof(DaprMetaData.Value), out var value))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterNotDefined,
                    item.Node.GetLocation(),
                    "invoke-dapr-binding.metadata.item",
                    nameof(DaprMetaData.Value)
                ));
                continue;
            }

            mataDataElement.Value = value.Value!;

            element.Add(mataDataElement);
        }

        parentElement.Add(element);
    }

    private static void HandleData(IDocumentCompilationContext context, InitializerValue dataValue,
        XElement parentElement)
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