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

    [TestMethod]
    public async Task Decompiler_GivesPoliciesInOneDirectoryDistinctClassNames()
    {
        var temp = CreateTempDirectory();
        try
        {
            var orders = Path.Combine(temp, "source", "orders");
            Directory.CreateDirectory(orders);
            const string xml = "<policies><inbound><base /></inbound></policies>";
            await File.WriteAllTextAsync(Path.Combine(orders, "policy.xml"), xml);
            await File.WriteAllTextAsync(Path.Combine(orders, "get-order.xml"), xml);
            await File.WriteAllTextAsync(Path.Combine(orders, "get_order.xml"), xml);
            var single = Path.Combine(temp, "source", "single");
            Directory.CreateDirectory(single);
            await File.WriteAllTextAsync(Path.Combine(single, "policy.xml"), xml);
            // a file that isn't a policy is skipped and doesn't make the fragment share its directory
            var limits = Path.Combine(temp, "source", "limits");
            Directory.CreateDirectory(limits);
            await File.WriteAllTextAsync(Path.Combine(limits, "policy.xml"), "<fragment><base /></fragment>");
            await File.WriteAllTextAsync(Path.Combine(limits, "notes.xml"), "<notes />");

            var output = Path.Combine(temp, "out");
            var result = await RunToolAsync("Decompiling", "--source", Path.Combine(temp, "source"), "--out", output);

            Assert.AreEqual(0, result.ExitCode, result.Error);
            var classNames = new[] { "policy.cs", "get-order.cs", "get_order.cs" }
                .Select(file => File.ReadAllText(Path.Combine(output, "orders", file)))
                .Select(code => code.Split('\n').Single(line => line.StartsWith("public class ")).Trim())
                .ToList();
            CollectionAssert.AllItemsAreUnique(classNames);
            CollectionAssert.Contains(classNames, "public class OrdersPolicy : IDocument");
            CollectionAssert.Contains(classNames, "public class OrdersGetOrderPolicy : IDocument");
            StringAssert.Contains(await File.ReadAllTextAsync(Path.Combine(output, "single", "policy.cs")),
                "public class SinglePolicy : IDocument");
            StringAssert.Contains(await File.ReadAllTextAsync(Path.Combine(output, "limits", "policy.cs")),
                "[Document(\"limits\"");
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    [TestMethod]
    public async Task Decompiler_ValidatesGeneratedCode_UnlessNoValidate()
    {
        var temp = CreateTempDirectory();
        try
        {
            // "1 2" is not a number, so the generated C# does not parse.
            var policy = Path.Combine(temp, "policy.xml");
            await File.WriteAllTextAsync(policy,
                """<policies><inbound><rate-limit calls="1 2" renewal-period="60" /></inbound></policies>""");

            var validated = await RunToolAsync("Decompiling", "--source", policy, "--out", Path.Combine(temp, "a"));
            var notValidated = await RunToolAsync("Decompiling", "--source", policy, "--out", Path.Combine(temp, "b"),
                "--no-validate");

            Assert.AreNotEqual(0, validated.ExitCode);
            StringAssert.Contains(validated.Error, "syntax errors");
            Assert.AreEqual(0, notValidated.ExitCode, notValidated.Error);
            Assert.IsTrue(File.Exists(Path.Combine(temp, "b", "policy.cs")));
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    [TestMethod]
    public async Task Decompiler_ReadsThePolicyInTheGivenFormat()
    {
        var temp = CreateTempDirectory();
        try
        {
            var policy = Path.Combine(temp, "policy.xml");
            await File.WriteAllTextAsync(policy,
                """<policies><inbound><set-header name="h" exists-action="override"><value>@(context.Request.Method + "&amp;")</value></set-header></inbound></policies>""");

            var raw = await RunToolAsync("Decompiling", "--source", policy, "--out", Path.Combine(temp, "a"));
            var asXml = await RunToolAsync("Decompiling", "--source", policy, "--out", Path.Combine(temp, "b"),
                "--policy-format", "xml");
            var invalid = await RunToolAsync("Decompiling", "--source", policy, "--out", Path.Combine(temp, "c"),
                "--pf", "auto");
            var number = await RunToolAsync("Decompiling", "--source", policy, "--out", Path.Combine(temp, "d"),
                "--pf", "1");

            // rawxml is the default, as for the compiler: the entity is part of the expression
            Assert.AreEqual(0, raw.ExitCode, raw.Error);
            StringAssert.Contains(await File.ReadAllTextAsync(Path.Combine(temp, "a", "policy.cs")), "Method + \"&amp;\";");
            Assert.AreEqual(0, asXml.ExitCode, asXml.Error);
            StringAssert.Contains(await File.ReadAllTextAsync(Path.Combine(temp, "b", "policy.cs")), "Method + \"&\";");
            Assert.AreNotEqual(0, invalid.ExitCode);
            StringAssert.Contains(invalid.Error, "Invalid policy format value 'auto'");
            Assert.AreNotEqual(0, number.ExitCode);
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
