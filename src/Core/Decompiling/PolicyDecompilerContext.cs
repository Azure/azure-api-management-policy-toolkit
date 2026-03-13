// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling;

public record ExpressionMethodInfo(
    string Name,
    string ReturnType,
    string Body,
    bool IsMultiLine,
    string? NamedValueName = null,
    string? NamedValueTemplateLiteral = null);

public class PolicyDecompilerContext
{
    private readonly List<ExpressionMethodInfo> _expressionMethods = new();
    private int _expressionCounter;
    private readonly Dictionary<string, IPolicyDecompiler> _decompilers = new();
    private IPolicyDecompiler? _fallbackDecompiler;

    public IReadOnlyList<ExpressionMethodInfo> ExpressionMethods => _expressionMethods;

    public void RegisterDecompiler(IPolicyDecompiler decompiler)
    {
        _decompilers[decompiler.PolicyName] = decompiler;
    }

    public void RegisterFallback(IPolicyDecompiler decompiler)
    {
        _fallbackDecompiler = decompiler;
    }

    public void Reset()
    {
        _expressionMethods.Clear();
        _expressionCounter = 0;
    }

    #region Policy Dispatch

    public void EmitPolicies(CodeWriter writer, IEnumerable<XElement> elements, string contextVar)
    {
        foreach (var element in elements)
        {
            EmitPolicy(writer, element, contextVar);
        }
    }

    public void EmitPolicy(CodeWriter writer, XElement element, string contextVar)
    {
        var policyName = element.Name.LocalName;
        if (_decompilers.TryGetValue(policyName, out var decompiler))
        {
            decompiler.Decompile(writer, element, contextVar, this);
        }
        else if (_fallbackDecompiler != null)
        {
            _fallbackDecompiler.Decompile(writer, element, contextVar, this);
        }
    }

    #endregion

    #region Simple Call Helper

    public static void EmitSimpleCall(CodeWriter writer, XElement element, string contextVar, string methodName)
    {
        var prefix = GetContextPrefix(element, contextVar);
        writer.AppendLine($"{prefix}{methodName}();");
    }

    #endregion

    #region Expression Handling

    public bool IsExpression(string value) =>
        (value.StartsWith("@(") && value.EndsWith(")")) ||
        (value.StartsWith("@{") && value.EndsWith("}"));

    public string HandleValue(string value, string suggestedName, string returnType = "string")
    {
        if (IsExpression(value))
        {
            return CreateExpressionMethodReference(value, suggestedName, returnType);
        }
        if (IsNamedValueToken(value))
        {
            return NamedValueCall(value, returnType);
        }
        if (ContainsNamedValueToken(value))
        {
            return CreateNamedValueStringExpression(value, suggestedName);
        }
        return Literal(value);
    }

    public string HandleIntValue(string value, string suggestedName)
    {
        if (IsExpression(value))
        {
            return CreateExpressionMethodReference(value, suggestedName, "int");
        }
        if (IsNamedValueToken(value))
        {
            return NamedValueCall(value, "int");
        }
        if (TryParseIsoDuration(value, out var seconds))
        {
            return seconds.ToString();
        }
        return value;
    }

    public string HandleBoolValue(string value, string suggestedName)
    {
        if (IsExpression(value))
        {
            return CreateExpressionMethodReference(value, suggestedName, "bool");
        }
        if (IsNamedValueToken(value))
        {
            return NamedValueCall(value, "bool");
        }
        return value.ToLowerInvariant();
    }

    public string HandleUintValue(string value, string suggestedName)
    {
        if (IsExpression(value))
        {
            return CreateExpressionMethodReference(value, suggestedName, "uint");
        }
        if (IsNamedValueToken(value))
        {
            return NamedValueCall(value, "int");
        }
        return value;
    }

    public string HandleDoubleValue(string value, string suggestedName)
    {
        if (IsExpression(value))
        {
            return CreateExpressionMethodReference(value, suggestedName, "double");
        }
        if (IsNamedValueToken(value))
        {
            return NamedValueCall(value, "double");
        }
        return value;
    }

