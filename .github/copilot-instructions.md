# Copilot Instructions

## Project Overview

Azure API Management policy toolkit: a set of C# libraries that let users author APIM policy documents in C# instead of XML, compile them to XML, and unit-test them with a gateway emulator. .NET 8, C# 13.

## Build, Test, Lint

```bash
dotnet build                    # Build entire solution
dotnet test                     # Run all tests
dotnet test --project test/Test.Core      # Compiler tests only
dotnet test --project test/Test.Testing   # Emulator tests only
dotnet test --filter "FullyQualifiedName~RateLimitTests"  # Single test class
```

Test framework: **MSTest** with **FluentAssertions**. Tests use `[TestClass]` / `[TestMethod]` / `[DataRow]`.

## Architecture

The toolkit has a three-layer pipeline for each policy:

1. **Authoring** (`src/Authoring/`) — C# records and interfaces that users write against
   - Config records in `Configs/{PolicyName}Config.cs` define the policy shape
   - Section interfaces (`IInboundContext`, `IOutboundContext`, `IBackendContext`, `IOnErrorContext`, `IFragmentContext`) expose policy methods to users
2. **Compilation** (`src/Core/`) — Roslyn-based C#-to-XML transpiler
   - Compiler classes in `Compiling/Policy/{PolicyName}Compiler.cs` implement `IMethodPolicyHandler`
   - Auto-registered via reflection on the `Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy` namespace — no manual DI wiring needed
3. **Emulator** (`src/Testing/`) — In-memory gateway emulator for unit testing
   - Handler classes in `Emulator/Policies/{PolicyName}Handler.cs` extend `PolicyHandler<T>`
   - Discovered via reflection + `[Section(nameof(IInboundContext))]` attributes
   - `GatewayContext` is the central mock with section proxies, stores, and expression context
   - `TestDocument` orchestrates test execution across policy sections

Supporting projects: `src/Analyzers/` (Roslyn analyzers), `src/Compiling/` (CLI compiler tool), `src/Generators/` (source generators), `src/Templates/` (project templates).

## Adding a New Policy

Follow the guide in `docs/AddPolicyGuide.md`. Each policy requires coordinated changes across layers:

| Artifact | Location | Naming |
|---|---|---|
| Config record | `src/Authoring/Configs/{PolicyName}Config.cs` | `{PolicyName}Config` |
| Section interface method | `src/Authoring/I{Section}Context.cs` | `{PolicyName}(config)` |
| Compiler | `src/Core/Compiling/Policy/{PolicyName}Compiler.cs` | `{PolicyName}Compiler` |
| Compiler tests | `test/Test.Core/Compiling/{PolicyName}Tests.cs` | `{PolicyName}Tests` |
| Emulator handler | `src/Testing/Emulator/Policies/{PolicyName}Handler.cs` | `{PolicyName}Handler` |
| Emulator tests | `test/Test.Testing/Emulator/Policies/{PolicyName}Tests.cs` | `{PolicyName}Tests` |

Use structurally similar existing policies as references:
- **Simple attributes**: `RateLimitByKey`
- **Attributes + child elements**: `RateLimit`
- **String-list children**: `ValidateJwt`
- **Complex nested configs**: `Cors`
- **Direct parameters (no config)**: `SetMethod`

## Key Conventions

### Config Records
- Public records with `required init` properties for required params, nullable for optional
- `[ExpressionAllowed]` attribute on properties that accept policy expressions
- XML doc comments on the record and every property

### Compilers
- Must be `public` in namespace `Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy` (auto-registration)
- `MethodName` property returns `nameof(IInboundContext.{PolicyName})`
- Use `TryExtractingConfigParameter<T>()` to extract config, `AddAttribute()` for XML attributes
- Report missing required params via `CompilationErrors.RequiredParameterNotDefined`

### Emulator Handlers
- `internal` classes with `[Section(nameof(I{Section}Context))]` attribute(s) — stack multiple for multi-section
- Extend `PolicyHandler<TConfig>` (single config), `PolicyHandler<T1, T2>` (two params), or `PolicyHandlerOptionalParam<T>` (optional config)
- `PolicyName` property returns `nameof(IInboundContext.{PolicyName})`

### Tests
- Compiler tests use `[TestClass]` with `[DataRow]` patterns, use `code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml)`
- Cover: required-only, each optional field, expression values for `[ExpressionAllowed]` properties, each applicable section
- Emulator tests use `.AsTestDocument()`, `SetupInbound()`, `.RunInbound()` etc.

### Code Style
- Copyright header: `// Copyright (c) Microsoft Corporation.` + `// Licensed under the MIT License.`
- No `var` — use explicit types
- Private fields: `_camelCase`, private static fields: `s_camelCase`
- 4-space indentation, CRLF line endings, Allman braces
- Root namespace: `Microsoft.Azure.ApiManagement.PolicyToolkit`

## Copilot Skills and Agents

This repo has custom Copilot skills in `.github/skills/` and agents in `.github/agents/` for policy implementation workflows.

**When asked to add a new policy, always use the appropriate agent rather than implementing manually:**
- **`@implement-policy-compiled`** — use this agent to add a new policy's authoring config, context interface method, compiler, and compiler tests. It follows a mandatory phased workflow (information gathering → codebase discovery → planning → tests → authoring → compilation).
- **`@implement-emulator-policy`** — use this agent to add or fix a gateway emulator handler and its tests. It follows a mandatory phased workflow (discovery → branch → tests → handler → commit).

Supporting skills (used by the agents, also available directly):
- **`policy-authoring`** — config record and context interface conventions
- **`policy-compilation`** — compiler class patterns
- **`policy-emulator`** — emulator handler patterns
- **`policy-testing`** / **`policy-emulator-testing`** — test conventions
- **`policy-codebase-reference`** — file inventory and reference policy selection
