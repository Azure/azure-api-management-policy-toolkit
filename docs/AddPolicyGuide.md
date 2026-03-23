# Contributing a new policy (feature)

This guide outlines the steps to support a new policy, or new capabilities for an existing policy.

All contributions should follow the steps provided in this guide to support a policy (feature) and its configuration.

If you cannot follow the defined process, please open an issue to discuss before implementing it. 

## TL;DR

When adding a new policy, you will typically need to create or modify the following files:

- **[Introduce the configuration](#introduce-the-configuration)**: `src/Authoring/Configs/YourPolicyConfig.cs` (Exampe: `RateLimitConfig.cs`)
- **[Enable using in respective section or fragment](#enable-using-in-respective-section-or-fragment)**: `src/Authoring/IInboundContext.cs` (or other context)
- **[Support compilation](#support-compilation)**: `src/Core/Compiling/Policy/YourPolicyCompiler.cs` (Exampe: `RateLimitCompiler.cs`)
- **[Provide automated tests](#provide-automated-tests)**: `test/Test.Core/Compiling/YourPolicyTests.cs` (Exampe: `RateLimitTests.cs`)
- **[Document your policy](#document-your-policy)**: `docs/AvailablePolicies.md`
- **[Implement a decompiler](#implement-a-decompiler)**: `src/Core/Decompiling/Policy/YourPolicyDecompiler.cs` (Example: `SetMethodDecompiler.cs`)
- **[Create round-trip tests](#create-round-trip-tests)**: `test/Test.Decompiling/TestData/your-policy.xml` + DataRow in `RoundTripTests.cs`

| Artifact | Location | Naming |
|---|---|---|
| Config record | `src/Authoring/Configs/{PolicyName}Config.cs` | `{PolicyName}Config` |
| Section interface method | `src/Authoring/I{Section}Context.cs` | `{PolicyName}(config)` |
| Compiler | `src/Core/Compiling/Policy/{PolicyName}Compiler.cs` | `{PolicyName}Compiler` |
| Compiler tests | `test/Test.Core/Compiling/{PolicyName}Tests.cs` | `{PolicyName}Tests` |
| Decompiler | `src/Core/Decompiling/Policy/{PolicyName}Decompiler.cs` | `{PolicyName}Decompiler` |
| Round-trip test XML | `test/Test.Decompiling/TestData/{policy-name}.xml` | DataRow in `RoundTripTests.cs` |

We recommend using existing policies as detailed examples, such as `RateLimit` or `Quota` policies. These contain all possible aspects of a policy compilation implementation.

## Steps to add a new policy

### Introduce the configuration

- Create `src/Authoring/Configs/YourPolicyConfig.cs` as a public record.
  - Required parameters as `required` `init` properties
  - Optional properties should be `nullable`
  - Properties allowing policy expressions should have `[ExpressionAllowed]` attribute assigned.
  - Add XML documentation for the record and its properties.

```csharp
  /// <summary>
  /// Description of config.
  /// </summary>
  public record YourPolicyConfig
  {
      /// <summary>
      /// Description of your property.
      /// </summary>
      [ExpressionAllowed]
      public required string Property { get; init; }

      /// <summary>
      /// Optional property description.
      /// </summary>
      [ExpressionAllowed]
      public int? OptionalProperty { get; init; }

      // Add more properties as needed.
  }
```

### Enable using in respective section or fragment

- Add a method signature to section context interfaces in which policy is avaliable (e.g. `src/Authoring/IInboundContext.cs`).
  Add method to policy fragment context interface to make policy avaliable in policy fragment. 
  Make sure to add XML documentation.

```csharp
    /// <summary>
    /// Description of your policy.
    /// <param name="config">
    /// Configuration for the YourPolicy policy.
    /// </param>
    /// </summary>
    void YourPolicy(YourPolicyConfig config);
```

### Support compilation

- Create compiler class `src/Core/Compiling/Policy/YourPolicyCompiler.cs` implementing `IMethodPolicyHandler`.
  This class will be responsible for translating the C# method call into the corresponding XML policy element.
  - Make sure that the class is `public` and in `Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy` namespace.
    This is required for the automatic adding your policy compiler to the compilation.
  - Implement the `Handle` method to extract parameters from the method invocation and construct the XML element.
  - Use `TryExtractingConfigParameter<T>` to extract the config object into initialization object.
  - Use `AddAttribute` extension method to add attributes to the XML element.
  - Report diagnostics for missing required parameters using `context.ReportDiagnostic`.
    For avaliable errors see `CompilationErrors.cs` file.
  - For complex policies with sub elements, refer to existing compilers for guidance (eg. RateLimitCompiler).

```csharp
namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class YourPolicyCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.YourPolicy);
    
    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<YourPolicyConfig>(context, "your-policy", out var values))
        {
            return;
        }

        var element = new XElement("your-policy");

        if (!element.AddAttribute(values, nameof(YourPolicyConfig.Property), "propery"))
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.RequiredParameterNotDefined,
                node.GetLocation(),
                "your-policy",
                nameof(YourPolicyConfig.Property)
            ));
            return;
        }

        element.AddAttribute(values, nameof(YourPolicyConfig.OptionalProperty), "optional-property");

        context.AddPolicy(element);
    }
}
```

### Provide automated tests

- Add tests to `test/Test.Core/Compiling/YourPolicyTests.cs`. 
  - Use a `[DataRow]` for each test case, following the pattern of existing tests.
  - Check that compiler 
    - handles compiling policy in all of sections which you added the method to
    - required aparameters with constant values
    - optional parameters with constant values
    - expressions for properties which define [ExpressionAllowed] attribute

```csharp
[TestClass]
public class YourPolicyTests : PolicyCompilerTestBase
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.YourPolicy(new YourPolicyConfig()
                    {
                        Property = "Value"
                    });
            }
        }
        """,
        """
        <policies>
            <inbound>
                <your-policy property="Value" />
            </inbound>
        </policies>
        """)]
    public void ShouldCompileYourPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}
```

### Implement a decompiler

- Create a decompiler class `src/Core/Decompiling/Policy/YourPolicyDecompiler.cs` implementing `IPolicyDecompiler`.
  This class converts XML policy elements back into C# method calls, enabling round-trip workflows.
  - Make sure that the class is in the `Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy` namespace.
    Decompilers are discovered via reflection — no manual DI registration is needed.
  - The `PolicyName` property must return the XML element name (e.g. `"your-policy"`).
  - Implement the `Decompile` method to generate the corresponding C# code using the `CodeWriter`.
  - Reference examples by complexity:
    - **Simple (direct parameters)**: `SetMethodDecompiler.cs`
    - **Config-based (attributes + child elements)**: `EmitMetricDecompiler.cs`
    - **Complex (nested configs, multiple children)**: `ValidateJwtDecompiler.cs`

### Create round-trip tests

- Create a test XML file `test/Test.Decompiling/TestData/your-policy.xml` containing a representative policy document that exercises required and optional parameters.
- Add a `[DataRow("your-policy.xml")]` entry to the `RealPolicyFile_RoundTrips` method in `test/Test.Decompiling/RoundTripTests.cs`.
- The round-trip test validates that XML → C# → XML produces semantically equivalent output with no loss.

### Document your policy

- Update `docs/AvailablePolicies.md` to include your new policy in the list of implemented policies.
