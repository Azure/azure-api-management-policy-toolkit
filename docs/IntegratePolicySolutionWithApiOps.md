# Integrate policy solution with repository containing data for APIOps

Azure API Management policy toolkit is very elastic and allows a developer to define how the structure of the repository
will look. In this guide, we will cover how a toolkit project can produce policies which can be easily published
by [APIOps tool](https://azure.github.io/apiops) to Azure API Management instance.

## APIOps repository structure

The repository with Azure API Management data created by APIOps tool should have a structure similar to the folder tree below.
We included folders and files about policies in the repository, and we skipped folders and files not relevant in the
example.

```
.
├── artifacts
│    ├── policy.xml
│    ├── apis
│    │   └── echo-api
│    │       ├── policy.xml
│    │       └── operations
│    │           ├── get
│    │           │   └── policy.xml
│    │           └── post
│    │               └── policy.xml
│    └── products
│        └── Unlimited
│            └── policy.xml
└── configuration.dev.yaml
```

## Adding a policy project to the repository

The best way to add a policy project to the repository is to add it as a subfolder of repository root.
This way, the policy project can separate from the APIOps data and can be easily updated and maintained.
In the example below we added the policy project to the `policies` folder.
We added a dotnet tool to be available from the root of the repository (`./.config/dotnet-tools.json`).

```
.
├── artifacts
│    ├── policy.xml
│    ├── apis
│    │   └── echo-api
│    │       ├── policy.xml
│    │       └── operations
│    │           ├── get
│    │           │   └── policy.xml
│    │           └── post
│    │               └── policy.xml
│    └── products
│        └── Unlimited
│            └── policy.xml
├── configuration.dev.yaml
├── .config
│   └── dotnet-tools.json
└── policies
    ├── policies.sln
    ├── src
    │   ├── src.csproj
    │   ├── apis
    │   │   └── echo-api
    │   │       ├── ApiEchoApiPolicy.cs
    │   │       └── operations
    │   │           ├── get
    │   │           │   └── ApiEchoApiGetPolicy.cs
    │   │           └── post
    │   │               └── ApiEchoApiPostPolicy.cs
    │   └── products
    │       └── Unlimited
    │           └── ProductUnlimitedPolicy.cs
    └── test 
        ├── test.csproj
        ...
```

## Setting name of the policy file

APIOps accepts policies when they are named `policy.xml`.
The toolkit compiler by default produces file names equal to the name of a class
when name is not provided in `Document` attribute.
To make sure that `policy.xml` file is produced for each C# policy class make sure to set `policy.xml` as Document name.

```csharp
[Document("policy.xml")]
```

The name can also be a compile-time string constant, including concatenated constants, so a shared constant can be
used for every policy class.

```csharp
[Document(PolicyFiles.Name)]
```

In the above example, we used a hierarchical structure of the policy toolkit project.
Please refer to the [hierarchical structure](../docs/SolutionStructureRecommendation.md) for more information on that
structure and other recommended structures.

## Updating policy files in APIOps artifacts folder

Now, as we have all the pieces in place, we can run the compiler.
The compiler should target `artifacts` folder with APIOps data.
An example of the command to run the compiler is below.

```shell
dotnet azure-apim-policy-compiler --s .\policies\src\ --o .\artifacts\
```

After the command is executed the `artifacts` folder should contain all the policy files which can be easily published
by APIOps tool to an Azure API Management instance.