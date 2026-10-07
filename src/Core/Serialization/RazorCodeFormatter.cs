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

    // The characters of a named value's name, as API Management allows them.
    internal const string NamedValueName = @"[A-Za-z0-9_.\-]+";

    // A named value token such as {{port}} can be used as code; it would parse as nested blocks.
    private readonly static Regex NamedValueToken = new Regex(@"\{\{" + NamedValueName + @"\}\}", RegexOptions.Compiled);

    /// <summary>
    /// Reflows the C# inside policy expressions (<c>@(...)</c> and <c>@{...}</c>) directly
    /// on the document tree, operating on the raw (unescaped) expression text stored in
    /// attribute values and text nodes.
    /// <para>
    /// Expressions which are XML-encoded during serialization cannot be reformatted from
    /// the serialized string (the entities would be reparsed as C# and corrupted). Formatting
    /// the tree before serialization lets the writer encode the already-formatted expressions.
    /// </para>
    /// </summary>
    public static void FormatExpressions(XElement element)
    {
        // The markup of a liquid set-body is template text: an @(...) in it isn't a policy expression.
        var templates = element.DescendantsAndSelf().Where(RawXmlContent.IsMarkupBody).ToList();
        foreach (var node in element.DescendantsAndSelf())
        {
            if (templates.Any(template => node == template || node.Ancestors().Contains(template)))
            {
                continue;
            }

            foreach (var attribute in node.Attributes())
            {
                if (CSharpCodeStart.IsMatch(attribute.Value))
                {
                    attribute.Value = Format(attribute.Value);
                }
            }

            // Text nodes are formatted one by one, so sibling elements and comments are kept.
            foreach (var text in node.Nodes().OfType<XText>())
            {
                if (CSharpCodeStart.IsMatch(text.Value))
                {
                    text.Value = Format(text.Value);
                }
            }
        }
    }

    public static string Format(string code)
    {
        return ReplaceExpressions(code, (cSharpCode, isMultiline) =>
        {
            var formattedCode = FormatCSharpCode(cSharpCode);
            return isMultiline
                ? $"@{{{Environment.NewLine}{formattedCode}{Environment.NewLine}}}"
                : $"@({formattedCode})";
        });
    }

    public static string ToCleanXml(string code, out IReadOnlyDictionary<string, string> markerToCode)
    {
        var expressions = new Dictionary<string, string>();
        var result = ReplaceExpressions(code, (cSharpCode, isMultiline) =>
        {
            var marker = $"__expression__{Guid.NewGuid()}__";
            expressions.Add(marker, NormalizeExpression(cSharpCode, isMultiline));
            return marker;
        });
        markerToCode = expressions;
        return result;
    }

    private static string NormalizeExpression(string cSharpCode, bool isMultiline)
    {
        var formatlessCode = WithNamedValuesProtected(cSharpCode, protectedCode =>
            new TriviaRemoverRewriter().Visit(CSharpSyntaxTree.ParseText(protectedCode).GetRoot())
                .NormalizeWhitespace("", "\n").ToFullString().Trim());
        return isMultiline ? $"@{{{formatlessCode}}}" : $"@({formatlessCode})";
    }

    // Replaces every expression, from its @ to its closing bracket, with the text returned by the callback.
    // The callback gets the trimmed C# code and whether the expression is a multi-statement one.
    // An expression without a closing bracket is left as it is.
    private static string ReplaceExpressions(string code, Func<string, bool, string> replace)
    {
        var result = new StringBuilder();
        var lastIndex = 0;
        var match = CSharpCodeStart.Match(code);
        while (match.Success)
        {
            if (!TryFindClosingIndex(code, match, out var index, out var isMultiline))
            {
                match = CSharpCodeStart.Match(code, match.Index + 2);
                continue;
            }

            result.Append(code, lastIndex, match.Index - lastIndex);
            var cSharpCode = code.Substring(match.Index + 2, index - match.Index - 2).Trim();
            result.Append(replace(cSharpCode, isMultiline));
            lastIndex = index + 1;
            match = CSharpCodeStart.Match(code, lastIndex);
        }

        result.Append(code, lastIndex, code.Length - lastIndex);
        return result.ToString();
    }

    // The code is read with the C# lexer, so brackets in string literals, character literals
    // and comments are not counted.
    private static bool TryFindClosingIndex(string code, Match match, out int index, out bool isMultiline)
    {
        isMultiline = code[match.Index + 1] == '{';
        var openKind = isMultiline ? SyntaxKind.OpenBraceToken : SyntaxKind.OpenParenToken;
        var closeKind = isMultiline ? SyntaxKind.CloseBraceToken : SyntaxKind.CloseParenToken;
        var start = match.Index + 2;
        var open = 1;
        foreach (var token in SyntaxFactory.ParseTokens(code, start, start))
        {
            if (token.IsKind(openKind))
            {
                ++open;
            }
            else if (token.IsKind(closeKind) && --open == 0)
            {
                index = token.SpanStart;
                return true;
            }
        }

        index = -1;
        return false;
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
    internal static string WithNamedValuesProtected(string code, Func<string, string> format)
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
