---
name: policy-codebase-reference
description: "Reference guide for the Azure API Management policy toolkit codebase structure. Use this skill when you need to find existing policies, infrastructure, or naming conventions."
---

# Policy Toolkit Codebase Reference

This skill provides an inventory of the codebase structure, shared infrastructure, reference policy selection, and naming conventions for the Azure API Management policy toolkit.

## File Path Conventions

### Authoring & Compilation

| Artifact | Location | Example |
|---|---|---|
| Config record | `src/Authoring/Configs/{PolicyName}Config.cs` | `RateLimitConfig.cs` |
| Expression attribute | `src/Authoring/Attributes/ExpressionAllowedAttribute.cs` | — |
| Section interfaces | `src/Authoring/I{Section}Context.cs` | `IInboundContext.cs` |
| Compiler class | `src/Core/Compiling/Policy/{PolicyName}Compiler.cs` | `RateLimitCompiler.cs` |
| Compiler test class | `test/Test.Core/Compiling/{PolicyName}Tests.cs` | `RateLimitTests.cs` |
| Available policies doc | `docs/AvailablePolicies.md` | — |
| Diagnostics | `src/Core/Compiling/Diagnostics/CompilationErrors.cs` | — |
| IoC / auto-registration | `src/Core/IoC/CompilerModule.cs` | — |
| Test initialization | `test/Test.Core/CompilerTestInitialize.cs` | — |
| Assertion helpers | `test/Test.Core/Assertions/` | `CompilationResultAssertion.cs` |
| Generic compiler utilities | `src/Core/Compiling/Policy/GenericCompiler.cs` | — |
| Add-policy guide | `docs/AddPolicyGuide.md` | — |

### Gateway Emulator

| Artifact | Location | Example |
|---|---|---|
| Emulator handler class | `src/Testing/Emulator/Policies/{PolicyName}Handler.cs` | `SetHeaderHandler.cs` |
| Handler base classes | `src/Testing/Emulator/Policies/PolicyHandler.cs` | — |
| IPolicyHandler interface | `src/Testing/Emulator/IPolicyHandler.cs` | — |
| Section attribute | `src/Testing/Emulator/Policies/SectionAttribute.cs` | — |
| Argument extensions | `src/Testing/Emulator/Policies/ArgumentsExtensions.cs` | — |
| SectionContextProxy | `src/Testing/Emulator/SectionContextProxy.cs` | — |
| GatewayContext | `src/Testing/GatewayContext.cs` | — |
| TestDocument | `src/Testing/TestDocument.cs` | — |
| Emulator test class | `test/Test.Testing/Emulator/Policies/{PolicyName}Tests.cs` | `SetHeaderTests.cs` |
| Emulator policy checklist | `docs/EmulatorPolicyChecklist.md` | — |

## Naming Conventions

### Authoring & Compilation

| Artifact | Convention | Example |
|---|---|---|
| Config record | `{PolicyName}Config` | `RateLimitConfig` |
| Sub-config record | `{DescriptiveName}` | `ApiRateLimit`, `AddressRange` |
| Compiler class | `{PolicyName}Compiler` | `RateLimitCompiler` |
| Compiler test class | `{PolicyName}Tests` | `RateLimitTests` |
| Context method | `{PolicyName}` | `RateLimit` |
| XML element name | kebab-case | `rate-limit` |
| Config namespace | `Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring` | — |
| Compiler namespace | `Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy` | — |
| Compiler test namespace | `Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling` | — |

### Gateway Emulator

| Artifact | Convention | Example |
|---|---|---|
| Handler class | `{PolicyName}Handler` | `SetHeaderHandler` |
| Emulator test class | `{PolicyName}Tests` | `SetHeaderTests` |
| PolicyName property | PascalCase method name from section interface | `SetHeader` |
| Handler namespace | `Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies` | — |
| Emulator test namespace | `Test.Emulator.Emulator.Policies` | — |

> **Note:** `JsonToXmlHandle.cs` is a naming inconsistency in the source (missing the `r` in `Handler`). Do not rename it.

## Copyright Header