    public string HandleConditionExpression(string value, string suggestedName)
    {
        value = value.Trim();
        if (value.StartsWith("@(") && value.EndsWith(")"))
        {
            var body = value.Substring(2, value.Length - 3);
            return CreateExpressionMethod(body, suggestedName, "bool", false);
        }
        if (value.StartsWith("@{") && value.EndsWith("}"))
        {
            var body = value.Substring(2, value.Length - 3);
            return CreateExpressionMethod(body, suggestedName, "bool", true);
        }
        return value.ToLowerInvariant();
    }

    public string CreateExpressionMethodReference(string value, string suggestedName, string returnType)
    {
        if (value.StartsWith("@(") && value.EndsWith(")"))
        {
            var body = value.Substring(2, value.Length - 3);
            return CreateExpressionMethod(body, suggestedName, returnType, false);
        }
        if (value.StartsWith("@{") && value.EndsWith("}"))
        {
            var body = value.Substring(2, value.Length - 3);
            return CreateExpressionMethod(body, suggestedName, returnType, true);
        }
        return Literal(value);
    }

    public string CreateExpressionMethod(string body, string suggestedName, string returnType, bool isMultiLine)
    {
        body = ReplaceNamedValueTokens(body);
        var name = GenerateUniqueMethodName(suggestedName);
        _expressionMethods.Add(new ExpressionMethodInfo(name, returnType, body, isMultiLine));
        return $"{name}(context.ExpressionContext)";
    }

    public string GenerateUniqueMethodName(string suggestedName)
    {
        var name = $"{suggestedName}{_expressionCounter}";
        _expressionCounter++;
        return name;
    }

    public string CreateNamedValueStringExpression(string value, string suggestedName)
    {
        var parts = new List<string>();
        int pos = 0;
        foreach (Match match in NamedValueTokenPattern.Matches(value))
        {
            if (match.Index > pos)
            {
                parts.Add(Literal(value.Substring(pos, match.Index - pos)));
            }
            parts.Add($"context.NamedValue(\"{match.Groups[1].Value}\")");
            pos = match.Index + match.Length;
        }
        if (pos < value.Length)
        {
            parts.Add(Literal(value.Substring(pos)));
        }
        var body = string.Join(" + ", parts);
        var methodName = GenerateUniqueMethodName(suggestedName);
        _expressionMethods.Add(new ExpressionMethodInfo(
            methodName, "dynamic", body, IsMultiLine: false,
            NamedValueTemplateLiteral: value));
        return $"{methodName}(context.ExpressionContext)";
    }

    public string NamedValueCall(string token, string returnType = "dynamic")
    {
        var name = token.Substring(2, token.Length - 4);
        var methodName = GenerateUniqueMethodName($"NamedValue_{ToPascalCase(name)}");
        _expressionMethods.Add(new ExpressionMethodInfo(
            methodName, returnType,
            $"context.NamedValue(\"{name}\")",
            IsMultiLine: false,
            NamedValueName: name));
        return $"{methodName}(context.ExpressionContext)";
    }

    public static bool TryParseIsoDuration(string value, out long seconds)
    {
        seconds = 0;
        if (string.IsNullOrEmpty(value) || value[0] != 'P') return false;
        try
        {
            var ts = System.Xml.XmlConvert.ToTimeSpan(value);
            seconds = (long)ts.TotalSeconds;
            return true;
        }
        catch { return false; }
    }

    public static bool IsNamedValueToken(string value) =>
        NamedValueTokenPattern.IsMatch(value) && value.StartsWith("{{") && value.EndsWith("}}");

    public static bool ContainsNamedValueToken(string value) =>
        NamedValueTokenPattern.IsMatch(value);

