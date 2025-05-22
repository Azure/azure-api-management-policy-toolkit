// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class AppendQueryParameterCompiler()
    : BaseSetQueryParameterCompiler(nameof(IInboundContext.AppendQueryParameter), "append");

public class SetQueryParameterCompiler()
    : BaseSetQueryParameterCompiler(nameof(IInboundContext.SetQueryParameter), "override");

public class SetIfNotExistQueryParameterCompiler()
    : BaseSetQueryParameterCompiler(nameof(IInboundContext.SetQueryParameterIfNotExist), "skip");

public class RemoveQueryParameterCompiler()
    : BaseSetQueryParameterCompiler(nameof(IInboundContext.RemoveQueryParameter), "delete");

public abstract class BaseSetQueryParameterCompiler : IMethodPolicyHandler
{
    readonly string _type;

    protected BaseSetQueryParameterCompiler(string methodName, string type)
    {
        MethodName = methodName;
        _type = type;
    }

    public string MethodName { get; }

    public void Handle(IDocumentCompilationContext context, InvocationExpressionSyntax node)
    {
        var arguments = node.ArgumentList.Arguments;
        if (_type != "delete" && arguments.Count < 2 ||
            _type == "delete" && arguments.Count != 1)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.ArgumentCountMissMatchForPolicy,
                node.ArgumentList.GetLocation(),
                "set-query-parameter"));
            return;
        }

        var element = new XElement("set-query-parameter");

        var name = node.ArgumentList.Arguments[0].Expression.ProcessParameter(context);
        element.Add(new XAttribute("name", name));
        element.Add(new XAttribute("exists-action", _type));

        for (int i = 1; i < arguments.Count; i++)
        {
            element.Add(new XElement("value", arguments[i].Expression.ProcessParameter(context)));
        }

        context.AddPolicy(element);
    }
}