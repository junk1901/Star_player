namespace StarPlayer.SimHarness;

/// <summary>
/// F-C2-A path safety beyond string prefixes: a junction or symlink makes a path that is textually inside a root really live elsewhere. The official seed and output paths are refused when any
/// EXISTING component from the volume root down (every ancestor of the root, the root itself, then each component below it) is a reparse point (link, junction, mount point); a component that does
/// not exist yet is new and cannot be a link. Anything that cannot be inspected is refused (fail-closed). This is a check at the moment of use, not a lock: the official write path (F-C2-B) still needs handle-based, atomic creation to close the check-to-use window.
/// </summary>
internal static class PathSafety
{
    /// <summary>The first path (the volume root, each ancestor of the root down to the root, then each segment in turn) that is a reparse point or cannot be inspected; null when none is.</summary>
    public static string? FirstReparsePoint(string root, IEnumerable<string> segments)
    {
        var current = Path.GetFullPath(root);
        var ancestors = new List<string>();
        for (var d = new DirectoryInfo(current); d is not null; d = d.Parent)
            ancestors.Insert(0, d.FullName);
        foreach (var path in ancestors)
            switch (IsLinkOrUnreadable(path))
            {
                case null: return null;                                                       // the root does not exist: nothing below it can be read
                case true: return path;
            }

        foreach (var segment in segments)
        {
            current = Path.Combine(current, segment);
            switch (IsLinkOrUnreadable(current))
            {
                case null: return null;                                                       // not there yet: everything below it is new
                case true: return current;
            }
        }

        return null;
    }

    // true: a link (or unreadable, which is refused alike); false: a plain file or directory; null: does not exist.
    private static bool? IsLinkOrUnreadable(string path)
    {
        try
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);   // reads the entry itself, so a dangling link is still seen as a link
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return true;
        }
    }
}
