// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;

public static class CompilationErrors
{
    public readonly static DiagnosticDescriptor PolicySectionCannotBeExpression = new(
        "APIM9999",
        "Policy section method is not allowed as expression",
        "Method '{0}' is not allowed as expression because it is a policy section method",
        "PolicyDocumentCompilation",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Description.",
        helpLinkUri: "TODO",
        customTags: ["APIM", "ApiManagement"]);

    public readonly static DiagnosticDescriptor OnlyOneOfTreeShouldBeDefined = new(
        "APIM9998",
        "Only one of three parameters should be defined",
        "Policy '{0}' requires only one of '{1}', '{2}' or '{3}' to be defined",
        "PolicyDocumentCompilation",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Description.",
        helpLinkUri: "TODO",
        customTags: ["APIM", "ApiManagement"]);

    public readonly static DiagnosticDescriptor OnlyOneOfTwoShouldBeDefined = new(
        "APIM9997",
        "Only one of two parameters should be defined",
        "Policy '{0}' requires only one of '{1}' or '{2}' to be defined",
        "PolicyDocumentCompilation",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Description.",
        helpLinkUri: "TODO",
        customTags: ["APIM", "ApiManagement"]);

    public readonly static DiagnosticDescriptor AtLeastOneOfTwoShouldBeDefined = new(
        "APIM9996",
        "At least One of two parameters should be defined",
        "Policy '{0}' requires at least one of '{1}' or '{2}' to be defined",
        "PolicyDocumentCompilation",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Description.",
        helpLinkUri: "TODO",
        customTags: ["APIM", "ApiManagement"]);

    public readonly static DiagnosticDescriptor NotSupportedType = new(
        "APIM9995",
        "Not supported type",
        "Not supported type '{1}' for '{0}' policy",
        "PolicyDocumentCompilation",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Description.",
        helpLinkUri: "TODO",
        customTags: ["APIM", "ApiManagement"]);

    public readonly static DiagnosticDescriptor ValueShouldBe = new(
        "APIM9994",
        "Value should be",
        "Value for '{0}' policy should be '{1}' but is not",
        "PolicyDocumentCompilation",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Description.",
        helpLinkUri: "TODO",
        customTags: ["APIM", "ApiManagement"]);

    public readonly static DiagnosticDescriptor NotSupportedStatement = new(
        "APIM9993",
        "Not supported statement",
        "Statement '{0}' is not supported in policy document",
        "PolicyDocumentCompilation",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Description.",
        helpLinkUri: "TODO",
        customTags: ["APIM", "ApiManagement"]);

    public readonly static DiagnosticDescriptor ExpressionNotSupported = new(
        "APIM9992",
        "Not supported expression",
        "Expression of type '{0}' not supported in policy document. Only '{1}' is supported.",
        "PolicyDocumentCompilation",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Description.",
        helpLinkUri: "TODO",
        customTags: ["APIM", "ApiManagement"]);

    public readonly static DiagnosticDescriptor MethodNotSupported = new(
        "APIM9991",
        "Not supported method",
        "Method '{0}' not supported in policy document",
        "PolicyDocumentCompilation",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Description.",
        helpLinkUri: "TODO",
        customTags: ["APIM", "ApiManagement"]);

    public readonly static DiagnosticDescriptor ArgumentCountMissMatchForPolicy = new(
        "APIM2001",
        "Argument count miss match for policy",
        "Argument count miss match for '{0}' policy",
        "PolicyDocumentCompilation",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Description.",
        helpLinkUri: "TODO",
        customTags: ["APIM", "ApiManagement"]);

    public readonly static DiagnosticDescriptor PolicyArgumentIsNotAnObjectCreation = new(
        "APIM2002",
        "Argument should be an object creation expression",
        "Argument for '{0}' policy should be an object creation expression",
        "PolicyDocumentCompilation",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Description.",
        helpLinkUri: "TODO",
        customTags: ["APIM", "ApiManagement"]);

