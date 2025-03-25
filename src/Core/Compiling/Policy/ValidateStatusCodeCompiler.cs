using System.Xml.Linq;

using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class ValidateStatusCodeCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IOutboundContext.ValidateStatusCode);

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<ValidateStatusCodeConfig>(context, "validate-status-code",
                out var values))
        {
            return;
        }

        XElement element = new("validate-status-code");

        if (!element.AddAttribute(values, nameof(ValidateStatusCodeConfig.UnspecifiedStatusCodeAction),
                "unspecified-status-code-action"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.ArgumentList.GetLocation(),
                "validate-status-code",
                nameof(ValidateStatusCodeConfig.UnspecifiedStatusCodeAction)
            ));
            return;
        }

        element.AddAttribute(values, nameof(ValidateStatusCodeConfig.ErrorVariableName), "error-variable-name");

        if (values.TryGetValue(nameof(ValidateStatusCodeConfig.StatusCodes), out var statusCodesValue))
        {
            HandleStatusCodes(context, statusCodesValue, element);
        }

        context.AddPolicy(element);
    }

    private static void HandleStatusCodes(ICompilationContext context, InitializerValue statusCodesValue,
        XElement parentElement)
    {
        foreach (var statusCodeValue in statusCodesValue.UnnamedValues ?? [])
        {
            if (!statusCodeValue.TryGetValues<ValidateStatusCode>(out var validateStatusCodeValues))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                    statusCodeValue.Node.GetLocation(),
                    "validate-status-code.status-code",
                    nameof(ValidateStatusCode)
                ));
                continue;
            }

            XElement statusCodeElement = new("status-code");
            if (!statusCodeElement.AddAttribute(validateStatusCodeValues, nameof(ValidateStatusCode.Code), "code"))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterNotDefined,
                    statusCodeValue.Node.GetLocation(),
                    "validate-status-code.status-code",
                    nameof(ValidateStatusCode.Code)
                ));
                continue;
            }

            if (!statusCodeElement.AddAttribute(validateStatusCodeValues, nameof(ValidateStatusCode.Action), "action"))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterNotDefined,
                    statusCodeValue.Node.GetLocation(),
                    "validate-status-code.status-code",
                    nameof(ValidateStatusCode.Action)
                ));
                continue;
            }

            parentElement.Add(statusCodeElement);
        }
    }
}