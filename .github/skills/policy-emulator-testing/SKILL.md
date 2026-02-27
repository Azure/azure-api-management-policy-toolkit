---
name: policy-emulator-testing
description: "Conventions and patterns for writing gateway emulator policy handler tests in the Azure API Management policy toolkit. Use this skill when creating or modifying test files in test/Test.Testing/Emulator/Policies/."
---

# Policy Emulator Testing Patterns

This skill describes how to write tests for gateway emulator policy handlers in the Azure API Management policy toolkit.

## Test File — `test/Test.Testing/Emulator/Policies/{PolicyName}Tests.cs`

### File Template

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Test.Emulator.Emulator.Policies;

[TestClass]
public class {PolicyName}Tests
{
    // Inner document class implementing IDocument
    class Simple{PolicyName} : IDocument
    {
        public void Inbound(IInboundContext context)
        {
            context.{PolicyName}(new {PolicyName}Config
            {
                // config properties
            });
        }
        public void Outbound(IOutboundContext context) { }
        public void Backend(IBackendContext context) { }
        public void OnError(IOnErrorContext context) { }
    }

    [TestMethod]
    public void Should{ExpectedBehavior}()
    {
        // Arrange
        var test = new Simple{PolicyName}().AsTestDocument();

        // Act
        test.RunInbound();

        // Assert (using FluentAssertions)
        test.Context.{assertion};
    }
}
```

### Key Conventions

- **Namespace**: `Test.Emulator.Emulator.Policies` (matches existing test convention)
- **Class name**: `{PolicyName}Tests`
- **Framework**: MSTest (`[TestClass]`, `[TestMethod]`)
- **Assertions**: FluentAssertions
- **Test document creation**: `new SimpleDocument().AsTestDocument()` extension method (or `new TestDocument(new SimpleDocument())` — both are equivalent)
- **Inner document classes** implement `IDocument` with all four section methods. Unused sections have empty bodies.
- **Section execution**: `test.RunInbound()`, `test.RunOutbound()`, `test.RunBackend()`, `test.RunOnError()`

## Required Test Coverage

Each policy must have tests covering:

### 1. Basic Execution

Create a document with the policy call, run through `TestDocument`, assert context state changes:

```csharp
[TestMethod]
public void ShouldModifyContextState()
{
    var test = new SimpleSetStatus().AsTestDocument();

    test.RunInbound();

    test.Context.Response.StatusCode.Should().Be(200);
    test.Context.Response.StatusReason.Should().Be("OK");
}
```

### 2. Callback Override

Verify that `SetupInbound()` (or appropriate section) `.{PolicyName}().WithCallback(...)` correctly overrides default behavior:

```csharp
[TestMethod]
public void ShouldExecuteCallback()
{
    var test = new Simple{PolicyName}().AsTestDocument();

    test.SetupInbound()
        .{PolicyName}()
        .WithCallback((context, config) =>
        {
            context.Variables["test"] = "value";
        });

    test.RunInbound();

    test.Context.Variables.Should().ContainKey("test")
        .WhoseValue.Should().Be("value");
}
```

### 3. Predicate-Based Mock

For policies called multiple times with different configs in the same section:

```csharp
[TestMethod]
public void ShouldSelectCorrectCallback()
{
    var test = new Multi{PolicyName}().AsTestDocument();

    test.SetupInbound()
        .{PolicyName}((ctx, config) => config.SomeProperty == "A")
        .WithCallback((ctx, cfg) => { /* handle A */ });

    test.SetupInbound()
        .{PolicyName}((ctx, config) => config.SomeProperty == "B")
        .WithCallback((ctx, cfg) => { /* handle B */ });

    test.RunInbound();
    // Assert...
}
```

### 4. Pre-configured Context

When the handler depends on existing context state:

```csharp
[TestMethod]
public void ShouldHandleExistingHeaders()
{
    var test = new Simple{PolicyName}().AsTestDocument();
    test.Context.Request.Headers["Content-Type"] = ["application/json"];

    test.RunInbound();

    test.Context.Request.Headers.Should().ContainKey("Content-Type");
}
```

### 5. Error/Exception Scenarios

Not all policies support `.WithError()`. This API is specific to certain mock providers (e.g., `MockAuthenticationManagedIdentityProvider`). For policies without a dedicated error API, simulate errors via callbacks:

```csharp
[TestMethod]
public void ShouldSimulateError()
{
    var test = new Simple{PolicyName}().AsTestDocument();

    test.SetupInbound()
        .{PolicyName}()
        .WithCallback((context, config) =>
        {
            context.Response.StatusCode = 500;
            context.Response.StatusReason = "Internal Server Error";
            throw new FinishSectionProcessingException();
        });

    test.RunInbound();

    test.Context.Response.StatusCode.Should().Be(500);
}
```

For authentication policies that have `.WithError()`:

```csharp
[TestMethod]
public void ShouldThrowOnError()
{
    var test = new SimpleAuthenticationManagedIdentity().AsTestDocument();

    test.SetupInbound()
        .AuthenticationManagedIdentity()
        .WithError("InternalServerError");

    var ex = Assert.ThrowsException<PolicyException>(() => test.RunInbound());
    ex.Policy.Should().Be("AuthenticationManagedIdentity");
}
```

### 6. Multi-Section Tests

If the policy is available in multiple sections, add one test per section:

```csharp
[TestMethod]
public void ShouldWorkInOutboundSection()
{
    var test = new Outbound{PolicyName}().AsTestDocument();

    test.RunOutbound();

    test.Context.{assertion};
}
```

## Mock Setup Patterns

### Token Provider Hook (Authentication policies)

```csharp
test.SetupInbound()
    .AuthenticationManagedIdentity()
    .WithTokenProviderHook((resource, clientId) => $"{resource}{clientId}/token");
```

### Return Value (Authentication, Cache)

```csharp
test.SetupInbound()
    .AuthenticationManagedIdentity()
    .ReturnsToken("mock-token");
```

### Cache Store Setup

```csharp
test.SetupCacheStore()
    .WithExternalCacheSetup()
    .WithExternalCacheValue("key", "test");

test.SetupCacheStore().WithInternalCacheValue("key", "test");
```

### Response Example Store (MockResponse)

```csharp
var example = new ResponseExample(200, "{ \"ok\": true }", "application/json");
test.SetupResponseExampleStore().Add(test.Context, example);
```

## Test Infrastructure

Tests rely on shared infrastructure that you do not need to modify:

- **`TestDocument`** (`src/Testing/TestDocument.cs`) — Wraps an `IDocument` and executes sections through proxies. Catches `FinishSectionProcessingException` gracefully.
- **`.AsTestDocument()`** extension method — Creates `TestDocument` from `IDocument`.
- **`SetupInbound()`/`SetupOutbound()`/etc.** — Returns `MockPoliciesProvider<TSection>` for fluent mock configuration.
- **`GatewayContext`** (`src/Testing/GatewayContext.cs`) — Contains mock request/response, variables, and emulator stores.
- **`Usings.cs`** (`test/Test.Testing/Usings.cs`) — Global usings for `FluentAssertions` and `Microsoft.VisualStudio.TestTools.UnitTesting`.

## Build and Test Commands

```bash
# Run all emulator tests
dotnet test --project test/Test.Testing

# Run tests for a specific policy
dotnet test --project test/Test.Testing --filter "FullyQualifiedName~{PolicyName}Tests"
```
