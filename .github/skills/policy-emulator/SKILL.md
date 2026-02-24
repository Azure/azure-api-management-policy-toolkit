---
name: policy-emulator
description: "Conventions and patterns for creating gateway emulator policy handlers in the Azure API Management policy toolkit. Use this skill when creating or modifying handler classes in src/Testing/Emulator/Policies/."
---

# Policy Emulator Handler Patterns

This skill describes how to implement gateway emulator policy handlers that simulate Azure API Management policy behavior in the local testing framework.

For the current backlog of policies that need implementation, see `docs/EmulatorPolicyChecklist.md`.

## Handler File — `src/Testing/Emulator/Policies/{PolicyName}Handler.cs`

### Auto-Registration

Handlers are discovered automatically by `SectionContextProxy` via reflection. Requirements:
- Class must be in namespace `Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies`
- Class must implement `IPolicyHandler`
- Class must have one or more `[Section(nameof(I{Section}Context))]` attributes
- No manual DI registration needed

### Handler Base Classes

There are four base class patterns. Choose based on the policy's method signature in the section interfaces.

#### `PolicyHandler<TConfig>` — Single Config (Most Common)

For policies that take a single config object parameter.

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Emulator.Policies;

[Section(nameof(IInboundContext))]
internal class {PolicyName}Handler : PolicyHandler<{PolicyName}Config>
{
    public override string PolicyName => nameof(IInboundContext.{PolicyName});

    protected override void Handle(GatewayContext context, {PolicyName}Config config)
    {
        // Implementation here
    }
}
```

**Built-in behavior:** Has `CallbackSetup` list of `(predicate, action)` tuples. If any predicate matches, the callback is executed instead of `Handle()`.

#### `PolicyHandlerOptionalParam<TConfig>` — Optional Config

Use when the entire policy call can omit the config object (the method can be called with no arguments), not just when individual config properties are optional.

```csharp
[Section(nameof(IInboundContext))]
internal class {PolicyName}Handler : PolicyHandlerOptionalParam<{PolicyName}Config>
{
    public override string PolicyName => nameof(IInboundContext.{PolicyName});

    protected override void Handle(GatewayContext context, {PolicyName}Config? config)
    {
        // config may be null
    }
}
```

#### `PolicyHandler<TParam1, TParam2>` — Two Parameters

For policies with two direct parameters (e.g., SetHeader takes name + values).

```csharp
[Section(nameof(IInboundContext))]
[Section(nameof(IOutboundContext))]
internal class {PolicyName}Handler : PolicyHandler<string, string[]>
{
    public override string PolicyName => nameof(IInboundContext.{PolicyName});

    protected override void Handle(GatewayContext context, string param1, string[] param2)
    {
        // Implementation here
    }
}
```

#### `IPolicyHandler` — Direct Implementation (Complex)

Use for wrapper policies (Retry, Wait), no-config policies, or handlers with non-standard signatures.

```csharp
[Section(nameof(IInboundContext))]
internal class {PolicyName}Handler : IPolicyHandler
{
    public string PolicyName => nameof(IInboundContext.{PolicyName});

    public List<Tuple<Func<GatewayContext, {PolicyName}Config, bool>, Action<GatewayContext, {PolicyName}Config>>> CallbackHooks { get; } = [];

    public object? Handle(GatewayContext context, object?[]? args)
    {
        var config = args.ExtractArgument<{PolicyName}Config>();

        foreach (var (predicate, callback) in CallbackHooks)
        {
            if (predicate(context, config))
            {
                callback(context, config);
                return null;
            }
        }

        // Default implementation here
        return null;
    }
}
```

### Reference Handler Selection Guide

When implementing a new handler, choose the closest structural match as your reference:

| Policy Structure | Reference Handler |
|---|---|
| Single config, simple state change | `SetStatusHandler` |
| Two parameters (name + values) | `SetHeaderHandler` |
| Config with callback hooks only (no-op) | `RateLimitHandler` |
| Optional config | `MockResponseHandler` |
| Validation + short-circuit | `CheckHeaderHandler` |
| Authentication + token | `AuthenticationManagedIdentityHandler` |
| Cache interaction | `CacheLookupValueHandler` |
| Logging/store interaction | `LogToEventHubHandler` |
| No-config (no-arg method) | `BaseHandler` |
| Custom flow control / wrapper | `ReturnResponseHandler` |

---

## Mocking Strategies

Choose based on what the real Azure APIM policy does.

### No-Op with Callbacks (Rate Limiting, Metrics, Token Limits)

Default `Handle` does nothing. All behavior is injected via `CallbackSetup`:

```csharp
protected override void Handle(GatewayContext context, RateLimitConfig config)
{
    // No-op by default in emulator.
    // Test authors use CallbackSetup to simulate rate limit exceeded, etc.
}
```

### Context Mutation (SetHeader, SetBody, SetVariable, RewriteUri)

Actually modifies context state:

```csharp
protected override void Handle(GatewayContext context, string name, string[] values)
{
    context.Request.Headers[name] = values;
}
```

### Validation + Short-Circuit (CheckHeader, ValidateJwt)

Validates conditions, throws `FinishSectionProcessingException` on failure:

```csharp
protected override void Handle(GatewayContext context, CheckHeaderConfig config)
{
    if (!context.Request.Headers.ContainsKey(config.HeaderName))
    {
        context.Response.StatusCode = config.FailCheckHttpCode;
        // set error body...
        throw new FinishSectionProcessingException();
    }
}
```

### External Service Mock (SendRequest, Dapr, ForwardRequest)

Uses callbacks to provide mock responses. Default should throw or no-op:

```csharp
protected override void Handle(GatewayContext context, SendRequestConfig config)
{
    // Default: no-op, requires callback setup
    throw new NotImplementedException(
        "SendRequest requires mock setup. Use SetupInbound().SendRequest().WithCallback(...)");
}
```

### Store Interaction (Cache, LogToEventHub, Trace)

Reads from or writes to emulator stores:

```csharp
protected override void Handle(GatewayContext context, LogToEventHubConfig config)
{
    // Validate and store
    context.LoggerStore.AddEvent(config.LoggerId, message);
}
```

### Wrapper / Flow Control (Retry, Wait)

Executes inner delegate actions. Requires direct `IPolicyHandler` implementation:

```csharp
public object? Handle(GatewayContext context, object?[]? args)
{
    var config = args.ExtractArgument<RetryConfig>();
    var action = args.ExtractArgument<Action<IInboundContext>>();

    for (int i = 0; i < config.Count; i++)
    {
        try { action(context.InboundProxy.Object); break; }
        catch { if (i == config.Count - 1) throw; }
    }
    return null;
}
```

---

## Edge Cases

### Wrapper Policies (Retry, Wait, LimitConcurrency)
These policies wrap inner policy delegates. They require custom `IPolicyHandler` implementations that execute delegate parameters. Study `ReturnResponseHandler` for the direct-implementation pattern.

### Inherited Handlers (AzureOpenAi variants)
AzureOpenAi handlers inherit from their Llm counterparts. Implement the Llm handler first, then create the AzureOpenAi variant as a thin subclass with only `PolicyName` override.

### Policies Without Config (CrossDomain, RedirectContentUrls)
Some policies have no config parameter — they are no-arg methods. These need a custom handler approach. Study `BaseHandler` for the no-config pattern.

### JsonToXmlHandle Naming
`JsonToXmlHandle.cs` is a naming inconsistency in the source (missing the `r` in `Handler`). Do not rename it — match the existing name when referencing.
