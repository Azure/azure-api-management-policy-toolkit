// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Serialization;

public static class RazorCodeFormatter
{
    private readonly static Regex CSharpCodeStart = new Regex("(@\\()|(@{)", RegexOptions.Compiled);

    // A named value token such as {{port}} can be used as code; it would parse as nested blocks.
    private readonly static Regex NamedValueToken = new Regex(@"\{\{[A-Za-z0-9_.\-]+\}\}", RegexOptions.Compiled);

    /// <summary>
    /// Reflows the C# inside policy expressions (<c>@(...)</c> and <c>@{...}</c>) directly
    /// on the document tree, operating on the raw (unescaped) expression text stored in
    /// attribute and leaf element values.
    /// <para>
    /// This is used for the <c>xml</c> output format, where expressions are XML-encoded
    /// during serialization and therefore cannot be reformatted from the serialized
    /// string (the entities would be reparsed as C# and corrupted). Formatting the tree
    /// before serialization lets the writer encode the already-formatted expressions.
    /// </para>
    /// </summary>
    public static void FormatExpressions(XElement element)
    {
        foreach (var node in element.DescendantsAndSelf())
        {
            foreach (var attribute in node.Attributes())
            {
                if (CSharpCodeStart.IsMatch(attribute.Value))
                {
                    attribute.Value = Format(attribute.Value);
                }
            }

            if (!node.HasElements && CSharpCodeStart.IsMatch(node.Value))
            {
                node.Value = Format(node.Value);
            }
        }
    }

    public static string Format(string code)
    {
        var result = new StringBuilder();
        var lastIndex = 0;
        foreach (Match match in CSharpCodeStart.Matches(code))
        {
            result.Append(code, lastIndex, match.Index - lastIndex + 2);
            var index = FindClosingIndex(code, match, out bool isMultiline);
            if (isMultiline) result.AppendLine();

            var cSharpCode = code.Substring(match.Index + 2, index - match.Index - 2).Trim();
            var formattedCode = FormatCSharpCode(cSharpCode);
            result.Append(formattedCode);

            if (isMultiline) result.AppendLine();

            lastIndex = index;
        }

        result.Append(code, lastIndex, code.Length - lastIndex);
        return result.ToString();
    }

    public static string ToCleanXml(string code, out IReadOnlyDictionary<string, string> markerToCode)
    {
        var expressions = new Dictionary<string, string>();
        var result = new StringBuilder();
        var lastIndex = 0;
        foreach (Match match in CSharpCodeStart.Matches(code))
        {
            result.Append(code, lastIndex, match.Index - lastIndex);
            var index = FindClosingIndex(code, match, out var isMultiline);
            var cSharpCode = code.Substring(match.Index + 2, index - match.Index - 2).Trim();
            var formatlessCode = WithNamedValuesProtected(cSharpCode, protectedCode =>
                new TriviaRemoverRewriter().Visit(CSharpSyntaxTree.ParseText(protectedCode).GetRoot())
                    .NormalizeWhitespace("", "\n").ToString());
            var marker = $"__expression__{Guid.NewGuid()}__";
            expressions.Add(marker, isMultiline ? $"@{{{formatlessCode}}}" : $"@({formatlessCode})");
            result.Append(marker);
            lastIndex = index + 1;
        }

        result.Append(code, lastIndex, code.Length - lastIndex);
        markerToCode = expressions;
        return result.ToString();
    }

    private static int FindClosingIndex(string code, Match match, out bool isMultiline)
    {
        var index = match.Index + 2;
        int open = 1;
        var openCharacter = code[match.Index + 1];
        isMultiline = openCharacter == '{';
        var closeCharacter = isMultiline ? '}' : ')';
        do
        {
            var character = code[index++];
            if (character == openCharacter) ++open;
            else if (character == closeCharacter) --open;
        } while (open > 0);

        return --index;
    }

    private static string FormatCSharpCode(string code)
    {
        return WithNamedValuesProtected(code, protectedCode => CSharpSyntaxTree.ParseText(protectedCode)
            .GetRoot()
            .NormalizeWhitespace(eol: Environment.NewLine)
            .ToFullString()
            .Trim());
    }

    // Replaces named value tokens with identifiers while the code is reformatted, then restores them.
    private static string WithNamedValuesProtected(string code, Func<string, string> format)
    {
        var tokens = new Dictionary<string, string>();
        var protectedCode = NamedValueToken.Replace(code, match =>
        {
            var placeholder = $"__apim_named_value_{tokens.Count}__";
            tokens.Add(placeholder, match.Value);
            return placeholder;
        });
        var formatted = format(protectedCode);
        foreach (var (placeholder, token) in tokens)
        {
            formatted = formatted.Replace(placeholder, token, StringComparison.Ordinal);
        }

        return formatted;
    }
}