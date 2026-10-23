// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;
using Microsoft.Azure.ApiManagement.PolicyToolkit.IoC;
using Microsoft.Extensions.DependencyInjection;

var sourceOption = new Option<string>("--s", "--source")
{
    Description = "C# project file or source directory",
    Required = true
};
sourceOption.Validators.Add(result =>
{
    var path = result.GetValueOrDefault<string>();
    if (path is not null && !Directory.Exists(path) &&
        !(Path.GetExtension(path).Equals(".csproj", StringComparison.OrdinalIgnoreCase) && File.Exists(path)))
    {
        result.AddError($"Source project file or directory not found: {path}");
    }
});

var outputOption = new Option<string>("--o", "--out", "--output")
{
    Description = "Output directory for policy XML",
    Required = true
};

var extensionOption = new Option<string>("--ext")
{
    DefaultValueFactory = _ => "xml",
    Description = "Output file extension"
};

var formatOption = new Option<bool>("--format")
{
    DefaultValueFactory = _ => true,
    Description = "Format generated documents (true or false)"
};

var policyFormatOption = new Option<string>("--pf", "--policy-format")
{
    DefaultValueFactory = _ => "rawxml",
    Description = "Policy content format: rawxml (default) or xml"
};
policyFormatOption.Validators.Add(result =>
{
    var value = result.GetValueOrDefault<string>();
    if (value is not null && !value.Equals("rawxml", StringComparison.OrdinalIgnoreCase) &&
        !value.Equals("xml", StringComparison.OrdinalIgnoreCase))
    {
        result.AddError($"Invalid policy format value '{value}'. Use 'rawxml' (default) or 'xml'.");
    }
});

var rootCommand = new RootCommand("Azure API Management Policy Compiler - C# to XML")
{
    sourceOption,
    outputOption,
    extensionOption,
    formatOption,
    policyFormatOption,
};

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var options = new CompilerOptions(
        parseResult.GetValue(sourceOption)!,
        parseResult.GetValue(outputOption)!,
        parseResult.GetValue(extensionOption)!,
        parseResult.GetValue(formatOption),
        parseResult.GetValue(policyFormatOption)!);

    await using var serviceProvider = new ServiceCollection()
        .SetupCompiler()
        .BuildServiceProvider();

    if (options.IsProjectSource)
    {
        await Console.Out.WriteLineAsync("Project mode");
        var compiler = serviceProvider.GetRequiredService<ProjectCompiler>();
        var result = await compiler.Compile(options.ToProjectCompilerOptions(), cancellationToken);
        return result.DocumentResults.Sum(r => r.Errors.Length);
    }
    else
    {
        await Console.Error.WriteLineAsync(
            "Directory mode is deprecated. Please use project mode by pointing source parameter to .csproj file or to folder with one .csproj file.");
        var compiler = serviceProvider.GetRequiredService<DirectoryCompiler>();
        var result = await compiler.Compile(options.ToDirectoryCompilerOptions());
        return result.DocumentResults.Sum(r => r.Errors.Length);
    }
});

return await rootCommand.Parse(args).InvokeAsync();