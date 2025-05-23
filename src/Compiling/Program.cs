// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;
using Microsoft.Azure.ApiManagement.PolicyToolkit.IoC;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var config = new ConfigurationBuilder()
    .AddCommandLine(args)
    .Build();
var options = new CompilerOptions(config);

await using ServiceProvider serviceProvider = new ServiceCollection()
    .SetupCompiler()
    .BuildServiceProvider();

if (options.IsProjectSource)
{
    await Console.Out.WriteLineAsync("Project mode");
    var compiler = serviceProvider.GetRequiredService<ProjectCompiler>();
    var result = await compiler.Compile(options.ToProjectCompilerOptions());
    return result.DocumentResults.Sum(r => r.Diagnostics.Count);
}
else
{
    await Console.Error.WriteLineAsync(
        "Directory mode is deprecated. Please use project mode by pointing source parameter to .csproj file or to folder with one .csproj file.");
    var compiler = serviceProvider.GetRequiredService<DirectoryCompiler>();
    var result = await compiler.Compile(options.ToDirectoryCompilerOptions());
    return result.DocumentResults.Sum(r => r.Diagnostics.Count);
}