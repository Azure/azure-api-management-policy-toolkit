// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class AuthenticationBasicDecompiler : IPolicyDecompiler
{
    public string PolicyName => "authentication-basic";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var username = element.Attribute("username")?.Value ?? "";
        var password = element.Attribute("password")?.Value ?? "";
        var usernameExpr = context.HandleValue(username, "BasicUsername");
        var passwordExpr = context.HandleValue(password, "BasicPassword");
        writer.AppendLine($"{prefix}AuthenticationBasic({usernameExpr}, {passwordExpr});");
    }
}
