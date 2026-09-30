// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;
using Microsoft.Azure.ApiManagement.PolicyToolkit.IoC;
using Microsoft.Extensions.DependencyInjection;

var sourceOption = new Option<string>(
    aliases: ["--s", "--source"],
    description: "C# project file or source directory")
{ IsRequired = true };
sourceOption.AddValidator(result =>
{
    var path = result.GetValueOrDefault<string>();
    if (path is not null && !Directory.Exists(path) &&
        !(Path.GetExtension(path).Equals(".csproj", StringComparison.OrdinalIgnoreCase) && File.Exists(path)))
    {
        result.ErrorMessage = $"Source project file or directory not found: {path}";
    }
});

var outputOption = new Option<string>(
    aliases: ["--o", "--out", "--output"],
    description: "Output directory for policy XML")
{ IsRequired = true };

var extensionOption = new Option<string>(
    name: "--ext",
    getDefaultValue: () => "xml",
    description: "Output file extension");

var formatOption = new Option<bool>(
    name: "--format",
    getDefaultValue: () => true,
    description: "Format generated documents (true or false)");

var policyFormatOption = new Option<string>(
    aliases: ["--pf", "--policy-format"],
    getDefaultValue: () => "rawxml",
    description: "Policy content format: rawxml (default) or xml");
policyFormatOption.AddValidator(result =>
{
    var value = result.GetValueOrDefault<string>();
    if (value is not null && !value.Equals("rawxml", StringComparison.OrdinalIgnoreCase) &&
        !value.Equals("xml", StringComparison.OrdinalIgnoreCase))
    {
        result.ErrorMessage = $"Invalid policy format value '{value}'. Use 'rawxml' (default) or 'xml'.";
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

rootCommand.SetHandler(async context =>
{
    var options = new CompilerOptions(
        context.ParseResult.GetValueForOption(sourceOption)!,
        context.ParseResult.GetValueForOption(outputOption)!,
        context.ParseResult.GetValueForOption(extensionOption)!,
        context.ParseResult.GetValueForOption(formatOption),
        context.ParseResult.GetValueForOption(policyFormatOption)!);

    await using var serviceProvider = new ServiceCollection()
        .SetupCompiler()
        .BuildServiceProvider();

    if (options.IsProjectSource)
    {
        await Console.Out.WriteLineAsync("Project mode");
        var compiler = serviceProvider.GetRequiredService<ProjectCompiler>();
        var result = await compiler.Compile(options.ToProjectCompilerOptions());
        context.ExitCode = result.DocumentResults.Sum(r => r.Errors.Length);
    }
    else
    {
        await Console.Error.WriteLineAsync(
            "Directory mode is deprecated. Please use project mode by pointing source parameter to .csproj file or to folder with one .csproj file.");
        var compiler = serviceProvider.GetRequiredService<DirectoryCompiler>();
        var result = await compiler.Compile(options.ToDirectoryCompilerOptions());
        context.ExitCode = result.DocumentResults.Sum(r => r.Errors.Length);
    }
});

return await rootCommand.InvokeAsync(args);