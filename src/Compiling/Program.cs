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
var compiler = serviceProvider.GetRequiredService<DirectoryCompiler>();

var result = await compiler.Compile(options.ToDirectoryCompilerOptions());

return result.DocumentResults.Sum(r => r.Diagnostics.Count);