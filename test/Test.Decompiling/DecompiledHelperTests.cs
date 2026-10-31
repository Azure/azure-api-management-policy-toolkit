// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Tests.Decompiling;

[TestClass]
public class DecompiledHelperTests
{
    private static string Decompile(string xml) =>
        new PolicyDecompiler().DecompileDocument(xml, "HelperPolicy", "HelperTest");

    // Builds the generated C# against the authoring library, as a project that holds it would.
    private static IEnumerable<string> BuildErrors(string csharp)
    {
        var assemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(IDocument).Assembly.Location)
            .Distinct();
        var compilation = CSharpCompilation.Create(
            "Helpers",
            [CSharpSyntaxTree.ParseText(csharp)],
            assemblies.Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.ToString());
    }

    [TestMethod]
    public void TypeNamedInAnInterpolatedString_GetsItsUsingDirective()
    {
        var csharp = Decompile(
            """<policies><inbound><set-variable name="a" value="@($&quot;{Guid.NewGuid()}&quot;)" /></inbound></policies>""");

        csharp.Should().Contain("using System;");
        BuildErrors(csharp).Should().BeEmpty(csharp);
    }

    [TestMethod]
    public void TypesNamedInAStatementExpression_GetTheirUsingDirectives()
    {
        var csharp = Decompile(
            """<policies><inbound><set-variable name="a" value="@{ var id = Guid.NewGuid(); return $&quot;{id}{Encoding.UTF8.WebName}&quot;; // end&#10;}" /></inbound></policies>""");

        csharp.Should().Contain("using System;").And.Contain("using System.Text;");
    }

    [TestMethod]
    public void TypesNamedInExpressions_GetTheirUsingDirectives()
    {
        var csharp = Decompile(
            """
            <policies>
                <inbound>
                    <set-variable name="a" value="@(context.Request.Body.As<JObject>().ToString())" />
                    <set-variable name="b" value="@(Regex.IsMatch(context.Request.Method, "GET"))" />
                    <set-variable name="c" value="@(Encoding.UTF8.GetByteCount(context.Request.Method))" />
                </inbound>
            </policies>
            """);

        csharp.Should().Contain("using Newtonsoft.Json.Linq;")
            .And.Contain("using System.Text.RegularExpressions;")
            .And.Contain("using System.Text;")
            .And.NotContain("using System.Xml.Linq;");
        BuildErrors(csharp).Should().BeEmpty(csharp);
    }

    [TestMethod]
    public void TypesNamedInAFragment_GetTheirUsingDirectives()
    {
        var csharp = new PolicyDecompiler().DecompileFragment(
            """<fragment><set-variable name="a" value="@(Guid.NewGuid().ToString())" /></fragment>""",
            "fragment", "HelperFragment", "HelperTest");

        csharp.Should().Contain("using System;");
        BuildErrors(csharp).Should().BeEmpty(csharp);
    }

    [TestMethod]
    public void QueryExpression_GetsTheLinqUsingDirective()
    {
        var csharp = Decompile(
            """<policies><inbound><set-variable name="a" value="@(string.Join(",", from h in context.Request.Headers select h.Key))" /></inbound></policies>""");

        csharp.Should().Contain("using System.Linq;");
        BuildErrors(csharp).Should().BeEmpty(csharp);
    }

    [TestMethod]
    public void AuthorizationInAnExpression_StaysTheGatewaysType()
    {
        var csharp = Decompile(
            """<policies><inbound><set-variable name="a" value="@(((Authorization)context.Variables["auth"]).AccessToken + IPAddress.Loopback)" /></inbound></policies>""");

        csharp.Should().Contain("using System.Net;")
            .And.Contain("using Authorization = Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.Authorization;");
        BuildErrors(csharp).Should().BeEmpty(csharp);
    }

    [TestMethod]
    public void AuthorizationInAnInterpolatedString_StaysTheGatewaysType()
    {
        var csharp = Decompile(
            """<policies><inbound><set-variable name="a" value="@($&quot;{((Authorization)context.Variables[&quot;auth&quot;]).AccessToken}&quot; + System.Net.IPAddress.Loopback)" /></inbound></policies>""");

        BuildErrors(csharp).Should().BeEmpty(csharp);
    }

    [TestMethod]
    public void DocumentWithoutSuchTypes_GetsNoExtraUsingDirectives()
    {
        var csharp = Decompile(
            """<policies><inbound><set-header name="X"><value>@(context.Request.Method)</value></set-header></inbound></policies>""");

        csharp.Should().StartWith("using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;");
    }
}
