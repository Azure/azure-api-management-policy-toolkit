// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Tests.Decompiling;

[TestClass]
public class DecompiledHelperNameTests
{
    [TestMethod]
    [DataRow("user id", "GetUserId0")]
    [DataRow("user:id", "GetUserId0")]
    [DataRow("user-id", "GetUserId0")]
    [DataRow("user_id", "GetUserId0")]
    [DataRow("$id", "GetId0")]
    [DataRow("1st", "Get1st0")]
    public void HelperOfAVariable_GetsAValidName(string variable, string helper)
    {
        var csharp = new PolicyDecompiler().DecompileDocument(
            $"""<policies><inbound><set-variable name="{variable}" value="@(context.RequestId)" /></inbound></policies>""",
            "HelperPolicy",
            "HelperTest");

        csharp.Should().Contain($"{helper}(IExpressionContext context)");
        CSharpSyntaxTree.ParseText(csharp).GetDiagnostics().Should().BeEmpty(csharp);
    }
}
