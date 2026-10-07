# Steps for deploying policies created by the policy toolkit

The Azure API Management policy toolkit targets the advanced user who leverages a CI/CD pipeline to check and deploy
changes made to Azure API Management instance.
The solution with policy documents can be easily integrated with any infrastructure as code repository
containing data about Azure API Management instance like ARM templates, Bicep, Terraform or APIOps.

In this tutorial, we will show you what steps need to be added to the CI/CD pipeline to integrate the policy documents
created by the policy toolkit. We will execute them locally to understand what is happening under the hood.

## Prerequisites

### Infrastructure as code repository

We assume that you have a policy document solution created, and you use a hierarchical approach for the project
structure. You can read about the hierarchical structure in
the [Policy documents repository and solution structure](./SolutionStructureRecomendation.md).
The repository which we will be talking about looks like the one in the following schema:

```
.
├── policies.sln
├── .gitignore
├── .config
│   └── dotnet-tools.json
├── infrastructure
│   └── deployment.bicep
├── src
│   ├── src.csproj
│   └── apis
│       └── echo-api
│           └── ApiEchoApiPolicy.cs
└── test
    ├── test.csproj
    └── apis
        └── echo-api
            └── ApiEchoApiPolicyTests.cs
```

### Azure API Management instance

We assume that you have an Azure API Management instance created and that you have access to it.
We assume that you have an API created in the Azure API Management instance named `echo-api`. We assume that you can test the policy by invoking some operations on the API.

### Azure CLI

In this tutorial, we will use a Bicep file for the deployment description.
To be able to use the Bicep file, an Azure CLI tool needs to be available in the environment.
You can download the Azure CLI from the [official site](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli).

## Building and testing

The policy document's solution is a standard .NET project.
Because of that, building and testing the solution is straightforward.
You need to execute standard .NET commands to build and test the solution in the root of the solution folder.
These steps are essential to know that the policy documents are correct and that they are not breaking the build.

#### Building the solution

```shell
dotnet build
```

#### Testing the solution

```shell
dotnet test
```

In your CI/CD pipeline,
these two commands should be split into separate steps, and if any of them fails, the pipeline should fail as well.

## Compiling the policy documents

After standard C# compilation and testing is done, the policy documents can be compiled by the policy toolkit compiler.
Because the Bicep file for deployment is in the `.\infrastructure\` folder, the compilation target folder should be
`.\infrastructure\` as well. This will allow easy referencing of the policy documents in the bicep file.

```shell
dotnet azure-apim-policy-compiler --s .\src\ --o .\infrastructure\
```

The command will produce the policy documents and the folder structure will look like this:

```
.
├── policies.sln
├── infrastructure
│   ├── deployment.bicep
│   └── apis
│       └── echo-api
│           └── ApiEchoApiPolicy.xml
├── src
│   ├── src.csproj
│   └── apis
│       └── echo-api
│           └── ApiEchoApiPolicy.cs
└── ...
```

Your CI/CD pipeline should also fail if the compilation of the policy documents fails. A policy document with
compilation errors isn't written to the output folder; the compiler prints its errors and the name of the document it
skipped.

## Deploying the policy documents

The Bicep file for the deployment is in the `.\infrastructure\` folder.
The Bicep file is referencing the service, the API and the policy document.
The Bicep file should look like this:

```bicep
param servicename string

resource service 'Microsoft.ApiManagement/service@2023-03-01-preview' existing = {
  name: servicename
  scope: resourceGroup()
}

resource echoApi 'Microsoft.ApiManagement/service/apis@2023-03-01-preview' existing = {
  parent: service
  name: 'echo-api'
}

resource echoApiPolicy 'Microsoft.ApiManagement/service/apis/policies@2023-03-01-preview' = {
  parent: echoApi
  name: 'policy'
  properties: {
    format: 'rawxml'
    value: loadTextContent('./apis/${echoApi.name}/ApiEchoApiPolicy.xml', 'utf-8')
  }
}
```

Please notice that the `loadTextContent` function is used to load the content of the policy document.
The content is loaded from the file under following path `./apis/echo-api/ApiEchoApiPolicy.xml`.

The Azure CLI can do the deployment. The following command will deploy the policy document to the Azure API Management instance:

```shell
cd .\infrastructure\
az deployment group create \
  --resource-group <<YOUR_RESOURCE_GROUP>> \
  --template-file .\deployment.bicep \
  --parameters servicename=<<YOUR_SERVICE_NAME>> \
  --name deploy-1
