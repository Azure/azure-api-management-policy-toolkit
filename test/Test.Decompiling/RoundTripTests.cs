// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

using FluentAssertions;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling;
using Microsoft.Azure.ApiManagement.PolicyToolkit.IoC;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Tests.Decompiling;

[TestClass]
public class RoundTripTests
{
    private static readonly IEnumerable<MetadataReference> References = GetReferences();

    private static MetadataReference[] GetReferences()
    {
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var refs = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(XElement).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IDocument).Assembly.Location),
        };
        // Add common runtime assemblies needed for compiling real-world policy expressions
        foreach (var asm in new[] {
            "System.Runtime.dll", "System.Collections.dll", "System.Linq.dll",
            "System.Console.dll", "netstandard.dll",
            "System.Text.RegularExpressions.dll" })
        {
            var path = Path.Combine(runtimeDir, asm);
            if (File.Exists(path))
                refs.Add(MetadataReference.CreateFromFile(path));
        }

        // Add commonly used packages in APIM policy expressions
        TryAddAssemblyReference(refs, typeof(Newtonsoft.Json.JsonConvert));
        TryAddAssemblyReference(refs, typeof(Newtonsoft.Json.Linq.JObject));
        TryAddAssemblyReference(refs, typeof(System.Security.Cryptography.SHA256));
        TryAddAssemblyReference(refs, typeof(System.Net.WebUtility));
        TryAddAssemblyReference(refs, typeof(System.Xml.Linq.XElement));

        return refs.ToArray();
    }

    private static void TryAddAssemblyReference(List<MetadataReference> refs, Type type)
    {
        try
        {
            var location = type.Assembly.Location;
            if (!string.IsNullOrEmpty(location) && !refs.Any(r => r.Display == location))
                refs.Add(MetadataReference.CreateFromFile(location));
        }
        catch
        {
            // Package not available — skip
        }
    }

    private static ServiceProvider s_serviceProvider = null!;
    private static DocumentCompiler s_compiler = null!;
    private static PolicyDecompiler s_decompiler = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        ServiceCollection serviceCollection = new();
        s_serviceProvider = serviceCollection
            .SetupCompiler()
            .BuildServiceProvider();
        s_compiler = s_serviceProvider.GetRequiredService<DocumentCompiler>();
        s_decompiler = new PolicyDecompiler();
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        s_serviceProvider.Dispose();
    }

    [TestMethod]
    public void SimpleBase_RoundTrips()
    {
        var xml = "<policies><inbound><base /></inbound></policies>";
        AssertRoundTrip(xml);
    }

    [TestMethod]
    public void SetVariable_RoundTrips()
    {
        var xml = """<policies><inbound><set-variable name="test" value="hello" /></inbound></policies>""";
        AssertRoundTrip(xml);
    }

    [TestMethod]
    public void SetHeader_Override_RoundTrips()
    {
        var xml = """<policies><inbound><set-header name="X-Custom" exists-action="override"><value>myvalue</value></set-header></inbound></policies>""";
        AssertRoundTripSemantic(xml);
    }

    [TestMethod]
    public void Choose_When_Otherwise_RoundTrips()
    {
        var xml = """
            <policies>
                <inbound>
                    <choose>
                        <when condition="@(1 > 0)">
                            <set-variable name="local" value="true" />
                        </when>
                        <otherwise>
                            <set-variable name="local" value="false" />
                        </otherwise>
                    </choose>
                </inbound>
            </policies>
            """;
        AssertRoundTrip(xml);
    }

    [TestMethod]
    public void SetBackendService_RoundTrips()
    {
        var xml = """<policies><backend><set-backend-service base-url="https://api.example.com" /></backend></policies>""";
        AssertRoundTrip(xml);
    }

    [TestMethod]
    public void SendRequest_RoundTrips()
    {
        var xml = """
            <policies>
                <inbound>
                    <send-request response-variable-name="response" mode="new" timeout="30">
                        <set-url>https://api.example.com/resource</set-url>
                        <set-method>GET</set-method>
                    </send-request>
                </inbound>
            </policies>
            """;
        AssertRoundTrip(xml);
    }

    [TestMethod]
    public void ReturnResponse_RoundTrips()
    {
        var xml = """
            <policies>
                <inbound>
                    <return-response>
                        <set-status code="200" reason="OK" />
                    </return-response>
                </inbound>
            </policies>
            """;
        AssertRoundTrip(xml);
    }

    [TestMethod]
    public void IncludeFragment_RoundTrips()
    {
        var xml = """<policies><inbound><include-fragment fragment-id="my-fragment" /></inbound></policies>""";
        AssertRoundTrip(xml);
    }

    [TestMethod]
    public void Expression_SingleLine_RoundTrips()
    {
        var xml = """<policies><inbound><set-variable name="ip" value="@(context.Request.IpAddress)" /></inbound></policies>""";
        AssertRoundTrip(xml);
    }

    [TestMethod]
    public void Expression_MultiLine_RoundTrips()
    {
        // Multiline expressions lose internal whitespace during round-trip
        // (decompiler normalizes body formatting). Use semantic comparison.
        var xml = """<policies><inbound><set-variable name="result" value="@{var x = context.Request.IpAddress;return x;}" /></inbound></policies>""";
        AssertRoundTripSemantic(xml);
    }

    [TestMethod]
    public void NamedValueToken_RoundTrips()
    {
        var xml = """<policies><inbound><set-variable name="key" value="{{api-key}}" /></inbound></policies>""";
        AssertRoundTrip(xml);
    }

    [TestMethod]
    public void NamedValueTokenConcatenatedWithVariable_RoundTrips()
    {
        var xml = """<policies><inbound><set-variable name="url" value="@{var id = context.Request.Url.Path;return &quot;{{Base}}/&quot; + id;}" /></inbound></policies>""";
        AssertRoundTripSemantic(xml);
    }

    [TestMethod]
    [DataRow("context.Variables.TryGetValue(&quot;x&quot;, out var v) &amp;&amp; (string)v == &quot;a&quot;",
        DisplayName = "Out variable")]
    [DataRow("context.Variables[&quot;x&quot;] is string s &amp;&amp; s.Length &gt; 0", DisplayName = "Pattern variable")]
    [DataRow("context.Request.Headers.Keys.Any(a =&gt; int.TryParse(a, out var n) &amp;&amp; n &gt; 1) || context.Request.Headers.Keys.Any(b =&gt; int.TryParse(b, out var n) &amp;&amp; n &gt; 2)",
        DisplayName = "Same out variable in sibling lambdas")]
    public void ConditionWithDeclaration_RoundTrips(string condition)
    {
        var xml = $"""<policies><inbound><choose><when condition="@({condition})"><set-variable name="matched" value="true" /></when></choose></inbound></policies>""";
        AssertRoundTrip(xml);
    }

    [TestMethod]
    [DataRow("{{flag}}", DisplayName = "Named value as the whole condition")]
    [DataRow("context.Variables.ContainsKey(&quot;{{v}}&quot;)", DisplayName = "Named value in a string argument")]
    public void NamedValueTokenInCondition_RoundTrips(string condition)
    {
        var xml = $"""<policies><inbound><choose><when condition="@({condition})"><set-variable name="matched" value="true" /></when></choose></inbound></policies>""";
        AssertRoundTrip(xml);
    }

    [TestMethod]
    [DataRow("@(&quot;enabled=&quot; + {{flag}})", DisplayName = "After a string")]
    [DataRow("@({{flag}} + &quot;x&quot;)", DisplayName = "Before a string")]
    [DataRow("@(({{n}}) * 2)", DisplayName = "Parenthesized operand")]
    [DataRow("@(-({{n}}))", DisplayName = "Parenthesized under a unary operator")]
    [DataRow("@(({{n}}).ToString())", DisplayName = "Parenthesized receiver")]
    [DataRow("@($&quot;a{({{n}})}b&quot;)", DisplayName = "Interpolation hole")]
    public void NamedValueTokenUsedAsCode_RoundTrips(string value)
    {
        var xml = $"""<policies><inbound><set-variable name="value" value="{value}" /></inbound></policies>""";
        AssertRoundTrip(xml);
    }

    [TestMethod]
    public void NamedValueTokenAfterInterpolationBrace_CompilesToHoleWithNamedValue()
    {
        // API Management substitutes {{n}} one brace in, so $"{{{n}}}" is a hole around the named value's code.
        var xml = """<policies><inbound><set-variable name="value" value="@($&quot;{{{n}}}&quot;)" /></inbound></policies>""";

        var csharp = s_decompiler.DecompileDocument(xml, "RoundTripPolicy", "RoundTripTest");
        var result = CompileCSharp(csharp);

        result.Errors.Should().BeEmpty("the decompiled C# should compile.\nGenerated C#:\n{0}", csharp);
        result.Document.Descendants("set-variable").Single().Attribute("value")!.Value.Should().Be("@($\"{({{n}})}\")");
    }

    [TestMethod]
    [DataRow("@(&quot;{{BaseUrl}}&quot;.Trim('/'))", DisplayName = "Literal followed by a member access")]
    [DataRow("@(@&quot;{{Root}}\\logs&quot;)", DisplayName = "Verbatim string")]
    [DataRow("@($&quot;https://{{Host}}/{context.Request.Url.Path}&quot;)", DisplayName = "Interpolated string")]
    [DataRow("@($@&quot;a{{x}}b&quot;)", DisplayName = "Verbatim interpolated string")]
    [DataRow("@($&quot;{{\\&quot;k\\&quot;: \\&quot;{{s}}\\&quot;}}&quot;)", DisplayName = "Escaped braces in a JSON template")]
    public void NamedValueTokenInStringLiteral_RoundTrips(string value)
    {
        var xml = $"""<policies><inbound><set-variable name="value" value="{value}" /></inbound></policies>""";
        AssertRoundTrip(xml);
    }

    [TestMethod]
    [DataRow("@{string s = {{k}}; return s.ToUpper();}")]
    [DataRow("@(new string[] { {{k}} }.Length)")]
    public void NamedValueTokenConvertedToString_RoundTrips(string value)
    {
        var xml = $"""<policies><inbound><set-variable name="value" value="{value}" /></inbound></policies>""";
        AssertRoundTripSemantic(xml);
    }

    [TestMethod]
    public void FullPolicySections_RoundTrips()
    {
        var xml = """
            <policies>
                <inbound>
                    <base />
                    <set-variable name="env" value="prod" />
                </inbound>
                <backend>
                    <base />
                </backend>
                <outbound>
                    <base />
                </outbound>
                <on-error>
                    <base />
                </on-error>
            </policies>
            """;
        AssertRoundTrip(xml);
    }

    [TestMethod]
    [DataRow("cache-lookup-store.xml")]
    [DataRow("return-response-gone.xml")]
    [DataRow("cors-auth-cert.xml")]
    [DataRow("rate-limit-cache-rewrite.xml")]
    [DataRow("managed-identity-fragment.xml")]
    [DataRow("cache-conditional-store.xml")]
    [DataRow("managed-identity-find-replace.xml")]
    [DataRow("llm-content-safety.xml")]
    [DataRow("validate-content.xml")]
    [DataRow("validate-client-certificate.xml")]
    [DataRow("validate-azure-ad-token.xml")]
    [DataRow("validate-headers.xml")]
    [DataRow("validate-parameters.xml")]
    [DataRow("validate-status-code.xml")]
    [DataRow("validate-odata-request.xml")]
    [DataRow("send-service-bus-message.xml")]
    [DataRow("invoke-dapr-binding.xml")]
    [DataRow("publish-to-dapr.xml")]
    [DataRow("azure-openai-emit-token-metric.xml")]
    [DataRow("llm-emit-token-metric.xml")]
    [DataRow("azure-openai-semantic-cache-lookup.xml")]
    [DataRow("llm-semantic-cache-lookup.xml")]
    [DataRow("azure-openai-semantic-cache-store.xml")]
    [DataRow("llm-semantic-cache-store.xml")]
    [DataRow("azure-openai-token-limit.xml")]
    [DataRow("llm-token-limit.xml")]
    [DataRow("cross-domain.xml")]
    [DataRow("proxy.xml")]
    [DataRow("quota.xml")]
    [DataRow("quota-by-key.xml")]
    [DataRow("trace.xml")]
    [DataRow("wait.xml")]
    [DataRow("cors.xml")]
    [DataRow("forward-request.xml")]
    [DataRow("get-authorization-context.xml")]
    [DataRow("ip-filter.xml")]
    [DataRow("json-to-xml.xml")]
    [DataRow("jsonp.xml")]
    [DataRow("limit-concurrency.xml")]
    [DataRow("log-to-eventhub.xml")]
    [DataRow("mock-response.xml")]
    [DataRow("redirect-content-urls.xml")]
    [DataRow("set-backend-service.xml")]
    [DataRow("set-method.xml")]
    [DataRow("set-query-parameter.xml")]
    [DataRow("xml-to-json.xml")]
    [DataRow("authentication-basic.xml")]
    [DataRow("cache-lookup-value.xml")]
    [DataRow("cache-store-value.xml")]
    [DataRow("cache-remove-value.xml")]
    [DataRow("cache-value.xml")]
    [DataRow("check-header.xml")]
    [DataRow("emit-metric.xml")]
    [DataRow("validate-jwt.xml")]
    public void RealPolicyFile_RoundTrips(string fileName)
    {
        var filePath = Path.Combine("TestData", fileName);
        var rawXml = File.ReadAllText(filePath);
        // Preprocess to escape C# expressions, making it valid XML
        var preprocessed = PolicyDecompiler.PreprocessXml(rawXml);
        // Strip XML comments (decompiler doesn't preserve them)
        var doc = XDocument.Parse(preprocessed);
        doc.DescendantNodes().OfType<XComment>().Remove();
        var xml = doc.ToString(SaveOptions.DisableFormatting);
        AssertRoundTripSemantic(xml);
    }

    /// <summary>
    /// Tests that complex real-world policy files can be decompiled and recompiled
    /// without crashes, even when expression methods reference types not available
    /// in the test compilation (e.g., JObject from Newtonsoft.Json).
    /// </summary>
    [TestMethod]
    [DataRow("rate-limit-by-key-retry.xml")]
    [DataRow("xsl-transform.xml")]
    public void ComplexPolicyFile_DecompilesAndCompiles(string fileName)
    {
        var filePath = Path.Combine("TestData", fileName);
        var rawXml = File.ReadAllText(filePath);
        var preprocessed = PolicyDecompiler.PreprocessXml(rawXml);
        var doc = XDocument.Parse(preprocessed);
        doc.DescendantNodes().OfType<XComment>().Remove();
        var xml = doc.ToString(SaveOptions.DisableFormatting);

        // Step 1: Decompile should succeed
        var csharp = s_decompiler.DecompileDocument(xml.Trim(), "RoundTripPolicy", "RoundTripTest");
        csharp.Should().NotBeNullOrEmpty("decompilation should produce C# code");

        // Step 2: The decompiled C# should parse without syntax errors
        var syntaxTree = CSharpSyntaxTree.ParseText(csharp);
        var syntaxErrors = syntaxTree.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        syntaxErrors.Should().BeEmpty(
            "the decompiled C# should have no syntax errors.\nGenerated C#:\n{0}", csharp);

        // Step 3: Compilation should produce a document (even with expression resolution warnings)
        var compilationResult = CompileCSharp(csharp);
        compilationResult.Document.Should().NotBeNull(
            "the compilation should produce a document.\nErrors: {0}\nGenerated C#:\n{1}",
            string.Join(", ", compilationResult.Errors.Select(e => e.ToString())),
            csharp);
    }

    /// <summary>
    /// Tests that policy files with named value tokens ({{name}}) used as code in
    /// expressions can be decompiled and recompiled. The decompiler replaces {{name}}
    /// tokens outside string literals with context.NamedValue("name") calls, producing
    /// valid C#. The compiler converts them back to {{name}} in XML.
    /// </summary>
    [TestMethod]
    [DataRow("named-value-backend-service.xml")]
    [DataRow("named-value-identity-mirror.xml")]
    public void PolicyWithNamedValues_Decompiles(string fileName)
    {
        var filePath = Path.Combine("TestData", fileName);
        var rawXml = File.ReadAllText(filePath);
        var preprocessed = PolicyDecompiler.PreprocessXml(rawXml);
        var doc = XDocument.Parse(preprocessed);
        doc.DescendantNodes().OfType<XComment>().Remove();
        var xml = doc.ToString(SaveOptions.DisableFormatting);

        var csharp = s_decompiler.DecompileDocument(xml.Trim(), "RoundTripPolicy", "RoundTripTest");
        csharp.Should().NotBeNullOrEmpty("decompilation should produce C# code");
        csharp.Should().Contain("class RoundTripPolicy", "decompiled code should contain the policy class");
        csharp.Should().Contain("context.NamedValue(", "named value tokens should be converted to NamedValue calls");
    }

    [TestMethod]
    [DataRow("""<outbound><validate-status-code unspecified-status-code-action="prevent" errors-variable-name="errors"><status-code code="200" action="ignore" /><status-code code="404" action="detect" /></validate-status-code></outbound>""",
        DisplayName = "validate-status-code with status codes")]
    [DataRow("""<inbound><validate-jwt header-name="Authorization"><openid-config url="https://login.example/.well-known/openid-configuration" validate-connectivity="false" /></validate-jwt></inbound>""",
        DisplayName = "validate-jwt openid-config validate-connectivity")]
    public void PolicyChildElements_RoundTrip(string sections)
    {
        AssertRoundTrip($"<policies>{sections}</policies>");
    }

    [TestMethod]
    [DataRow("""<inbound><validate-parameters specified-parameter-action="prevent" unspecified-parameter-action="ignore" errors-variable-name="errors"><headers specified-parameter-action="detect" unspecified-parameter-action="ignore"><parameter name="Authorization" action="prevent" /></headers><query specified-parameter-action="prevent" unspecified-parameter-action="detect"><parameter name="id" action="ignore" /><parameter name="filter" action="detect" /></query><path specified-parameter-action="prevent"><parameter name="orderId" action="detect" /></path></validate-parameters></inbound>""",
        DisplayName = "validate-parameters with headers, query and path")]
    [DataRow("""<inbound><validate-parameters specified-parameter-action="prevent" unspecified-parameter-action="ignore"><query specified-parameter-action="@(&quot;detect&quot;)" unspecified-parameter-action="{{action}}" /></validate-parameters></inbound>""",
        DisplayName = "validate-parameters group with expression and named value actions")]
    [DataRow("""<outbound><validate-headers specified-header-action="ignore" unspecified-header-action="prevent" errors-variable-name="errors"><header name="X-One" action="detect" /><header name="X-Two" action="prevent" /></validate-headers></outbound>""",
        DisplayName = "validate-headers with headers")]
    [DataRow("""<inbound><authentication-certificate body="@(context.Variables.GetValueOrDefault&lt;byte[]&gt;(&quot;cert&quot;))" password="secret" /></inbound>""",
        DisplayName = "authentication-certificate with body")]
    [DataRow("""<inbound><authentication-certificate body="not-an-expression" /></inbound>""",
        DisplayName = "authentication-certificate with literal body")]
    [DataRow("""<backend><set-backend-service base-url="{{scheme}}://{{host}}" /></backend>""",
        DisplayName = "Two named values in one value")]
    [DataRow("""<inbound><set-variable name="my var" value="@(1 + 1)" /><set-variable name="9 lives!" value="@(2 + 2)" /></inbound>""",
        DisplayName = "set-variable names that are not identifiers")]
    [DataRow("""<inbound><json-to-xml apply="always" namespace-separator="@(':')" /></inbound>""",
        DisplayName = "json-to-xml namespace-separator expression")]
    [DataRow("""<inbound><json-to-xml apply="always" namespace-separator="{{separator}}" /></inbound>""",
        DisplayName = "json-to-xml namespace-separator named value")]
    [DataRow("""<inbound><json-to-xml apply="always" namespace-separator=":" /></inbound>""",
        DisplayName = "json-to-xml namespace-separator char")]
    [DataRow("""<inbound><json-to-xml apply="always" namespace-separator="::" /></inbound>""",
        DisplayName = "json-to-xml namespace-separator longer than a char")]
    [DataRow("""<inbound><cors><allowed-origins><origin>*</origin></allowed-origins><allowed-headers><header>*</header></allowed-headers><allowed-methods preflight-result-max-age="@((uint)300)"><method>GET</method></allowed-methods></cors></inbound>""",
        DisplayName = "cors preflight-result-max-age expression")]
    [DataRow("""<inbound><cors><allowed-origins><origin>*</origin></allowed-origins><allowed-headers><header>*</header></allowed-headers><allowed-methods preflight-result-max-age="{{max-age}}"><method>GET</method></allowed-methods></cors></inbound>""",
        DisplayName = "cors preflight-result-max-age named value")]
    [DataRow("""<inbound><validate-content unspecified-content-type-action="prevent" max-size="1024" size-exceeded-action="detect"><content-type-map><type to="application/json" from="a/b" when="@(context.Request.Method == &quot;POST&quot;)" /><type to="application/json" from="c/d" when="{{map-enabled}}" /></content-type-map><content validate-as="json" action="detect" allow-additional-properties="{{allow}}" case-insensitive-property-names="true" /></validate-content></inbound>""",
        DisplayName = "validate-content bool attributes as expression and named value")]
    [DataRow("""<inbound><set-header name="X-Mode" exists-action="@(context.Request.Method == &quot;GET&quot; ? &quot;override&quot; : &quot;skip&quot;)"><value>v</value></set-header></inbound>""",
        DisplayName = "set-header with expression exists-action")]
    [DataRow("""<inbound><set-header name="X-Empty" exists-action="override" /></inbound>""",
        DisplayName = "set-header override without values")]
    [DataRow("""<inbound><set-query-parameter name="mode" exists-action="{{action}}"><value>v</value></set-query-parameter></inbound>""",
        DisplayName = "set-query-parameter with named value exists-action")]
    [DataRow("""<inbound><set-query-parameter name="empty" exists-action="skip" /></inbound>""",
        DisplayName = "set-query-parameter skip without values")]
    [DataRow("""<inbound><set-header name="X-Trace"><value>@(context.Request.IpAddress)-@(context.Request.Method)</value></set-header></inbound>""",
        DisplayName = "Two expressions in one element value")]
    [DataRow("""<inbound><set-variable name="pair" value="@(context.Request.Method) and @{return context.Request.IpAddress;}" /></inbound>""",
        DisplayName = "Two expressions in one attribute value")]
    [DataRow("""<inbound><choose id="route"><when condition="@(1 &gt; 0)"><set-variable name="matched" value="true" /></when><otherwise><set-variable name="matched" value="false" /></otherwise></choose></inbound>""",
        DisplayName = "choose with id")]
    [DataRow("""<inbound><choose><when condition="true"><set-variable name="matched" value="true" /></when></choose></inbound>""",
        DisplayName = "choose with condition true")]
    [DataRow("""<inbound><choose><when condition="false"><set-variable name="matched" value="true" /></when><otherwise><base /></otherwise></choose></inbound>""",
        DisplayName = "choose with condition false")]
    [DataRow("""<inbound><choose><when condition="{{flag}}"><set-variable name="matched" value="true" /></when><when condition="{{a}}{{b}}"><set-variable name="matched" value="maybe" /></when></choose></inbound>""",
        DisplayName = "choose with named value conditions")]
    [DataRow("""<inbound><unknown-policy check="@(context.Request.Headers.GetValueOrDefault(&quot;a&quot;, &quot;&quot;).Length &gt; 1 &amp;&amp; 1 &lt; 2)" note="a &amp; b"><item>@(context.Variables.ContainsKey(&quot;x&quot;) &amp;&amp; 1 &lt; 2)</item></unknown-policy></inbound>""",
        DisplayName = "Inline policy with characters XML escapes in expressions")]
    [DataRow("""<inbound><choose><when condition="true" /></choose></inbound>""",
        DisplayName = "choose with an empty when")]
    [DataRow("""<inbound><choose><when condition="@(1 &gt; 0)"><base /></when><otherwise /></choose></inbound>""",
        DisplayName = "choose with an empty otherwise")]
    [DataRow("""<inbound><send-request mode="new" response-variable-name="r"><set-url>https://example.org</set-url><set-method>GET</set-method><authentication-certificate body="AAECAw==" password="p" /></send-request></inbound>""",
        DisplayName = "send-request with a literal certificate body")]
    [DataRow("""<inbound><llm-semantic-cache-lookup score-threshold="{{threshold}}" embeddings-backend-id="embeddings" embeddings-backend-auth="system-assigned" /></inbound>""",
        DisplayName = "semantic cache lookup score threshold from a named value")]
    [DataRow("""<inbound><send-request response-variable-name="r" mode="new"><set-url>https://example.org</set-url><set-method>GET</set-method><authentication-certificate body="{{certificate}}" /></send-request></inbound>""",
        DisplayName = "send-request with a certificate body from a named value")]
    public void ConformancePolicy_RoundTrips(string sections)
    {
        var xml = $"<policies>{sections}</policies>";
        AssertRoundTrip(xml);
        AssertValidCSharpSyntax(s_decompiler.DecompileDocument(xml, "RoundTripPolicy", "RoundTripTest"));
    }

    [TestMethod]
    [DataRow("""<send-request response-variable-name="r" mode="new"><set-url>https://api.example.com</set-url><set-method>GET</set-method><authentication-certificate thumbprint="ABCDEF" password="{{cert-password}}" /></send-request>""",
        DisplayName = "send-request certificate password")]
    [DataRow("""<send-request response-variable-name="r" mode="new"><set-url>https://api.example.com</set-url><set-method>GET</set-method><authentication-certificate body="@(context.Variables.GetValueOrDefault&lt;byte[]&gt;(&quot;cert&quot;))" password="secret" /></send-request>""",
        DisplayName = "send-request certificate body")]
    [DataRow("""<send-request response-variable-name="r" mode="new"><set-url>https://api.example.com</set-url><set-method>GET</set-method><authentication-managed-identity resource="https://vault.azure.net" output-token-variable-name="token" ignore-error="true" /></send-request>""",
        DisplayName = "send-request managed identity token variable and ignore-error")]
    [DataRow("""<send-one-way-request mode="new"><set-url>https://api.example.com</set-url><set-method>POST</set-method><authentication-managed-identity resource="https://vault.azure.net" client-id="abc" output-token-variable-name="token" ignore-error="false" /></send-one-way-request>""",
        DisplayName = "send-one-way-request managed identity token variable and ignore-error")]
    public void NestedAuthentication_RoundTrips(string policy)
    {
        AssertRoundTripSemantic($"<policies><inbound>{policy}</inbound></policies>");
    }

    [TestMethod]
    public void ChooseWithNamedValueCondition_DecompilesToIfStatement()
    {
        var xml = """<policies><inbound><choose><when condition="{{flag}}"><base /></when></choose></inbound></policies>""";

        var csharp = s_decompiler.DecompileDocument(xml, "RoundTripPolicy", "RoundTripTest");

        csharp.Should().Contain("if (NamedValue_Flag0(context.ExpressionContext))");
        csharp.Should().NotContain("InlinePolicy");
    }

    [TestMethod]
    public void ChooseWithIdAndConstantCondition_DecompilesToIfStatement()
    {
        var xml = """<policies><inbound><choose id="route"><when condition="true"><base /></when><when condition="false"><base /></when></choose></inbound></policies>""";

        var csharp = s_decompiler.DecompileDocument(xml, "RoundTripPolicy", "RoundTripTest");

        csharp.Should().Contain("context.WithId(\"route\");");
        csharp.Should().Contain("if (true)");
        csharp.Should().Contain("else if (false)");
        csharp.Should().NotContain("InlinePolicy");
    }

    [TestMethod]
    public void ChooseWithPaddedConstantCondition_DecompilesToIfStatement()
    {
        var xml = """<policies><inbound><choose><when condition=" true "><base /></when></choose></inbound></policies>""";

        var csharp = s_decompiler.DecompileDocument(xml, "RoundTripPolicy", "RoundTripTest");

        csharp.Should().Contain("if (true)");
        csharp.Should().NotContain("InlinePolicy");
    }

    [TestMethod]
    [DataRow("""<choose><when condition="True"><base /></when></choose>""")]
    [DataRow("""<choose><when><base /></when></choose>""")]
    [DataRow("""<choose><otherwise><base /></otherwise></choose>""")]
    public void ChooseTheGatewayRejects_IsKeptAsWritten(string choose)
    {
        var csharp = s_decompiler.DecompileDocument(
            $"<policies><inbound>{choose}</inbound></policies>", "RoundTripPolicy", "RoundTripTest");

        csharp.Should().Contain("InlinePolicy");
    }

    [TestMethod]
    public void SetBodyWithMarkup_KeepsInnerXml()
    {
        var xml = """
            <policies>
                <inbound>
                    <set-body template="liquid">
                        <Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
                            <s:Body note="a &amp; b"><a>{{body.x}}</a></s:Body>
                        </Envelope>
                    </set-body>
                </inbound>
            </policies>
            """;
        var body = XDocument.Parse(xml, LoadOptions.PreserveWhitespace).Descendants("set-body").Single();
        var expected = string.Concat(body.Nodes().Select(n => n.ToString(SaveOptions.DisableFormatting)))
            .ReplaceLineEndings("\n");

        var csharp = s_decompiler.DecompileDocument(xml, "RoundTripPolicy", "RoundTripTest");
        var result = CompileCSharp(csharp);

        AssertValidCSharpSyntax(csharp);
        result.Errors.Should().BeEmpty("the decompiled C# should compile.\nGenerated C#:\n{0}", csharp);
        var compiled = result.Document.Descendants("set-body").Single();
        compiled.Attribute("template")!.Value.Should().Be("liquid");
        compiled.Elements().Should().ContainSingle(child => child.Name.LocalName == "Envelope");
        string.Concat(compiled.Nodes().Select(n => n.ToString(SaveOptions.DisableFormatting)))
            .ReplaceLineEndings("\n").Should().Be(expected, "Generated C#:\n{0}", csharp);
    }

    [TestMethod]
    public void MultiLineAttributeExpression_KeepsCodeAfterLineComment()
    {
        var xml = """
            <policies>
                <inbound>
                    <set-variable name="total" value="@{
                        // a comment on the first line
                        var a = 1;
                        return a + 41;
                    }" />
                    <choose>
                        <when condition="@{
                            // another comment
                            return context.Request.Method == &quot;GET&quot;;
                        }">
                            <base />
                        </when>
                    </choose>
                </inbound>
            </policies>
            """;

        var csharp = s_decompiler.DecompileDocument(xml, "RoundTripPolicy", "RoundTripTest");
        var result = CompileCSharp(csharp);

        AssertValidCSharpSyntax(csharp);
        result.Errors.Should().BeEmpty("the decompiled C# should compile.\nGenerated C#:\n{0}", csharp);
        var value = result.Document.Descendants("set-variable").Single().Attribute("value")!.Value;
        value.Should().Contain("var a = 1;").And.Contain("return a + 41;");
        var condition = result.Document.Descendants("when").Single().Attribute("condition")!.Value;
        condition.Should().Contain("context.Request.Method == \"GET\"");
    }

    [TestMethod]
    public void TypesNamedInExpressions_GetTheirUsingDirectives()
    {
        var xml = """<policies><inbound><set-variable name="a" value="@(context.Request.Body.As&lt;JObject&gt;())" /><set-variable name="b" value="@(Regex.IsMatch(context.Request.Method, &quot;GET&quot;))" /><set-variable name="c" value="@(Encoding.UTF8.GetBytes(&quot;x&quot;).Length)" /></inbound></policies>""";

        var csharp = s_decompiler.DecompileDocument(xml, "RoundTripPolicy", "RoundTripTest");

        csharp.Should().Contain("using Newtonsoft.Json.Linq;")
            .And.Contain("using System.Text.RegularExpressions;")
            .And.Contain("using System.Text;")
            .And.NotContain("using System.Xml.Linq;");
        AssertRoundTrip(xml);
    }

    [TestMethod]
    public void QueryExpression_GetsTheLinqUsingDirective()
    {
        var xml = """<policies><inbound><set-variable name="a" value="@(string.Join(&quot;,&quot;, from h in context.Request.Headers select h.Key))" /></inbound></policies>""";

        var csharp = s_decompiler.DecompileDocument(xml, "RoundTripPolicy", "RoundTripTest");

        csharp.Should().Contain("using System.Linq;");
        CompileCSharp(csharp).Errors.Should().BeEmpty("the decompiled C# should compile.\nGenerated C#:\n{0}", csharp);
    }

    [TestMethod]
    public void DocumentWithoutSuchTypes_GetsNoExtraUsingDirectives()
    {
        var csharp = s_decompiler.DecompileDocument(
            """<policies><inbound><set-header name="X"><value>@(context.Request.Method)</value></set-header></inbound></policies>""",
            "RoundTripPolicy", "RoundTripTest");

        csharp.Should().StartWith("using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;");
    }

    [TestMethod]
    public void BaseInFragment_IsDecompiledAsWrittenSoCompilingReportsIt()
    {
        // the gateway saves <base /> in a fragment, but an API that includes the fragment fails when called
        var csharp = s_decompiler.DecompileFragment(
            "<fragment><base /><set-header name=\"X\"><value>1</value></set-header></fragment>",
            "fragment-id", "RoundTripFragment", "RoundTripTest");

        csharp.Should().Contain("context.Base();").And.NotContain("InlinePolicy");
        CompileCSharp(csharp).Errors.Should().ContainSingle(error => error.Id == "APIM2031");
    }

    [TestMethod]
    public void InlinePolicyFallback_EmitsRawExpressions()
    {
        var xml = """<policies><inbound><unknown-policy check="@(context.Variables.ContainsKey(&quot;x&quot;) &amp;&amp; 1 &lt; 2)" note="a &amp; &quot;b&quot;" /></inbound></policies>""";

        var csharp = s_decompiler.DecompileDocument(xml, "RoundTripPolicy", "RoundTripTest");

        csharp.Should().Contain(
            """context.InlinePolicy(@"<unknown-policy check=""@(context.Variables.ContainsKey(""x"") && 1 < 2)"" note=""a &amp; &quot;b&quot;"" />");""");
    }

    [TestMethod]
    [DataRow("@(a)", true)]
    [DataRow("@{return a;}", true)]
    [DataRow("@(a)-@(b)", false)]
    [DataRow("@(a) @{return b;}", false)]
    [DataRow("@(\")\" + a)", true)]
    [DataRow("@(')' + a)", true)]
    [DataRow("@(a /* ) */ + b)", true)]
    [DataRow("@(a", false)]
    [DataRow("text @(a)", false)]
    public void IsExpression_RequiresOneBalancedExpression(string value, bool expected)
    {
        new PolicyDecompilerContext().IsExpression(value).Should().Be(expected);
    }

    private static void AssertValidCSharpSyntax(string csharp)
    {
        CSharpSyntaxTree.ParseText(csharp).GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty("the decompiled C# should be syntactically valid.\nGenerated C#:\n{0}", csharp);
    }

    private static void AssertRoundTrip(string originalXml)
    {
        // Step 1: Normalize the original XML through the same serialization pipeline
        var normalizedOriginal = NormalizeXml(originalXml);

        // Step 2: Decompile XML → C#
        var csharp = s_decompiler.DecompileDocument(originalXml.Trim(), "RoundTripPolicy", "RoundTripTest");

        // Step 3: Compile C# → XML
        var compilationResult = CompileCSharp(csharp);
        compilationResult.Errors.Should().BeEmpty(
            "the decompiled C# should compile without errors.\nGenerated C#:\n{0}", csharp);
        compilationResult.Document.Should().NotBeNull(
            "the compilation should produce a document.\nGenerated C#:\n{0}", csharp);

        // Step 4: Serialize the compiled XElement through the same pipeline
        var compiledXml = SerializeXElement(compilationResult.Document);

        // Step 5: Compare normalized XMLs
        compiledXml.Should().Be(normalizedOriginal,
            "the round-tripped XML should match the original.\nGenerated C#:\n{0}", csharp);
    }

    /// <summary>
    /// Semantic round-trip assertion for real-world policy files.
    /// Normalizes attribute ordering and C# expression whitespace before comparing,
    /// since the compiler may emit attributes in a different order and the C# formatter
    /// normalizes whitespace in expressions.
    /// </summary>
    private static void AssertRoundTripSemantic(string originalXml)
    {
        // Step 1: Decompile XML → C#
        var csharp = s_decompiler.DecompileDocument(originalXml.Trim(), "RoundTripPolicy", "RoundTripTest");

        // Step 2: Compile C# → XML
        var compilationResult = CompileCSharp(csharp);
        compilationResult.Errors.Should().BeEmpty(
            "the decompiled C# should compile without errors.\nGenerated C#:\n{0}", csharp);
        compilationResult.Document.Should().NotBeNull(
            "the compilation should produce a document.\nGenerated C#:\n{0}", csharp);

        // Step 3: Normalize both XMLs for semantic comparison
        var originalDoc = XDocument.Parse(originalXml.Trim());
        var compiledDoc = new XDocument(compilationResult.Document);
        NormalizeForSemanticComparison(originalDoc.Root!);
        NormalizeForSemanticComparison(compiledDoc.Root!);

        var normalizedOriginal = NormalizeSerializedExpressions(SerializeXElement(originalDoc.Root!));
        var normalizedCompiled = NormalizeSerializedExpressions(SerializeXElement(compiledDoc.Root!));

        // Step 4: Compare
        if (normalizedCompiled != normalizedOriginal)
        {
            var origLines = normalizedOriginal.Split(Environment.NewLine);
            var compLines = normalizedCompiled.Split(Environment.NewLine);
            var diffSb = new StringBuilder();
            diffSb.AppendLine("Semantic diff (expected vs actual):");
            int maxLen = Math.Max(origLines.Length, compLines.Length);
            for (int i = 0; i < maxLen; i++)
            {
                var o = i < origLines.Length ? origLines[i] : "<EOF>";
                var c = i < compLines.Length ? compLines[i] : "<EOF>";
                if (o != c)
                    diffSb.AppendLine($"  Line {i + 1}:\n    Expected: {o}\n    Actual:   {c}");
            }
            diffSb.AppendLine($"\nGenerated C#:\n{csharp}");
            Assert.Fail(diffSb.ToString());
        }
    }

    /// <summary>
    /// Normalizes an XML tree for semantic comparison:
    /// - Sorts attributes alphabetically
    /// - Normalizes C# expression whitespace
    /// - Sorts unordered child elements (vary-by-*, allowed-*)
    /// </summary>
    private static void NormalizeForSemanticComparison(XElement element)
    {
        var apimDefaults = PolicyXmlComparer.ApimDefaults.Value;

        // Sort attributes alphabetically, filtering APIM defaults
        var attrs = element.Attributes()
            .Where(a => !(apimDefaults.TryGetValue(a.Name.LocalName, out var def) &&
                          string.Equals(a.Value.Trim(), def, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(a => a.Name.LocalName).ToList();
        element.RemoveAttributes();
        foreach (var attr in attrs)
        {
            var normalized = NormalizeExpressionWhitespace(attr.Value);
            // Normalize escaped quotes (\" vs " in XML attribute values)
            normalized = normalized.Replace("\\\"", "\"");
            element.SetAttributeValue(attr.Name, normalized);
        }

        // Normalize text content that contains expressions
        foreach (var textNode in element.Nodes().OfType<XText>().ToList())
        {
            var value = textNode.Value;
            if (value.Contains("@(") || value.Contains("@{") || value.Contains("@$"))
            {
                value = NormalizeExpressionWhitespace(value);
                value = value.Replace("\\\"", "\"");
                textNode.Value = value;
            }
        }

        // Recursively process child elements first
        foreach (var child in element.Elements().ToList())
        {
            NormalizeForSemanticComparison(child);
        }

        // Sort unordered child element groups (vary-by-*, allowed-*)
        SortChildElementGroups(element, "vary-by-header", "vary-by-query-parameter");
        SortChildElementGroups(element, "allowed-origins", "allowed-headers", "allowed-methods", "expose-headers");
    }

    /// <summary>
    /// Sorts groups of sibling elements with the given names relative to each other,
    /// preserving their position relative to other element types.
    /// </summary>
    private static void SortChildElementGroups(XElement parent, params string[] groupNames)
    {
        var groupSet = new HashSet<string>(groupNames);
        var children = parent.Elements().ToList();
        var groupElements = children.Where(c => groupSet.Contains(c.Name.LocalName)).ToList();
        if (groupElements.Count <= 1) return;

        var sorted = groupElements.OrderBy(e => e.Name.LocalName).ThenBy(e => e.ToString()).ToList();
        int sortedIndex = 0;
        for (int i = 0; i < children.Count && sortedIndex < sorted.Count; i++)
        {
            if (groupSet.Contains(children[i].Name.LocalName))
            {
                children[i].ReplaceWith(sorted[sortedIndex]);
                sortedIndex++;
            }
        }
    }

    /// <summary>
    /// Normalizes whitespace within C# expressions (@{...} and @(...)) while preserving
    /// whitespace in string literals.
    /// </summary>
    private static string NormalizeExpressionWhitespace(string value)
    {
        if (!value.Contains("@(") && !value.Contains("@{") && !value.Contains("@$"))
            return value;

        var result = new StringBuilder(value.Length);
        int i = 0;

        while (i < value.Length)
        {
            // Look for expression starts
            if (i < value.Length && value[i] == '@' && i + 1 < value.Length &&
                (value[i + 1] == '(' || value[i + 1] == '{'))
            {
                char open = value[i + 1];
                char close = open == '(' ? ')' : '}';
                result.Append('@');
                result.Append(open);
                i += 2;
                i = NormalizeExpressionBody(value, i, result, open, close);
                continue;
            }
            // Handle $@ and @$ prefixed strings that start expressions
            if (i < value.Length && value[i] == '@' && i + 1 < value.Length && value[i + 1] == '$' &&
                i + 2 < value.Length && value[i + 2] == '(')
            {
                result.Append("@$(");
                i += 3;
                i = NormalizeExpressionBody(value, i, result, '(', ')');
                continue;
            }

            result.Append(value[i]);
            i++;
        }

        return result.ToString();
    }

    private static string NormalizeSerializedExpressions(string xml) =>
        NormalizeExpressionWhitespace(xml).Replace("\\\"", "\"");

    private static int NormalizeExpressionBody(string value, int start, StringBuilder result,
        char open, char close)
    {
        int i = start;
        int depth = 1;
        bool inString = false;
        bool inVerbatim = false;
        bool inChar = false;

        // Skip leading whitespace in expression body
        while (i < value.Length && char.IsWhiteSpace(value[i]))
            i++;

        while (i < value.Length && depth > 0)
        {
            char c = value[i];

            // Track char literals
            if (inChar)
            {
                if (c == '\\' && i + 1 < value.Length)
                {
                    result.Append(c);
                    result.Append(value[i + 1]);
                    i += 2;
                    continue;
                }
                if (c == '\'') inChar = false;
                result.Append(c);
                i++;
                continue;
            }

            // Track string literals (preserve whitespace inside strings)
            if (inString)
            {
                if (inVerbatim)
                {
                    if (c == '"')
                    {
                        if (i + 1 < value.Length && value[i + 1] == '"')
                        {
                            result.Append("\"\"");
                            i += 2;
                            continue;
                        }
                        inString = false;
                        inVerbatim = false;
                    }
                }
                else
                {
                    if (c == '\\' && i + 1 < value.Length)
                    {
                        result.Append(c);
                        result.Append(value[i + 1]);
                        i += 2;
                        continue;
                    }
                    if (c == '"')
                    {
                        inString = false;
                    }
                }
                result.Append(c);
                i++;
                continue;
            }

            // Not in string/char - strip all whitespace (normalization)
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            // Strip single-line comments (// to end of line)
            if (c == '/' && i + 1 < value.Length && value[i + 1] == '/')
            {
                i += 2;
                while (i < value.Length && value[i] != '\n' && value[i] != '\r')
                    i++;
                continue;
            }
            // Strip multi-line comments (/* ... */)
            if (c == '/' && i + 1 < value.Length && value[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < value.Length && !(value[i] == '*' && value[i + 1] == '/'))
                    i++;
                if (i + 1 < value.Length) i += 2;
                continue;
            }

            // String start detection
            if (c == '\'')
            {
                inChar = true;
                result.Append(c);
                i++;
                continue;
            }
            if (c == '@' && i + 1 < value.Length && value[i + 1] == '"')
            {
                inString = true;
                inVerbatim = true;
                result.Append("@\"");
                i += 2;
                continue;
            }
            if (c == '$' && i + 2 < value.Length && value[i + 1] == '@' && value[i + 2] == '"')
            {
                inString = true;
                inVerbatim = true;
                result.Append("$@\"");
                i += 3;
                continue;
            }
            if (c == '$' && i + 1 < value.Length && value[i + 1] == '"')
            {
                inString = true;
                result.Append("$\"");
                i += 2;
                continue;
            }
            if (c == '"')
            {
                inString = true;
                result.Append(c);
                i++;
                continue;
            }

            // Bracket depth tracking
            if (c == open) depth++;
            if (c == close) depth--;

            if (depth == 0)
            {
                // Strip trailing whitespace before closing bracket
                while (result.Length > 0 && result[result.Length - 1] == ' ')
                    result.Length--;
                result.Append(close);
                i++;
                break;
            }

            result.Append(c);
            i++;
        }

        return i;
    }

    private static IDocumentCompilationResult CompileCSharp(string csharpCode)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(csharpCode);
        var compilation = CSharpCompilation.Create(
            Guid.NewGuid().ToString(),
            syntaxTrees: [syntaxTree],
            references: References);
        var semantics = compilation.GetSemanticModel(syntaxTree);
        var policy = syntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.AttributeLists.ContainsAttributeOfType<DocumentAttribute>(semantics));

        try
        {
            return s_compiler.Compile(compilation, policy);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("SyntaxTree"))
        {
            // Roslyn may throw when expression methods reference types not in
            // the compilation's references (e.g., JObject from Newtonsoft.Json).
            // Re-throw with the generated C# code for diagnostics.
            throw new InvalidOperationException(
                $"Roslyn compilation failed - likely missing type references.\nGenerated C#:\n{csharpCode}", ex);
        }
    }

    private static string NormalizeXml(string xml)
    {
        var doc = XDocument.Parse(xml.Trim());
        return SerializeXElement(doc.Root!);
    }

    private static readonly XmlWriterSettings SerializeSettings = new()
    {
        OmitXmlDeclaration = true,
        ConformanceLevel = ConformanceLevel.Fragment,
        Indent = true,
        IndentChars = "    ",
        NewLineChars = Environment.NewLine,
    };

    private static string SerializeXElement(XElement element)
    {
        var sb = new StringBuilder();
        using (var writer = CustomXmlWriter.Create(sb, SerializeSettings))
        {
            writer.Write(element);
        }
        return sb.ToString();
    }
}
