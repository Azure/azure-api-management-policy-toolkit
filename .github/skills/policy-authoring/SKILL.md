---
name: policy-authoring
description: "Conventions and patterns for creating policy authoring types in the Azure API Management policy toolkit. Use this skill when creating or modifying config records in src/Authoring/Configs/ or adding methods to section context interfaces."
---

# Policy Authoring Patterns

This skill describes how to create authoring types (config records and context interface methods) for policies in the Azure API Management policy toolkit.

## Config Record — `src/Authoring/Configs/{PolicyName}Config.cs`

### File Template

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Configuration for the {xml-element-name} policy.<br/>
/// {Brief description of what the policy does.}
/// </summary>
public record {PolicyName}Config
{
    /// <summary>
    /// {Property description}. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required {Type} {PropertyName} { get; init; }

    /// <summary>
    /// Optional. {Property description}.
    /// </summary>
    public {Type}? {OptionalPropertyName} { get; init; }
}
```

### Property Rules

- **Required properties**: use the `required` keyword + `init` setter.
- **Optional properties**: use a nullable type (`?`) + `init` setter. Do **not** use `required`.
- **Expression-enabled properties**: decorate with `[ExpressionAllowed]` attribute (from `src/Authoring/Attributes/ExpressionAllowedAttribute.cs`). Add "Policy expressions are allowed." to the XML doc.
- **Constrained properties** (enums, allowed values): use either:
  - A C# `enum` type (preferred for fixed, type-safe values)
  - A `string` property with XML doc listing allowed values (for values documented outside code)
  - Validation is applied at **compilation time** by the compiler class, **not at authoring time**. The config record accepts any value; the compiler reports diagnostics if invalid.
- **Sub-configs** (child element arrays): define as separate `public record` types in the same file.
- **Sub-config naming convention**: Use descriptive nouns that reflect their purpose (e.g., `ApiRateLimit`, `AddressRange`, `MatchCondition`), not generic names like `Item` or `Element`.
- **Every public type and member** must have `/// <summary>` XML documentation covering:
  - Purpose
  - Required vs optional status
  - Default value (if any)
  - Expression support (yes/no)
  - Allowed values / enums (if applicable)

### Real Examples

**Simple attributes with expression support** — `RateLimitByKeyConfig.cs`:
```csharp
public record RateLimitByKeyConfig
{
    [ExpressionAllowed]
    public required int Calls { get; init; }

    [ExpressionAllowed]
    public required int RenewalPeriod { get; init; }

    [ExpressionAllowed]
    public required string CounterKey { get; init; }

    [ExpressionAllowed]
    public bool? IncrementCondition { get; init; }

    public string? RetryAfterHeaderName { get; init; }
}
```

**Nested sub-records** — `RateLimitConfig.cs`:
```csharp
public record RateLimitConfig
{
    public required int Calls { get; init; }
    public required int RenewalPeriod { get; init; }
    public string? RetryAfterHeaderName { get; init; }
    public ApiRateLimit[]? Apis { get; init; }
}

public record ApiRateLimit : EntityLimitConfig
{
    public OperationRateLimit[]? Operations { get; init; }
}

public abstract record EntityLimitConfig
{
    public string? Name { get; init; }
    public string? Id { get; init; }
    public required int Calls { get; init; }
    public required int RenewalPeriod { get; init; }
}
```

**Typed child arrays** — `IpFilterConfig.cs`:
```csharp
public record IpFilterConfig
{
    /// <summary>
    /// The action to apply: 'allow' or 'deny'. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string Action { get; init; }

    /// <summary>
    /// Optional. IP addresses. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public string[]? Addresses { get; init; }

    /// <summary>
    /// Optional. IP address ranges.
    /// </summary>
    public AddressRange[]? AddressRanges { get; init; }
}

/// <summary>
/// An IP address range with 'from' and 'to' octets.
/// </summary>
public record AddressRange
{
    /// <summary>
    /// The starting IP address. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string From { get; init; }

    /// <summary>
    /// The ending IP address. Policy expressions are allowed.
    /// </summary>
    [ExpressionAllowed]
    public required string To { get; init; }
}
```

**Constrained values with enum** — `AdvancedPolicyConfig.cs` (example pattern):
```csharp
/// <summary>
/// The caching behavior for the policy action.
/// </summary>
public enum CachingBehavior
{
    /// <summary>Cache the response.</summary>
    Store,
    /// <summary>Reuse a cached response if available.</summary>
    Validate,
    /// <summary>Bypass cache entirely.</summary>
    Bypass
}

public record AdvancedPolicyConfig
{
    /// <summary>
    /// Required. The caching behavior: 'Store', 'Validate', or 'Bypass'.
    /// </summary>
    public required CachingBehavior Behavior { get; init; }
}
```

## Context Interface Methods

Add method signatures to the appropriate section interfaces. Policies may appear in one or more sections:

- `src/Authoring/IInboundContext.cs`
- `src/Authoring/IOutboundContext.cs`
- `src/Authoring/IBackendContext.cs`
- `src/Authoring/IOnErrorContext.cs`
- `src/Authoring/IFragmentContext.cs`

### Method Signature Template

```csharp
/// <summary>
/// {Description of the policy behavior}.<br/>
/// Compiled to <a href="{documentation-url}">{xml-element-name}</a> policy.
/// </summary>
/// <param name="config">
/// Configuration for the {xml-element-name} policy.
/// </param>
void {MethodName}({PolicyName}Config config);
```

### Rules

- The method name is PascalCase and matches the config name minus the `Config` suffix (e.g., `RateLimit` for `RateLimitConfig`).
- All section interfaces extend `IHaveExpressionContext`, giving access to `ExpressionContext`.
- For parameters that are expressions (not config objects), use `[ExpressionAllowed]` on the parameter itself.
- Some policies use direct parameters instead of a config object (e.g., `void SetMethod(string method)`). Use this pattern only for policies with 1–2 simple parameters and no optional fields.

### `IFragmentContext` Duplication

`IFragmentContext` currently duplicates method signatures from the section-specific interfaces (see the `//TODO` comment at the top of the file). **Every time you add a policy method to any section interface, you must also add the same signature to `IFragmentContext.cs`.** Copy the exact same method signature verbatim. Do not attempt to refactor this pattern.
