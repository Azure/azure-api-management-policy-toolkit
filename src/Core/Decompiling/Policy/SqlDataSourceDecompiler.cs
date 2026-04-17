// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;

public class SqlDataSourceDecompiler : IPolicyDecompiler
{
    public string PolicyName => "sql-data-source";

    public void Decompile(CodeWriter writer, XElement element, string contextVar, PolicyDecompilerContext context)
    {
        var prefix = PolicyDecompilerContext.GetContextPrefix(element, contextVar);
        var props = new List<string>();

        var connectionInfoEl = element.Element("connection-info");
        if (connectionInfoEl != null)
        {
            var ciProps = new List<string>();
            var connStrEl = connectionInfoEl.Element("connection-string");
            if (connStrEl != null)
            {
                ciProps.Add($"ConnectionString = {context.HandleValue(PolicyDecompilerContext.GetElementText(connStrEl), "ConnectionString")}");
                var useMi = connStrEl.Attribute("use-managed-identity")?.Value;
                if (useMi != null) ciProps.Add($"UseManagedIdentity = {context.HandleValue(useMi, "UseManagedIdentity")}");
                var clientIdAttr = connStrEl.Attribute("client-id")?.Value;
                if (clientIdAttr != null) ciProps.Add($"ClientId = {context.HandleValue(clientIdAttr, "ClientId")}");
            }
            props.Add($"ConnectionInfo = new SqlConnectionInfoConfig {{ {string.Join(", ", ciProps)} }}");
        }

        var requestEl = element.Element("request");
        if (requestEl != null)
        {
            var reqProps = new List<string>();
            var sqlStatementEl = requestEl.Element("sql-statement");
            if (sqlStatementEl != null)
                reqProps.Add($"SqlStatement = {context.HandleValue(PolicyDecompilerContext.GetElementText(sqlStatementEl), "SqlStatement")}");

            var parametersEl = requestEl.Element("parameters");
            if (parametersEl != null)
            {
                var parameters = parametersEl.Elements("parameter").Select(p =>
                {
                    var pProps = new List<string>
                    {
                        $"Name = {PolicyDecompilerContext.Literal(p.Attribute("name")?.Value ?? "")}",
                        $"SqlType = {PolicyDecompilerContext.Literal(p.Attribute("sql-type")?.Value ?? "")}",
                        $"Value = {context.HandleValue(PolicyDecompilerContext.GetElementText(p), "ParameterValue")}"
                    };
                    return $"new SqlParameterConfig {{ {string.Join(", ", pProps)} }}";
                });
                reqProps.Add($"Parameters = new SqlParameterConfig[] {{ {string.Join(", ", parameters)} }}");
            }
            props.Add($"Request = new SqlRequestConfig {{ {string.Join(", ", reqProps)} }}");
        }

        context.AddOptionalStringProp(props, element, "single-result", "SingleResult");
        context.AddOptionalStringProp(props, element, "timeout", "Timeout");

        PolicyDecompilerContext.EmitConfigCall(writer, prefix, "SqlDataSource", "SqlDataSourceConfig", props);
    }
}