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
            var formatlessCode = new TriviaRemoverRewriter().Visit(CSharpSyntaxTree.ParseText(cSharpCode).GetRoot())
                .NormalizeWhitespace("", "\n").ToString();
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
        return CSharpSyntaxTree.ParseText(code)
            .GetRoot()
            .NormalizeWhitespace(eol: Environment.NewLine)
            .ToFullString()
            .Trim();
    }
}