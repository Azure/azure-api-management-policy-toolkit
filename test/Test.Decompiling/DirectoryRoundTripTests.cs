// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using System.Xml.Linq;

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

/// <summary>
/// Configurable round-trip test that validates all policy and fragment XML files
/// in a directory can be decompiled to C# and recompiled back to equivalent XML.
///
/// Configure the policy directory via:
///   - Environment variable: APIM_POLICY_DIR
///   - MSTest .runsettings: PolicyDirectory parameter
///   - Default: skips tests if no directory is configured
///
/// Usage in consuming repositories:
///   1. Reference this test project (or package)
///   2. Set APIM_POLICY_DIR to your Common/ folder path
///   3. Run: dotnet test --filter "FullyQualifiedName~DirectoryRoundTripTests"
/// </summary>
[TestClass]
public class DirectoryRoundTripTests
{
    private static readonly IEnumerable<MetadataReference> References = GetReferences();
    private static ServiceProvider s_serviceProvider = null!;
    private static DocumentCompiler s_compiler = null!;
    private static PolicyDecompiler s_decompiler = null!;
    private static string? s_policyDirectory;

    private static MetadataReference[] GetReferences()
    {
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var refs = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(XElement).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IDocument).Assembly.Location),
        };
        foreach (var asm in new[]
                 {
                     "System.Runtime.dll", "System.Collections.dll", "System.Linq.dll",
                     "System.Console.dll", "netstandard.dll",
                     "System.Text.RegularExpressions.dll"
                 })
        {
            var path = Path.Combine(runtimeDir, asm);
            if (File.Exists(path))
                refs.Add(MetadataReference.CreateFromFile(path));
        }

        // Add commonly used packages in APIM policy expressions
        TryAddAssemblyReference(refs, typeof(Newtonsoft.Json.JsonConvert));
        TryAddAssemblyReference(refs, typeof(System.IdentityModel.Tokens.Jwt.JwtSecurityToken));

        return refs.ToArray();
    }

    private static void TryAddAssemblyReference(List<MetadataReference> refs, Type type)
    {
        try
        {
            refs.Add(MetadataReference.CreateFromFile(type.Assembly.Location));
        }
        catch
        {
            // Package not available — skip
        }
    }

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        var dir = Environment.GetEnvironmentVariable("APIM_POLICY_DIR")
                  ?? context.Properties["PolicyDirectory"]?.ToString();

        // Resolve relative paths against the current working directory
        if (!string.IsNullOrEmpty(dir))
            s_policyDirectory = Path.GetFullPath(dir);
        else
            s_policyDirectory = null;

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
        s_serviceProvider?.Dispose();
    }

    [TestMethod]
    public void AllPolicies_Decompile_Successfully()
    {
        if (string.IsNullOrEmpty(s_policyDirectory) || !Directory.Exists(s_policyDirectory))
        {
            Assert.Inconclusive(
                $"Policy directory not configured or not found. " +
                $"Set APIM_POLICY_DIR environment variable to your policy XML root directory.");
            return;
        }

        var xmlFiles = Directory.GetFiles(s_policyDirectory, "*.xml", SearchOption.AllDirectories);
        Console.WriteLine($"Found {xmlFiles.Length} XML files in {s_policyDirectory}");

        int total = 0, decompiled = 0;
        var failures = new List<(string file, string error)>();

        foreach (var file in xmlFiles)
        {
            var rawXml = File.ReadAllText(file);
            if (!rawXml.Contains("<policies>") && !rawXml.Contains("<fragment>"))
                continue;

            total++;
            var relativePath = Path.GetRelativePath(s_policyDirectory, file);

            try
            {
                var (xml, isFragment) = PreprocessAndParse(rawXml);
                var className = SanitizeClassName(relativePath);

                if (isFragment)
                {
                    var fragmentId = Path.GetDirectoryName(relativePath)?.Split(Path.DirectorySeparatorChar).Last()
                                     ?? "unknown";
                    s_decompiler.DecompileFragment(xml, fragmentId, className, "RoundTripTest");
                }
                else
                {
                    s_decompiler.DecompileDocument(xml, className, "RoundTripTest");
                }

                decompiled++;
            }
            catch (Exception ex)
            {
                failures.Add((relativePath, ex.Message.Split('\n')[0]));
            }
        }

        Console.WriteLine($"\nDecompile results: {decompiled}/{total} succeeded");
        if (failures.Count > 0)
        {
            Console.WriteLine($"\nFailures ({failures.Count}):");
            foreach (var (f, err) in failures.Take(20))
                Console.WriteLine($"  {f}: {err}");
            if (failures.Count > 20)
                Console.WriteLine($"  ... and {failures.Count - 20} more");
        }

        Assert.IsTrue(
            decompiled > 0,
            "At least one policy should decompile successfully");
        Assert.IsTrue(
            (double)decompiled / total >= 0.95,
            $"At least 95% of policies should decompile. Got {decompiled}/{total} ({100.0 * decompiled / total:F1}%)");
    }

    [TestMethod]
    public void AllPolicies_RoundTrip_Successfully()
    {
        if (string.IsNullOrEmpty(s_policyDirectory) || !Directory.Exists(s_policyDirectory))
        {
            Assert.Inconclusive(
                $"Policy directory not configured or not found. " +
                $"Set APIM_POLICY_DIR environment variable to your policy XML root directory.");
            return;
        }

        var xmlFiles = Directory.GetFiles(s_policyDirectory, "*.xml", SearchOption.AllDirectories);

        int total = 0, exactMatch = 0, semanticMatch = 0, decompileOnly = 0, failed = 0;
        var failures = new List<(string file, string phase, string error)>();

        foreach (var file in xmlFiles)
        {
            var rawXml = File.ReadAllText(file);
            if (!rawXml.Contains("<policies>") && !rawXml.Contains("<fragment>"))
                continue;

            total++;
            var relativePath = Path.GetRelativePath(s_policyDirectory, file);

            try
            {
                var result = RunRoundTrip(rawXml, relativePath);
                switch (result)
                {
                    case RoundTripResult.ExactMatch:
                        exactMatch++;
                        break;
                    case RoundTripResult.SemanticMatch:
                        semanticMatch++;
                        break;
                    case RoundTripResult.DecompileOnly:
                        decompileOnly++;
                        break;
                }
            }
            catch (RoundTripException ex)
            {
                failures.Add((relativePath, ex.Phase, ex.Message.Split('\n')[0]));
                failed++;
            }
        }

        Console.WriteLine($"\n=== ROUND-TRIP RESULTS ===");
        Console.WriteLine($"Total policies:     {total}");
        Console.WriteLine($"Exact match:        {exactMatch}");
        Console.WriteLine($"Semantic match:     {semanticMatch}");
        Console.WriteLine($"Decompile only:     {decompileOnly} (compile skipped: {{named-values}} or missing type refs)");
        Console.WriteLine($"Failed:             {failed}");
        Console.WriteLine(
            $"Success rate:       {100.0 * (exactMatch + semanticMatch + decompileOnly) / total:F1}%");

        if (failures.Count > 0)
        {
            Console.WriteLine($"\nFailures ({failures.Count}):");
            foreach (var (f, phase, err) in failures.Take(30))
                Console.WriteLine($"  [{phase}] {f}: {err}");
            if (failures.Count > 30)
                Console.WriteLine($"  ... and {failures.Count - 30} more");
        }

        Assert.AreEqual(total, exactMatch,
            $"All policies should round-trip with exact match. Got {exactMatch}/{total} ({semanticMatch} semantic, {decompileOnly} decompile-only, {failed} failed)");
    }

    private enum RoundTripResult
    {
        ExactMatch,
        SemanticMatch,
        DecompileOnly,
    }

    private RoundTripResult RunRoundTrip(string rawXml, string relativePath)
    {
        // Parse
        string xml;
        bool isFragment;
        try
        {
            (xml, isFragment) = PreprocessAndParse(rawXml);
        }
        catch (Exception ex)
        {
            throw new RoundTripException("XML Parse", ex.Message, ex);
        }

        var className = SanitizeClassName(relativePath);

        // Decompile
        string csharp;
        try
        {
            csharp = isFragment
                ? s_decompiler.DecompileFragment(xml, className, className, "RoundTripTest")
                : s_decompiler.DecompileDocument(xml, className, "RoundTripTest");
        }
        catch (Exception ex)
        {
            throw new RoundTripException("Decompile", ex.Message, ex);
        }

        // Replace {{named-values}} with valid C# identifiers for compilation.
        // APIM named-value tokens like {{CluEnabled}} are deployment-time placeholders
        // that may appear outside string literals in expression bodies, producing
        // invalid C# syntax. We replace them with unique identifiers, compile,
        // then restore them in the output XML for comparison.
        var namedValueMap = new Dictionary<string, string>();
        var csharpForCompilation = ReplaceNamedValues(csharp, namedValueMap);

        // Check for syntax errors
        var syntaxTree = CSharpSyntaxTree.ParseText(csharpForCompilation);
        var syntaxErrors = syntaxTree.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (syntaxErrors.Any())
            return RoundTripResult.DecompileOnly;

        // Compile
        IDocumentCompilationResult compilationResult;
        try
        {
            var compilation = CSharpCompilation.Create(
                Guid.NewGuid().ToString(),
                syntaxTrees: [syntaxTree],
                references: References);
            var semantics = compilation.GetSemanticModel(syntaxTree);
            var policyClass = syntaxTree.GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c =>
                    c.AttributeLists.ContainsAttributeOfType<DocumentAttribute>(semantics));

            if (policyClass is null)
                return RoundTripResult.DecompileOnly;

            compilationResult = s_compiler.Compile(compilation, policyClass);
        }
        catch
        {
            return RoundTripResult.DecompileOnly;
        }

        if (compilationResult.Document is null)
            return RoundTripResult.DecompileOnly;

        // Compare XMLs element-by-element
        var originalDoc = XDocument.Parse(xml.Trim());
        var compiledDoc = new XDocument(compilationResult.Document);

        // Restore {{named-values}} in compiled XML for comparison
        RestoreNamedValuesInXml(compiledDoc.Root!, namedValueMap);

        // Structural comparison — formats C# expressions identically on both sides
        var diff = PolicyXmlComparer.Compare(originalDoc.Root!, compiledDoc.Root!);
        if (diff == null)
            return RoundTripResult.ExactMatch;

        Console.WriteLine($"DIFF [{relativePath}]: {diff}");
        return RoundTripResult.SemanticMatch;
    }

    private static readonly Regex NamedValuePattern = new(@"\{\{([A-Za-z][A-Za-z0-9_.\-]*)\}\}", RegexOptions.Compiled);

    /// <summary>
    /// Replaces APIM {{named-value}} tokens with valid C# identifiers.
    /// Builds a reverse map (placeholder → original) for later restoration.
    /// </summary>
    private static string ReplaceNamedValues(string csharp, Dictionary<string, string> reverseMap)
    {
        if (!csharp.Contains("{{"))
            return csharp;

        // Map each unique {{name}} to a stable placeholder
        var tokenToPlaceholder = new Dictionary<string, string>(StringComparer.Ordinal);

        return NamedValuePattern.Replace(csharp, match =>
        {
            var token = match.Value; // e.g., "{{CluEnabled}}"
            if (!tokenToPlaceholder.TryGetValue(token, out var placeholder))
            {
                placeholder = $"__NV{tokenToPlaceholder.Count}__";
                tokenToPlaceholder[token] = placeholder;
                reverseMap[placeholder] = token;
            }
            return placeholder;
        });
    }

    /// <summary>
    /// Restores {{named-value}} tokens in compiled XML by walking all attributes and text nodes.
    /// </summary>
    private static void RestoreNamedValuesInXml(XElement element, Dictionary<string, string> reverseMap)
    {
        if (reverseMap.Count == 0) return;

        foreach (var attr in element.Attributes().ToList())
        {
            var val = attr.Value;
            foreach (var (placeholder, original) in reverseMap)
                val = val.Replace(placeholder, original, StringComparison.Ordinal);
            if (val != attr.Value) attr.Value = val;
        }

        foreach (var text in element.Nodes().OfType<XText>().ToList())
        {
            var val = text.Value;
            foreach (var (placeholder, original) in reverseMap)
                val = val.Replace(placeholder, original, StringComparison.Ordinal);
            if (val != text.Value) text.Value = val;
        }

        foreach (var child in element.Elements())
            RestoreNamedValuesInXml(child, reverseMap);
    }

    private static (string xml, bool isFragment) PreprocessAndParse(string rawXml)
    {
        var preprocessed = PolicyDecompiler.PreprocessXml(rawXml);
        var doc = XDocument.Parse(preprocessed);
        doc.DescendantNodes().OfType<XComment>().Remove();
        var isFragment = doc.Root?.Name.LocalName == "fragment";
        return (doc.ToString(SaveOptions.DisableFormatting), isFragment);
    }

    private static string SanitizeClassName(string path)
    {
        var name = "Policy_" + Regex.Replace(
            Path.GetFileNameWithoutExtension(path) + "_" +
            (Path.GetDirectoryName(path) ?? ""),
            @"[^\w]", "_");
        if (name.Length > 200) name = name[..200];
        return name;
    }

    private sealed class RoundTripException : Exception
    {
        public string Phase { get; }

        public RoundTripException(string phase, string message, Exception? inner = null)
            : base(message, inner)
        {
            Phase = phase;
        }
    }
}
