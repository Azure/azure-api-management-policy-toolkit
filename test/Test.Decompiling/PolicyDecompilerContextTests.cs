// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Tests.Decompiling;

[TestClass]
public class PolicyDecompilerContextTests
{
    [TestMethod]
    [DataRow("@$\"a{{x}}b\"", "(@$\"a\" + context.NamedValue(\"x\") + $@\"b\")")]
    [DataRow("$@\"a{{x}}b\"", "($@\"a\" + context.NamedValue(\"x\") + $@\"b\")")]
    [DataRow("$\"a{ {{x}} }b\"", "$\"a{ (context.NamedValue(\"x\")) }b\"")]
    [DataRow("$\"{{\\\"k\\\": \\\"{{s}}\\\"}}\"", "($\"{{\\\"k\\\": \\\"\" + context.NamedValue(\"s\") + $\"\\\"}}\")")]
    [DataRow("$\"{x.Get(\"{{v}}\", \"}\")}\"", "$\"{x.Get(\"{{v}}\", \"}\")}\"")]
    [DataRow("$\"{{\" + a + \"}}\"", "$\"{{\" + a + \"}}\"")]
    [DataRow("\"{{\" + a + \"}}\"", "\"{{\" + a + \"}}\"")]
    [DataRow("$\"{x.Split('{')[0]} {{v}}\"", "($\"{x.Split('{')[0]} \" + context.NamedValue(\"v\") + $\"\")")]
    [DataRow("$@\"a{ {{x}} }b\"", "$@\"a{ (context.NamedValue(\"x\")) }b\"")]
    [DataRow("$\"{{{a}}}\"", "$\"{(context.NamedValue(\"a\"))}\"")]
    [DataRow("$\"x{{{a}}.Length}\"", "$\"x{(context.NamedValue(\"a\")).Length}\"")]
    [DataRow("$@\"{{{a}}}\"", "$@\"{(context.NamedValue(\"a\"))}\"")]
    [DataRow("$\"{{{{a}}}}\"", "($\"{{\" + context.NamedValue(\"a\") + $\"}}\")")]
    public void ShouldReplaceNamedValueTokensInStrings(string body, string expected)
    {
        PolicyDecompilerContext.ReplaceNamedValueTokens(body).Should().Be(expected);
    }
}