    public static string ReplaceNamedValueTokens(string body)
    {
        if (!body.Contains("{{"))
            return body;

        var result = new StringBuilder(body.Length + 64);
        int i = 0;
        bool inString = false;
        bool inVerbatim = false;
        bool inInterpolated = false;
        bool inLineComment = false;
        bool inBlockComment = false;
        bool inChar = false;
        int interpolatedBraceDepth = 0;

        while (i < body.Length)
        {
            char c = body[i];

            if (inLineComment)
            {
                result.Append(c);
                if (c == '\n') inLineComment = false;
                i++; continue;
            }

            if (inBlockComment)
            {
                result.Append(c);
                if (c == '*' && i + 1 < body.Length && body[i + 1] == '/')
                {
                    result.Append(body[++i]);
                    inBlockComment = false;
                }
                i++; continue;
            }

            if (inChar)
            {
                result.Append(c);
                if (c == '\\' && i + 1 < body.Length) { result.Append(body[++i]); i++; continue; }
                if (c == '\'') inChar = false;
                i++; continue;
            }

            if (!inString)
            {
                if (c == '/' && i + 1 < body.Length)
                {
                    if (body[i + 1] == '/') { inLineComment = true; result.Append(c); result.Append(body[++i]); i++; continue; }
                    if (body[i + 1] == '*') { inBlockComment = true; result.Append(c); result.Append(body[++i]); i++; continue; }
                }

                if (c == '\'') { inChar = true; result.Append(c); i++; continue; }

                if (c == '"')
                {
                    inVerbatim = i > 0 && body[i - 1] == '@';
                    inInterpolated = (i > 0 && body[i - 1] == '$')
                        || (i > 1 && ((body[i - 1] == '@' && body[i - 2] == '$') || (body[i - 1] == '$' && body[i - 2] == '@')));
                    if (inInterpolated && !inVerbatim && i > 0 && body[i - 1] == '@' && i > 1 && body[i - 2] == '$')
                        inVerbatim = true;
                    inString = true;
                    interpolatedBraceDepth = 0;
                    result.Append(c); i++; continue;
                }

                if (c == '{' && i + 1 < body.Length && body[i + 1] == '{')
                {
                    var match = NamedValueTokenPattern.Match(body, i);
                    if (match.Success && match.Index == i && IsValidTokenName(match.Groups[1].Value))
                    {
                        result.Append($"context.NamedValue(\"{match.Groups[1].Value}\")");
                        i += match.Length; continue;
                    }
                }

                result.Append(c); i++;
            }
            else
            {
                if (!inVerbatim && c == '\\' && i + 1 < body.Length)
                {
                    result.Append(c); result.Append(body[++i]); i++; continue;
                }

                if (inInterpolated && !inVerbatim)
                {
                    if (c == '{' && (i + 1 >= body.Length || body[i + 1] != '{'))
                    {
                        interpolatedBraceDepth++;
                        result.Append(c); i++; continue;
                    }
                    if (c == '}' && (i + 1 >= body.Length || body[i + 1] != '}') && interpolatedBraceDepth > 0)
                    {
                        interpolatedBraceDepth--;
                        result.Append(c); i++; continue;
                    }
                }

                if (c == '"' && interpolatedBraceDepth == 0)
                {
                    if (inVerbatim && i + 1 < body.Length && body[i + 1] == '"')
                    {
                        result.Append("\"\""); i += 2; continue;
                    }
                    result.Append(c);
                    inString = false; inVerbatim = false; inInterpolated = false;
                    i++; continue;
                }

                if (c == '{' && i + 1 < body.Length && body[i + 1] == '{')
                {
                    var end = body.IndexOf("}}", i + 2, StringComparison.Ordinal);
                    if (end > 0)
                    {
                        var name = body.Substring(i + 2, end - i - 2).Trim();
                        if (IsValidTokenName(name))
                        {
                            var strPrefix = inInterpolated ? "$" : "";
                            var verbPrefix = inVerbatim ? "@" : "";
                            result.Append($"\" + context.NamedValue(\"{name}\") + {strPrefix}{verbPrefix}\"");
                            i = end + 2; continue;
                        }
                    }
                }

                result.Append(c); i++;
            }
        }

        return result.ToString();
    }

