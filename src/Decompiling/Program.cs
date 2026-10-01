// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Decompiling;

var sourceOption = new Option<string?>("--s", "--source")
{
    Description = "Input policy XML file or directory (recursive scan)"
};

var inputOption = new Option<FileInfo[]?>("--input", "-i")
{
    Description = "Input policy XML file(s)",
    AllowMultipleArgumentsPerToken = true
};

var inputDirOption = new Option<DirectoryInfo?>("--input-dir", "-d")
{
    Description = "Input directory (recursive scan)"
};

var patternOption = new Option<string>("--pattern", "-p")
{
    DefaultValueFactory = _ => "*.xml",
    Description = "File pattern for directory scans"
};

var outputOption = new Option<DirectoryInfo?>("--o", "--out", "-o", "--output")
{
    Description = "Output directory (also --o; default: same as input)"
};

var outputExtOption = new Option<string>("--ext", "--output-extension")
{
    DefaultValueFactory = _ => ".cs",
    Description = "Output file extension (e.g. '.cs', '.d.cs')"
};

var namespaceOption = new Option<string>("--namespace", "-n")
{
    DefaultValueFactory = _ => "Generated",
    Description = "Base namespace"
};

var scopeOption = new Option<string>("--scope", "-s")
{
    DefaultValueFactory = _ => "Operation",
    Description = "Policy scope"
};

var docIdRootOption = new Option<DirectoryInfo?>("--doc-id-root")
{
    Description = "Root path for computing relative DocumentId (for traceability)"
};

var documentSuffixOption = new Option<string>("--document-suffix")
{
    DefaultValueFactory = _ => "Policy",
    Description = "Suffix for document class names (e.g. 'Policy', 'Document')"
};

var fragmentSuffixOption = new Option<string>("--fragment-suffix")
{
    DefaultValueFactory = _ => "Policy",
    Description = "Suffix for fragment class names (e.g. 'Policy', 'Fragment')"
};

var noValidateOption = new Option<bool>("--no-validate")
{
    Description = "Skip validation"
};

var verboseOption = new Option<bool>("--verbose", "-v")
{
    Description = "Verbose output"
};

var generateCommand = new Command("generate", "Decompile policy XML file(s) to C# code");
var rootCommand = new RootCommand("Azure API Management Policy Decompiler - XML to C#")
{
    generateCommand
};

foreach (var option in new Option[]
         {
             sourceOption,
             inputOption,
             inputDirOption,
             patternOption,
             outputOption,
             outputExtOption,
             namespaceOption,
             scopeOption,
             docIdRootOption,
             documentSuffixOption,
             fragmentSuffixOption,
             noValidateOption,
             verboseOption,
         })
{
    option.Recursive = true;
    rootCommand.Options.Add(option);
}

