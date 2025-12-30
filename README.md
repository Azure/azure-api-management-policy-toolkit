# Azure API Management policy toolkit

**Azure API management policy toolkit** is a set of libraries and tools for authoring [**policy documents**](https://learn.microsoft.com/azure/api-management/api-management-howto-policies) for [**Azure API Management**](https://learn.microsoft.com/azure/api-management/). The toolkit was designed to help **create** and **test** policy documents with complex expressions.

Before the Policy toolkit, policy documents were written in Razor format, which is hard to read and understand, especially when there are multiple expressions. The feedback loop on new documents or even the smallest changes was very long, requiring a live Azure API Management instance, a policy document deployment, and manual testing through the API request.

The policy toolkit changes that. It allows you to write policy documents in C# language, which is more natural and doesn't require you to jump between C# and XML for expression creation. Creating policy documents in C# also brings the advantage of using simple C# code for unit testing of policy documents.

## Documentation

:exclamation: Packages are only avaliable for download from github release. We are working to bring them to public nuget.

#### Azure API Management policy toolkit documentation for users.
* [Quick start](docs/QuickStart.md)
* [Available policies](docs/AvailablePolicies.md)
* [Solution structure recommendation](docs/SolutionStructureRecommendation.md)
* [Steps for deploying policies created by the policy toolkit](docs/IntegratePolicySolution.md)
* [Integrate policy solution with APIOps](docs/IntegratePolicySolutionWithApiOps.md)

#### Azure API Management policy toolkit documentation for contributors.
* [Contributor guide](CONTRIBUTING.md)
* [Development environment setup](docs/DevEnvironmentSetup.md)