Every new `.cs` file must start with:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
```

## Reference Policy Selection Guide

When implementing a new policy, choose the closest structural match as your reference:

| Policy Structure | Reference Config | Reference Compiler | Reference Tests |
|---|---|---|---|
| Simple attributes only | `RateLimitByKeyConfig` | `RateLimitByKeyCompiler` | `RateLimitByKeyTests` |
| Attributes + child elements | `RateLimitConfig` | `RateLimitCompiler` | `RateLimitTests` |
| Attributes + typed child arrays | `IpFilterConfig` | `IpFilterCompiler` | `IpFilterTests` |
| String-list child elements | `ValidateJwtConfig` | `ValidateJwtCompiler` | `ValidateJwtTests` |
| Complex nested configs | `CorsConfig` | `CorsCompiler` | `CorsTests` |
| Direct parameters (no config) | — | `SetMethodCompiler` | `SetMethodTests` |

## Shared Infrastructure Inventory

### Expression Support

- **`[ExpressionAllowed]`** attribute (`src/Authoring/Attributes/ExpressionAllowedAttribute.cs`) — Marks properties or parameters that accept policy expressions.
- **`Expression<T>`** delegate (`src/Authoring/Expression.cs`) — `delegate T Expression<out T>(IExpressionContext context)`.
- **`IHaveExpressionContext`** interface (`src/Authoring/IHaveExpressionContext.cs`) — Base interface for all section contexts, providing `IExpressionContext ExpressionContext { get; }`.

### Compiler Infrastructure

- **`IMethodPolicyHandler`** interface (`src/Core/Compiling/IMethodPolicyHandler.cs`) — The primary compiler interface. Properties: `string MethodName { get; }`. Method: `void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)`.
- **`IReturnValueMethodPolicyHandler`** interface (`src/Core/Compiling/IReturnValueMethodPolicyHandler.cs`) — For policies returning a value. Currently disabled (the `LocalDeclarationStatementCompiler` is commented out in `CompilerModule.cs`).
- **Auto-registration** (`src/Core/IoC/CompilerModule.cs`) — Uses reflection to find and register all public, non-abstract classes in the `Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy` namespace that implement `IMethodPolicyHandler`. No manual DI wiring needed for standard compilers.
- **`GenericCompiler.HandleList`** (`src/Core/Compiling/Policy/GenericCompiler.cs`) — Utility for compiling string arrays into repeated child XML elements.
- **`CompilationErrors`** (`src/Core/Compiling/Diagnostics/CompilationErrors.cs`) — Static class with `DiagnosticDescriptor` fields for all compilation error types.

### Compiler Test Infrastructure

- **`CompilerTestInitialize`** (`test/Test.Core/CompilerTestInitialize.cs`) — Assembly-level MSTest setup. Creates Roslyn compilation with references to `System`, `System.Xml.Linq`, and the Authoring assembly. Provides the `CompileDocument()` extension method.
- **`CompilationResultAssertion`** (`test/Test.Core/Assertions/CompilationResultAssertion.cs`) — FluentAssertions extension providing `BeSuccessful()` and `DocumentEquivalentTo(string expectedXml)`.
- **`Usings.cs`** (`test/Test.Core/Usings.cs`) — Global usings: `FluentAssertions`, `Microsoft.VisualStudio.TestTools.UnitTesting`, assertion helpers, test extensions.

### Emulator Infrastructure

- **`IPolicyHandler`** interface (`src/Testing/Emulator/IPolicyHandler.cs`) — The handler interface. Properties: `string PolicyName { get; }`. Method: `object? Handle(GatewayContext context, object?[]? args)`.
- **`PolicyHandler<T>`** base classes (`src/Testing/Emulator/Policies/PolicyHandler.cs`) — Three generic variants: single config, optional config, and two-parameter. All include `CallbackSetup` for test mocking.
- **`SectionContextProxy<T>`** (`src/Testing/Emulator/SectionContextProxy.cs`) — Uses `System.Reflection.DispatchProxy` to dynamically route method calls to handlers. Discovers handlers via reflection on namespace + `[Section]` attribute.
- **`[Section]`** attribute (`src/Testing/Emulator/Policies/SectionAttribute.cs`) — Constructor takes `string scope` (use `nameof(IInboundContext)`, etc.). Multiple attributes can be stacked.
- **`GatewayContext`** (`src/Testing/GatewayContext.cs`) — Central mock object with section proxies, mock stores (CertificateStore, CacheStore, ResponseExampleStore, LoggerStore), and expression context.
- **`TestDocument`** (`src/Testing/TestDocument.cs`) — Orchestrates test execution across policy sections. Wraps `IDocument` and calls section methods through proxies.
- **`FinishSectionProcessingException`** — Thrown by handlers (e.g., MockResponse, ReturnResponse) to terminate section processing early.

### Emulator Test Infrastructure

- **`.AsTestDocument()`** extension — Creates `TestDocument` from `IDocument` for fluent test setup.
- **`SetupInbound()`/`SetupOutbound()`/etc.** — Returns `MockPoliciesProvider<TSection>` for mock configuration.
- **`Usings.cs`** (`test/Test.Testing/Usings.cs`) — Global usings for `FluentAssertions` and MSTest.

## Section Interfaces

Policies can be available in one or more pipeline sections:

| Section | Interface File | XML Section |
|---|---|---|
| Inbound | `src/Authoring/IInboundContext.cs` | `<inbound>` |
| Outbound | `src/Authoring/IOutboundContext.cs` | `<outbound>` |
| Backend | `src/Authoring/IBackendContext.cs` | `<backend>` |
| On-Error | `src/Authoring/IOnErrorContext.cs` | `<on-error>` |
| Fragment | `src/Authoring/IFragmentContext.cs` | N/A (reusable fragment) |

Note: `IFragmentContext` duplicates method signatures from other interfaces (see `//TODO` at top of file). Copy signatures verbatim when adding policies there.

## Emulator Reference Handler Selection Guide

When implementing a new emulator handler, choose the closest structural match:

| Policy Structure | Reference Handler | Reference Test |
|---|---|---|
| Single config, simple state change | `SetStatusHandler` | `SetStatusTests` |
| Two parameters (name + values) | `SetHeaderHandler` | `SetHeaderTests` |
| Config with callback hooks only (no-op) | `RateLimitHandler` | — |
| Optional config | `MockResponseHandler` | `MockResponseTests` |
| Validation + short-circuit | `CheckHeaderHandler` | `CheckHeaderTests` |
| Authentication + token | `AuthenticationManagedIdentityHandler` | `AuthenticationManagedIdentityHandlerTests` |
| Cache interaction | `CacheLookupValueHandler` | `CacheLookupValueTests` |
| Logging/store interaction | `LogToEventHubHandler` | `LogToEventHubTests` |
| No-config (no-arg method) | `BaseHandler` | `BaseTests` |
| Custom flow control / wrapper | `ReturnResponseHandler` | — |

## Build and Test Commands

```bash
# Build the entire solution
dotnet build

# Run all compiler tests
dotnet test --project test/Test.Core

# Run all emulator tests
dotnet test --project test/Test.Testing

# Run tests for a specific policy
dotnet test --filter "FullyQualifiedName~{PolicyName}Tests"
```
