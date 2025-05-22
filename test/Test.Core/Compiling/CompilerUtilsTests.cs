// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Text;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class CompilerUtilsTests
{
    [TestMethod]
    // Basic cases
    [DataRow("UserPolicy", null, "UserPolicy.xml", DisplayName = "Simple filename")]
    [DataRow("UserPolicy.xml", null, "UserPolicy.xml", DisplayName = "Filename with extension")]
    [DataRow("CustomName", "UserPolicy", "CustomName.xml", DisplayName = "Custom name via attribute")]
    // Cases that require using Document attribute due to invalid class names
    [DataRow("/UserPolicy", "TestPolicy", "UserPolicy.xml", DisplayName = "Filename with leading slash")]
    [DataRow("/UserPolicy.xml", "TestPolicy", "UserPolicy.xml", DisplayName = "Filename with leading slash and extension")]
    [DataRow("/CustomName", "TestPolicy", "CustomName.xml", DisplayName = "Absolute path in attribute")]
    [DataRow("Folder/UserPolicy", "TestPolicy", "Folder/UserPolicy.xml", DisplayName = "Path with forward slash")]
    [DataRow("/Folder/UserPolicy", "TestPolicy", "Folder/UserPolicy.xml", DisplayName = "Path with leading forward slash")]
    [DataRow(@"Folder\UserPolicy", "TestPolicy", "Folder/UserPolicy.xml", DisplayName = "Path with backslash")]
    [DataRow(@"\Folder\UserPolicy", "TestPolicy", "Folder/UserPolicy.xml", DisplayName = "Path with leading backslash")]
    public void ExtractDocumentFileName_ShouldHandlePathsCorrectly(string expectedFileName, string className, string expectedResult)
    {
        // Arrange
        var documentAttribute = className != null 
            ? $"[Document(\"{expectedFileName}\")]\npublic class {className} {{}}"
            : $"public class {expectedFileName} {{}}";
        
        // Debug output to understand string content    
        var debugOutput = new StringBuilder();
        debugOutput.AppendLine($"Input filename: '{expectedFileName}'");
        debugOutput.AppendLine($"Document attribute: '{documentAttribute}'");
        Console.WriteLine(debugOutput.ToString());
            
        var syntaxTree = CSharpSyntaxTree.ParseText(documentAttribute);
        var root = syntaxTree.GetRoot();
        var classDeclaration = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();

        // Act
        var result = classDeclaration.ExtractDocumentFileName("xml");
        
        Console.WriteLine($"Result: '{result}'");

        // Assert
        Assert.AreEqual(expectedResult, result);
    }
}