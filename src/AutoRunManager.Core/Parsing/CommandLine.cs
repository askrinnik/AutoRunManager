using System.Text.RegularExpressions;

namespace AutoRunManager.Core.Parsing;

public static partial class CommandLine
{
    /// <summary>
    /// Extracts the program file from a command line as stored in Run keys, services, tasks and
    /// similar places: expands environment variables, handles quotes, kernel-style prefixes
    /// (<c>\??\</c>, <c>\SystemRoot\</c>) and paths relative to the Windows folder
    /// (<c>system32\...</c>), and cuts off arguments after a known file extension.
    /// </summary>
    /// <returns>The path, or <see langword="null"/> for an empty command. The path may be bare
    /// (<c>rundll32.exe</c>) or not exist.</returns>
    public static string? GetExecutablePath(string? command) =>
        GetExecutablePath(command, Environment.GetFolderPath(Environment.SpecialFolder.Windows));

    internal static string? GetExecutablePath(string? command, string windowsDirectory)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        string text = Environment.ExpandEnvironmentVariables(command).Trim();
        windowsDirectory = windowsDirectory.TrimEnd('\\');

        if (text.StartsWith(@"\??\", StringComparison.Ordinal))
        {
            text = text[4..];
        }

        if (text.StartsWith(@"\SystemRoot\", StringComparison.OrdinalIgnoreCase))
        {
            text = windowsDirectory + text[@"\SystemRoot".Length..];
        }
        else if (text.StartsWith(@"system32\", StringComparison.OrdinalIgnoreCase))
        {
            text = windowsDirectory + @"\" + text;
        }

        if (text.StartsWith('"'))
        {
            // Some installers double the quotes: ""C:\...\app.exe"".
            string inner = text.TrimStart('"');
            int end = inner.IndexOf('"');
            return (end >= 0 ? inner[..end] : inner).Trim();
        }

        Match match = ExecutablePattern().Match(text);
        return match.Success ? match.Groups["path"].Value.Trim() : text;
    }

    // Shortest prefix ending in a known extension that is followed by whitespace, a comma
    // (rundll32 entry points), a quote or the end.
    [GeneratedRegex(
        @"^(?<path>.+?\.(exe|com|lnk|url|cmd|bat|vbs|vbe|js|jse|wsf|ps1|msi|dll|cpl|scr|sys|ocx))(?=[\s,""]|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExecutablePattern();
}
