# Azure API Management Policy Toolkit Decompiler

This project builds a .NET tool that converts Azure API Management policy XML into equivalent C# authoring code. It is
designed for round-trip workflows: decompile an existing policy, adjust the generated C# code, then compile it back to
XML.

## Capabilities

The decompiler can:

- Convert a policy document (`<policies>`) into a C# class that implements `IDocument`.
- Convert a policy fragment (`<fragment>`) into a C# class that implements `IFragment`.
- Emit the appropriate `Inbound`, `Outbound`, `Backend`, `OnError`, and `Fragment` methods.
- Generate expression helper methods for policy expressions and named values.
- Process one or many files, including recursive directory scans.
- Generate classes with custom namespaces, suffixes, and document IDs for traceability.
- Use policy-specific decompilers where available and fall back to `InlinePolicy` for unsupported XML.

This makes it useful for reverse-engineering an existing APIM policy into the toolkit's authoring model, auditing a
policy, or migrating legacy XML policies into C#-based source control workflows.

## Install

Install the Microsoft Azure API Management Policy Toolkit decompiler CLI tool with [NuGet][nuget]:

```shell
dotnet tool install Azure.ApiManagement.PolicyToolkit.Decompiling
```

## Usage

The command name is `azure-apim-policy-decompiler`.

```shell
azure-apim-policy-decompiler --input .\policy.xml --output .\generated
```

Process all XML files under a folder recursively:

```shell
azure-apim-policy-decompiler --input-dir .\policies --pattern "*.xml" --output .\generated
```

Common options:

- `--input` / `--input-dir`: one or more input files, or a directory to scan recursively
- `--pattern`: file pattern for directory scans (`*.xml` by default)
- `--output`: output directory for generated C# files
- `--ext`: output file extension (`.cs` by default)
- `--namespace`: base namespace for generated classes
- `--scope`: document scope (`Operation` by default)
- `--doc-id-root`: root path used to compute relative `DocumentId` values
- `--document-suffix` / `--fragment-suffix`: name suffixes for generated class names
- `--verbose`: print progress information while generating files

## Example output

Input XML:

```xml

<policies>
    <inbound>
        <base/>
        <set-header name="X-Hello" exists-action="override">
            <value>World</value>
        </set-header>
    </inbound>
</policies>
```

Generated C#:

```csharp
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Generated;

[Document]
public class Policy : IDocument
{
    public void Inbound(IInboundContext context)
    {
        context.Base();
        context.SetHeader("X-Hello", "World");
    }
}
```

## Documentation

Documentation is available to help you learn how to use this package:

- [Quickstart][qs].

## Examples

Code samples for using the toolkit can be found in the following locations

- [Example project][ep]

## Troubleshooting

- File an issue via [GitHub Issues][ghi].
- For questions, suggestions, or discussions, please use [GitHub Discussions][ghd]

## Contributing

For details on contributing to this repository, see the [contributing guide][cg].

This project welcomes contributions and suggestions. Most contributions require you to agree to a Contributor License
Agreement (CLA) declaring that you have the right to, and actually do, grant us the rights to use your contribution. For
details, visit <https://cla.microsoft.com>.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide a CLA and decorate
the PR appropriately (for example, label, comment). Follow the instructions provided by the bot. You'll only need to do
this action once across all repositories using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct][coc]. For more information, see
the [Code of Conduct FAQ][coc_faq] or contact
<opencode@microsoft.com> with any other questions or comments.

<!-- LINKS -->

[nuget]: https://www.nuget.org/

[qs]: https://github.com/Azure/azure-api-management-policy-toolkit/blob/main/docs/QuickStart.md

[ap]: https://github.com/Azure/azure-api-management-policy-toolkit/blob/main/docs/AvailablePolicies.md

[of]: https://github.com/Azure/azure-api-management-policy-toolkit/blob/main/docs/OutputFormat.md

[ep]: https://github.com/Azure/azure-api-management-policy-toolkit/tree/main/example

[ghi]: https://github.com/Azure/azure-api-management-policy-toolkit/issues

[ghd]: https://github.com/Azure/azure-api-management-policy-toolkit/discussions

[cg]: https://github.com/Azure/azure-api-management-policy-toolkit/blob/main/CONTRIBUTING.md

[coc]: https://opensource.microsoft.com/codeofconduct/

[coc_faq]: https://opensource.microsoft.com/codeofconduct/faq/