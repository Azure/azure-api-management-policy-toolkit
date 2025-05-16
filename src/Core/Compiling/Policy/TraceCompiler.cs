// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class TraceCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.Trace);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<TraceConfig>(context, "trace",
                out IReadOnlyDictionary<string, InitializerValue>? values))
        {
            return;
        }

        XElement element = new("trace");
        if (!element.AddAttribute(values, nameof(TraceConfig.Source), "source"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "trace",
                nameof(TraceConfig.Source)
            ));
            return;
        }

        if (!values.TryGetValue(nameof(TraceConfig.Message), out InitializerValue? message) || message.Value is null)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "trace",
                nameof(TraceConfig.Message)
            ));
            return;
        }

        element.Add(new XElement("message", message.Value));

        element.AddAttribute(values, nameof(TraceConfig.Severity), "severity");

        if (values.TryGetValue(nameof(TraceConfig.Metadata), out InitializerValue? metadata))
        {
            element.Add(HandleMetadata(context, metadata).ToArray());
        }

        context.AddPolicy(element);
    }

    private static IEnumerable<object> HandleMetadata(ICompilationContext context, InitializerValue metadata)
    {
        List<object> elements = new();
        foreach (InitializerValue data in metadata.UnnamedValues ?? [])
        {
            if (!data.TryGetValues<TraceMetadata>(out IReadOnlyDictionary<string, InitializerValue>? metadataValues))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                    data.Node.GetLocation(),
                    "trace.metadata",
                    nameof(TraceMetadata)
                ));
                continue;
            }

            XElement xMetadata = new("metadata");
            if (!xMetadata.AddAttribute(metadataValues, nameof(TraceMetadata.Name), "name"))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterNotDefined,
                    data.Node.GetLocation(),
                    "trace.metadata",
                    nameof(TraceMetadata.Name)
                ));
                continue;
            }

            if (!xMetadata.AddAttribute(metadataValues, nameof(TraceMetadata.Value), "value"))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterNotDefined,
                    data.Node.GetLocation(),
                    "trace.metadata",
                    nameof(TraceMetadata.Value)
                ));
                continue;
            }

            elements.Add(xMetadata);
        }

        return elements;
    }
}