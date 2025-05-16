// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ExpressionDefinitionAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Rules.Expression.All;

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze |
                                               GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeLambda, SyntaxKind.SimpleLambdaExpression,
            SyntaxKind.ParenthesizedLambdaExpression);
    }

    private readonly static IReadOnlySet<string> AllowedExpressionReturnTypes = new HashSet<string>()
    {
        "System.Boolean",
        "System.Byte",
        "System.Char",
        "System.DateTime",
        "System.Decimal",
        "System.Double",
        "System.Enum",
        "System.Guid",
        "System.Int16",
        "System.Int32",
        "System.Int64",
        "System.String",
        "System.UInt16",
        "System.UInt32",
        "System.UInt64",
        "System.Uri",
        "Newtonsoft.Json.Linq.JObject",
    };

    private const string ContextParamType =
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.IExpressionContext";

    private const string ContextParamName = "context";

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MethodDeclarationSyntax method)
        {
            throw new Exception();
        }

        var model = context.SemanticModel;

        var isExpression = method.AttributeLists.ContainsExpressionAttribute(model);
        if (!isExpression) return;

        var type = model.GetTypeInfo(method.ReturnType).Type;
        if (type == null)
        {
            var diagnostic = Diagnostic.Create(Rules.Expression.ReturnTypeNotAllowed, method.ReturnType.GetLocation(),
                method.ReturnType.ToString());
            context.ReportDiagnostic(diagnostic);
        }
        else
        {
            var fullTypeName = type.ToFullyQualifiedString();
            if (!AllowedExpressionReturnTypes.Contains(fullTypeName))
            {
                var diagnostic = Diagnostic.Create(Rules.Expression.ReturnTypeNotAllowed,
                    method.ReturnType.GetLocation(), fullTypeName);
                context.ReportDiagnostic(diagnostic);
            }
        }

        CheckParameters(context, method.ParameterList);
    }


    private static void AnalyzeLambda(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not LambdaExpressionSyntax lambda)
        {
            throw new Exception();
        }

        if (!lambda.IsExpressionLambda(context.SemanticModel))
        {
            return;
        }

        switch (lambda)
        {
            case ParenthesizedLambdaExpressionSyntax parenthesizedLambda:
                CheckParameters(context, parenthesizedLambda.ParameterList);
                break;
            case SimpleLambdaExpressionSyntax simpleLambda:
                CheckParameter(context, simpleLambda.Parameter);
                break;
        }
    }

    private static void CheckParameters(SyntaxNodeAnalysisContext context, ParameterListSyntax parameterList)
    {
        var parameters = parameterList.Parameters;
        if (parameters.Count != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rules.Expression.WrongParameterCount,
                parameterList.GetLocation(), parameters.Count));
        }
        else
        {
            var parameter = parameters[0];
            CheckParameter(context, parameter);
        }
    }

    private static void CheckParameter(SyntaxNodeAnalysisContext context, ParameterSyntax parameter)
    {
        var parameterSymbol = context.SemanticModel.GetDeclaredSymbol(parameter);

        if (parameterSymbol == null)
        {
            throw new Exception();
        }

        if (parameterSymbol.Type.ToFullyQualifiedString() != ContextParamType)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rules.Expression.WrongParameterType,
                parameter.Type?.GetLocation(), parameterSymbol.Type.ToFullyQualifiedString(), ContextParamType));
        }

        if (parameter.Identifier.ValueText != ContextParamName)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rules.Expression.WrongParameterName,
                parameter.Identifier.GetLocation(), parameter.Identifier.ValueText, ContextParamName));
        }
    }
}