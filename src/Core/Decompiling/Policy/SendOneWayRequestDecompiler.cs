// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class SendOneWayRequestDecompiler : IPolicyDecompiler
{
    public string PolicyName => "send-one-way-request";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();
        context.AddOptionalStringProp(props, element, "mode", "Mode");
        context.AddOptionalIntProp(props, element, "timeout", "Timeout");

        var url = element.Element("set-url");
        if (url != null)
        {
            var urlValue = PolicyDecompilerContext.GetElementText(url);
            props.Add($"Url = {context.HandleValue(urlValue, "RequestUrl")}");
        }

        var method = element.Element("set-method");
        if (method != null)
        {
            props.Add($"Method = {PolicyDecompilerContext.Literal(PolicyDecompilerContext.GetElementText(method))}");
        }

        var headers = element.Elements("set-header").ToList();
        if (headers.Count > 0)
        {
            var headerConfigs = headers.Select(context.BuildHeaderConfigString).ToList();
            props.Add($"Headers = new HeaderConfig[]\n            {{\n                {string.Join(",\n                ", headerConfigs)},\n            }}");
        }

        var body = element.Element("set-body");
        if (body != null)
        {
            props.Add(context.BuildBodyConfigProperty(body));
        }

        EmitSendRequestAuthentication(context, element, props);
        EmitSendRequestProxy(element, props);

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "SendOneWayRequest", "SendOneWayRequestConfig", props);
    }

    private static void EmitSendRequestAuthentication(PolicyDecompilerContext context, XElement element, List<string> props)
    {
        var authBasic = element.Element("authentication-basic");
        if (authBasic != null)
        {
            var u = authBasic.Attribute("username")?.Value ?? "";
            var p = authBasic.Attribute("password")?.Value ?? "";
            props.Add($"Authentication = new BasicAuthenticationConfig {{ Username = {context.HandleValue(u, "Username")}, Password = {context.HandleValue(p, "Password")} }}");
            return;
        }

        var authCert = element.Element("authentication-certificate");
        if (authCert != null)
        {
            var certProps = new List<string>();
            var thumb = authCert.Attribute("thumbprint")?.Value;
            var certId = authCert.Attribute("certificate-id")?.Value;
            if (thumb != null) certProps.Add($"Thumbprint = {context.HandleValue(thumb, "Thumbprint")}");
            if (certId != null) certProps.Add($"CertificateId = {context.HandleValue(certId, "CertificateId")}");
            props.Add($"Authentication = new CertificateAuthenticationConfig {{ {string.Join(", ", certProps)} }}");
            return;
        }

        var authMi = element.Element("authentication-managed-identity");
        if (authMi != null)
        {
            var resource = authMi.Attribute("resource")?.Value ?? "";
            var miProps = new List<string> { $"Resource = {context.HandleValue(resource, "Resource")}" };
            var clientId = authMi.Attribute("client-id")?.Value;
            if (clientId != null) miProps.Add($"ClientId = {context.HandleValue(clientId, "ClientId")}");
            props.Add($"Authentication = new ManagedIdentityAuthenticationConfig {{ {string.Join(", ", miProps)} }}");
        }
    }

    private static void EmitSendRequestProxy(XElement element, List<string> props)
    {
        var proxy = element.Element("proxy");
        if (proxy != null)
        {
            var proxyProps = new List<string>();
            var url = proxy.Attribute("url")?.Value;
            if (url != null) proxyProps.Add($"Url = {PolicyDecompilerContext.Literal(url)}");
            var username = proxy.Attribute("username")?.Value;
            if (username != null) proxyProps.Add($"Username = {PolicyDecompilerContext.Literal(username)}");
            var password = proxy.Attribute("password")?.Value;
            if (password != null) proxyProps.Add($"Password = {PolicyDecompilerContext.Literal(password)}");
            if (proxyProps.Count > 0)
            {
                props.Add($"Proxy = new ProxyConfig {{ {string.Join(", ", proxyProps)} }}");
            }
        }
    }
}
