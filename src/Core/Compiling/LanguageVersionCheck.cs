// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Runtime.CompilerServices;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

internal static class LanguageVersionCheck
{
    private static readonly ConditionalWeakTable<Compilation, DownlevelCompilation> DownlevelCompilations = new();

    // API Management compiles policy expressions as C# 7, so the helper code emitted into this expression is bound
    // again as C# 7.3 and any newer language feature it uses is reported at its source location.
    public static void ReportNewerLanguageFeatures(IDocumentCompilationContext context, IEnumerable<SyntaxNode> sources)
    {
        var reported = new HashSet<(TextSpan, string)>();
        foreach (var source in sources)
        {
            // Helpers from a referenced project are checked against that project's compilation.
            if (CompilerUtils.FindCompilation(context.Compilation, source.SyntaxTree)
                    is not CSharpCompilation compilation ||
                DownlevelCompilations
                    .GetValue(compilation, static original => new DownlevelCompilation((CSharpCompilation)original))
                    .GetSemanticModel(source.SyntaxTree) is not { } model)
            {
                continue;
            }

            // Errors that only appear as C# 7.3 come from newer language features, whatever their diagnostic ID.
            var existing = CompilerUtils.CachedModel(compilation, source.SyntaxTree).GetDiagnostics(source.Span)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(diagnostic => (diagnostic.Location.SourceSpan, diagnostic.Id))
                .ToHashSet();
            foreach (var diagnostic in model.GetDiagnostics(source.Span))
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error &&
                    !existing.Contains((diagnostic.Location.SourceSpan, diagnostic.Id)) &&
                    reported.Add((diagnostic.Location.SourceSpan, diagnostic.Id)))
                {
                    context.Report(Diagnostic.Create(
                        CompilationErrors.UnsupportedLanguageFeature,
                        Location.Create(source.SyntaxTree, diagnostic.Location.SourceSpan),
                        diagnostic.GetMessage(CultureInfo.InvariantCulture)));
                }
            }
        }
    }

    private sealed class DownlevelCompilation(CSharpCompilation original)
    {
        private readonly Lazy<(CSharpCompilation Compilation, Dictionary<SyntaxTree, SyntaxTree> Trees)> _downlevel =
            new(() =>
            {
                var trees = original.SyntaxTrees.ToDictionary(
                    tree => tree,
                    tree => tree.WithRootAndOptions(
                        tree.GetRoot(),
                        ((CSharpParseOptions)tree.Options).WithLanguageVersion(LanguageVersion.CSharp7_3)));
                return (original.RemoveAllSyntaxTrees().AddSyntaxTrees(trees.Values), trees);
            });

        public SemanticModel? GetSemanticModel(SyntaxTree tree) =>
            _downlevel.Value.Trees.TryGetValue(tree, out var downlevelTree)
                ? CompilerUtils.CachedModel(_downlevel.Value.Compilation, downlevelTree)
                : null;
    }
}