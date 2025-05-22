// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class ValidateContentCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.ValidateContent);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<ValidateContentConfig>(context, "validate-content", out var values))
        {
            return;
        }

        XElement element = new("validate-content");

        if (!element.AddAttribute(values, nameof(ValidateContentConfig.UnspecifiedContentTypeAction),
                "unspecified-content-type-action"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.ArgumentList.GetLocation(),
                "validate-content",
                nameof(ValidateContentConfig.UnspecifiedContentTypeAction)
            ));
            return;
        }

        if (!element.AddAttribute(values, nameof(ValidateContentConfig.MaxSize), "max-size"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.ArgumentList.GetLocation(),
                "validate-content",
                nameof(ValidateContentConfig.MaxSize)
            ));
            return;
        }

        if (!element.AddAttribute(values, nameof(ValidateContentConfig.SizeExceededAction), "size-exceeded-action"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.ArgumentList.GetLocation(),
                "validate-content",
                nameof(ValidateContentConfig.SizeExceededAction)
            ));
            return;
        }

        element.AddAttribute(values, nameof(ValidateContentConfig.ErrorsVariableName), "errors-variable-name");

        // Handle ContentTypeMap
        if (values.TryGetValue(nameof(ValidateContentConfig.ContentTypeMap), out var contentTypeMapValue))
        {
            HandleContentTypeMap(context, contentTypeMapValue, element);
        }

        // Handle ContentTypes
        if (values.TryGetValue(nameof(ValidateContentConfig.Contents), out var contentTypesValue))
        {
            HandleContents(context, contentTypesValue, element);
        }

        context.AddPolicy(element);
    }

    private static void HandleContentTypeMap(ICompilationContext context, InitializerValue contentTypeMapValue,
        XElement parentElement)
    {
        if (!contentTypeMapValue.TryGetValues<ContentTypeMapConfig>(out var mapConfigValues))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                contentTypeMapValue.Node.GetLocation(),
                "validate-content.content-type-map",
                nameof(ContentTypeMapConfig)
            ));
            return;
        }

        XElement mapElement = new("content-type-map");
        mapElement.AddAttribute(mapConfigValues, nameof(ContentTypeMapConfig.AnyContentTypeValue),
            "any-content-type-value");
        mapElement.AddAttribute(mapConfigValues, nameof(ContentTypeMapConfig.MissingContentTypeValue),
            "missing-content-type-value");

        // Handle content type mappings
        if (mapConfigValues.TryGetValue(nameof(ContentTypeMapConfig.Types), out var typesValue))
        {
            HandleTypeMap(context, typesValue, mapElement);
        }

        parentElement.Add(mapElement);
    }

    private static void HandleTypeMap(ICompilationContext context, InitializerValue typesValue, XElement mapElement)
    {
        foreach (var typeValue in typesValue.UnnamedValues ?? [])
        {
            if (!typeValue.TryGetValues<ContentTypeMap>(out var typeMapValues))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                    typeValue.Node.GetLocation(),
                    "validate-content.content-type-map.type",
                    nameof(ContentTypeMap)
                ));
                continue;
            }

            XElement typeElement = new("type");
            if (!typeElement.AddAttribute(typeMapValues, nameof(ContentTypeMap.To), "to"))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterNotDefined,
                    typeValue.Node.GetLocation(),
                    "validate-content.content-type-map.type",
                    nameof(ContentTypeMap.To)
                ));
                continue;
            }

            typeElement.AddAttribute(typeMapValues, nameof(ContentTypeMap.From), "from");
            typeElement.AddAttribute(typeMapValues, nameof(ContentTypeMap.When), "when");

            mapElement.Add(typeElement);
        }
    }

    private static void HandleContents(ICompilationContext context, InitializerValue contentTypesValue,
        XElement parentElement)
    {
        foreach (var contentTypeValue in contentTypesValue.UnnamedValues ?? [])
        {
            if (!contentTypeValue.TryGetValues<ValidateContent>(out var validateContentTypeValues))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                    contentTypeValue.Node.GetLocation(),
                    "validate-content.content",
                    nameof(ValidateContent)
                ));
                continue;
            }

            XElement contentTypeElement = new("content");
            if (!contentTypeElement.AddAttribute(validateContentTypeValues, nameof(ValidateContent.ValidateAs),
                    "validate-as"))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterNotDefined,
                    contentTypeValue.Node.GetLocation(),
                    "validate-content.content",
                    nameof(ValidateContent.ValidateAs)
                ));
                continue;
            }

            if (!contentTypeElement.AddAttribute(validateContentTypeValues, nameof(ValidateContent.Action),
                    "action"))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterNotDefined,
                    contentTypeValue.Node.GetLocation(),
                    "validate-content.content",
                    nameof(ValidateContent.Action)
                ));
                continue;
            }

            contentTypeElement.AddAttribute(validateContentTypeValues, nameof(ValidateContent.Type), "type");
            contentTypeElement.AddAttribute(validateContentTypeValues, nameof(ValidateContent.SchemaId),
                "schema-id");
            contentTypeElement.AddAttribute(validateContentTypeValues, nameof(ValidateContent.SchemaRef),
                "schema-ref");
            contentTypeElement.AddAttribute(validateContentTypeValues,
                nameof(ValidateContent.AllowAdditionalProperties), "allow-additional-properties");
            contentTypeElement.AddAttribute(validateContentTypeValues,
                nameof(ValidateContent.CaseInsensitivePropertyNames), "case-insensitive-property-names");

            parentElement.Add(contentTypeElement);
        }
    }
}