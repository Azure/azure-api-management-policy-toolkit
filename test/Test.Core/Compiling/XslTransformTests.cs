// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class XslTransformTests
{
    [TestMethod]
    [DataRow(
        """"
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.XslTransform(new XslTransformConfig
                {
                    StyleSheet =    """
                                    <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
                                        <xsl:template match="/">
                                            <xsl:value-of select="." />
                                        </xsl:template>
                                    </xsl:stylesheet>
                                    """,
                });
            }
            public void Outbound(IOutboundContext context) {
                context.XslTransform(new XslTransformConfig
                {
                    StyleSheet =    """
                                    <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
                                        <xsl:template match="/">
                                            <xsl:value-of select="." />
                                        </xsl:template>
                                    </xsl:stylesheet>
                                    """,
                });
            }
            public void OnError(IOnErrorContext context) {
                context.XslTransform(new XslTransformConfig
                {
                    StyleSheet =    """
                                    <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
                                        <xsl:template match="/">
                                            <xsl:value-of select="." />
                                        </xsl:template>
                                    </xsl:stylesheet>
                                    """,
                });
            }
        }
        """",
        """
        <policies>
            <inbound>
                <xsl-transform>
                    <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
                        <xsl:template match="/">
                            <xsl:value-of select="." />
                        </xsl:template>
                    </xsl:stylesheet>
                </xsl-transform>
            </inbound>
            <outbound>
                <xsl-transform>
                    <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
                        <xsl:template match="/">
                            <xsl:value-of select="." />
                        </xsl:template>
                    </xsl:stylesheet>
                </xsl-transform>
            </outbound>
            <on-error>
                <xsl-transform>
                    <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
                        <xsl:template match="/">
                            <xsl:value-of select="." />
                        </xsl:template>
                    </xsl:stylesheet>
                </xsl-transform>
            </on-error>
        </policies>
        """,
        DisplayName = "Should compile xsl-transform policy in sections"
    )]
    [DataRow(
        """"
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.XslTransform(new XslTransformConfig
                {
                    StyleSheet =    """
                                    <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
                                        <xsl:output method="xml" indent="yes" />
                                        <xsl:param name="User-Agent" />
                                        <xsl:template match="* | @* | node()">
                                            <xsl:copy>
                                                <xsl:if test="self::* and not(parent::*)">
                                                    <xsl:attribute name="User-Agent">
                                                        <xsl:value-of select="$User-Agent" />
                                                    </xsl:attribute>
                                                </xsl:if>
                                                <xsl:apply-templates select="* | @* | node()" />
                                            </xsl:copy>
                                        </xsl:template>
                                    </xsl:stylesheet>
                                    """,
                    Parameters = [
                        new XslTransformParameter
                        {
                            Name = "User-Agent",
                            Value = "Agent-Value"
                        }
                    ]
                });
            }
        }
        """",
        """
        <policies>
            <inbound>
                <xsl-transform>
                    <parameter name="User-Agent">Agent-Value</parameter>
                    <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
                        <xsl:output method="xml" indent="yes" />
                        <xsl:param name="User-Agent" />
                        <xsl:template match="* | @* | node()">
                            <xsl:copy>
                                <xsl:if test="self::* and not(parent::*)">
                                    <xsl:attribute name="User-Agent">
                                        <xsl:value-of select="$User-Agent" />
                                    </xsl:attribute>
                                </xsl:if>
                                <xsl:apply-templates select="* | @* | node()" />
                            </xsl:copy>
                        </xsl:template>
                    </xsl:stylesheet>
                </xsl-transform>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile xsl-transform policy with single parameter"
    )]
    [DataRow(
        """"
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.XslTransform(new XslTransformConfig
                {
                    StyleSheet =    """
                                    <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
                                        <xsl:output method="xml" indent="yes" />
                                        <xsl:param name="User-Agent" />
                                        <xsl:template match="* | @* | node()">
                                            <xsl:copy>
                                                <xsl:if test="self::* and not(parent::*)">
                                                    <xsl:attribute name="User-Agent">
                                                        <xsl:value-of select="$User-Agent" />
                                                    </xsl:attribute>
                                                </xsl:if>
                                                <xsl:apply-templates select="* | @* | node()" />
                                            </xsl:copy>
                                        </xsl:template>
                                    </xsl:stylesheet>
                                    """,
                    Parameters = [
                        new XslTransformParameter { Name = "User-Agent", Value = "Agent-Value" },
                        new XslTransformParameter { Name = "Test1", Value = "Test1" },
                        new XslTransformParameter { Name = "Test2", Value = "Test2" }
                    ]
                });
            }
        }
        """",
        """
        <policies>
            <inbound>
                <xsl-transform>
                    <parameter name="User-Agent">Agent-Value</parameter>
                    <parameter name="Test1">Test1</parameter>
                    <parameter name="Test2">Test2</parameter>
                    <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
                        <xsl:output method="xml" indent="yes" />
                        <xsl:param name="User-Agent" />
                        <xsl:template match="* | @* | node()">
                            <xsl:copy>
                                <xsl:if test="self::* and not(parent::*)">
                                    <xsl:attribute name="User-Agent">
                                        <xsl:value-of select="$User-Agent" />
                                    </xsl:attribute>
                                </xsl:if>
                                <xsl:apply-templates select="* | @* | node()" />
                            </xsl:copy>
                        </xsl:template>
                    </xsl:stylesheet>
                </xsl-transform>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile xsl-transform policy with multiple parameter"
    )]
    [DataRow(
        """"
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.XslTransform(new XslTransformConfig
                {
                    StyleSheet =    """
                                    <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
                                        <xsl:output method="xml" indent="yes" />
                                        <xsl:param name="User-Agent" />
                                        <xsl:template match="* | @* | node()">
                                            <xsl:copy>
                                                <xsl:if test="self::* and not(parent::*)">
                                                    <xsl:attribute name="User-Agent">
                                                        <xsl:value-of select="$User-Agent" />
                                                    </xsl:attribute>
                                                </xsl:if>
                                                <xsl:apply-templates select="* | @* | node()" />
                                            </xsl:copy>
                                        </xsl:template>
                                    </xsl:stylesheet>
                                    """,
                    Parameters = [
                        new XslTransformParameter
                        {
                            Name = "User-Agent",
                            Value = GetUserAgent(context.ExecutionContext)
                        }
                    ]
                });
            }
            string GetUserAgent(IInboundContext context) => 
                context.Request.Headers.GetValueOrDefault("User-Agent", "non-specified");
        }
        """",
        """
        <policies>
            <inbound>
                <xsl-transform>
                    <parameter name="User-Agent">@(context.Request.Headers.GetValueOrDefault("User-Agent", "non-specified"))</parameter>
                    <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
                        <xsl:output method="xml" indent="yes" />
                        <xsl:param name="User-Agent" />
                        <xsl:template match="* | @* | node()">
                            <xsl:copy>
                                <xsl:if test="self::* and not(parent::*)">
                                    <xsl:attribute name="User-Agent">
                                        <xsl:value-of select="$User-Agent" />
                                    </xsl:attribute>
                                </xsl:if>
                                <xsl:apply-templates select="* | @* | node()" />
                            </xsl:copy>
                        </xsl:template>
                    </xsl:stylesheet>
                </xsl-transform>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile xsl-transform policy with expression in parameter"
    )]
    [DataRow(
        """"
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Inbound(IInboundContext context) {
                context.XslTransform(new XslTransformConfig
                {
                    StyleSheet =    """
                                    <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
                                        <xsl:output omit-xml-declaration="yes" method="xml" indent="yes" />
                                        <xsl:template match="node()| @*|*">
                                            <xsl:copy>
                                                <xsl:apply-templates select="@* | node()|*" />
                                            </xsl:copy>
                                        </xsl:template>
                                    </xsl:stylesheet>
                                    """,
                });
            }
        }
        """",
        """
        <policies>
            <inbound>
                <xsl-transform>
                    <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
                        <xsl:output omit-xml-declaration="yes" method="xml" indent="yes" />
                        <xsl:template match="node()| @*|*">
                            <xsl:copy>
                                <xsl:apply-templates select="@* | node()|*" />
                            </xsl:copy>
                        </xsl:template>
                    </xsl:stylesheet>
                </xsl-transform>
            </inbound>
        </policies>
        """,
        DisplayName = "Should compile xsl-transform policy from examples"
    )]
    public void ShouldCompileXslTransformPolicy(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}