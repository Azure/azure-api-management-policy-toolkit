// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class RedirectContentUrlsDecompiler : IPolicyDecompiler
{
    public string PolicyName => "redirect-content-urls";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        PolicyDecompilerContext.EmitSimpleCall(writer, element, contextVar, "RedirectContentUrls");
    }
}
