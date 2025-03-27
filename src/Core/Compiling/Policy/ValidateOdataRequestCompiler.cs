// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Azure.ApiManagement.PolicyToolkit.Authoring;

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class ValidateOdataRequestCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.ValidateOdataRequest);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<ValidateOdataRequestConfig>(context, "validate-odata-request",
                out var values))
        {
            return;
        }

        XElement element = new("validate-odata-request");

        element.AddAttribute(values, nameof(ValidateOdataRequestConfig.ErrorVariableName), "error-variable-name");
        element.AddAttribute(values, nameof(ValidateOdataRequestConfig.DefaultOdataVersion), "default-odata-version");
        element.AddAttribute(values, nameof(ValidateOdataRequestConfig.MinOdataVersion), "min-odata-version");
        element.AddAttribute(values, nameof(ValidateOdataRequestConfig.MaxOdataVersion), "max-odata-version");
        element.AddAttribute(values, nameof(ValidateOdataRequestConfig.MaxSize), "max-size");

        context.AddPolicy(element);
    }
}