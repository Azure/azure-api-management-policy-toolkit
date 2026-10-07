// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Serialization;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling;

public class PolicyDecompiler
{
    private readonly PolicyDecompilerContext _context = new();

    public PolicyDecompiler()
    {
        var decompilers = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(type =>
                type is
                {
                    IsClass: true,
                    IsAbstract: false,
                    IsPublic: true,
                    Namespace: "Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling.Policy"
                }
                && type != typeof(InlinePolicyDecompiler)
                && typeof(IPolicyDecompiler).IsAssignableFrom(type))
            .Select(type => (IPolicyDecompiler)Activator.CreateInstance(type)!);

        foreach (var decompiler in decompilers)
        {
            _context.RegisterDecompiler(decompiler);
        }

        _context.RegisterFallback(new InlinePolicyDecompiler());
    }

    public string DecompileDocument(
        string xml,
        string className,
        string namespaceName,
        DecompileOptions? options = null)
    {
        _context.Reset();

        var doc = ParsePolicyXml(xml, options?.PolicyFormat ?? PolicyFormat.RawXml);
        var policies = doc.Root
            ?? throw new ArgumentException("Invalid XML: missing root element.");

        if (policies.Name.LocalName != "policies")
            throw new ArgumentException("Invalid policy document: root element must be <policies>.");

        var writer = new CodeWriter();

        EmitUsings(writer);
        writer.AppendLine($"namespace {namespaceName};");
        writer.AppendLine();
        EmitDocumentAttribute(writer, options);
        writer.AppendLine($"public class {className} : IDocument");
        writer.AppendLine("{");
        writer.IncreaseIndent();

        var sectionMap = new Dictionary<string, (string methodName, string contextType)>
        {
            ["inbound"] = ("Inbound", "IInboundContext"),
            ["backend"] = ("Backend", "IBackendContext"),
            ["outbound"] = ("Outbound", "IOutboundContext"),
            ["on-error"] = ("OnError", "IOnErrorContext"),
        };

        bool firstMethod = true;
        foreach (var child in policies.Elements())
        {
            if (sectionMap.TryGetValue(child.Name.LocalName, out var info))
            {
                if (!firstMethod) writer.AppendLine();
                EmitSection(writer, child, info.methodName, info.contextType);
                firstMethod = false;
            }
        }

        EmitExpressionMethods(writer);

        writer.DecreaseIndent();
        writer.AppendLine("}");

        return RequiredUsings.AddTo(writer.ToString(), _context.ExpressionMethods);
    }

    public string DecompileFragment(
        string xml,
        string fragmentId,
        string className,
        string namespaceName,
        DecompileOptions? options = null)
    {
        _context.Reset();

        var doc = ParsePolicyXml(xml, options?.PolicyFormat ?? PolicyFormat.RawXml);
        var fragment = doc.Root
            ?? throw new ArgumentException("Invalid XML: missing root element.");

        if (fragment.Name.LocalName != "fragment")
            throw new ArgumentException("Invalid policy fragment: root element must be <fragment>.");

        var writer = new CodeWriter();

        EmitUsings(writer);
        writer.AppendLine($"namespace {namespaceName};");
        writer.AppendLine();
        EmitFragmentAttribute(writer, fragmentId, options);
        writer.AppendLine($"public class {className} : IFragment");
        writer.AppendLine("{");
        writer.IncreaseIndent();

        EmitSection(writer, fragment, "Fragment", "IFragmentContext");
        EmitExpressionMethods(writer);

        writer.DecreaseIndent();
        writer.AppendLine("}");

        return RequiredUsings.AddTo(writer.ToString(), _context.ExpressionMethods);
    }

    #region Setup and Structure

    private static void EmitUsings(CodeWriter writer)
    {
        writer.AppendLine("using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;");
        writer.AppendLine("using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;");
        writer.AppendLine();
    }

