// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Xml.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Tests.Decompiling;

[TestClass]
public class ToolCliTests
{
    [TestMethod]
    public async Task BothTools_ShowHelpForSourceAndOutput()
    {
        var compiler = await RunToolAsync("Compiling", "--help");
        var decompiler = await RunToolAsync("Decompiling", "--help");

        Assert.AreEqual(0, compiler.ExitCode, compiler.Error);
        Assert.AreEqual(0, decompiler.ExitCode, decompiler.Error);
        StringAssert.Contains(compiler.Output, "--s, --source");
        StringAssert.Contains(compiler.Output, "--o, --out");
        StringAssert.Contains(decompiler.Output, "--s, --source");
        StringAssert.Contains(decompiler.Output, "--o");
    }

    [TestMethod]
    public async Task BothTools_RejectMissingOrInvalidOptions()
    {
        var missingOutput = await RunToolAsync("Compiling", "--s", ".");
        var missingSource = await RunToolAsync("Compiling", "--s", "not-a-project.csproj", "--o", ".");
        var invalidFormat = await RunToolAsync("Compiling", "--s", ".", "--o", ".", "--pf", "invalid");
        var missingInput = await RunToolAsync("Decompiling");
        var missingFile = await RunToolAsync("Decompiling", "--source", "not-a-policy.xml");

        Assert.AreNotEqual(0, missingOutput.ExitCode);
        StringAssert.Contains(missingOutput.Error, "required");
        Assert.AreNotEqual(0, missingSource.ExitCode);
        StringAssert.Contains(missingSource.Error, "Source project file or directory not found");
        Assert.AreNotEqual(0, invalidFormat.ExitCode);
        StringAssert.Contains(invalidFormat.Error, "Invalid policy format");
        Assert.AreNotEqual(0, missingInput.ExitCode);
        StringAssert.Contains(missingInput.Error, "No input files specified");
        Assert.AreNotEqual(0, missingFile.ExitCode);
        StringAssert.Contains(missingFile.Error, "Source file or directory not found");
    }

    [TestMethod]
    public async Task Compiler_AcceptsShortAndLongOptions()
    {
        var temp = CreateTempDirectory();
        try
        {
            var source = Path.Combine(temp, "source");
            Directory.CreateDirectory(source);
            await File.WriteAllTextAsync(Path.Combine(source, "Sample.cs"), """
                using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

                [Document]
                public class Sample : IDocument
                {
                    public void Inbound(IInboundContext context)
                    {
                        context.Base();
                    }
                }
                """);

            var shortOutput = Path.Combine(temp, "short");
            var shortResult = await RunToolAsync("Compiling", "--s", source, "--o", shortOutput,
                "--format", "false", "--pf", "XML");
            Assert.AreEqual(0, shortResult.ExitCode, shortResult.Error);
            Assert.AreEqual("policies", XDocument.Load(Path.Combine(shortOutput, "Sample.xml")).Root?.Name.LocalName);

            var longOutput = Path.Combine(temp, "long");
            var longResult = await RunToolAsync("Compiling", "--source", source, "--out", longOutput,
                "--policy-format", "xml");
            Assert.AreEqual(0, longResult.ExitCode, longResult.Error);
            Assert.IsTrue((await File.ReadAllTextAsync(Path.Combine(longOutput, "Sample.xml"))).Length >
                          (await File.ReadAllTextAsync(Path.Combine(shortOutput, "Sample.xml"))).Length);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    [TestMethod]
    public async Task Decompiler_AcceptsSourceFileAndDirectoryWithoutSubcommand_AndLegacyGenerate()
    {
        var temp = CreateTempDirectory();
        try
        {
            var source = Path.Combine(temp, "source");
            Directory.CreateDirectory(source);
            var policy = Path.Combine(source, "policy.xml");
            await File.WriteAllTextAsync(policy, "<policies><inbound><base /></inbound></policies>");

            var directOutput = Path.Combine(temp, "direct");
            var direct = await RunToolAsync("Decompiling", "--s", policy, "--o", directOutput);
            Assert.AreEqual(0, direct.ExitCode, direct.Error);
            StringAssert.Contains(await File.ReadAllTextAsync(Path.Combine(directOutput, "policy.cs")), "context.Base()");

            var nested = Path.Combine(source, "nested");
            Directory.CreateDirectory(nested);
            await File.WriteAllTextAsync(Path.Combine(nested, "other.xml"),
                "<policies><inbound><base /></inbound></policies>");
            var directoryOutput = Path.Combine(temp, "directory");
            var directory = await RunToolAsync("Decompiling", "--source", source, "--pattern", "*.xml",
                "--out", directoryOutput);
            Assert.AreEqual(0, directory.ExitCode, directory.Error);
            Assert.IsTrue(File.Exists(Path.Combine(directoryOutput, "nested", "other.cs")));

            var legacyOutput = Path.Combine(temp, "legacy");
            var legacy = await RunToolAsync("Decompiling", "generate", "-i", policy,
                Path.Combine(nested, "other.xml"), "-s", "Api", "--output", legacyOutput);
            Assert.AreEqual(0, legacy.ExitCode, legacy.Error);
            Assert.IsTrue(File.Exists(Path.Combine(legacyOutput, "policy.cs")));
            Assert.IsTrue(File.Exists(Path.Combine(legacyOutput, "other.cs")));
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"policy-toolkit-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunToolAsync(string tool, params string[] args)
    {
        var root = FindRepositoryRoot();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))
            .Parent!.Name;
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[]
                 {
                     "run", "--no-build", "--no-restore", "--configuration", configuration,
                     "--project", Path.Combine(root, "src", tool, $"{tool}.csproj"), "--"
                 }.Concat(args))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await output, await error);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "apim-policy-toolkit.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
