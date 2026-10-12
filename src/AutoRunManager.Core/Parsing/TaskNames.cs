using System.Text.RegularExpressions;

namespace AutoRunManager.Core.Parsing;

public static partial class TaskNames
{
    /// <summary>
    /// Display name for a scheduled task: drops GUIDs in braces and the user SID suffix that
    /// updaters append (<c>GoogleUpdateTaskUser-S-1-5-21-...</c> → <c>GoogleUpdateTaskUser</c>).
    /// </summary>
    public static string Clean(string taskName)
    {
        string name = GuidPattern().Replace(taskName, "");
        name = SidSuffixPattern().Replace(name, "");
        name = name.Trim();
        return name.Length > 0 ? name : taskName;
    }

    /// <summary>Splits <c>\Folder\Sub\Name</c> into folder (<c>\Folder\Sub</c>, root is <c>\</c>) and name.</summary>
    public static (string Folder, string Name) Split(string taskPath)
    {
        int slash = taskPath.LastIndexOf('\\');
        if (slash < 0)
        {
            return (@"\", taskPath);
        }

        string folder = slash == 0 ? @"\" : taskPath[..slash];
        return (folder, taskPath[(slash + 1)..]);
    }

    /// <summary>Tasks under <c>\Microsoft\</c> belong to Windows and are not listed.</summary>
    public static bool IsWindowsTask(string taskPath) =>
        taskPath.StartsWith(@"\Microsoft\", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"\{.*?\}")]
    private static partial Regex GuidPattern();

    [GeneratedRegex(@"-S-1-5-.*$")]
    private static partial Regex SidSuffixPattern();
}
