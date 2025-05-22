// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Policy;

public class AppendHeaderCompiler() : BaseSetHeaderCompiler(nameof(IInboundContext.AppendHeader), "append");

public class SetHeaderCompiler() : BaseSetHeaderCompiler(nameof(IInboundContext.SetHeader), "override");

public class SetHeaderIfNotExistCompiler() : BaseSetHeaderCompiler(nameof(IInboundContext.SetHeaderIfNotExist), "skip");

public class RemoveHeaderCompiler() : BaseSetHeaderCompiler(nameof(IInboundContext.RemoveHeader), "delete");

public abstract class BaseSetHeaderCompiler : IMethodPolicyHandler
{
    private readonly string _type;

    protected BaseSetHeaderCompiler(string methodName, string type)
    {
        MethodName = methodName;
        _type = type;
    }

    public string MethodName { get; }

    public void Handle(ICompilationContext context, InvocationExpressionSyntax node)
    {
        var arguments = node.ArgumentList.Arguments;
        if (_type != "delete" && arguments.Count < 2 ||
            _type == "delete" && arguments.Count != 1)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.ArgumentCountMissMatchForPolicy,
                node.ArgumentList.GetLocation(),
                "set-header"));
            return;
        }

        var element = new XElement("set-header");

        var name = node.ArgumentList.Arguments[0].Expression.ProcessParameter(context);
        element.Add(new XAttribute("name", name));
        element.Add(new XAttribute("exists-action", _type));

        for (int i = 1; i < arguments.Count; i++)
        {
            element.Add(new XElement("value", arguments[i].Expression.ProcessParameter(context)));
        }

        context.AddPolicy(element);
    }

    public static void HandleHeaders(ICompilationContext context, XElement root, InitializerValue headers)
    {
        foreach (var header in headers.UnnamedValues!)
        {
            if (!header.TryGetValues<HeaderConfig>(out var headerValues))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.PolicyArgumentIsNotOfRequiredType,
                    header.Node.GetLocation(),
                    $"{root.Name}.set-header",
                    nameof(HeaderConfig)
                ));
                continue;
            }

            var headerElement = new XElement("set-header");
            if (!headerElement.AddAttribute(headerValues, nameof(HeaderConfig.Name), "name"))
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.RequiredParameterNotDefined,
                    header.Node.GetLocation(),
                    $"{root.Name}.set-header",
                    nameof(HeaderConfig.Name)
                ));
                continue;
            }

            headerElement.AddAttribute(headerValues, nameof(HeaderConfig.ExistsAction), "exists-action");

            if (headerValues.TryGetValue(nameof(HeaderConfig.Values), out var values) &&
                values.UnnamedValues is not null)
            {
                foreach (var value in values.UnnamedValues)
                {
                    headerElement.Add(new XElement("value", value.Value!));
                }
            }

            root.Add(headerElement);
        }
    }
}