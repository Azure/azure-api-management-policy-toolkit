# Azure API Management policy toolkit

**Azure API management policy toolkit** is a set of libraries and tools for authoring [**policy documents**](https://learn.microsoft.com/azure/api-management/api-management-howto-policies) for [**Azure API Management**](https://learn.microsoft.com/azure/api-management/). The toolkit was designed to help **create** and **test** policy documents with complex expressions.

Before the Policy toolkit, policy documents were written in Razor format, which is hard to read and understand, especially when there are multiple expressions. The feedback loop on new documents or even the smallest changes was very long, requiring a live Azure API Management instance, a policy document deployment, and manual testing through the API request.

The policy toolkit changes that. It allows you to write policy documents in C# language, which is more natural and doesn't require you to jump between C# and XML for expression creation. Creating policy documents in C# also brings the advantage of using simple C# code for unit testing of policy documents.

The toolkit also includes a **decompiler** that converts existing APIM policy XML documents into C# code, enabling round-trip workflows: decompile existing policies to C#, edit them, then compile back to XML. See the [available policies](docs/AvailablePolicies.md) for supported policies and the `src/Decompiling/` CLI tool for batch decompilation.

## Documentation

The toolkit is available from NuGet:

* [Templates](https://www.nuget.org/packages/Microsoft.Azure.ApiManagement.PolicyToolkit.Templates)
* [Testing](https://www.nuget.org/packages/Microsoft.Azure.ApiManagement.PolicyToolkit.Testing)
* [Authoring](https://www.nuget.org/packages/Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring)
* [Compiling](https://www.nuget.org/packages/Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling)
* [Decompiling](https://www.nuget.org/packages/Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling)

#### Azure API Management policy toolkit documentation for users.
* [Quick start](docs/QuickStart.md)
* [Available policies](docs/AvailablePolicies.md)
* [Gateway emulator policy coverage and limitations](docs/EmulatorPolicyChecklist.md)
* [Solution structure recommendation](docs/SolutionStructureRecommendation.md)
* [Steps for deploying policies created by the policy toolkit](docs/IntegratePolicySolution.md)
* [Integrate policy solution with APIOps](docs/IntegratePolicySolutionWithApiOps.md)

#### Azure API Management policy toolkit documentation for contributors.
* [Contributor guide](CONTRIBUTING.md)
* [Development environment setup](docs/DevEnvironmentSetup.md)

## Gateway emulator

The Testing package runs C# policy documents through an in-memory gateway emulator.
Create a test document with `.AsTestDocument()`, configure policy callbacks or injected
services, then run a section such as `RunInbound()` or a coordinated request with
`RunAll()` / `PolicyPipeline.RunAll()`. Assert against the resulting gateway context.
External services are modeled through injected test implementations, not live Azure calls.

The [emulator checklist](docs/EmulatorPolicyChecklist.md) lists the 74 authored policy
methods, their sections, behavioral tests, and verified limitations. This is not full
APIM parity: for example, raw `InlinePolicy` XML is callback-only, `CrossDomain` does
not serve legacy client routes, and parallel `Wait` publishes message changes at
policy boundaries rather than sharing live gateway message objects between branches.
Use the section-typed `Wait` overload for parallel child policies:

```csharp
public void Inbound(IInboundContext context)
{
    context.Wait("all",
        branch => branch.SendRequest(new SendRequestConfig
        {
            Url = "https://api.example.com/first",
            ResponseVariableName = "first"
        }),
        branch => branch.SendRequest(new SendRequestConfig
        {
            Url = "https://api.example.com/second",
            ResponseVariableName = "second"
        }));
}
```

Each action must contain one direct `SendRequest` or `CacheLookupValue`, or one
`if`/`else if`/`else` chain that compiles to a single `choose` child. The analyzer
reports invalid branch shapes and captured outer contexts; the compiler
enforces the same boundaries. A `choose` branch can run any policy the emulator
already supports in that section. Configure injected services for external calls;
those services and policy callbacks must be safe for concurrent use. The previous
`Wait(Action, string?)` overload is obsolete but still compiles; its emulator
behavior remains explicit-mock-only.

For emulator contributions, run the two gates in
[`emulator-gates.ps1`](emulator-gates.ps1) from the admission worktree:

```powershell
.\emulator-gates.ps1 -Gate Admission -OwnedFiles $ownedFiles -TestFilter $testFilter
.\emulator-gates.ps1 -Gate Full
```

Set `$ownedFiles` to every path changed by the latest policy commit and `$testFilter`
to select every changed policy test class. The admission gate checks the allowlist
and targeted tests before the complete emulator test project; the full gate checks
the policy coverage audit, reruns that project, and builds the solution. Neither gate
runs BVT or E2E tests.