    public static bool IsValidTokenName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (name.Contains('\\') || name.Contains('{') || name.Contains('}') || name.Contains(':')) return false;
        return name.Any(char.IsLetter);
    }

    public static readonly Regex NamedValueTokenPattern = new(
        @"\{\{([A-Za-z][A-Za-z0-9_.\-]*)\}\}",
        RegexOptions.Compiled);

    #endregion

    #region Config Emission Helpers

    public static void EmitConfigCall(
        CodeWriter writer, string prefix, string methodName, string configTypeName, List<string> properties)
    {
        if (properties.Count == 0)
        {
            writer.AppendLine($"{prefix}{methodName}(new {configTypeName}());");
            return;
        }

        if (properties.Count <= 2 && properties.All(p => p.Length < 60 && !p.Contains('\n')))
        {
            writer.AppendLine($"{prefix}{methodName}(new {configTypeName} {{ {string.Join(", ", properties)} }});");
            return;
        }

        writer.AppendLine($"{prefix}{methodName}(new {configTypeName}");
        writer.AppendLine("{");
        writer.IncreaseIndent();
        foreach (var prop in properties)
        {
            writer.AppendLine($"{prop},");
        }
        writer.DecreaseIndent();
        writer.AppendLine("});");
    }

    public void EmitConfigCallWithBlock(
        CodeWriter writer, string prefix, string methodName, string configTypeName,
        List<string> properties, XElement element, string contextVar)
    {
        writer.Append($"{prefix}{methodName}(new {configTypeName}");
        if (properties.Count <= 2 && properties.All(p => p.Length < 60 && !p.Contains('\n')))
        {
            writer.AppendRaw($" {{ {string.Join(", ", properties)} }}, () =>\n");
        }
        else
        {
            writer.AppendRaw("\n");
            writer.AppendLine("{");
            writer.IncreaseIndent();
            foreach (var prop in properties)
            {
                writer.AppendLine($"{prop},");
            }
            writer.DecreaseIndent();
            writer.AppendLine("}, () =>");
        }
        writer.AppendLine("{");
        writer.IncreaseIndent();
        EmitPolicies(writer, element.Elements(), contextVar);
        writer.DecreaseIndent();
        writer.AppendLine("});");
    }

    #endregion

    #region Property Addition Helpers

    public void AddRequiredStringProp(List<string> props, XElement element, string xmlAttr, string propName)
    {
        var value = element.Attribute(xmlAttr)?.Value ?? "";
        props.Add($"{propName} = {HandleValue(value, propName)}");
    }

    public void AddRequiredProp(List<string> props, XElement element, string xmlAttr, string propName, string returnType)
    {
        var value = element.Attribute(xmlAttr)?.Value ?? "";
        props.Add($"{propName} = {HandleValue(value, propName, returnType)}");
    }

    public void AddRequiredExprStringProp(List<string> props, XElement element, string xmlAttr, string propName)
    {
        var value = element.Attribute(xmlAttr)?.Value ?? "";
        props.Add($"{propName} = {HandleValue(value, propName)}");
    }

    public void AddOptionalStringProp(List<string> props, XElement element, string xmlAttr, string propName)
    {
        var value = element.Attribute(xmlAttr)?.Value;
        if (value != null)
        {
            props.Add($"{propName} = {HandleValue(value, propName)}");
        }
    }

    public void AddOptionalProp(List<string> props, XElement element, string xmlAttr, string propName, string returnType)
    {
        var value = element.Attribute(xmlAttr)?.Value;
        if (value != null)
        {
            props.Add($"{propName} = {HandleValue(value, propName, returnType)}");
        }
    }

    public void AddOptionalExprStringProp(List<string> props, XElement element, string xmlAttr, string propName)
    {
        var value = element.Attribute(xmlAttr)?.Value;
        if (value != null)
        {
            props.Add($"{propName} = {HandleValue(value, propName)}");
        }
    }

    public void AddRequiredIntProp(List<string> props, XElement element, string xmlAttr, string propName)
    {
        var value = element.Attribute(xmlAttr)?.Value ?? "0";
        props.Add($"{propName} = {HandleIntValue(value, propName)}");
    }

    public void AddOptionalIntProp(List<string> props, XElement element, string xmlAttr, string propName)
    {
        var value = element.Attribute(xmlAttr)?.Value;
        if (value != null)
        {
            props.Add($"{propName} = {HandleIntValue(value, propName)}");
        }
    }

    public void AddOptionalIntPropWithEvaluator(List<string> props, XElement element, string xmlAttr, string propName, string evaluatorPropName)
    {
        var value = element.Attribute(xmlAttr)?.Value;
        if (value != null)
        {
            var reference = HandleIntValue(value, propName);
            props.Add($"{propName} = {reference}");
            if (IsExpression(value))
            {
                props.Add($"{evaluatorPropName} = () => {reference}");
            }
        }
    }

    public void AddOptionalUIntProp(List<string> props, XElement element, string xmlAttr, string propName)
    {
        var value = element.Attribute(xmlAttr)?.Value;
        if (value != null)
        {
            props.Add($"{propName} = {HandleIntValue(value, propName)}");
        }
    }

    public void AddOptionalDoubleProp(List<string> props, XElement element, string xmlAttr, string propName)
    {
        var value = element.Attribute(xmlAttr)?.Value;
        if (value != null)
        {
            props.Add($"{propName} = {HandleDoubleValue(value, propName)}");
        }
    }

    public void AddRequiredBoolProp(List<string> props, XElement element, string xmlAttr, string propName)
    {
        var value = element.Attribute(xmlAttr)?.Value ?? "false";
        props.Add($"{propName} = {HandleBoolValue(value, propName)}");
    }

    public void AddOptionalBoolProp(List<string> props, XElement element, string xmlAttr, string propName)
    {
        var value = element.Attribute(xmlAttr)?.Value;
        if (value != null)
        {
            props.Add($"{propName} = {HandleBoolValue(value, propName)}");
        }
    }

    public void AddRequiredBoolExprProp(List<string> props, XElement element, string xmlAttr, string propName)
    {
        var value = element.Attribute(xmlAttr)?.Value ?? "false";
        props.Add($"{propName} = {HandleBoolValue(value, propName)}");
    }

    public void AddOptionalBoolExprProp(List<string> props, XElement element, string xmlAttr, string propName)
    {
        var value = element.Attribute(xmlAttr)?.Value;
        if (value != null)
        {
            props.Add($"{propName} = {HandleBoolValue(value, propName)}");
        }
    }

    #endregion

    #region Config Builder Helpers

    public string BuildHeaderConfigString(XElement headerElement)
    {
        var name = headerElement.Attribute("name")?.Value ?? "";
        var existsAction = headerElement.Attribute("exists-action")?.Value;
        var values = headerElement.Elements("value").Select(v => GetElementTextOrValue(v)).ToList();

        var parts = new List<string> { $"Name = {Literal(name)}" };
        if (existsAction != null)
        {
            parts.Add($"ExistsAction = {Literal(existsAction)}");
        }
        if (values.Count > 0)
        {
            parts.Add($"Values = new[] {{ {string.Join(", ", values.Select(v => HandleValue(v, "HeaderVal")))} }}");
        }
        return $"new HeaderConfig {{ {string.Join(", ", parts)} }}";
    }

    public string BuildInvokeRequestHeaderConfigString(XElement headerElement)
    {
        var name = headerElement.Attribute("name")?.Value ?? "";
        var value = headerElement.Attribute("value")?.Value ?? GetElementText(headerElement);

        var parts = new List<string> { $"Name = {Literal(name)}" };
        if (!string.IsNullOrEmpty(value))
        {
            parts.Add($"Values = new[] {{ {HandleValue(value, "HeaderVal")} }}");
        }

        return $"new HeaderConfig {{ {string.Join(", ", parts)} }}";
    }

    public string BuildBodyConfigProperty(XElement bodyElement)
    {
        var valueChild = bodyElement.Element("value");
        string content;
        if (valueChild != null)
            content = GetElementTextOrValue(valueChild);
        else
            content = GetElementText(bodyElement);
        var contentExpr = HandleValue(content, "BodyContent");

        var bodyProps = new List<string> { $"Content = {contentExpr}" };
        var template = bodyElement.Attribute("template")?.Value;
        if (template != null) bodyProps.Add($"Template = {Literal(template)}");
        var xsiNil = bodyElement.Attribute("xsi-nil")?.Value;
        if (xsiNil != null) bodyProps.Add($"XsiNil = {Literal(xsiNil)}");
        var parseDate = bodyElement.Attribute("parse-date")?.Value;
        if (parseDate != null) bodyProps.Add($"ParseDate = {parseDate.ToLowerInvariant()}");
        if (valueChild != null) bodyProps.Add("UseValueElement = true");

        return $"Body = new BodyConfig {{ {string.Join(", ", bodyProps)} }}";
    }

    #endregion

    #region String Utilities

    public static string GetContextPrefix(XElement element, string contextVar)
    {
        var id = element.Attribute("id")?.Value;
        if (id != null)
        {
            return $"{contextVar}.WithId({Literal(id)}).";
        }
        return $"{contextVar}.";
    }

    public static string GetElementText(XElement element)
    {
        if (element.Nodes().All(n => n is XText))
        {
            return string.Concat(element.Nodes().OfType<XText>().Select(t => t.Value));
        }
        return element.Value;
    }

    public static string GetElementTextOrValue(XElement element)
    {
        return element.Value;
    }

    public static string Literal(string value)
    {
        if (value.Contains('\n') || value.Contains('\r'))
        {
            return $"@\"{EscapeStringForVerbatim(value)}\"";
        }
        return $"\"{EscapeString(value)}\"";
    }

    public static string EscapeString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    public static string EscapeStringForVerbatim(string value)
    {
        return value.Replace("\"", "\"\"");
    }

    public static string EscapeChar(char c)
    {
        return c switch
        {
            '\'' => "\\'",
            '\\' => "\\\\",
            '\n' => "\\n",
            '\r' => "\\r",
            '\t' => "\\t",
            _ => c.ToString(),
        };
    }

    public static string ToPascalCase(string kebabOrSnakeCase)
    {
        if (string.IsNullOrEmpty(kebabOrSnakeCase)) return "Value";

        var parts = kebabOrSnakeCase.Split('-', '_', '.');
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            if (part.Length == 0) continue;
            sb.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1) sb.Append(part.Substring(1));
        }
        return sb.Length > 0 ? sb.ToString() : "Value";
    }

    public static string StripSingleLineComments(string code)
    {
        var result = new StringBuilder(code.Length);
        bool inString = false;
        bool inVerbatim = false;
        bool inChar = false;

        for (int i = 0; i < code.Length; i++)
        {
            char c = code[i];

            if (inChar)
            {
                result.Append(c);
                if (c == '\\' && i + 1 < code.Length) { result.Append(code[++i]); continue; }
                if (c == '\'') inChar = false;
                continue;
            }

            if (inString)
            {
                result.Append(c);
                if (inVerbatim)
                {
                    if (c == '"')
                    {
                        if (i + 1 < code.Length && code[i + 1] == '"') { result.Append(code[++i]); continue; }
                        inString = false; inVerbatim = false;
                    }
                }
                else
                {
                    if (c == '\\' && i + 1 < code.Length) { result.Append(code[++i]); continue; }
                    if (c == '"') inString = false;
                }
                continue;
            }

            if (c == '/' && i + 1 < code.Length && code[i + 1] == '/')
            {
                int end = code.IndexOf('\n', i);
                if (end < 0) break;
                i = end - 1;
                continue;
            }

            if (c == '\'') { inChar = true; result.Append(c); continue; }
            if (c == '@' && i + 1 < code.Length && code[i + 1] == '"')
            {
                inString = true; inVerbatim = true;
                result.Append(c); result.Append(code[++i]); continue;
            }
            if (c == '$' && i + 2 < code.Length && code[i + 1] == '@' && code[i + 2] == '"')
            {
                inString = true; inVerbatim = true;
                result.Append(c); result.Append(code[++i]); result.Append(code[++i]); continue;
            }
            if (c == '$' && i + 1 < code.Length && code[i + 1] == '"')
            {
                inString = true;
                result.Append(c); result.Append(code[++i]); continue;
            }
            if (c == '"') { inString = true; result.Append(c); continue; }

            result.Append(c);
        }

        return result.ToString();
    }

    #endregion
}