    private static void EmitDocumentAttribute(CodeWriter writer, DecompileOptions? options)
    {
        var sb = new StringBuilder();
        sb.Append("[Document(");
        bool hasPositional = false;
        if (options?.DocumentId != null)
        {
            sb.Append(PolicyDecompilerContext.Literal(options.DocumentId));
            hasPositional = true;
        }
        if (options?.Scope != null)
        {
            if (hasPositional) sb.Append(", ");
            sb.Append($"Scope = DocumentScope.{options.Scope}");
        }
        sb.Append(")]");
        writer.AppendLine(sb.ToString());
    }

    private static void EmitFragmentAttribute(CodeWriter writer, string fragmentId, DecompileOptions? options)
    {
        var sb = new StringBuilder();
        sb.Append($"[Document({PolicyDecompilerContext.Literal(fragmentId)}, Type = DocumentType.Fragment");
        if (options?.Scope != null)
        {
            sb.Append($", Scope = DocumentScope.{options.Scope}");
        }
        sb.Append(")]");
        writer.AppendLine(sb.ToString());
    }

    private void EmitSection(CodeWriter writer, XElement section, string methodName, string contextType)
    {
        writer.AppendLine($"public void {methodName}({contextType} context)");
        writer.AppendLine("{");
        writer.IncreaseIndent();
        _context.EmitPolicies(writer, section.Elements(), "context");
        writer.DecreaseIndent();
        writer.AppendLine("}");
    }

    #endregion

    #region Expression Methods

    private void EmitExpressionMethods(CodeWriter writer)
    {
        foreach (var method in _context.ExpressionMethods)
        {
            writer.AppendLine();
            if (method.NamedValueName != null)
            {
                writer.AppendLine($"[NamedValue(\"{method.NamedValueName}\")]");
            }
            if (method.NamedValueTemplateLiteral != null)
            {
                writer.AppendLine($"[NamedValue({PolicyDecompilerContext.Literal(method.NamedValueTemplateLiteral)})]");
            }
            if (method.IsMultiLine)
            {
                writer.AppendLine("[Expression]");
                writer.AppendLine($"{method.ReturnType} {method.Name}(IExpressionContext context)");
                writer.AppendLine("{");
                writer.IncreaseIndent();
                EmitMultiLineBody(writer, method.Body);
                writer.DecreaseIndent();
                writer.AppendLine("}");
            }
            else
            {
                writer.AppendLine($"{method.ReturnType} {method.Name}(IExpressionContext context) => {method.Body};");
            }
        }
    }

