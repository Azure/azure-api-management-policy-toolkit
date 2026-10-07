// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.IO;

public static class PathUtils
{
    public static bool IsNotInObjOrBinFolder(string path)
    {
        return path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .All(d => !d.Equals("obj", StringComparison.OrdinalIgnoreCase) &&
                      !d.Equals("bin", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks only the folders below <paramref name="rootFolder"/> (the project or source folder),
    /// so a project which itself is located under a folder named <c>bin</c> or <c>obj</c> is not excluded.
    /// </summary>
    public static bool IsNotInObjOrBinFolder(string path, string rootFolder)
    {
        return IsNotInObjOrBinFolder(Path.GetRelativePath(Path.GetFullPath(rootFolder), Path.GetFullPath(path)));
    }

    public static string PrepareOutputPath(string path, string extension)
    {
        var normalizedPath = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var unrootedPath = UnrootPath(normalizedPath);
        return Path.HasExtension(unrootedPath) ? unrootedPath : Path.ChangeExtension(unrootedPath, extension);
    }

    public static string UnrootPath(string path)
    {
        return Path.IsPathRooted(path) ? Path.GetRelativePath(Path.GetPathRoot(path)!, path) : path;
    }
}