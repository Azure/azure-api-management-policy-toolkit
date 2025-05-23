// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.IO;

namespace Test.Core.IO;

[TestClass]
public class PathUtilsTests
{
    [TestMethod]
    [DataRow("UserPolicy", "xml", "UserPolicy.xml")]
    [DataRow("UserPolicy", ".xml", "UserPolicy.xml")]
    [DataRow("UserPolicy", "cshtml", "UserPolicy.cshtml")]
    [DataRow("UserPolicy", ".cshtml", "UserPolicy.cshtml")]
    [DataRow("UserPolicy.cshtml", "xml", "UserPolicy.cshtml")]
    [DataRow(@"\UserPolicy", "xml", @"UserPolicy.xml")]
    [DataRow(@"/UserPolicy", "xml", @"UserPolicy.xml")]
    [DataRow(@"Folder\UserPolicy", "xml", @"Folder/UserPolicy.xml")]
    [DataRow(@"Folder/UserPolicy", "xml", @"Folder/UserPolicy.xml")]
    [DataRow(@"\Folder\UserPolicy", "xml", @"Folder/UserPolicy.xml")]
    [DataRow(@"\Folder/UserPolicy", "xml", @"Folder/UserPolicy.xml")]
    [DataRow(@"/Folder/UserPolicy", "xml", @"Folder/UserPolicy.xml")]
    [DataRow(@"/Folder//UserPolicy", "xml", @"Folder/UserPolicy.xml")]
    public void PrepareOutputPath_ShouldHandlePathsCorrectly(string actualPath, string extension, string expectedPath)
    {
        var actual = PathUtils.PrepareOutputPath(actualPath, extension);
        Path.GetFullPath(actual).Should().Be(Path.GetFullPath(expectedPath));
    }

    [TestMethod]
    [DataRow(@"UserFile.cs", true)]
    [DataRow(@"a\UserFile.cs", true)]
    [DataRow(@"a/UserFile.cs", true)]
    [DataRow(@"a\b\UserFile.cs", true)]
    [DataRow(@"a/b/UserFile.cs", true)]
    [DataRow(@"/a/b/UserFile.cs", true)]
    [DataRow(@"\a\b\UserFile.cs", true)]
    [DataRow(@"bin\NotUserFile.cs", false)]
    [DataRow(@"obj\NotUserFile.cs", false)]
    [DataRow(@"bin/NotUserFile.cs", false)]
    [DataRow(@"obj/NotUserFile.cs", false)]
    [DataRow(@"a/bin/NotUserFile.cs", false)]
    [DataRow(@"a/obj/NotUserFile.cs", false)]
    [DataRow(@"a\bin\NotUserFile.cs", false)]
    [DataRow(@"a\obj\NotUserFile.cs", false)]
    public void IsNotInObjOrBinFolder_ChecksCorrectly(string path, bool expected)
    {
        PathUtils.IsNotInObjOrBinFolder(path).Should().Be(expected);
    }
}