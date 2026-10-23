// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class IncludeFragmentTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { context.IncludeFragment("fragment-inbound"); }
            public void Outbound(IOutboundContext context) { context.IncludeFragment("fragment-outbound"); }
            public void Backend(IBackendContext context) { context.IncludeFragment("fragment-backend"); }
            public void OnError(IOnErrorContext context) { context.IncludeFragment("fragment-on-error"); }
        }
        """,
        """
        <policies>
            <inbound>
                <include-fragment fragment-id="fragment-inbound" />
            </inbound>
            <outbound>
                <include-fragment fragment-id="fragment-outbound" />
            </outbound>
            <backend>
                <include-fragment fragment-id="fragment-backend" />
            </backend>
            <on-error>
                <include-fragment fragment-id="fragment-on-error" />
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile include-fragment policy in sections"
    )]
    public void ShouldCompileIncludeFragmentPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) { context.IncludeFragment(new InboundFragment { XFragmentValue = "inbound-value" }); }
            public void Outbound(IOutboundContext context) { context.IncludeFragment(new OutboundFragment { XFragmentValue = "outbound-value" }); }
            public void Backend(IBackendContext context) { context.IncludeFragment(new BackendFragment()); }
            public void OnError(IOnErrorContext context) { context.IncludeFragment(new OnErrorFragment { XFragmentValue = "on-error-value" }); }
        }
        """,
        """
        [Document( Type = DocumentType.Fragment )] 
        public class InboundFragment : IFragment
        {
            [FragmentVariable]
            public string XFragmentValue { get; set; };

            public void Fragment(IFragmentContext context)
            {
                context.SetHeader("X-Fragment", XFragmentValue);
            }
        }

        [Document("my-fragment", Type = DocumentType.Fragment )] 
        public class OutboundFragment : IFragment
        {
            [FragmentVariable]
            public string XFragmentValue { get; set; };

            public void Fragment(IFragmentContext context)
            {
                context.SetHeader("X-Fragment", XFragmentValue);
            }
        }

        [Document( Type = DocumentType.Fragment )] 
        public class BackendFragment : IFragment
        {
            [FragmentVariable]
            public string XFragmentValue { get; set; };

            public void Fragment(IFragmentContext context)
            {
                context.SetHeader("X-Fragment", XFragmentValue);
            }
        }

        [Document( Type = DocumentType.Fragment )] 
        public class OnErrorFragment : IFragment
        {
            [FragmentVariable("my-variable")]
            public string XFragmentValue { get; set; };

            public void Fragment(IFragmentContext context)
            {
                context.SetHeader("X-Fragment", XFragmentValue);
            }
        }
        """,
        """
        <policies>
            <inbound>
                <set-variable name="InboundFragment-XFragmentValue" value="inbound-value" />
                <include-fragment fragment-id="InboundFragment" />
            </inbound>
            <outbound>
                <set-variable name="my-fragment-XFragmentValue" value="outbound-value" />
                <include-fragment fragment-id="my-fragment" />
            </outbound>
            <backend>
                <choose>
                    <when condition="@(true)">
                        <set-variable name="BackendFragment-XFragmentValue" value="" />
                        <include-fragment fragment-id="BackendFragment" />
                    </when>
                </choose>
            </backend>
            <on-error>
                <set-variable name="OnErrorFragment-my-variable" value="on-error-value" />
                <include-fragment fragment-id="OnErrorFragment" />
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile include-fragment policy using objects with variables in sections"
    )]
    public void ShouldCompileIncludeFragmentPolicyWithVariables(string code, string fragment, string expectedXml)
    {
        code.CompileDocument(fragment).Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }

    [TestMethod]
    [DataRow("[Document]", "NamedFragment")]
    [DataRow("[Document(\"explicit-name\")]", "explicit-name")]
    [DataRow("[Document(Names.Fragment)]", "constant-name")]
    [DataRow("[Document(nameof(NamedFragment))]", "NamedFragment")]
    public void ShouldUseTheExportedFragmentName(string attribute, string expectedName)
    {
        const string code = """
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context) { context.IncludeFragment(new Test.NamedFragment()); }
            }
            """;
        var fragment = $$"""
            {{attribute}}
            public class NamedFragment : IFragment
            {
                public void Fragment(IFragmentContext context) { context.SetHeader("X", "value"); }
            }
            public static class Names
            {
                public const string Fragment = "constant-name";
            }
            """;
        var expected = $$"""
            <policies>
                <inbound>
                    <include-fragment fragment-id="{{expectedName}}" />
                </inbound>
            </policies>
            """;

        code.CompileDocument(fragment).Should().BeSuccessful().And.DocumentEquivalentTo(expected);
    }

    [TestMethod]
    [DataRow("", "", "fallback")]
    [DataRow(" = null;", "", "fallback")]
    [DataRow(" = null!;", "", "fallback")]
    [DataRow(" = \"initial\";", "", "initial")]
    [DataRow(" = \"\";", "", "")]
    [DataRow("", "{ Value = \"\" }", "")]
    [DataRow(" = \"initial\";", "{ Value = null }", "fallback")]
    [DataRow(" = \"initial\";", "{ Value = null! }", "fallback")]
    [DataRow("", "{ Value = \"second\" }", "second")]
    public void ShouldResetFragmentVariablesForEachInstance(
        string propertyInitializer, string objectInitializer, string expectedValue)
    {
        var code = $$"""
            [Document]
            public class PolicyDocument : IDocument
            {
                public void Inbound(IInboundContext context)
                {
                    context.IncludeFragment(new ValueFragment { Value = "first" });
                    context.IncludeFragment(new ValueFragment() {{objectInitializer}});
                }
            }
            """;
        var fragment = $$"""
            [Document("values")]
            public class ValueFragment : IFragment
            {
                [FragmentVariable("value", DefaultValue = "fallback")]
                public string Value { get; set; }{{propertyInitializer}}
                public void Fragment(IFragmentContext context) { context.SetHeader("X", Value); }
            }
            """;
        var expected = $$"""
            <policies>
                <inbound>
                    <set-variable name="values-value" value="first" />
                    <include-fragment fragment-id="values" />
                    <set-variable name="values-value" value="{{expectedValue}}" />
                    <include-fragment fragment-id="values" />
                </inbound>
            </policies>
            """;

        code.CompileDocument(fragment).Should().BeSuccessful().And.DocumentEquivalentTo(expected);
    }
}