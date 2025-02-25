# Quick Start Guide

In this guide we will show you how to create a simple policy document, test it and compile it to a policy file.

We will cover the following topics:

1. Creation of a new solution for policy documents
2. Writing a simple policy document
3. Compiling policy document
4. Writing complex policy document
5. Testing expressions in policy document

## Set up project for authoring policies

1. Check that you have latest [.NET SDK 8 sdk](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) version installed.
2. Open terminal and create a new solution by executing
    ```shell
    dotnet new sln --output PolicySolution
    cd PolicySolution
    ```
3. Create a new class library project by executing
    ```shell
    dotnet new classlib --output Contoso.Apis.Policies
    dotnet sln add ./Contoso.Apis.Policies
    ```
4. :exclamation: Azure API Management Policy toolkit is not yet published to NuGet.
    Because of that, we need to create a local nuget repository for the packages and put the libraries there.
   1. Create a local nuget repository for the packages by executing
       ```shell
       mkdir packages
       ```
   2. Download the Azure API Management policy toolkit libraries from GitHub release and put them in the `packages` folder.
   3. Create a file named `nuget.config` in the solution folder with the content:
       ```xml
       <configuration>
         <packageSources>
           <!-- local feed for the project -->
           <add key="local" value="./packages" />
         </packageSources>
       </configuration>
       ```
5. Add Azure API Management policy toolkit library by running
    ```shell
    cd ./Contoso.Apis.Policies
    dotnet add package Azure.ApiManagement.PolicyToolkit.Authoring
    ```

