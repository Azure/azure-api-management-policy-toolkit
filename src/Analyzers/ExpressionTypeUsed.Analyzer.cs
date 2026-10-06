// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class TypeUsedAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Rules.TypeUsed.All;

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze |
                                               GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.InvocationExpression,
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxKind.ElementAccessExpression,
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.ObjectInitializerExpression,
            SyntaxKind.AnonymousObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeTypeName, SyntaxKind.IdentifierName);
    }

    // API Management resolves type names against every namespace it imports, so a name declared in two of them is
    // rejected unless it is written with its namespace, whatever the using directives of the source file are.
    private readonly static IReadOnlyDictionary<string, IReadOnlyCollection<string>> AmbiguousTypeNames =
        new Dictionary<string, IReadOnlyCollection<string>>()
        {
            { "Formatting", new HashSet<string>() { "Newtonsoft.Json.Formatting", "System.Xml.Formatting" } },
        };

    private static void AnalyzeTypeName(SyntaxNodeAnalysisContext context)
    {
        var name = (IdentifierNameSyntax)context.Node;
        if (!AmbiguousTypeNames.TryGetValue(name.Identifier.ValueText, out var candidates) ||
            name.Parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == name ||
            name.Parent is QualifiedNameSyntax qualifiedName && qualifiedName.Right == name ||
            name.Parent is AliasQualifiedNameSyntax ||
            !IsPartOfPolicyExpression(context))
        {
            return;
        }

        // The compiler writes a using alias out as the name it stands for, so that is not a bare name.
        if (context.SemanticModel.GetAliasInfo(name) is null &&
            context.SemanticModel.GetSymbolInfo(name).Symbol is INamedTypeSymbol type &&
            candidates.Contains(type.ToFullyQualifiedString()))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rules.TypeUsed.AmbiguousTypeName, name.GetLocation(),
                name.Identifier.ValueText, type.ToFullyQualifiedString()));
        }
    }

    private readonly static IReadOnlyCollection<string> AllowedTypes = new HashSet<string>()
    {
        #region mslib

        "System.Array",
        "System.BitConverter",
        "System.Boolean",
        "System.Byte",
        "System.Char",
        "System.Collections.Generic.Dictionary<TKey, TValue>",
        "System.Collections.Generic.HashSet<T>",
        "System.Collections.Generic.ICollection<T>",
        "System.Collections.Generic.IDictionary<TKey, TValue>",
        "System.Collections.Generic.IEnumerable<T>",
        "System.Collections.Generic.IEnumerator<T>",
        "System.Collections.Generic.IList<T>",
        "System.Collections.Generic.IReadOnlyCollection<T>",
        "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>",
        "System.Collections.Generic.ISet<T>",
        "System.Collections.Generic.KeyValuePair<TKey, TValue>",
        "System.Collections.Generic.List<T>",
        "System.Collections.Generic.Queue<T>",
        "System.Collections.Generic.Stack<T>",
        "System.Convert",
        "System.DateTime",
        "System.DateTimeKind",
        "System.DateTimeOffset",
        "System.Decimal",
        "System.Double",
        "System.Enum",
        "System.Exception",
        "System.Guid",
        "System.Int16",
        "System.Int32",
        "System.Int64",
        "System.IO.StringReader",
        "System.IO.StringWriter",
        "System.Linq.Enumerable",
        "System.Math",
        "System.MidpointRounding",
        "System.Net.IPAddress",
        "System.Net.WebUtility",
        "System.Nullable",
        "System.Object",
        "System.Random",
        "System.SByte",
        "System.Security.Cryptography.AsymmetricAlgorithm",
        "System.Security.Cryptography.CipherMode",
        "System.Security.Cryptography.HashAlgorithm",
        "System.Security.Cryptography.HashAlgorithmName",
        "System.Security.Cryptography.HMAC",
        "System.Security.Cryptography.HMACMD5",
        "System.Security.Cryptography.HMACSHA1",
        "System.Security.Cryptography.HMACSHA256",
        "System.Security.Cryptography.HMACSHA384",
        "System.Security.Cryptography.HMACSHA512",
        "System.Security.Cryptography.KeyedHashAlgorithm",
        "System.Security.Cryptography.MD5",
        "System.Security.Cryptography.Oid",
        "System.Security.Cryptography.PaddingMode",
        "System.Security.Cryptography.RNGCryptoServiceProvider",
        "System.Security.Cryptography.RSA",
        "System.Security.Cryptography.RSAEncryptionPadding",
        "System.Security.Cryptography.RSASignaturePadding",
        "System.Security.Cryptography.SHA1",
        "System.Security.Cryptography.SHA1Managed",
        "System.Security.Cryptography.SHA256",
        "System.Security.Cryptography.SHA256Managed",
        "System.Security.Cryptography.SHA384",
        "System.Security.Cryptography.SHA384Managed",
        "System.Security.Cryptography.SHA512",
        "System.Security.Cryptography.SHA512Managed",
        "System.Security.Cryptography.SymmetricAlgorithm",
        "System.Security.Cryptography.X509Certificates.PublicKey",
        "System.Security.Cryptography.X509Certificates.RSACertificateExtensions",
        "System.Security.Cryptography.X509Certificates.X500DistinguishedName",
        "System.Security.Cryptography.X509Certificates.X509Certificate",
        "System.Security.Cryptography.X509Certificates.X509Certificate2",
        "System.Security.Cryptography.X509Certificates.X509ContentType",
        "System.Security.Cryptography.X509Certificates.X509NameType",
        "System.Single",
        "System.String",
        "System.StringComparer",
        "System.StringComparison",
        "System.StringSplitOptions",
        "System.Text.Encoding",
        "System.Text.RegularExpressions.Capture",
        "System.Text.RegularExpressions.CaptureCollection",
        "System.Text.RegularExpressions.Group",
        "System.Text.RegularExpressions.GroupCollection",
        "System.Text.RegularExpressions.Match",
        "System.Text.RegularExpressions.Regex",
        "System.Text.RegularExpressions.RegexOptions",
        "System.Text.StringBuilder",
        "System.TimeSpan",
        "System.TimeZone",
        "System.TimeZoneInfo.AdjustmentRule",
        "System.TimeZoneInfo.TransitionTime",
        "System.TimeZoneInfo",
        "System.Tuple",
        "System.UInt16",
        "System.UInt32",
        "System.UInt64",
        "System.Uri",
        "System.UriPartial",
        "System.Xml.Linq.Extensions",
        "System.Xml.Linq.XAttribute",
        "System.Xml.Linq.XCData",
        "System.Xml.Linq.XComment",
        "System.Xml.Linq.XContainer",
        "System.Xml.Linq.XDeclaration",
        "System.Xml.Linq.XDocument",
        "System.Xml.Linq.XDocumentType",
        "System.Xml.Linq.XElement",
        "System.Xml.Linq.XName",
        "System.Xml.Linq.XNamespace",
        "System.Xml.Linq.XNode",
        "System.Xml.Linq.XNodeDocumentOrderComparer",
        "System.Xml.Linq.XNodeEqualityComparer",
        "System.Xml.Linq.XObject",
        "System.Xml.Linq.XProcessingInstruction",
        "System.Xml.Linq.XText",
        "System.Xml.XmlNodeType",

        #endregion mslib

        #region Newtonsoft.Json

        "Newtonsoft.Json.Formatting",
        "Newtonsoft.Json.JsonConvert",
        "Newtonsoft.Json.Linq.Extensions",
        "Newtonsoft.Json.Linq.JArray",
        "Newtonsoft.Json.Linq.JConstructor",
        "Newtonsoft.Json.Linq.JContainer",
        "Newtonsoft.Json.Linq.JObject",
        "Newtonsoft.Json.Linq.JProperty",
        "Newtonsoft.Json.Linq.JRaw",
        "Newtonsoft.Json.Linq.JToken",
        "Newtonsoft.Json.Linq.JTokenType",
        "Newtonsoft.Json.Linq.JValue",

        #endregion Newtonsoft.Json

        #region Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions

        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.Authorization",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.BasicAuthCredentials",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.ByteArrayExtensions",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.CarbonIntensityCategory",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.DictionaryExtensions",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.IFoundry",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.ISustainabilityInfo",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.X509Certificate2Extensions",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.IApi",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.IExpressionContext",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.IContextApi",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.IBackend",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.IDeployment",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.IGateway",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.IWorkspace",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.IGroup",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.Jwt",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.ILastError",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.IMessageBody",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.IAzureVnetInfo",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.IOperation",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.IPrivateEndpointConnection",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.IProduct",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.ProductState",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.IRequest",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.IResponse",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.ISubscription",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.ISubscriptionKeyParameterNames",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.IUrl",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.IUser",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.StringExtensions",
        "Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions.IUserIdentity",

        #endregion Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions
    };

    private readonly static IReadOnlyDictionary<string, IReadOnlyCollection<string>> AllowedInTypes =
        new Dictionary<string, IReadOnlyCollection<string>>()
        {
            { "Newtonsoft.Json.JsonConvert", new HashSet<string>() { "SerializeObject", "DeserializeObject" } },
            // The gateway accepts the type but none of its members: ToString(), Equals() or GetType() called on
            // a value typed object, or on a type that doesn't declare them itself, is rejected.
            { "System.Object", new HashSet<string>() },
            {
                "System.DateTime", new HashSet<string>()
                {
                    ".ctor",
                    "Add",
                    "AddDays",
                    "AddHours",
                    "AddMilliseconds",
                    "AddMinutes",
                    "AddMonths",
                    "AddSeconds",
                    "AddTicks",
                    "AddYears",
                    "Date",
                    "Day",
                    "DayOfWeek",
                    "DayOfYear",
                    "DaysInMonth",
                    "Hour",
                    "IsDaylightSavingTime",
                    "IsLeapYear",
                    "MaxValue",
                    "Millisecond",
                    "Minute",
                    "MinValue",
                    "Month",
                    "Now",
                    "Parse",
                    "Second",
                    "Subtract",
                    "Ticks",
                    "TimeOfDay",
                    "Today",
                    "ToString",
                    "UtcNow",
                    "Year"
                }
            },
            { "System.DateTimeKind", new HashSet<string>() { "Utc" } },
            { "System.Enum", new HashSet<string>() { "Parse", "TryParse", "ToString" } },
            {
                "System.Net.IPAddress",
                new HashSet<string>()
                {
                    "AddressFamily",
                    "Equals",
                    "GetAddressBytes",
                    "IsLoopback",
                    "Parse",
                    "TryParse",
                    "ToString"
                }
            },
            { "System.Security.Cryptography.X509Certificates.X500DistinguishedName", new HashSet<string>() { "Name" } },
            { "System.Text.RegularExpressions.Capture", new HashSet<string>() { "Index", "Length", "Value" } },
            { "System.Text.RegularExpressions.CaptureCollection", new HashSet<string>() { "Count", "Item" } },
            { "System.Text.RegularExpressions.Group", new HashSet<string>() { "Captures", "Success" } },
            { "System.Text.RegularExpressions.GroupCollection", new HashSet<string>() { "Count", "Item" } },
            { "System.Text.RegularExpressions.Match", new HashSet<string>() { "Empty", "Groups", "Result" } },
            {
                "System.Text.RegularExpressions.Regex", new HashSet<string>()
                {
                    ".ctor",
                    "IsMatch",
                    "Match",
                    "Matches",
                    "Replace",
                    "Unescape",
                    "Split"
                }
            },
        };

    private readonly static IReadOnlyDictionary<string, IReadOnlyCollection<string>> DisallowedInTypes =
        new Dictionary<string, IReadOnlyCollection<string>>()
        {
            { "System.Xml.Linq.XDocument", new HashSet<string>() { "Load" } },
        };

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var node = context.Node;

        if (!IsPartOfPolicyExpression(context))
        {
            return;
        }

        // The member access of a method call is reported through the invocation itself.
        if (node.Parent is InvocationExpressionSyntax invocation && invocation.Expression == node)
        {
            return;
        }

        var nodeSymbol = context.SemanticModel.GetSymbolInfo(node).Symbol;
        if (nodeSymbol == null)
        {
            return;
        }

        // A type named to qualify a member, Shared.Nested.Line() or TimeZoneInfo.AdjustmentRule.X, isn't a use of
        // its own containing type; it is checked as the type of the member used.
        if (nodeSymbol is INamedTypeSymbol)
        {
            return;
        }

        // A helper may take a section context and read its ExpressionContext, which the compiler maps to the
        // gateway's context.
        // The receiver has to be one of the authoring library's section contexts itself.
        if (nodeSymbol is IPropertySymbol { Name: "ExpressionContext" } &&
            node is MemberAccessExpressionSyntax { Expression: var receiver } &&
            context.SemanticModel.GetTypeInfo(receiver).Type?.IsSectionContext() == true)
        {
            return;
        }

        // Invoking a delegate typed member, like context.Trace("message"), is a use of that member and not of the
        // delegate type.
        if (nodeSymbol is IMethodSymbol { MethodKind: MethodKind.DelegateInvoke } &&
            node is InvocationExpressionSyntax delegateInvocation &&
            context.SemanticModel.GetSymbolInfo(delegateInvocation.Expression).Symbol is
                { Kind: SymbolKind.Property or SymbolKind.Field } delegateMember)
        {
            nodeSymbol = delegateMember;
        }

        // Members of an expression helper library ([Expression] on the class) are analysed in their own project, so
        // calls to its methods and uses of its constants are allowed, from source or a referenced assembly.
        if (nodeSymbol.IsExpressionLibraryMember() &&
            (nodeSymbol is IMethodSymbol && node is InvocationExpressionSyntax ||
             nodeSymbol is IFieldSymbol { IsConst: true, Type.TypeKind: not TypeKind.Enum }))
        {
            return;
        }

        // Calls to [Expression] helpers (also from a referenced assembly) and to the helpers of a policy document are
        // expanded by the compiler, and source constants are folded into literals, except values of source enums,
        // which don't exist in API Management. A helper passed as a method group isn't expanded. A document helper
        // that isn't marked [Expression] may call any helper declared in source, as it could before it was analysed.
        if (nodeSymbol is IMethodSymbol && node is InvocationExpressionSyntax &&
            (nodeSymbol.HasExpressionAttribute() || nodeSymbol.IsDocumentMember() ||
             !nodeSymbol.DeclaringSyntaxReferences.IsDefaultOrEmpty &&
             !node.IsPartOfMarkedPolicyExpressionMethod(context.SemanticModel) &&
             !node.IsPartOfPolicyExpressionDelegate(context.SemanticModel)) ||
            !nodeSymbol.DeclaringSyntaxReferences.IsDefaultOrEmpty &&
            nodeSymbol is IFieldSymbol { IsConst: true, ContainingType.TypeKind: not TypeKind.Enum } field &&
            !(field.Type.TypeKind == TypeKind.Enum && !field.Type.DeclaringSyntaxReferences.IsDefaultOrEmpty))
        {
            return;
        }

        var symbol = nodeSymbol.ContainingType;
        if (symbol == null)
        {
            return;
        }

        // API Management accepts anonymous types: new { a = 1 }.a
        if (symbol.IsAnonymousType)
        {
            return;
        }

        // Indexers are named "this[]" in Roslyn; the allow lists use their metadata name ("Item").
        var memberName = nodeSymbol is IPropertySymbol { IsIndexer: true } ? nodeSymbol.MetadataName : nodeSymbol.Name;
        var typeName = GetTypeName(symbol);
        if (AllowedTypes.Contains(typeName))
        {
            if (AllowedInTypes.TryGetValue(typeName, out var allowed) && !allowed.Contains(memberName))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rules.TypeUsed.DisallowedMember, node.GetLocation(),
                    memberName));
            }
            else if (DisallowedInTypes.TryGetValue(typeName, out var disallowed) &&
                     disallowed.Contains(memberName))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rules.TypeUsed.DisallowedMember, node.GetLocation(),
                    memberName));
            }
        }
        else
        {
            context.ReportDiagnostic(Diagnostic.Create(Rules.TypeUsed.DisallowedType, node.GetLocation(), typeName));
        }
    }

    private static bool IsPartOfPolicyExpression(SyntaxNodeAnalysisContext context)
    {
        return context.Node.IsPartOfPolicyExpressionMethod(context.SemanticModel) ||
               context.Node.IsPartOfPolicyExpressionDelegate(context.SemanticModel);
    }

    private static string GetTypeName(INamedTypeSymbol symbol)
    {
        if (!symbol.IsGenericType)
        {
            return symbol.ToFullyQualifiedString();
        }

        // System.Nullable and System.Tuple are listed without type parameters and are allowed with any number of them.
        var nameWithoutTypeParameters = symbol.OriginalDefinition.ToFullyQualifiedStringWithoutTypeParameters();
        return AllowedTypes.Contains(nameWithoutTypeParameters)
            ? nameWithoutTypeParameters
            : symbol.OriginalDefinition.ToFullyQualifiedString();
    }
}