Func<ParseResult, Task<int>> handler = async parseResult =>
{
    var source = parseResult.GetValue(sourceOption);
    var input = parseResult.GetValue(inputOption);
    var inputDir = parseResult.GetValue(inputDirOption);
    var pattern = parseResult.GetValue(patternOption)!;
    var output = parseResult.GetValue(outputOption);
    var outputExt = parseResult.GetValue(outputExtOption)!;
    var baseNamespace = parseResult.GetValue(namespaceOption)!;
    var scope = parseResult.GetValue(scopeOption)!;
    var docIdRoot = parseResult.GetValue(docIdRootOption);
    var documentSuffix = parseResult.GetValue(documentSuffixOption)!;
    var fragmentSuffix = parseResult.GetValue(fragmentSuffixOption)!;
    var verbose = parseResult.GetValue(verboseOption);

    // Discover XML files
    var xmlFiles = new List<(string fullPath, string basePath)>();

    if (source is not null)
    {
        var sourcePath = Path.GetFullPath(source);
        if (File.Exists(sourcePath))
        {
            xmlFiles.Add((sourcePath, Path.GetDirectoryName(sourcePath)!));
        }
        else if (Directory.Exists(sourcePath))
        {
            foreach (var file in Directory.GetFiles(sourcePath, pattern, SearchOption.AllDirectories))
            {
                xmlFiles.Add((file, sourcePath));
            }
        }
        else
        {
            await Console.Error.WriteLineAsync($"Error: Source file or directory not found: {sourcePath}");
            return 1;
        }
    }

    if (input is { Length: > 0 })
    {
        foreach (var file in input)
        {
            if (!file.Exists)
            {
                await Console.Error.WriteLineAsync($"Error: File not found: {file.FullName}");
                return 1;
            }
            xmlFiles.Add((file.FullName, file.Directory!.FullName));
        }
    }

    if (inputDir != null)
    {
        if (!inputDir.Exists)
        {
            await Console.Error.WriteLineAsync($"Error: Directory not found: {inputDir.FullName}");
            return 1;
        }
        var dirFiles = Directory.GetFiles(inputDir.FullName, pattern, SearchOption.AllDirectories);
        foreach (var file in dirFiles)
        {
            xmlFiles.Add((file, inputDir.FullName));
        }
    }

    if (xmlFiles.Count == 0)
    {
        await Console.Error.WriteLineAsync(source is null && inputDir is null && input is not { Length: > 0 }
            ? "Error: No input files specified. Use --source, --input or --input-dir."
            : "Error: No input XML files found for the given source and pattern.");
        return 1;
    }

    var decompiler = new PolicyDecompiler();
    var decompileOptions = new DecompileOptions { Scope = scope };
    int succeeded = 0;
    int failed = 0;
    int skipped = 0;

    foreach (var (fullPath, basePath) in xmlFiles)
    {
        var relativePath = Path.GetRelativePath(basePath, fullPath);
        var relativeDir = Path.GetDirectoryName(relativePath) ?? "";

        // Determine output path
        string outputDir;
        if (output != null)
        {
            outputDir = Path.Combine(output.FullName, relativeDir);
        }
        else
        {
            outputDir = Path.GetDirectoryName(fullPath)!;
        }

        var outputFile = Path.Combine(
            outputDir,
            Path.GetFileNameWithoutExtension(fullPath) + outputExt);

        try
        {
            var xml = await File.ReadAllTextAsync(fullPath);
            var preprocessed = PolicyDecompiler.PreprocessXml(xml);
            XDocument doc;
            try { doc = XDocument.Parse(preprocessed); }
            catch (Exception ex)
            {
                failed++;
                await Console.Error.WriteLineAsync($"Error parsing {relativePath}: {ex.Message}");
                continue;
            }

            var rootElement = doc.Root?.Name.LocalName;
            if (rootElement != "policies" && rootElement != "fragment") { skipped++; continue; }

            // Generate namespace from base namespace + relative directory
            var namespaceName = BuildNamespace(baseNamespace, relativeDir);

            // Compute DocumentId from doc-id-root if specified
            var fileOptions = decompileOptions;
            if (docIdRoot != null)
            {
                var docId = Path.GetRelativePath(docIdRoot.FullName, fullPath).Replace('\\', '/');
                fileOptions = fileOptions with { DocumentId = docId };
            }

            string result;
            if (rootElement == "fragment")
            {
                var fragmentId = GetFragmentId(fullPath, basePath);
                var className = BuildClassName(fullPath, basePath, fragmentSuffix);
                if (verbose)
                {
                    await Console.Out.WriteLineAsync($"Processing: {relativePath}");
                    await Console.Out.WriteLineAsync($"  Fragment: {fragmentId} -> {namespaceName}.{className}");
                    await Console.Out.WriteLineAsync($"  Output: {outputFile}");
                }
                result = decompiler.DecompileFragment(xml, fragmentId, className, namespaceName, fileOptions);
            }
            else
            {
                var className = BuildClassName(fullPath, basePath, documentSuffix);
                if (verbose)
                {
                    await Console.Out.WriteLineAsync($"Processing: {relativePath}");
                    await Console.Out.WriteLineAsync($"  Document: {namespaceName}.{className}");
                    await Console.Out.WriteLineAsync($"  Output: {outputFile}");
                }
                result = decompiler.DecompileDocument(xml, className, namespaceName, fileOptions);
            }

            Directory.CreateDirectory(outputDir);
            await File.WriteAllTextAsync(outputFile, result);
            succeeded++;

            if (verbose)
            {
                await Console.Out.WriteLineAsync($"  OK");
            }
        }
        catch (Exception ex)
        {
            failed++;
            await Console.Error.WriteLineAsync($"Error processing {relativePath}: {ex.Message}");
            if (verbose)
            {
                await Console.Error.WriteLineAsync($"  {ex.StackTrace}");
            }
        }
    }

    await Console.Out.WriteLineAsync();
    await Console.Out.WriteLineAsync($"Decompilation complete: {succeeded + failed + skipped} file(s) found, {succeeded} succeeded, {skipped} skipped, {failed} failed.");

    return failed > 0 ? 1 : 0;
};

rootCommand.SetAction(handler);
generateCommand.SetAction(handler);

return await rootCommand.Parse(args).InvokeAsync();

static string SanitizeIdentifier(string name)
{
    // Replace hyphens, dots, and other non-alphanumeric chars with spaces for PascalCase conversion
    var parts = Regex.Split(name, @"[^a-zA-Z0-9]+")
        .Where(p => p.Length > 0)
        .Select(p => char.ToUpper(p[0], CultureInfo.InvariantCulture) + p[1..]);
    var result = string.Join("", parts);

    // Ensure it starts with a letter or underscore
    if (result.Length == 0)
        return "_Generated";
    if (char.IsDigit(result[0]))
        result = "_" + result;
    return result;
}

static string BuildClassName(string fullPath, string basePath, string suffix)
{
    var relativePath = Path.GetRelativePath(basePath, fullPath);
    var relativeDir = Path.GetDirectoryName(relativePath) ?? "";
    var segments = relativeDir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Where(s => s.Length > 0)
        .ToArray();

    // Use the last directory segment as the class name basis
    // If file is at root (no directory), use the file name
    string nameBasis;
    if (segments.Length > 0)
    {
        nameBasis = segments[^1];
    }
    else
    {
        nameBasis = Path.GetFileNameWithoutExtension(fullPath);
    }

    var sanitized = SanitizeIdentifier(nameBasis);
    if (!sanitized.EndsWith(suffix, StringComparison.Ordinal))
    {
        sanitized += suffix;
    }
    return sanitized;
}

static string BuildNamespace(string baseNamespace, string relativeDir)
{
    if (string.IsNullOrEmpty(relativeDir))
        return baseNamespace;

    var segments = relativeDir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Where(s => s.Length > 0)
        .Select(SanitizeIdentifier);

    return baseNamespace + "." + string.Join(".", segments);
}

static string GetFragmentId(string fullPath, string basePath)
{
    var relativePath = Path.GetRelativePath(basePath, fullPath);
    var relativeDir = Path.GetDirectoryName(relativePath) ?? "";
    var segments = relativeDir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Where(s => s.Length > 0)
        .ToArray();

    // The fragment ID is typically the parent directory name
    return segments.Length > 0 ? segments[^1] : Path.GetFileNameWithoutExtension(fullPath);
}
