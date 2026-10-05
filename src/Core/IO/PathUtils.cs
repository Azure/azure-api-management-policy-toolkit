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
        var normalizedPath = path
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .Replace(Path.DirectorySeparatorChar == '\\' ? '/' : '\\', Path.DirectorySeparatorChar);

        if (IsRootedOnAnyPlatform(normalizedPath))
        {
            throw new ArgumentException("The output path must be relative.", nameof(path));
        }

        if (normalizedPath.Split(Path.DirectorySeparatorChar).Contains(".."))
        {
            throw new ArgumentException("The output path cannot contain parent directory segments.", nameof(path));
        }

        return Path.HasExtension(normalizedPath) ? normalizedPath : Path.ChangeExtension(normalizedPath, extension);
    }

    public static string UnrootPath(string path)
    {
        return Path.IsPathRooted(path) ? Path.GetRelativePath(Path.GetPathRoot(path)!, path) : path;
    }

    internal static string GetPathWithinFolder(string folder, params string[] paths)
    {
        var fullFolder = Path.GetFullPath(folder);
        var normalizedPaths = paths.Select(path => path
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .Replace(Path.DirectorySeparatorChar == '\\' ? '/' : '\\', Path.DirectorySeparatorChar));

        var fullPath = Path.GetFullPath(Path.Combine([fullFolder, .. normalizedPaths]));
        if (!IsPathWithinFolder(fullFolder, fullPath) ||
            !IsPathWithinFolder(ResolveExistingLinks(fullFolder), ResolveExistingLinks(fullPath)))
        {
            throw new InvalidOperationException($"The output path '{fullPath}' is outside the output folder.");
        }

        return fullPath;
    }

    private static bool IsRootedOnAnyPlatform(string path)
    {
        return Path.IsPathRooted(path) ||
               path.StartsWith(Path.DirectorySeparatorChar) ||
               (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':');
    }

    private static bool IsPathWithinFolder(string folder, string path)
    {
        var relativePath = Path.GetRelativePath(folder, path);
        return !Path.IsPathRooted(relativePath) &&
               !relativePath.Equals("..", StringComparison.Ordinal) &&
               !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
               !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static string ResolveExistingLinks(string path)
    {
        var root = Path.GetPathRoot(path)!;
        var currentPath = root;
        var segments = path[root.Length..].Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            currentPath = Path.Combine(currentPath, segment);
            FileSystemInfo pathInfo = Directory.Exists(currentPath)
                ? new DirectoryInfo(currentPath)
                : new FileInfo(currentPath);

            if (pathInfo.LinkTarget is not null)
            {
                currentPath = pathInfo.ResolveLinkTarget(true)?.FullName
                    ?? throw new IOException($"Could not resolve the output path link '{currentPath}'.");
            }
        }

        return Path.GetFullPath(currentPath);
    }
}
