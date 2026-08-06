# Azure API Management Policy Toolkit Decompiler

This project builds a dotnet tool that decompiles Azure API Management XML policy documents into C# policy documents.

## Install

Install the Azure API Management Policy Toolkit decompiler CLI tool with [NuGet][nuget]:

```shell
dotnet tool install Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling
```

## Usage

Decompile one or more policy XML files:

```shell
azure-apim-policy-decompiler generate --input policy.xml
```

Use `--input-dir` to recursively decompile policy XML files from a directory.

## Documentation

Documentation is available to help you learn how to use the toolkit:

- [Quickstart][qs]

## Troubleshooting

- File an issue via [GitHub Issues][ghi].
- For questions, suggestions, or discussions, use [GitHub Discussions][ghd].

## Contributing

For details on contributing to this repository, see the [contributing guide][cg].

This project has adopted the [Microsoft Open Source Code of Conduct][coc].

<!-- LINKS -->

[nuget]: https://www.nuget.org/
[qs]: https://github.com/Azure/azure-api-management-policy-toolkit/blob/main/docs/QuickStart.md
[ghi]: https://github.com/Azure/azure-api-management-policy-toolkit/issues
[ghd]: https://github.com/Azure/azure-api-management-policy-toolkit/discussions
[cg]: https://github.com/Azure/azure-api-management-policy-toolkit/blob/main/CONTRIBUTING.md
[coc]: https://opensource.microsoft.com/codeofconduct/