```

## Pipeline example

With above knowledge, creating a pipeline should be straightforward.
In this section, we will show you two examples of pipelines definitions:

* The GitHub Actions pipeline
* The Azure Pipelines definition

Both pipelines will look similar. The following steps will be present in both of them:

1. Check out the repository
2. Set up build and test using the .NET SDK
3. Build the solution
4. Test the solution
5. Restore the policy document compiler
6. Compile the policy documents
7. Deploy the policy documents

### GitHub Actions pipeline example

```yaml
on:
  workflow_dispatch:
    inputs:
      resourceGroup:
        description: 'The resource group of the Azure API Management instance'
        required: true
      apimServiceName:
        description: 'The name of the Azure API Management instance'
        required: true

name: DeployPolicyDocuments 

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:

    - name: Checkout repository
      uses: actions/checkout@v3
      
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 11.0.x

    - name: Restore dependencies
      run: dotnet restore

    - name: Build with dotnet
      run: dotnet build --no-restore

    - name: Test with dotnet
      run: dotnet test --no-build --logger trx --results-directory "TestResults"

    - name: Upload dotnet test results
      if: ${{ always() }}
      uses: actions/upload-artifact@v4
      with:
        name: dotnet-test-results
        path: TestResults

    - name: Restore policy document compiler
      run: dotnet tool restore

    - name: Compile policy documents
      run: dotnet azure-apim-policy-compiler --s .\src\ --o .\infrastructure\

    - name: Azure Login
      uses: azure/login@v2
      with:
        creds: ${{ secrets.AZURE_CREDENTIALS }}

    - name: Deploy policy documents
      uses: azure/cli@v2
      env:
        AZURE_RESOURCE_GROUP: ${{ inputs.resourceGroup }}
        AZURE_API_MANAGEMENT_SERVICE_NAME: ${{ inputs.apimServiceName }}
        RUN_NUMBER: ${{ github.run_number }}
      with:
        azcliversion: latest
        inlineScript: |
          cd .\infrastructure\
          az deployment group create \
            --resource-group $AZURE_RESOURCE_GROUP \
            --template-file .\deployment.bicep \
            --parameters servicename=$AZURE_API_MANAGEMENT_SERVICE_NAME \
            --name deploy-$RUN_NUMBER
```

### Azure Pipelines example

```yaml
parameters:
  - name: 'subscription'
    displayName: 'Subscription for Azure API Management'
    type: string
  - name: 'resourceGroup'
    displayName: 'Azure API Management resource group'
    type: string
  - name: 'serviceName'
    displayName: 'Azure API Management service name'
    type: string

pool:
  vmImage: 'ubuntu-latest'

steps:

  - task: UseDotNet@2
    displayName: 'Setup .NET'
    inputs:
      version: 11.x
      performMultiLevelLookup: true
      includePreviewVersions: true

  - script: dotnet restore
    displayName: 'Restore dependencies'

  - script: dotnet build --no-restore
    displayName: 'Build with dotnet'

  - script: dotnet test --no-build --logger trx --results-directory "TestResults"
    displayName: 'Test with dotnet'

  - task: PublishTestResults@2
    displayName: 'Collect tests results'
    inputs:
      testRunner: VSTest
      testResultsFiles: './TestResults/*.trx'

  - script: dotnet tool restore
    displayName: 'Restore tools'

  - script: dotnet azure-apim-policy-compiler --s .\src\ --o .\infrastructure\
    displayName: 'Compile policy documents'

  - task: AzureCLI@2
    displayName: Azure CLI
    inputs:
      azureSubscription: ${{ parameters.subscription }}
      scriptType: cmd
      scriptLocation: inlineScript
      inlineScript: |
        cd .\infrastructure\
        az deployment group create \
          --resource-group ${{ parameters.resourceGroup }} \
          --template-file .\deployment.bicep \
          --parameters servicename=${{ parameters.serviceName }} \
          --name deploy-$(Build.BuildNumber)
```

## Conclusion

Now you know what steps are required to deploy the policy documents.
You can replicate these steps in the CI/CD pipeline.

We prepared a short guide to integrate the policy documents solution with APIOps.
You can read about it in the [APIOps integration](./IntegratePolicySolutionWithApiOps.md) document.
