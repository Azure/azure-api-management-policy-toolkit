# Azure API Management Policy Toolkit Compiling tool

This project builds a dotnet tool which can compile C# policy documents into Azure API Management XML (rawxml / Razor)
policy documents.

## Install
Install the Microsoft Azure Api Management Policy Toolkit compiler CLI tool with [NuGet][nuget]:

```shell
dotnet tool install Azure.ApiManagement.PolicyToolkit.Compiling
```

If the package is not available on the public NuGet feed for your environment, you can install it from the repository
`output/` folder after running `dotnet pack` at the repository root.

### Inspect generated policy

```cshtml
<policies>
    <inbound>
        <base/>
        <set-header name="X-Hello" exists-action="override">
            <value>@("World")</value>
        </set-header>
    </inbound>
</policies>
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

For details on contributing to this repository, see the [contributing
guide][cg].

This project welcomes contributions and suggestions. Most contributions
require you to agree to a Contributor License Agreement (CLA) declaring
that you have the right to, and actually do, grant us the rights to use
your contribution. For details, visit <https://cla.microsoft.com>.

When you submit a pull request, a CLA-bot will automatically determine
whether you need to provide a CLA and decorate the PR appropriately
(for example, label, comment). Follow the instructions provided by the
bot. You'll only need to do this action once across all repositories
using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct][coc]. For
more information, see the [Code of Conduct FAQ][coc_faq] or contact
<opencode@microsoft.com> with any other questions or comments.

<!-- LINKS -->

[nuget]: https://www.nuget.org/

[qs]: https://github.com/Azure/azure-api-management-policy-toolkit/blob/main/docs/QuickStart.md

[ep]: https://github.com/Azure/azure-api-management-policy-toolkit/tree/main/example

[ghi]: https://github.com/Azure/azure-api-management-policy-toolkit/issues

[ghd]: https://github.com/Azure/azure-api-management-policy-toolkit/discussions

[cg]: https://github.com/Azure/azure-api-management-policy-toolkit/blob/main/CONTRIBUTING.md

[coc]: https://opensource.microsoft.com/codeofconduct/

[coc_faq]: https://opensource.microsoft.com/codeofconduct/faq/