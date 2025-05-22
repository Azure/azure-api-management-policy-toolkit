// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class AuthenticationManageIdentityReturnValueCompiler : IReturnValueMethodPolicyHandler
{
    public string MethodName => nameof(IInboundContext.AuthenticationManagedIdentity);

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node, string variableName)
    {
        var policy = new XElement("authentication-managed-identity");
        var resource = node.ArgumentList.Arguments[0].Expression.ProcessParameter(context);
        policy.Add(new XAttribute("resource", resource));
        policy.Add(new XAttribute("output-token-variable-name", variableName));

        context.AddPolicy(policy);
    }
}