    public readonly static DiagnosticDescriptor PolicyArgumentIsNotOfRequiredType = new(
        "APIM2002",
        "Argument should be of required type",
        "Argument for '{0}' policy should be of type '{1}' but is not",
        "PolicyDocumentCompilation",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Description.",
        helpLinkUri: "TODO",
        customTags: ["APIM", "ApiManagement"]);

    public readonly static DiagnosticDescriptor PolicyObjectCreationDoesNotContainInitializerSection = new(
        "APIM2002",
        "Object creation should contain initializer section",
        "Argument should be an object creation expression with initializer section",
        "PolicyDocumentCompilation",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Description.",
        helpLinkUri: "TODO",
        customTags: ["APIM", "ApiManagement"]);

    public readonly static DiagnosticDescriptor ObjectInitializerContainsNotAnAssigmentExpression = new(
        "APIM2003",
        "Object initializer should only contain assigment expressions",
        "Object initializer should only contain assigment expressions",
        "PolicyDocumentCompilation",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Description.",
        helpLinkUri: "TODO",
        customTags: ["APIM", "ApiManagement"]);

    public readonly static DiagnosticDescriptor InvalidExpression = new(
        "APIM2004",
        "Argument should be an method call expression",
        "Argument should be an method call expression",
        "PolicyDocumentCompilation",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Description.",
        helpLinkUri: "TODO",
        customTags: ["APIM", "ApiManagement"]);

    public readonly static DiagnosticDescriptor NotSupportedParameter = new(
        "APIM2005",
        "Parameter definition is not supported",
        "Parameter definition is not supported",
        "PolicyDocumentCompilation",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Description.",
        helpLinkUri: "TODO",
        customTags: ["APIM", "ApiManagement"]);

    public readonly static DiagnosticDescriptor RequiredParameterNotDefined = new(
        "APIM2006",
        "Required parameter was not defined",
        "Required '{1}' parameter was not defined for '{0}' policy",
        "PolicyDocumentCompilation",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Description.",
        helpLinkUri: "TODO",
        customTags: ["APIM", "ApiManagement"]);

    public readonly static DiagnosticDescriptor RequiredParameterIsEmpty = new(
        "APIM2007",
        "Required parameter requires at least one element",
        "Required '{1}' parameter for '{0}' policy is empty but needs at least one element",
        "PolicyDocumentCompilation",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Description.",
        helpLinkUri: "TODO",
        customTags: ["APIM", "ApiManagement"]);

    public static readonly DiagnosticDescriptor RequiredParameterHasXmlErrors = new(
        "APIM2008",
        "Required parameter requires has XML errors",
        "Required '{1}' parameter for '{0}' policy has XML errors: '{2}'",
        "PolicyDocumentCompilation",
        DiagnosticSeverity.Error,
        true,
        "Description.",
        "TODO",
        ["APIM", "ApiManagement"]);

    public readonly static DiagnosticDescriptor CannotFindMethodCode = new(
        "APIM2009",
        "Cannot find method code",
        "Cannot find method code. Method '{0}' is not declared in the projects source code.",
        "PolicyDocumentCompilation",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Description.",
        helpLinkUri: "TODO",
        customTags: ["APIM", "ApiManagement"]);

    public readonly static DiagnosticDescriptor InvalidConstantReference = new(
        "APIM2010",
        "Invalid constant reference for policy parameter",
        "Argument should be an constant field reference",
        "PolicyDocumentCompilation",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Description.",
        helpLinkUri: "TODO",
        customTags: ["APIM", "ApiManagement"]);

    public readonly static DiagnosticDescriptor IsNotAConstant = new(
        "APIM2011",
        "External value is not a constant",
        "Field '{0}' should be a const for it to be inlined in the policy document",
        "PolicyDocumentCompilation",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Description.",
        helpLinkUri: "TODO",
        customTags: ["APIM", "ApiManagement"]);
}