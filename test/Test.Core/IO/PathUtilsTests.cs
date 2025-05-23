// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.ApiManagement.PolicyToolkit.IO;

namespace Test.Core.IO;

[TestClass]
public class PathUtilsTests
{
    [TestMethod]
    [DynamicData(nameof(PrepareOutputPath_ShouldHandlePathsCorrectly_Data), DynamicDataSourceType.Method)]
    public void PrepareOutputPath_ShouldHandlePathsCorrectly(string actualPath, string extension, string expectedPath)
    {
        var actual = PathUtils.PrepareOutputPath(actualPath, extension);
        Path.GetFullPath(actual).Should().Be(Path.GetFullPath(expectedPath));
    }

    public static IEnumerable<object[]> PrepareOutputPath_ShouldHandlePathsCorrectly_Data()
    {
        yield return ["UserPolicy", "xml", "UserPolicy.xml"];
        yield return ["UserPolicy", ".xml", "UserPolicy.xml"];
        yield return ["UserPolicy", "cshtml", "UserPolicy.cshtml"];
        yield return ["UserPolicy", ".cshtml", "UserPolicy.cshtml"];
        yield return ["UserPolicy.cshtml", "xml", "UserPolicy.cshtml"];
        yield return [$"{Path.DirectorySeparatorChar}UserPolicy", "xml", "UserPolicy.xml"];
        yield return [$"{Path.AltDirectorySeparatorChar}UserPolicy", "xml", "UserPolicy.xml"];
        yield return
        [
            $"Folder{Path.DirectorySeparatorChar}UserPolicy", "xml",
            $"Folder{Path.DirectorySeparatorChar}UserPolicy.xml"
        ];
        yield return
        [
            $"Folder{Path.AltDirectorySeparatorChar}UserPolicy", "xml",
            $"Folder{Path.DirectorySeparatorChar}UserPolicy.xml"
        ];
        yield return
        [
            $"{Path.DirectorySeparatorChar}Folder{Path.DirectorySeparatorChar}UserPolicy", "xml",
            $"Folder{Path.DirectorySeparatorChar}UserPolicy.xml"
        ];
        yield return
        [
            $"{Path.AltDirectorySeparatorChar}Folder{Path.AltDirectorySeparatorChar}UserPolicy", "xml",
            $"Folder{Path.DirectorySeparatorChar}UserPolicy.xml"
        ];
        yield return
        [
            $"{Path.DirectorySeparatorChar}Folder{Path.AltDirectorySeparatorChar}UserPolicy", "xml",
            $"Folder{Path.DirectorySeparatorChar}UserPolicy.xml"
        ];
        yield return
        [
            $"{Path.AltDirectorySeparatorChar}Folder{Path.DirectorySeparatorChar}UserPolicy", "xml",
            $"Folder{Path.DirectorySeparatorChar}UserPolicy.xml"
        ];
        yield return
        [
            $"Folder{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}UserPolicy", "xml",
            $"Folder{Path.DirectorySeparatorChar}UserPolicy.xml"
        ];
        yield return
        [
            $"Folder{Path.AltDirectorySeparatorChar}{Path.AltDirectorySeparatorChar}UserPolicy", "xml",
            $"Folder{Path.DirectorySeparatorChar}UserPolicy.xml"
        ];
    }

    [TestMethod]
    [DynamicData(nameof(IsNotInObjOrBinFolder_ChecksCorrectly_Data), DynamicDataSourceType.Method)]
    public void IsNotInObjOrBinFolder_ChecksCorrectly(string path, bool expected)
    {
        PathUtils.IsNotInObjOrBinFolder(path).Should().Be(expected);
    }

    public static IEnumerable<object[]> IsNotInObjOrBinFolder_ChecksCorrectly_Data()
    {
        yield return [$"UserFile.cs", true];
        yield return [$"a{Path.DirectorySeparatorChar}UserFile.cs", true];
        yield return [$"a{Path.AltDirectorySeparatorChar}UserFile.cs", true];
        yield return [$"a{Path.DirectorySeparatorChar}b{Path.DirectorySeparatorChar}UserFile.cs", true];
        yield return [$"a{Path.AltDirectorySeparatorChar}b{Path.DirectorySeparatorChar}UserFile.cs", true];
        yield return
        [
            $"{Path.AltDirectorySeparatorChar}a{Path.AltDirectorySeparatorChar}b{Path.AltDirectorySeparatorChar}UserFile.cs",
            true
        ];
        yield return
        [
            $"{Path.DirectorySeparatorChar}a{Path.DirectorySeparatorChar}b{Path.DirectorySeparatorChar}UserFile.cs",
            true
        ];
        yield return [$"bin{Path.DirectorySeparatorChar}NotUserFile.cs", false];
        yield return [$"obj{Path.DirectorySeparatorChar}NotUserFile.cs", false];
        yield return [$"bin{Path.AltDirectorySeparatorChar}NotUserFile.cs", false];
        yield return [$"obj{Path.AltDirectorySeparatorChar}NotUserFile.cs", false];
        yield return [$"a{Path.AltDirectorySeparatorChar}bin{Path.AltDirectorySeparatorChar}NotUserFile.cs", false];
        yield return [$"a{Path.AltDirectorySeparatorChar}obj{Path.AltDirectorySeparatorChar}NotUserFile.cs", false];
        yield return [$"a{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}NotUserFile.cs", false];
        yield return [$"a{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}NotUserFile.cs", false];
    }
}