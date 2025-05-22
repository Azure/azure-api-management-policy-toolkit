// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class ValidateParametersCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.ValidateParameters);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<ValidateParametersConfig>(context, "validate-parameters",
                out var values))
        {
            return;
        }

        XElement element = new("validate-parameters");

        if (!element.AddAttribute(values, nameof(ValidateParametersConfig.SpecifiedParameterAction),
                "specified-parameter-action"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.ArgumentList.GetLocation(),
                "validate-parameters",
                nameof(ValidateParametersConfig.SpecifiedParameterAction)
            ));
            return;
        }

        if (!element.AddAttribute(values, nameof(ValidateParametersConfig.UnspecifiedParameterAction),
                "unspecified-parameter-action"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.ArgumentList.GetLocation(),
                "validate-parameters",
                nameof(ValidateParametersConfig.UnspecifiedParameterAction)
            ));
            return;
        }

        element.AddAttribute(values, nameof(ValidateParametersConfig.ErrorsVariableName), "errors-variable-name");

        if (values.TryGetValue(nameof(ValidateParametersConfig.Headers), out var headersValue))
        {
            AddHeadersElement(context, headersValue, element);
        }

        if (values.TryGetValue(nameof(ValidateParametersConfig.Query), out var queryValue))
        {
            AddQueryElement(context, queryValue, element);
        }

        if (values.TryGetValue(nameof(ValidateParametersConfig.Path), out var pathValue))
        {
            AddPathElement(context, pathValue, element);
        }

        context.AddPolicy(element);
    }

    private static void AddHeadersElement(ICompilationContext context, InitializerValue headersValue,
        XElement parentElement)
    {
        if (!headersValue.TryGetValues<ValidateHeaderParameters>(out var headerParams))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                headersValue.Node.GetLocation(),
                "validate-parameters.headers",
                nameof(ValidateHeaderParameters)
            ));
            return;
        }

        XElement headersElement = new("headers");

        if (!headersElement.AddAttribute(headerParams, nameof(ValidateHeaderParameters.SpecifiedParameterAction),
                "specified-parameter-action"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                headersValue.Node.GetLocation(),
                "validate-parameters.headers",
                nameof(ValidateHeaderParameters.SpecifiedParameterAction)
            ));
            return;
        }

        if (!headersElement.AddAttribute(headerParams, nameof(ValidateHeaderParameters.UnspecifiedParameterAction),
                "unspecified-parameter-action"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                headersValue.Node.GetLocation(),
                "validate-parameters.headers",
                nameof(ValidateHeaderParameters.UnspecifiedParameterAction)
            ));
            return;
        }

        if (headerParams.TryGetValue(nameof(ValidateHeaderParameters.Parameters), out var parametersValue))
        {
            AddParameters(context, parametersValue, headersElement, "validate-parameters.headers");
        }

        parentElement.Add(headersElement);
    }

    private static void AddQueryElement(ICompilationContext context, InitializerValue queryValue,
        XElement parentElement)
    {
        if (!queryValue.TryGetValues<ValidateQueryParameters>(out var queryParams))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                queryValue.Node.GetLocation(),
                "validate-parameters.query",
                nameof(ValidateQueryParameters)
            ));
            return;
        }

        XElement queryElement = new("query");

        if (!queryElement.AddAttribute(queryParams, nameof(ValidateQueryParameters.SpecifiedParameterAction),
                "specified-parameter-action"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                queryValue.Node.GetLocation(),
                "validate-parameters.query",
                nameof(ValidateQueryParameters.SpecifiedParameterAction)
            ));
            return;
        }

        if (!queryElement.AddAttribute(queryParams, nameof(ValidateQueryParameters.UnspecifiedParameterAction),
                "unspecified-parameter-action"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                queryValue.Node.GetLocation(),
                "validate-parameters.query",
                nameof(ValidateQueryParameters.UnspecifiedParameterAction)
            ));
            return;
        }

        if (queryParams.TryGetValue(nameof(ValidateQueryParameters.Parameters), out var parametersValue))
        {
            AddParameters(context, parametersValue, queryElement, "validate-parameters.query");
        }

        parentElement.Add(queryElement);
    }

    private static void AddPathElement(ICompilationContext context, InitializerValue pathValue, XElement parentElement)
    {
        if (!pathValue.TryGetValues<ValidatePathParameters>(out var pathParams))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                pathValue.Node.GetLocation(),
                "validate-parameters.path",
                nameof(ValidatePathParameters)
            ));
            return;
        }

        XElement pathElement = new("path");

        if (!pathElement.AddAttribute(pathParams, nameof(ValidatePathParameters.SpecifiedParameterAction),
                "specified-parameter-action"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                pathValue.Node.GetLocation(),
                "validate-parameters.path",
                nameof(ValidatePathParameters.SpecifiedParameterAction)
            ));
            return;
        }

        if (pathParams.TryGetValue(nameof(ValidatePathParameters.Parameters), out var parametersValue))
        {
            AddParameters(context, parametersValue, pathElement, "validate-parameters.path");
        }

        parentElement.Add(pathElement);
    }

    private static void AddParameters(ICompilationContext context, InitializerValue parametersValue,
        XElement parentElement, string policyPath)
    {
        foreach (var paramValue in parametersValue.UnnamedValues ?? [])
        {
            if (!paramValue.TryGetValues<ValidateParameter>(out var paramValues))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                    paramValue.Node.GetLocation(),
                    $"{policyPath}.parameter",
                    nameof(ValidateParameter)
                ));
                continue;
            }

            XElement paramElement = new("parameter");

            if (!paramElement.AddAttribute(paramValues, nameof(ValidateParameter.Name), "name"))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterNotDefined,
                    paramValue.Node.GetLocation(),
                    $"{policyPath}.parameter",
                    nameof(ValidateParameter.Name)
                ));
                continue;
            }

            if (!paramElement.AddAttribute(paramValues, nameof(ValidateParameter.Action), "action"))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterNotDefined,
                    paramValue.Node.GetLocation(),
                    $"{policyPath}.parameter",
                    nameof(ValidateParameter.Action)
                ));
                continue;
            }

            parentElement.Add(paramElement);
        }
    }
}