# Guide to adding new policy

This guide outlines the steps to add a new policy to the system, following the established pattern used for existing
policies.

Guide covers how to add a new policy that can be used in policy documents with a config.
All new policies should follow this pattern. If a policy does not fit this pattern,
please discuss it in an issue before impementing it. 

## TL;DR

When adding a new policy, you will typically need to create or modify the following files:

- **Introduce the configuration**: `src/Authoring/Configs/YourPolicyConfig.cs` (Exampe: `RateLimitConfig.cs`)
- **Enable using in respective section or fragment**: `src/Authoring/IInboundContext.cs` (or other context)
- **Support compilation**: `src/Core/Compiling/Policy/YourPolicyCompiler.cs` (Exampe: `RateLimitCompiler.cs`)
- **Provide automated tests**: `test/Test.Core/Compiling/YourPolicyTests.cs` (Exampe: `RateLimitTests.cs`)
- **Document your policy**: `docs/AvailablePolicies.md`

Refer to existing policies for detailed examples. It is recommended to look at `RateLimit` or `Quota` policies.
They are contain all possible aspects of a policy compilation implementation.

## Steps to add a new policy

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

- Update `docs/AvailablePolicies.md` to include your new policy in the list of implemented policies.
