// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class CacheStoreDecompiler : IPolicyDecompiler
{
    public string PolicyName => "cache-store";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var duration = element.Attribute("duration")?.Value ?? "0";
        var cacheResponse = element.Attribute("cache-response")?.Value;
        var durationExpr = context.HandleIntValue(duration, "CacheDuration");
        if (cacheResponse != null)
        {
            var cacheResponseExpr = context.HandleBoolValue(cacheResponse, "CacheResponse");
            writer.AppendLine($"{prefix}CacheStore({durationExpr}, {cacheResponseExpr});");
        }
        else
        {
            writer.AppendLine($"{prefix}CacheStore({durationExpr}, null);");
        }
    }
}
