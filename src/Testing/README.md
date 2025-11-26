# Microsoft Azure ApiManagement Policy Toolkit Testing library

Microsoft Azure API Management is a hybrid, multicloud management platform for APIs across all environments. As a
platform-as-a-service, API Management supports the complete API lifecycle.

This library contains classes, implementation of authoring library interfaces and mocks which allow testing policy
expression and policy documents wrote in C# for Microsoft Azure Api Management.

## Getting started

### Install the package

Install the Microsoft Azure Api Management Policy Toolkit test library with [NuGet][nuget]:

```dotnetcli
# If the package is available on nuget.org
dotnet add package Microsoft.Azure.ApiManagement.PolicyToolkit.Testing
```

### Write test

```cs
using Contoso.Apis.Policies;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Contoso.Apis.Policies.Tests;

[TestClass]
public class ApiOperationPolicyTest
{
    [TestMethod]
    public void TestHelloFromExpression()
    {
        var context = new MockExpressionContext();
        Assert.AreEqual("World", ApiOperationPolicy.HelloFromExpression(context));
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