---
name: policy-testing
description: "Conventions and patterns for writing policy compilation tests in the Azure API Management policy toolkit. Use this skill when creating or modifying test files in test/Test.Core/Compiling/."
---

# Policy Testing Patterns

This skill describes how to write tests for policy compilation in the Azure API Management policy toolkit.

## Test File — `test/Test.Core/Compiling/{PolicyName}Tests.cs`

### File Template

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class {PolicyName}Tests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.{MethodName}(new {PolicyName}Config()
                    {
                        // required properties only
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <{xml-element-name} {attributes} />
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile {policy-name} policy"
    )]
    // ... more [DataRow] entries for each test case
    public void ShouldCompile{PolicyName}Policy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}
```

### Key Conventions

- **Namespace**: `Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling` (not the test namespace — this matches the existing convention)
- **Class name**: `{PolicyName}Tests`
- **Test method name**: `ShouldCompile{PolicyName}Policy`
- **Framework**: MSTest (`[TestClass]`, `[TestMethod]`, `[DataRow]`)
- **Assertions**: FluentAssertions via `code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml)`
- **Each test scenario** is a separate `[DataRow]` attribute on a single test method, with a `DisplayName`
- **C# code strings** use raw string literals (`"""..."""`) for readability
- **Test code does not need `using` statements** — `CompilerTestInitialize.CompileDocument()` wraps them automatically (adds `using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;` and `using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;`)

## Required Test Coverage

Each of the following must be a separate `[DataRow]`:

### 1. Required Fields Only (Constant Values)

The baseline test — all required properties set, no optional properties:

```csharp
[DataRow(
    """
    [Document]
    public class PolicyDocument : IDocument
    {
        public void Inbound(IInboundContext context) {
            context.RateLimit(new RateLimitConfig()
                {
                    Calls = 100,
                    RenewalPeriod = 10
                });
        }
    }
    """,
    """
    <policies>
        <inbound>
            <rate-limit calls="100" renewal-period="10" />
        </inbound>
    </policies>
    """,
    DisplayName = "Should compile rate limit policy"
)]
```

### 2. Each Optional Field Individually

One `[DataRow]` per optional field, added on top of required fields:

```csharp
DisplayName = "Should compile rate limit policy with retry after header name"
```

### 3. Expression Values

For every property marked with `[ExpressionAllowed]`, add a test using the expression pattern:

```csharp
[DataRow(
    """
    [Document]
    public class PolicyDocument : IDocument
    {
        public void Inbound(IInboundContext context) {
            context.RateLimitByKey(new RateLimitByKeyConfig()
                {
                    Calls = CallsExp(context.ExpressionContext),
                    RenewalPeriod = RenewalPeriodExp(context.ExpressionContext),
                    CounterKey = CounterKeyExp(context.ExpressionContext)
                });
        }
        
        int CallsExp(IExpressionContext context) => 100;
        int RenewalPeriodExp(IExpressionContext context) => 10;
        string CounterKeyExp(IExpressionContext context) => context.Product.Name;
    }
    """,
    """
    <policies>
        <inbound>
            <rate-limit-by-key calls="@(100)" renewal-period="@(10)" counter-key="@(context.Product.Name)" />
        </inbound>
    </policies>
    """,
    DisplayName = "Should compile rate limit by key policy with expressions"
)]
```

**Expression pattern rules:**
- **Expression method naming**: Name expression helper methods as `{PropertyName}Exp` (e.g., `CallsExp`, `CounterKeyExp`). Method must take `IExpressionContext` and return the appropriate type.
- Call it as `PropertyName = MethodName(context.ExpressionContext)` in the config initializer.
- The expected XML wraps the method's return expression in `@(...)`.
- Expression methods are defined as members of the `PolicyDocument` class.

### 4. Child Elements

If the policy has child element arrays:

```csharp
DisplayName = "Should compile rate limit policy with apis"
DisplayName = "Should compile rate limit policy with operations in api"
```

### 5. Multi-Section Tests

If the policy is available in multiple sections (e.g., inbound, outbound, backend), **add one test per section**:

```csharp
"""
[Document]
public class PolicyDocument : IDocument
{
    public void Outbound(IOutboundContext context) {
        context.{MethodName}(new {PolicyName}Config() { /* ... */ });
    }
}
""",
"""
<policies>
    <outbound>
        <{xml-element-name} {attributes} />
    </outbound>
</policies>
""",
DisplayName = "Should compile {policy-name} policy in outbound section"
```

Repeat for each additional section (backend, on-error, etc.).

### 6. All Optional Fields Together (if combinatorially important)

When interactions between optional fields matter, add a combined test.

### 7. Error Cases (Optional but Recommended)

For each required field, optionally add one test demonstrating omission:

```csharp
[DataRow(
    """
    [Document]
    public class PolicyDocument : IDocument
    {
        public void Inbound(IInboundContext context) {
            context.RateLimit(new RateLimitConfig()
                {
                    RenewalPeriod = 60
                    // Missing 'Calls' — required
                });
        }
    }
    """,
    null, // No valid XML expected; compilation produces diagnostic errors
    DisplayName = "Should report error when required 'Calls' field is missing"
)]
```

When `expectedXml` is `null`, the test expects compilation to fail with diagnostic errors. The compiler's validation logic (in Phase 6) will report the missing required field.

**Error case naming**: Use `DisplayName` values like "Should report error when {field} is missing" or "Should report error when {value} exceeds maximum."

## Test Infrastructure

Tests rely on shared infrastructure that you do not need to modify:

- **`CompilerTestInitialize`** (`test/Test.Core/CompilerTestInitialize.cs`) — Assembly-level setup that creates the Roslyn compilation environment and `DocumentCompiler` instance. Provides the `CompileDocument()` extension method.
- **`CompilationResultAssertion`** (`test/Test.Core/Assertions/CompilationResultAssertion.cs`) — FluentAssertions extensions for `BeSuccessful()` and `DocumentEquivalentTo()`.
- **`Usings.cs`** (`test/Test.Core/Usings.cs`) — Global usings for `FluentAssertions`, `Microsoft.VisualStudio.TestTools.UnitTesting`, and assertion helpers.
