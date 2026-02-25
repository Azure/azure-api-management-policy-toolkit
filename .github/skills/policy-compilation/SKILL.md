---
name: policy-compilation
description: "Conventions and patterns for creating policy compilers in the Azure API Management policy toolkit. Use this skill when creating or modifying compiler classes in src/Core/Compiling/Policy/."
---

# Policy Compilation Patterns

This skill describes how to implement compilers that convert authoring models to XML in the Azure API Management policy toolkit.

## Compiler Class — `src/Core/Compiling/Policy/{PolicyName}Compiler.cs`

### Config-Based Compiler Template (Most Common)

Use this for policies that take a config object parameter.

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class {PolicyName}Compiler : IMethodPolicyHandler
{
    public string MethodName => nameof(I{Section}Context.{MethodName});

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<{PolicyName}Config>(context, "{xml-element-name}", out var values))
        {
            return;
        }

        var element = new XElement("{xml-element-name}");

        // Required attributes — report diagnostic and return on failure
        if (!element.AddAttribute(values, nameof({PolicyName}Config.{RequiredProp}), "{xml-attr-name}"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "{xml-element-name}",
                nameof({PolicyName}Config.{RequiredProp})
            ));
            return;
        }

        // Optional attributes — no error reporting needed
        element.AddAttribute(values, nameof({PolicyName}Config.{OptionalProp}), "{xml-attr-name}");

        // Child elements — iterate sub-configs if applicable
        // (see "Handling Child Elements" below)

        context.AddPolicy(element);
    }
}
```

### Direct-Parameter Compiler Template

Use this for policies with 1–2 simple parameters and no optional fields (e.g., `SetMethod`, `FindAndReplace`).

```csharp
public class {PolicyName}Compiler : IMethodPolicyHandler
{
    public string MethodName => nameof(I{Section}Context.{MethodName});

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        if (node.ArgumentList.Arguments.Count != {expectedCount})
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.ArgumentCountMissMatchForPolicy,
                node.ArgumentList.GetLocation(),
                "{xml-element-name}"));
            return;
        }

        var value = node.ArgumentList.Arguments[0].Expression.ProcessParameter(context);
        context.AddPolicy(new XElement("{xml-element-name}", value));
    }
}
```

## Auto-Registration

Compiler classes do **not** need manual DI registration. The `CompilerModule.AddMethodPolicyHandlers()` method in `src/Core/IoC/CompilerModule.cs` uses reflection to find all types matching:
- `IsClass: true`
- `IsAbstract: false`
- `IsPublic: true`
- `Namespace: "Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy"`
- Implements `IMethodPolicyHandler`

Just ensure your compiler class meets all these criteria and it will be auto-registered.

## Key Utility Methods

### `TryExtractingConfigParameter<T>`

Extracts the config object from the method invocation. Returns a dictionary of property name → `InitializerValue`. Returns `false` and reports diagnostics if extraction fails.

### `AddAttribute`

Extension method on `XElement`. Adds an XML attribute from the values dictionary:
- **Property name to XML attribute conversion**: C# property names are converted to kebab-case XML attribute names automatically. For example:
  - `RenewalPeriod` → `renewal-period`
  - `RetryAfterHeaderName` → `retry-after-header-name`
  - The conversion happens in the `AddAttribute` helper; you supply the kebab-case name in the compiler call.
- **Expression handling**: Automatically wraps expression values in `@(...)` when the value comes from an expression method.
- **Return value**: `true` if the attribute was added, `false` if the key was not present (property not set).

### `GenericCompiler.HandleList`

Utility in `src/Core/Compiling/Policy/GenericCompiler.cs` for compiling string arrays into repeated child elements:

```csharp
GenericCompiler.HandleList(element, values, nameof(Config.Claims), "required-claims", "claim");
// Produces: <required-claims><claim>value1</claim><claim>value2</claim></required-claims>
```

Used by `ValidateJwtCompiler` and `ValidateAzureAdTokenCompiler`.

### `ProcessParameter`

Extension method on `ExpressionSyntax`. Processes a raw argument into its string value, handling both constants and expressions. Used in direct-parameter compilers.

## Handling Child Elements

### Typed Sub-Config Arrays

For child elements backed by sub-config records (e.g., `ApiRateLimit[]`):

```csharp
if (values.TryGetValue(nameof({PolicyName}Config.{ChildArray}), out var items))
{
    foreach (var item in items.UnnamedValues!)
    {
        var childElement = new XElement("{child-element-name}");
        var childValues = item.NamedValues!;

        childElement.AddAttribute(childValues, nameof({SubConfig}.{Prop}), "{attr-name}");
        // ... more attributes

        element.Add(childElement);
    }
}
```

### String Arrays

For simple string arrays (e.g., IP addresses):

```csharp
if (values.TryGetValue(nameof({PolicyName}Config.{StringArray}), out var items))
{
    foreach (var item in items.UnnamedValues!)
    {
        element.Add(new XElement("{child-element-name}", item.Value!));
    }
}
```

## Diagnostics

Error reporting uses descriptors from `src/Core/Compiling/Diagnostics/CompilationErrors.cs`. Common ones:

| Descriptor | When to Use |
|---|---|
| `RequiredParameterNotDefined` | A required attribute/property is missing |
| `RequiredParameterIsEmpty` | A required array is present but empty |
| `AtLeastOneOfTwoShouldBeDefined` | At least one of two mutually supportive properties must be set |
| `OnlyOneOfTwoShouldBeDefined` | Exactly one of two mutually exclusive properties must be set |
| `PolicyArgumentIsNotOfRequiredType` | A child element has the wrong type |
| `ArgumentCountMissMatchForPolicy` | Wrong number of arguments in direct-parameter compilers |

## XML Namespaces

- By default, compiled XML policies **do not** include `xmlns` attributes. XML elements use the element name directly (e.g., `<rate-limit />`), and the namespace context is managed at the gateway level.
- If a specific policy requires namespace prefixes or explicit schema references, this must be specified during authoring Phase 1. Handle with care and test thoroughly.

## `IReturnValueMethodPolicyHandler`

A second compiler interface exists for policies that return a value assigned to a variable (e.g., `AuthenticationManagedIdentity` returning a token). This interface is **not** auto-registered by `CompilerModule` and the syntax compiler for it (`LocalDeclarationStatementCompiler`) is currently commented out in `CompilerModule.cs`. If you encounter a policy that assigns its result to a variable, flag this to the user as a special case requiring manual wiring.