    private static void EmitMultiLineBody(CodeWriter writer, string body)
    {
        var lines = body.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0])) lines.RemoveAt(0);
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1])) lines.RemoveAt(lines.Count - 1);

        if (lines.Count == 0) return;

        int minIndent = int.MaxValue;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            int indent = 0;
            foreach (char c in line)
            {
                if (c == ' ') indent++;
                else if (c == '\t') indent += 4;
                else break;
            }
            minIndent = Math.Min(minIndent, indent);
        }
        if (minIndent == int.MaxValue) minIndent = 0;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                writer.AppendLine();
                continue;
            }
            int toRemove = 0;
            int removed = 0;
            while (toRemove < line.Length && removed < minIndent)
            {
                if (line[toRemove] == ' ') { removed++; toRemove++; }
                else if (line[toRemove] == '\t') { removed += 4; toRemove++; }
                else break;
            }
            writer.AppendLine(line.Substring(toRemove));
        }
    }

    #endregion

    #region XML Preprocessing

    /// <summary>
    /// Parses policy XML into a document. Whitespace between elements is dropped, except inside a
    /// set-body that carries markup (e.g. a liquid template), where it is part of the body content.
    /// </summary>
    private static XDocument ParsePolicyXml(string xml, PolicyFormat format)
    {
        var doc = XDocument.Parse(PreprocessXml(xml, format), LoadOptions.PreserveWhitespace);
        var insignificant = doc.DescendantNodes()
            .OfType<XText>()
            .Where(text => text is not XCData && string.IsNullOrWhiteSpace(text.Value) && !IsInMarkupBody(text))
            .ToList();
        foreach (var text in insignificant)
        {
            text.Remove();
        }

        return doc;
    }

    // Whitespace in a set-body written as markup, liquid or not, is body content: a SOAP body is kept as written.
    private static bool IsInMarkupBody(XText text) =>
        text.Ancestors("set-body").FirstOrDefault() is { } body && !RawXmlContent.IsValueElementBody(body) &&
        (body.HasElements || RawXmlContent.IsMarkupBody(body));

    /// <summary>
    /// Preprocesses APIM policy XML to handle C# expressions that contain characters
    /// invalid in raw XML (unescaped quotes, angle brackets, ampersands inside @(...) and @{...}).
    /// Uses a placeholder approach matching ApimPolicyHarness: extracts expressions,
    /// replaces with safe placeholders, parses XML, restores expressions in the DOM,
    /// then serializes. The XML serializer encodes special characters in expressions,
    /// producing output that is parseable by XDocument.Parse() while preserving
    /// expression content verbatim in the parsed DOM.
    /// </summary>
    public static string PreprocessXml(string xml) => PreprocessXml(xml, PolicyFormat.RawXml);

    /// <summary>
    /// Preprocesses policy text of the given format. As rawxml every expression is taken out before the text
    /// is parsed and put back as it was written; as xml the text has to be well-formed and is parsed as it is.
    /// </summary>
    public static string PreprocessXml(string xml, PolicyFormat format)
    {
        if (!Enum.IsDefined(format))
        {
            throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown policy format.");
        }

        if (format == PolicyFormat.Xml)
        {
            XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            return PreserveAttributeLineBreaks(xml);
        }

        // Scan for all @(...) and @{...} expression spans
        var spans = CollectExpressionSpans(xml);
        if (spans.Count == 0) return PreserveAttributeLineBreaks(xml);

        // Replace expressions with XML-safe placeholders
        var exprMap = new Dictionary<string, string>(spans.Count, StringComparer.Ordinal);
        var sb = new StringBuilder(xml.Length + 64);
        int cursor = 0;
        int index = 0;

        foreach (var (start, len) in spans.OrderBy(s => s.start))
        {
            if (start < cursor) continue;

            sb.Append(xml, cursor, start - cursor);
            var original = xml.Substring(start, len);
            var placeholder = $"__APIM_EXPR_{index++}__";
            exprMap[placeholder] = original;
            sb.Append(placeholder);
            cursor = start + len;
        }

        if (cursor < xml.Length)
            sb.Append(xml, cursor, xml.Length - cursor);

        // Parse sanitized XML with placeholders
        var sanitized = sb.ToString();
        XDocument doc;
        try
        {
            doc = XDocument.Parse(sanitized, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException)
        {
            // Try with more lenient XmlTextReader
            try
            {
                using var sr = new StringReader(sanitized);
                using var xr = new XmlTextReader(sr)
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    WhitespaceHandling = WhitespaceHandling.Significant
                };
                doc = XDocument.Load(xr, LoadOptions.PreserveWhitespace);
            }
            catch (XmlException)
            {
                return sanitized;
            }
        }

        // Restore expression placeholders in the DOM tree.
        // When serialized, the XML writer encodes special chars (<, >, &, ")
        // in expressions, making the output valid and re-parseable XML.
        if (exprMap.Count > 0 && doc.Root != null)
        {
            RestorePlaceholders(doc.Root, exprMap, format);
        }

        return doc.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>
    /// XML parsing normalizes line breaks and tabs in attribute values to spaces, which would join the lines
    /// of a multi-line expression (so a line comment would swallow the code after it). When an attribute
    /// spans lines, the document is re-read without normalization and written back with the line breaks
    /// as character references, which survive parsing.
    /// </summary>
    private static string PreserveAttributeLineBreaks(string xml)
    {
        if (xml.IndexOfAny(['\n', '\r', '\t']) < 0)
        {
            return xml;
        }

        XDocument doc;
        try
        {
            using var sr = new StringReader(xml);
            using var xr = new XmlTextReader(sr)
            {
                DtdProcessing = DtdProcessing.Prohibit,
                Normalization = false,
                WhitespaceHandling = WhitespaceHandling.All
            };
            doc = XDocument.Load(xr, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException)
        {
            return xml;
        }

        var spansLines = doc.Descendants()
            .SelectMany(element => element.Attributes())
            .Any(attribute => attribute.Value.IndexOfAny(['\n', '\r', '\t']) >= 0);
        return spansLines ? doc.ToString(SaveOptions.DisableFormatting) : xml;
    }

    /// <summary>
    /// Walks the DOM tree and replaces placeholder tokens with original expression text
    /// in attribute values, text, CDATA sections and comments. An expression a value begins with was written as
    /// code and is kept as it was; one anywhere else was XML-escaped text and its entities are decoded.
    /// </summary>
    private static void RestorePlaceholders(XElement root, Dictionary<string, string> map, PolicyFormat format)
    {
        foreach (var element in root.DescendantsAndSelf())
        {
            foreach (var attribute in element.Attributes().ToList())
            {
                attribute.Value = Restore(attribute.Value, map, format, IsInLiquidMarkup(element));
            }
        }

        // Text is restored node by node, so text next to child elements is covered and a CDATA section or a
        // comment stays what it is. Their content isn't parsed, so an entity in it is not decoded.
        foreach (var node in root.DescendantNodes().ToList())
        {
            switch (node)
            {
                case XCData cdata:
                    cdata.Value = Restore(cdata.Value, map, format: null);
                    break;
                case XText text:
                    text.Value = Restore(text.Value, map, format, IsInLiquidMarkup(text.Parent));
                    break;
                case XComment comment:
                    comment.Value = Restore(comment.Value, map, format: null);
                    break;
            }
        }
    }

    private static readonly Regex Entity =
        new(@"&(" + RawXmlContent.Entities + @");", RegexOptions.Compiled);

    // The markup of a liquid body is template text: an @(...) in it is XML-escaped like the rest, in rawxml too.
    private static bool IsInLiquidMarkup(XElement? element) =>
        element is not null && element.AncestorsAndSelf().Any(RawXmlContent.IsMarkupBody);

    // Without a format the content is not parsed (a CDATA section, a comment) and nothing is decoded. A value
    // that begins with an expression was written as code and is kept; an @(...) further into a value, or
    // anywhere in the markup of a liquid body, is text like the rest of it, which is XML-escaped in rawxml too,
    // and is decoded.
    private static string Restore(string value, Dictionary<string, string> map, PolicyFormat? format, bool isText = false)
    {
        var start = value.TrimStart();
        var decode = format is not null &&
                     (isText || !map.Keys.Any(key => start.StartsWith(key, StringComparison.Ordinal)));
        foreach (var kvp in map)
        {
            if (value.Contains(kvp.Key, StringComparison.Ordinal))
            {
                value = value.Replace(
                    kvp.Key,
                    decode ? DecodeXmlEntities(kvp.Value) : kvp.Value,
                    StringComparison.Ordinal);
            }
        }

        return value;
    }

    /// <summary>
    /// Decodes XML character entities in expression text that was extracted from raw XML.
    /// Expressions extracted before XML parsing may contain entities like &amp;quot; that
    /// need to be decoded to produce valid C# code.
    /// </summary>
    private static string DecodeXmlEntities(string text)
    {
        if (!text.Contains('&'))
            return text;

        // one pass, so the text an entity stands for is not decoded again: &amp;lt; is &lt;
        return Entity.Replace(text, match => match.Groups[1].Value switch
        {
            "quot" => "\"",
            "apos" => "'",
            "lt" => "<",
            "gt" => ">",
            "amp" => "&",
            var reference => DecodeCharacterReference(reference) ?? match.Value
        });
    }

    // #65 or #x41; a number that is no character is left as it was written
    private static string? DecodeCharacterReference(string reference)
    {
        var isHex = reference[1] == 'x';
        if (!int.TryParse(
                reference.Substring(isHex ? 2 : 1),
                isHex ? NumberStyles.AllowHexSpecifier : NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var code) ||
            code is < 0 or > 0x10FFFF or (>= 0xD800 and <= 0xDFFF) ||
            // a character XML doesn't allow, such as a control character, can't be written back to the document
            code <= char.MaxValue && !XmlConvert.IsXmlChar((char)code))
        {
            return null;
        }

        return char.ConvertFromUtf32(code);
    }

    /// <summary>
    /// Scans the XML text for balanced @{...} and @(...) expression spans.
    /// Uses a state machine that correctly handles strings, comments, char literals,
    /// and nested braces within C# expressions.
    /// </summary>
    internal static List<(int start, int length)> CollectExpressionSpans(string text)
    {
        var spans = new List<(int, int)>();
        for (int i = 0; i < text.Length - 1; i++)
        {
            if (text[i] == '@')
            {
                char next = text[i + 1];
                if (next == '{' || next == '(')
                {
                    char open = next;
                    char close = open == '{' ? '}' : ')';
                    if (TryScanBalanced(text, i, open, close, out int length))
                    {
                        spans.Add((i, length));
                        i += length - 1;
                    }
                }
            }
        }
        return spans;
    }

    /// <summary>
    /// Scans a balanced bracket expression starting at @ position.
    /// Handles nested braces, string literals (regular, verbatim, interpolated),
    /// char literals, line comments, and block comments.
    /// </summary>
    internal static bool TryScanBalanced(string text, int startAt, char open, char close, out int length)
    {
        length = 0;
        int i = startAt;
        if (i + 1 >= text.Length || text[i] != '@' || text[i + 1] != open)
            return false;
        i += 2;

        int depth = 1;
        bool inString = false;
        bool inVerbatim = false;
        bool inChar = false;
        bool inLineComment = false;
        bool inBlockComment = false;
        bool escaped = false;

        while (i < text.Length)
        {
            char c = text[i];
            char next2 = i + 1 < text.Length ? text[i + 1] : '\0';

            // Inside line comment
            if (inLineComment)
            {
                if (c == '\n') inLineComment = false;
                i++;
                continue;
            }

            // Inside block comment
            if (inBlockComment)
            {
                if (c == '*' && next2 == '/') { inBlockComment = false; i += 2; continue; }
                i++;
                continue;
            }

            // Inside char literal
            if (inChar)
            {
                if (escaped) { escaped = false; i++; continue; }
                if (c == '\\') { escaped = true; i++; continue; }
                if (c == '\'') { inChar = false; }
                i++;
                continue;
            }

            // Inside string literal
            if (inString)
            {
                if (escaped) { escaped = false; i++; continue; }
                if (!inVerbatim && c == '\\') { escaped = true; i++; continue; }
                if (c == '"')
                {
                    if (inVerbatim && next2 == '"')
                    {
                        i += 2; // escaped quote in verbatim string
                        continue;
                    }
                    inString = false;
                    inVerbatim = false;
                }
                i++;
                continue;
            }

            // Normal code - detect comments, strings, brackets
            if (c == '/' && next2 == '/') { inLineComment = true; i += 2; continue; }
            if (c == '/' && next2 == '*') { inBlockComment = true; i += 2; continue; }
            if (c == '\'') { inChar = true; i++; continue; }

            // Verbatim string: @"
            if (c == '@' && next2 == '"') { inString = true; inVerbatim = true; i += 2; continue; }
            // Interpolated verbatim: $@" or @$"
            if (c == '$' && next2 == '@' && i + 2 < text.Length && text[i + 2] == '"')
                { inString = true; inVerbatim = true; i += 3; continue; }
            if (c == '@' && next2 == '$' && i + 2 < text.Length && text[i + 2] == '"')
                { inString = true; inVerbatim = true; i += 3; continue; }
            // Interpolated regular: $"
            if (c == '$' && next2 == '"') { inString = true; i += 2; continue; }
            // Regular string: "
            if (c == '"') { inString = true; i++; continue; }

            // Bracket matching
            if (c == open) { depth++; i++; continue; }
            if (c == close)
            {
                depth--;
                i++;
                if (depth == 0)
                {
                    length = i - startAt;
                    return true;
                }
                continue;
            }

            i++;
        }

        return false;
    }

    #endregion
}
