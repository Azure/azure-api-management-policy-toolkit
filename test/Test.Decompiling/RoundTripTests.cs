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
    [DataRow("invoke-darp-binding.xml")]
    [DataRow("publish-to-darp.xml")]
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
