using AutoRunManager.Core.Model;
using AutoRunManager.Core.Platform;

namespace AutoRunManager.Core.Presentation;

/// <summary>
/// The short scope tag shown on a card: whose Startup folder a file is in, or which registry hive
/// (and 32-bit view) a value is in. Sections stay one per kind; the tag tells the scopes apart.
/// </summary>
public static class EntryScope
{
    public const string User = "User";
    public const string AllUsers = "All users";
    public const string Hklm32 = "HKLM 32-bit";

    /// <summary>The tag for an entry, or null for kinds without a meaningful scope (Store apps, tasks, services, WMI).</summary>
    public static string? Tag(StartupEntry entry, SystemContext context) => entry.Locator switch
    {
        FileLocator file when entry.Kind == EntryKind.StartupFolder => FolderTag(file.Path, context),
        RegistryValueLocator value => RegistryTag(value),
        _ => null,
    };

    private static string? FolderTag(string path, SystemContext context)
    {
        string? folder = Path.GetDirectoryName(path);
        if (SameFolder(folder, context.UserStartupFolder))
        {
            return User;
        }

        return SameFolder(folder, context.CommonStartupFolder) ? AllUsers : null;
    }

    private static string RegistryTag(RegistryValueLocator value)
    {
        string hive = RegistryValueLocator.HiveName(value.Hive);
        bool wow64 = value.KeyPath.Split('\\').Contains("WOW6432Node", StringComparer.OrdinalIgnoreCase);
        return wow64 ? $"{hive} 32-bit" : hive;
    }

    private static bool SameFolder(string? a, string b) =>
        a is not null && string.Equals(a.TrimEnd('\\'), b.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
}
