// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class GetAuthorizationContextDecompiler : IPolicyDecompiler
{
    public string PolicyName => "get-authorization-context";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddRequiredStringProp(props, element, "provider-id", "ProviderId");
        context.AddRequiredStringProp(props, element, "authorization-id", "AuthorizationId");
        context.AddRequiredStringProp(props, element, "context-variable-name", "ContextVariableName");
        context.AddOptionalStringProp(props, element, "identity-type", "IdentityType");
        context.AddOptionalStringProp(props, element, "identity", "Identity");
        context.AddOptionalBoolProp(props, element, "ignore-error", "IgnoreError");
        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "GetAuthorizationContext", "GetAuthorizationContextConfig", props);
    }
}
