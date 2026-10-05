// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling;

/// <summary>
/// Policy expressions name .NET types without a namespace (JObject, Regex, Encoding), which API Management
/// resolves itself. The generated C# needs the using directives for the ones the expressions use.
/// </summary>
internal static class RequiredUsings
{
    private static readonly (string Namespace, string[] Names)[] Namespaces =
    [
        ("System",
        [
            "Convert", "Guid", "DateTime", "DateTimeOffset", "DateTimeKind", "TimeSpan", "TimeZoneInfo", "Math",
            "String", "StringComparison", "StringSplitOptions", "Uri", "UriKind", "UriPartial", "Random", "Enum",
            "Exception", "Array", "Tuple", "Func", "Action", "Nullable", "Int32", "Int64", "Boolean", "Double",
            "Decimal", "Byte", "Char", "Object", "DayOfWeek", "Base64FormattingOptions", "BitConverter", "Version"
        ]),
        ("System.Collections.Generic",
        [
            "List", "Dictionary", "HashSet", "KeyValuePair", "IEnumerable", "IDictionary", "IList", "ICollection",
            "IReadOnlyDictionary", "IReadOnlyList", "IReadOnlyCollection", "Queue", "Stack", "SortedDictionary",
            "LinkedList", "ISet"
        ]),
        ("System.Globalization", ["CultureInfo", "DateTimeStyles", "NumberStyles"]),
        ("System.IO", ["Stream", "MemoryStream", "StreamReader", "StreamWriter", "StringReader", "StringWriter"]),
        ("System.Linq",
        [
            "Enumerable", "Where", "Select", "SelectMany", "Any", "All", "First", "FirstOrDefault", "Last",
            "LastOrDefault", "Single", "SingleOrDefault", "Count", "Sum", "Min", "Max", "Average", "OrderBy",
            "OrderByDescending", "ThenBy", "GroupBy", "Distinct", "ToList", "ToArray", "ToDictionary", "Skip",
            "Take", "Contains", "Concat", "Union", "Intersect", "Except", "Aggregate", "Cast", "OfType", "Zip",
            "Reverse", "ElementAt", "DefaultIfEmpty", "SequenceEqual", "ToLookup",
            // a query expression: from x in y select z
            "from"
        ]),
        ("System.Net", ["IPAddress", "WebUtility", "HttpStatusCode"]),
        ("System.Security.Claims", ["Claim"]),
        ("System.Security.Cryptography",
        [
            "SHA1", "SHA256", "SHA384", "SHA512", "MD5", "HMACSHA1", "HMACSHA256", "HMACSHA384", "HMACSHA512",
            "HMAC", "HashAlgorithm", "RSA", "RSAParameters", "RSAEncryptionPadding", "RSASignaturePadding",
            "HashAlgorithmName", "Aes", "SymmetricAlgorithm", "CipherMode", "PaddingMode", "DSA", "KeyedHashAlgorithm"
        ]),
        ("System.Security.Cryptography.X509Certificates",
            ["X509Certificate2", "X509Certificate", "X509ContentType", "X509NameType"]),
        ("System.Text", ["Encoding", "StringBuilder"]),
        ("System.Text.RegularExpressions",
            ["Regex", "RegexOptions", "Match", "MatchCollection", "Group", "GroupCollection", "Capture"]),
        ("System.Web", ["HttpUtility"]),
        ("System.Xml", ["XmlDocument", "XmlNode", "XmlElement", "XmlAttribute", "XmlNodeList", "XmlConvert"]),
        ("System.Xml.Linq",
        [
            "XElement", "XDocument", "XAttribute", "XNode", "XName", "XNamespace", "XText", "XComment",
            "XContainer", "XCData", "XDeclaration", "XProcessingInstruction", "XObject", "SaveOptions", "LoadOptions"
        ]),
        ("Newtonsoft.Json",
        [
            "JsonConvert", "Formatting", "JsonSerializerSettings", "NullValueHandling", "DateParseHandling",
            "DateFormatHandling", "DateTimeZoneHandling", "DefaultValueHandling", "MissingMemberHandling",
            "ReferenceLoopHandling", "JsonException", "JsonToken"
        ]),
        ("Newtonsoft.Json.Linq",
        [
            "JObject", "JArray", "JToken", "JValue", "JProperty", "JContainer", "JConstructor", "JRaw",
            "JTokenType"
        ]),
    ];

    /// <summary>
    /// The namespaces whose types or LINQ methods appear in the expressions, in the order they are written.
    /// </summary>
    public static IReadOnlyList<string> For(IEnumerable<ExpressionMethodInfo> methods)
    {
        var identifiers = methods
            .SelectMany(method => SyntaxFactory.ParseTokens(method.Body))
            .Where(token => token.IsKind(SyntaxKind.IdentifierToken))
            .Select(token => token.ValueText)
            .ToHashSet();
        return Namespaces
            .Where(entry => entry.Names.Any(identifiers.Contains))
            .Select(entry => entry.Namespace)
            .ToList();
    }

    /// <summary>
    /// Puts the using directives the expressions need in front of the generated document.
    /// </summary>
    public static string AddTo(string document, IEnumerable<ExpressionMethodInfo> methods)
    {
        var usings = string.Concat(For(methods).Select(name => $"using {name};{Environment.NewLine}"));
        return usings.Length == 0 ? document : usings + Environment.NewLine + document;
    }
}
