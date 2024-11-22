// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Azure.ApiManagement.PolicyToolkit.Compiling;
using Azure.ApiManagement.PolicyToolkit.Serialization;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .AddCommandLine(args)
    .Build();
var options = new CompilerOptions(config);
var files = Directory.GetFiles(options.SourceFolder, "*.cs", SearchOption.AllDirectories).Where(p => !Regex.IsMatch(p, @".*[\\/](obj|bin)[\\/].*"));

int numberOfErrors = 0;

foreach (var file in files)
{
    var fileName = Path.GetFileNameWithoutExtension(file);
    Console.Out.WriteLine($"File {fileName} Processing");
    var code = File.ReadAllText(file);
    var syntax = CSharpSyntaxTree.ParseText(code, path: file);

    var documents = syntax.GetRoot()
        .DescendantNodes()
        .OfType<ClassDeclarationSyntax>()
        .Where(c => c.AttributeLists.ContainsAttributeOfType("Document"));
    foreach (var document in documents)
    {
        var result = new CSharpPolicyCompiler(document).Compile();

        var formatter = new DiagnosticFormatter();
        numberOfErrors += result.Diagnostics.Count;
        foreach (var error in result.Diagnostics)
        {
            Console.Out.WriteLine(formatter.Format(error));
        }

        var codeBuilder = new StringBuilder();
        using (var writer = CustomXmlWriter.Create(codeBuilder, options.XmlWriterSettings))
        {
            writer.Write(new XComment(" This file is generated by the Azure API Management Policy Toolkit "));
            writer.Write(new XComment($" Version: {Assembly.GetExecutingAssembly().GetName().Version} "));
            writer.Write(result.Document);
        }

        var xml = codeBuilder.ToString();
        if (options.Format)
        {
            xml = RazorCodeFormatter.Format(xml);
        }

        var policyFileName = document.ExtractDocumentFileName();
        var extension = Path.GetExtension(policyFileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            policyFileName = $"{policyFileName}.{options.FileExtension}";
        }

        string targetFolder = Path.GetFullPath(Path.Combine(options.OutputFolder, Path.GetFullPath(file).Split(Path.GetFullPath(options.SourceFolder))[1].Replace(Path.GetFileName(file), "")));
        var targetFile = Path.Combine(targetFolder, policyFileName);
        var directoryPath = Path.GetDirectoryName(targetFile);
        
        if (directoryPath is not null && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
        File.WriteAllText(targetFile, xml);
        Console.Out.WriteLine($"File {targetFile} created");
    }

    Console.Out.WriteLine($"File {fileName} processed");
}

return numberOfErrors;