// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class ProxyDecompiler : IPolicyDecompiler
{
    public string PolicyName => "proxy";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddRequiredExprStringProp(props, element, "url", "Url");
        context.AddOptionalExprStringProp(props, element, "username", "Username");
        context.AddOptionalExprStringProp(props, element, "password", "Password");
        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "Proxy", "ProxyConfig", props);
    }
}