6. Open the solution in your IDE of choice. We
  tested [Visual Studio ](https://visualstudio.microsoft.com), [Raider](https://www.jetbrains.com/rider/), [Visual Studio Code](https://code.visualstudio.com/)
  with [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit),
  but any IDE with C# support should work.

## Writing a simple policy document

Azure API Management policy toolkit defines a new way of writing policy documents: One policy document is one C# class in
one `cs` file.
Let's create a new `ApiOperationPolicy.cs` file in the project which will be our policy document by executing the following
command.

```shell
dotnet new class -n ApiOperationPolicy
```

The class in the file should inherit from `IDocument` interface and have `Document` attribute
from `Azure.ApiManagement.PolicyToolkit.Authoring` namespace.

```csharp
using Azure.ApiManagement.PolicyToolkit.Authoring;

namespace Contoso.Apis.Policies;

[Document]
public class ApiOperationPolicy : IDocument
{
    
}
```

The `IDocument` type contains methods `Inbound`, `Outbound`, `Backend` and `OnError` which are used to define policy
sections.

Let's implement `Inbound` method. In the method let's add the policy to set header `X-Hello` to value `World`.

```csharp
[Document]
public class ApiOperationPolicy : IDocument
{
    public void Inbound(IInboundContext context)
    {
        context.Base();
        context.SetHeader("X-Hello", "World");
    }
}
```

Let's unpack the code above:

* `Inbound` method accepts one parameter `IInboundContext context`.
* `IInboundContext` contains methods which are directly mapped to policies.
* `Base` method is mapped to `base` policy.
* `SetHeader` maps to `set-header` policy
* Parameters `X-Hello` and `World` in `SetHeader` method are mapped to policy's `name` attribute and `value` element.

Great! We have a simple policy document. Now we would like to check if it really works as expected in Azure API
Management instance.
To do that we will need to use the Azure API Management policy toolkit document compiler to generate a policy document
which is understandable by Azure API Management.

## Compiling simple policy document

The next step would be to compile this policy to a format which Azure API Management can understand. Currently,
Azure API Management supports policy documents in Razor format. The Azure API Management policy toolkit provides a
dotnet tool called a compiler which can generate
Razor policy documents from C# classes.

To use the compiler, we first need to add it to our solution folder. To do that execute the following command in the
solution folder.

```shell
cd .. # Go to solution folder if not already there
dotnet tool install Azure.ApiManagement.PolicyToolkit.Compiling
````

After the installation, the compiler should be available in the project folder.
Now lets run the compiler to generate policy document. Execute compiler command in the solution folder.

```shell
dotnet azure-apim-policy-compiler --s .\Contoso.Apis.Policies --o . --format true
``` 

The compiler is a dotnet tool named `azure-apim-policy-compiler`. The `--s` parameter is a source folder with policy documents.
The `--o` parameter is an output folder for generated policy documents. The `--format` parameter is a flag which tells
the compiler to format the generated document.

The compiler will generate `ApiOperationPolicy.xml` file in the solution folder. The generated file should have the
following
content:

```razor
<policies>
    <inbound>
        <base/>
        <set-header name="X-Hello" exists-action="override">
            <value>World</value>
        </set-header>
    </inbound>
</policies>
```

The generated file is a Razor policy document which can be imported to Azure API Management instance.
You can now import the file to some API operation in your Azure API Management instance and test if the request will
contain header.
The policy should be imported without any issues and the header should be added to every request.

Great! We have created a simple policy document, compiled it and tested it in Azure API Management instance. Now let's
move to more complex policy document.

## Writing more complex policy document

The Azure API Management policy toolkit allows you to write more complex policy documents in a simple way.
Complex policy documents contain a lot of expressions which alter policy behavior based on request context.
The Azure API Management policy toolkit allows you to naturally create expressions, test them and compile them to a
policy document.

Let's assume that we want to add authorization header to the request.
If a request comes from Company IP addresses `10.0.0.0/24` it should use basic authorization with `username`
and `password` from named values.
If a request comes from other IP addresses it should use `Bearer` token received from https://graph.microsoft.com.
For every request we want to add header with the user id.

```csharp
using Azure.ApiManagement.PolicyToolkit.Authoring;
using Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

namespace Contoso.Apis.Policies;

[Document]
public class ApiOperationPolicy : IDocument
{
    public void Inbound(IInboundContext context)
    {
        if(IsCompanyIP(context.ExpressionContext))
        {
            context.AuthenticationBasic("{{username}}", "{{password}}");
        }
        else
        {
            context.AuthenticationManagedIdentity(new ManagedIdentityAuthenticationConfig 
            {
                Resource = "https://graph.microsoft.com"
            });
        }
        context.SetHeader("X-User-Id", GetUserId(context.ExpressionContext));
    }
    
    public static bool IsCompanyIP(IExpressionContext context)
        => context.Request.IpAddress.StartsWith("10.0.0.");

    private static string GetUserId(IExpressionContext context)
        => context.User.Id;
}
```

Let's unpack the code above it:

* `if` statement is an equivalent of `choose` policy with `when` element. `else` statement is an equivalent of
  `otherwise` element.
* `IsCompanyIP` is a method which checks if request comes from company IP addresses, and it is mapped to a policy
  expression
* Every method, other than section method are treated as expressions. They need to accept one parameter of type
  `IExpressionContext`
  with name `context`. Type is available in `Azure.ApiManagement.PolicyToolkit.Authoring.Expressions` namespace.
* `IExpressionContext` type contains the same properties as `context` object in policy expressions.
* `AuthenticationBasic` method is mapped to `authentication-basic` policy.
* `AuthenticationManagedIdentity` method is mapped to `authentication-managed-identity` policy.
* `SetHeader` method is mapped to `set-header` policy with override.

Expressions are methods in the class. They may be static or instance methods, and they can be private or public.
They need to accept one parameter of type `IExpressionContext` with name `context`.

To use an expression you just need to call that method in the place were you want to use it. In our example, we call
`IsCompanyIP` method in the `if` statement, and we call `GetUserId` method in the `SetHeader` method in value parameter.
In other more complex policies, which accept a configuration object, you will invoke method in the field initialization
assignment like in example below.

```csharp
context.SomePolicy(new Config()
{
    Field = Method(context.ExpressionContext)
});
```

Cool! We have a more complex policy document. Now let's compile it to a policy document.

```shell
dotnet azure-apim-policy-compiler --s .\source\ --o . --format true
```

Content of the generated file should be:

```razor
<policies>
    <inbound>
        <choose>
            <when condition="@(context.Request.IpAddress.StartsWith("10.0.0."))">
                <authentication-basic username="{{username}}" password="{{password}}"/>
            </when>
            <otherwise>
                <authentication-managed-identity resource="https://graph.microsoft.com"/>
            </otherwise>
        </choose>
        <set-header name="X-User-Id" exists-action="override">
            <value>@(context.User.Id)</value>
        </set-header>
    </inbound>
</policies>
```

Isn't it great, right? I hope it is. But we can now test the expressions in the policy document.

## Testing expressions in policy document

The Azure API Management policy toolkit provides a way to test expressions in policy documents. To do that we need to
create a test project from a solution folder. Then we will need to add a reference to the policy project and the testing
library. Last, we need to create a test class and write a test for the expression. All of that you can do by executing
the following commands.

```shell
dotnet new mstest --output Contoso.Apis.Policies.Tests
dotnet sln add ./Contoso.Apis.Policies.Tests
cd Contoso.Apis.Policies.Tests
dotnet add package Azure.ApiManagement.PolicyToolkit.Testing
dotnet add reference ..\Contoso.Apis.Policies
dotnet new class -n ApiOperationPolicyTest
```

Perfect! Now we can write a test for `IsCompanyIP` method in the class.

```csharp
using Contoso.Apis.Policies;

using Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace Contoso.Apis.Policies.Tests;

[TestClass]
public class ApiOperationPolicyTest
{
    [TestMethod]
    public void FilterSecrets()
    {
        var context = new MockExpressionContext();
        
        context.Request.IpAddress = "10.0.0.12";
        Assert.IsTrue(ApiOperationPolicy.IsCompanyIP(context));

        context.Request.IpAddress = "11.0.0.1";
        Assert.IsFalse(ApiOperationPolicy.IsCompanyIP(context));
    }
}
```

Let's unpack the code above:

* Test class is a standard MSTest class with one test method. You can use your favorite testing framework in place of MSTest. Policy framework is not dependent on any testing framework.
* `MockExpressionContext` is a class which is used to mock request context. It is available in
  `Azure.ApiManagement.PolicyToolkit.Testing.Expressions` namespace. It implements `IExpressionContext`
  interface and exposes helper properties to set up request context.
* `context.MockRequest.IpAddress = "10.0.0.12"` is setting a IpAddress for request.

To check that the expression works as expected, run the test by executing the following command.
Command can be executed in the test project folder or solution folder.

```shell
dotnet test
```

Great! We have tested the expression in the policy document. Now you can write more complex policy documents, test them
and compile them to a policy document.
