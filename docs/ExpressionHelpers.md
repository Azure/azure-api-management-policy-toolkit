# Expression helpers

Methods in a policy document that take an `IExpressionContext` are compiled to policy expressions. This guide covers
what those methods, and the helpers they call, can contain.

API Management compiles policy expressions as C# 7 and only allows `context` and a set of .NET types in them, so the
compiler expands helper calls into a single expression rather than emitting calls to methods that don't exist at
runtime.

## Calling other helpers

An expression method may call other methods declared in source, including methods in referenced projects. The
compiler recursively expands expression-bodied methods and methods containing a single `return` statement, and
reports recursive or unsafe helper graphs instead of emitting source-only method calls.

```csharp
public void Inbound(IInboundContext context)
{
    context.SetHeader("X-Tenant", Tenant(context.ExpressionContext));
}

private static string Tenant(IExpressionContext context) => Normalize(Header(context, "X-Tenant"));
private static string Header(IExpressionContext context, string name) => context.Request.Headers.GetValueOrDefault(name, "");
private static string Normalize(string value) => value.Trim().ToLowerInvariant();
```

compiles to

```razor
<set-header name="X-Tenant" exists-action="override">
    <value>@(context.Request.Headers.GetValueOrDefault("X-Tenant", "").Trim().ToLowerInvariant())</value>
</set-header>
```

- Constant arguments are substituted, and source-defined constants used by helpers are emitted as C# literals.
- Helpers may take `params` arguments.
- An extension helper can be called with `?.` when its body is a member chain on the receiver and the receiver has the
  type of the `this` parameter, such as `value?.Tidy()` with `Tidy(this string s) => s.Trim().ToUpper()`.
- Variables and lambda parameters declared by an expanded helper are renamed with a numeric suffix (for example
  `value__1`) so they can't clash with the code around them.

### How arguments are evaluated

Arguments are substituted into the helper's expression rather than evaluated first. A simple argument, such as a
literal, variable, member or element access, cast, `as` or `is`, or an operator other than division and modulo on
simple operands, may be evaluated more than once or not at all, depending on how the helper uses the parameter. Any
other argument, including a call or a division that could throw, is only accepted when the helper evaluates its
parameter exactly once.

## Conditions

Policy `if` conditions support boolean helper calls combined with `!`, `&&`, `||` and parentheses.

```csharp
if (IsCompanyIp(context.ExpressionContext) && !IsHealthCheck(context.ExpressionContext))
{
    ...
}
```

## Policy configuration factories

A policy configuration argument may be a call to a factory method, also from a referenced project. The factory must
return a single `new TConfig { ... }` expression and take no parameters other than policy section contexts, such as
`IInboundContext`.

```csharp
context.RateLimitByKey(Limits.PerProduct(context));

public static RateLimitByKeyConfig PerProduct(IInboundContext context) => new RateLimitByKeyConfig
{
    Calls = 100,
    RenewalPeriod = 10,
    CounterKey = ProductKey(context.ExpressionContext)
};
```

## Sharing helpers between projects

To share helpers between projects, mark the helper class with `[Expression]`. The analyzer then allows calls to its
methods and uses of its constants from policy expressions in other projects, and checks the class's methods in the
project that declares them.

```csharp
[Expression]
public static class TextHelpers
{
    public const string TenantHeader = "X-Tenant";

    public static string Tidy(this string value) => value.Trim().ToLowerInvariant();
}
```

Helpers are inlined from source, so the library must be a project reference. Calling a helper, property or
non-constant field of a compiled library is reported, although its constants can still be used.

## Named values

`context.NamedValue("name")` compiles to a raw `{{name}}` token, so the named value is used as code.

| C# expression | Compiled expression |
| --- | --- |
| `context.NamedValue("flag") == "on"` | `{{flag}} == "on"` |
| `"Bearer " + context.NamedValue("token")` | `"Bearer {{token}}"` |
| `((string)context.NamedValue("pattern")).Length` | `@"{{pattern}}".Length` |
| `(int)context.NamedValue("port") * 2` | `(int)({{port}}) * 2` |
| `(context.NamedValue("flag")) == "on"` | `{{flag}} == "on"` |

- Concatenated directly with a string literal, the named value becomes part of that string.
- Converted to `string`, for example returned from a `string` helper, passed to a `string` or `params string[]`
  parameter or cast with `(string)`, it compiles to the verbatim string `@"{{name}}"`, so a backslash in the named
  value's text isn't an escape sequence. Merged into an adjacent string, it takes that string's kind.
- Converted to another type, the cast applies to all of the named value's text, as in `(int)({{name}})`.
- A parenthesized call, `(context.NamedValue("name"))`, compiles to `{{name}}` without the parentheses, isn't merged
  into an adjacent string and isn't converted. The decompiler writes named values used as code this way. Add a second
  pair of parentheses to keep them.
- At either end of an interpolation hole a named value is always parenthesized, `{({{name}})}`, so it can't run into
  the hole's braces.
- An expression that is only a named value compiles to the plain attribute value, `{{name}}`.

## C# language version

API Management compiles policy expressions as C# 7, so helper code that uses a newer language feature, such as switch
expressions, target-typed `new()` or nullable reference type annotations, is reported with `APIM2019`.
