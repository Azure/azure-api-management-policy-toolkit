// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class SqlDataSourceCompiler : IMethodPolicyHandler
{
    public string MethodName => nameof(IBackendContext.SqlDataSource);

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        if (!node.TryExtractingConfigParameter<SqlDataSourceConfig>(context, "sql-data-source",
                out IReadOnlyDictionary<string, InitializerValue>? values))
        {
            return;
        }

        var element = new XElement("sql-data-source");
        element.AddAttribute(values, nameof(SqlDataSourceConfig.SingleResult), "single-result");
        element.AddAttribute(values, nameof(SqlDataSourceConfig.Timeout), "timeout");

        if (!values.TryGetValue(nameof(SqlDataSourceConfig.ConnectionInfo), out var connectionInfoValue))
        {
            context.Report(Diagnostic.Create(CompilationErrors.RequiredParameterNotDefined, node.GetLocation(), "sql-data-source", nameof(SqlDataSourceConfig.ConnectionInfo)));
            return;
        }

        if (!connectionInfoValue.TryGetValues<SqlConnectionInfoConfig>(out var connectionInfoValues))
        {
            context.Report(Diagnostic.Create(CompilationErrors.PolicyArgumentIsNotOfRequiredType, connectionInfoValue.Node.GetLocation(), "sql-data-source.connection-info", nameof(SqlConnectionInfoConfig)));
            return;
        }

        var connectionInfoElement = new XElement("connection-info");

        if (!connectionInfoValues.TryGetValue(nameof(SqlConnectionInfoConfig.ConnectionString), out var connStrValue) || connStrValue.Value is null)
        {
            context.Report(Diagnostic.Create(CompilationErrors.RequiredParameterNotDefined, connectionInfoValue.Node.GetLocation(), "sql-data-source.connection-info", nameof(SqlConnectionInfoConfig.ConnectionString)));
            return;
        }

        var connectionStringElement = new XElement("connection-string", connStrValue.Value);
        if (connectionInfoValues.TryGetValue(nameof(SqlConnectionInfoConfig.UseManagedIdentity), out var useMi) && useMi.Value is not null)
        {
            connectionStringElement.SetAttributeValue("use-managed-identity", useMi.Value);
        }
        if (connectionInfoValues.TryGetValue(nameof(SqlConnectionInfoConfig.ClientId), out var clientId) && clientId.Value is not null)
        {
            connectionStringElement.SetAttributeValue("client-id", clientId.Value);
        }
        connectionInfoElement.Add(connectionStringElement);
        element.Add(connectionInfoElement);

        if (!values.TryGetValue(nameof(SqlDataSourceConfig.Request), out var requestValue))
        {
            context.Report(Diagnostic.Create(CompilationErrors.RequiredParameterNotDefined, node.GetLocation(), "sql-data-source", nameof(SqlDataSourceConfig.Request)));
            return;
        }

        if (!requestValue.TryGetValues<SqlRequestConfig>(out var requestValues))
        {
            context.Report(Diagnostic.Create(CompilationErrors.PolicyArgumentIsNotOfRequiredType, requestValue.Node.GetLocation(), "sql-data-source.request", nameof(SqlRequestConfig)));
            return;
        }

        var requestElement = new XElement("request");

        if (!requestValues.TryGetValue(nameof(SqlRequestConfig.SqlStatement), out var sqlStatementValue) || sqlStatementValue.Value is null)
        {
            context.Report(Diagnostic.Create(CompilationErrors.RequiredParameterNotDefined, requestValue.Node.GetLocation(), "sql-data-source.request", nameof(SqlRequestConfig.SqlStatement)));
            return;
        }

        requestElement.Add(new XElement("sql-statement", sqlStatementValue.Value));

        if (requestValues.TryGetValue(nameof(SqlRequestConfig.Parameters), out var parametersValue))
        {
            var parametersElement = new XElement("parameters");
            var items = parametersValue.UnnamedValues ?? [];

            foreach (var param in items)
            {
                if (!param.TryGetValues<SqlParameterConfig>(out var paramValues))
                {
                    context.Report(Diagnostic.Create(CompilationErrors.PolicyArgumentIsNotOfRequiredType, param.Node.GetLocation(), "sql-data-source.request.parameter", nameof(SqlParameterConfig)));
                    continue;
                }

                if (!paramValues.TryGetValue(nameof(SqlParameterConfig.Name), out var nameValue) || nameValue.Value is null)
                {
                    context.Report(Diagnostic.Create(CompilationErrors.RequiredParameterNotDefined, param.Node.GetLocation(), "sql-data-source.request.parameter", nameof(SqlParameterConfig.Name)));
                    continue;
                }

                if (!paramValues.TryGetValue(nameof(SqlParameterConfig.SqlType), out var typeValue) || typeValue.Value is null)
                {
                    context.Report(Diagnostic.Create(CompilationErrors.RequiredParameterNotDefined, param.Node.GetLocation(), "sql-data-source.request.parameter", nameof(SqlParameterConfig.SqlType)));
                    continue;
                }

                if (!paramValues.TryGetValue(nameof(SqlParameterConfig.Value), out var valValue) || valValue.Value is null)
                {
                    context.Report(Diagnostic.Create(CompilationErrors.RequiredParameterNotDefined, param.Node.GetLocation(), "sql-data-source.request.parameter", nameof(SqlParameterConfig.Value)));
                    continue;
                }

                var paramElement = new XElement("parameter");
                paramElement.SetAttributeValue("name", nameValue.Value);
                paramElement.SetAttributeValue("sql-type", typeValue.Value);
                paramElement.Add(valValue.Value);
                parametersElement.Add(paramElement);
            }

            requestElement.Add(parametersElement);
        }

        element.Add(requestElement);
        context.AddPolicy(element);
    }
}