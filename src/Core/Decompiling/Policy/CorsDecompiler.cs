// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class CorsDecompiler : IPolicyDecompiler
{
    public string PolicyName => "cors";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddOptionalBoolProp(props, element, "allow-credentials", "AllowCredentials");
        context.AddOptionalStringProp(props, element, "terminate-unmatched-request", "TerminateUnmatchedRequest");

        var origins = element.Element("allowed-origins")?.Elements("origin")
            .Select(e => PolicyDecompilerContext.GetElementText(e)).ToList();
        if (origins != null && origins.Count > 0)
        {
            props.Add($"AllowedOrigins = new[] {{ {string.Join(", ", origins.Select(PolicyDecompilerContext.Literal))} }}");
        }

        var headers = element.Element("allowed-headers")?.Elements("header")
            .Select(e => PolicyDecompilerContext.GetElementText(e)).ToList();
        if (headers != null && headers.Count > 0)
        {
            props.Add($"AllowedHeaders = new[] {{ {string.Join(", ", headers.Select(PolicyDecompilerContext.Literal))} }}");
        }

        var methodsEl = element.Element("allowed-methods");
        if (methodsEl != null)
        {
            var preflightMaxAge = methodsEl.Attribute("preflight-result-max-age")?.Value;
            if (preflightMaxAge != null)
            {
                props.Add($"PreflightResultMaxAge = {preflightMaxAge}");
            }

            var methods = methodsEl.Elements("method").Select(e => PolicyDecompilerContext.GetElementText(e)).ToList();
            if (methods.Count > 0)
            {
                props.Add($"AllowedMethods = new[] {{ {string.Join(", ", methods.Select(PolicyDecompilerContext.Literal))} }}");
            }
        }

        var exposeHeaders = element.Element("expose-headers")?.Elements("header")
            .Select(e => PolicyDecompilerContext.GetElementText(e)).ToList();
        if (exposeHeaders != null && exposeHeaders.Count > 0)
        {
            props.Add($"ExposeHeaders = new[] {{ {string.Join(", ", exposeHeaders.Select(PolicyDecompilerContext.Literal))} }}");
        }

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "Cors", "CorsConfig", props);
    }
}
