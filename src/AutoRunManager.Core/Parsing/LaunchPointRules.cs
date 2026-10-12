namespace AutoRunManager.Core.Parsing;

/// <summary>Default values of system launch points; only values that differ are listed.</summary>
public static class LaunchPointRules
{
    public const string DefaultBootExecute = "autocheck autochk *";

    /// <summary><c>Winlogon\Shell</c> is <c>explorer.exe</c> by default.</summary>
    public static bool IsDefaultShell(string? value, string windowsDirectory)
    {
        // Compare the whole value: "explorer.exe, other.exe" starts a second program.
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        string shell = Environment.ExpandEnvironmentVariables(value).Trim().Trim('"');
        return shell.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase)
            || shell.Equals(Path.Combine(windowsDirectory, "explorer.exe"), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary><c>Winlogon\Userinit</c> is <c>%windir%\system32\userinit.exe,</c> by default.</summary>
    public static bool IsDefaultUserinit(string? value, string windowsDirectory)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        string[] programs = Environment.ExpandEnvironmentVariables(value)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string expected = Path.Combine(windowsDirectory, "system32", "userinit.exe");
        return programs is [string only]
            && (only.Equals(expected, StringComparison.OrdinalIgnoreCase) || only.Equals("userinit.exe", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Lines of a Session Manager <c>BootExecute</c>-style value other than the default check-disk line.</summary>
    public static IReadOnlyList<string> NonDefaultBootLines(IEnumerable<string>? lines) =>
        lines?
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.Equals(DefaultBootExecute, StringComparison.OrdinalIgnoreCase))
            .ToList()
        ?? [];
}
