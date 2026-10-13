using AutoRunManager.Core.Model;
using AutoRunManager.Core.Platform;

namespace AutoRunManager.Core.Presentation;

/// <summary>
/// The short scope tag shown on a card: whose Startup folder a file is in, which registry hive (and
/// 32-bit view) a value is in, or whom a task runs as. Sections stay one per kind; the tag tells the
/// scopes apart.
/// </summary>
public static class EntryScope
{
    public const string User = "User";
    public const string AllUsers = "All users";
    public const string System = "System";

    // LocalSystem, LocalService, NetworkService: by SID and by the names task definitions store.
    private static readonly string[] SystemAccounts =
        ["S-1-5-18", "S-1-5-19", "S-1-5-20", "SYSTEM", "LOCAL SERVICE", "LOCALSERVICE", "NETWORK SERVICE", "NETWORKSERVICE"];

    private static readonly string[] UsersGroup = ["S-1-5-32-545", "Users"];

    /// <summary>
    /// The tag for an entry: computed from the locator for Startup folder files and registry values;
    /// for other kinds the tag the source set (tasks, see <see cref="TaskTag"/>), or null (Store apps,
    /// services, WMI).
    /// </summary>
    public static string? Tag(StartupEntry entry, SystemContext context) => entry.Locator switch
    {
        FileLocator file when entry.Kind == EntryKind.StartupFolder => FolderTag(file.Path, context),
        RegistryValueLocator value => RegistryTag(value),
        _ => entry.Scope,
    };

    /// <summary>
    /// Whom a scheduled task runs as: <c>User</c> (the current user), <c>System</c> (LocalSystem,
    /// LocalService, NetworkService), another user's name, <c>All users</c> (the Users group) or
    /// another group's name; null without a principal.
    /// </summary>
    public static string? TaskTag(string? userId, string? groupId, SystemContext context)
    {
        if (!string.IsNullOrEmpty(userId))
        {
            if (context.IsCurrentUser(userId))
            {
                return User;
            }

            return SystemAccounts.Contains(AccountName(userId), StringComparer.OrdinalIgnoreCase) ? System : AccountName(userId);
        }

        if (string.IsNullOrEmpty(groupId))
        {
            return null;
        }

        return UsersGroup.Contains(AccountName(groupId), StringComparer.OrdinalIgnoreCase) ? AllUsers : AccountName(groupId);
    }

    /// <summary>Account name without its domain (<c>NT AUTHORITY\SYSTEM</c> → <c>SYSTEM</c>).</summary>
    private static string AccountName(string principal) => principal[(principal.LastIndexOf('\\') + 1)..];

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
