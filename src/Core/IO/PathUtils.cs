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

    public static string PrepareOutputPath(string path, string extension)
    {
        var unrootedPath = UnrootPath(path);
        return Path.HasExtension(path) ? path : Path.ChangeExtension(unrootedPath, extension);
    }

    public static string UnrootPath(string path)
    {
        return Path.IsPathRooted(path) ? Path.GetRelativePath(Path.GetPathRoot(path)!, path) : path;
    }
}