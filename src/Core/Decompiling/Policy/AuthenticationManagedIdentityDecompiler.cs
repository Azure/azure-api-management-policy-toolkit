// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class AuthenticationManagedIdentityDecompiler : IPolicyDecompiler
{
    public string PolicyName => "authentication-managed-identity";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddRequiredStringProp(props, element, "resource", "Resource");
        context.AddOptionalStringProp(props, element, "client-id", "ClientId");
        context.AddOptionalStringProp(props, element, "output-token-variable-name", "OutputTokenVariableName");
        context.AddOptionalBoolProp(props, element, "ignore-error", "IgnoreError");
        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "AuthenticationManagedIdentity", "ManagedIdentityAuthenticationConfig", props);
    }
}
