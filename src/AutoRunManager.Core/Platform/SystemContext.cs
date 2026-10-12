using System.Security.Principal;

namespace AutoRunManager.Core.Platform;

/// <summary>Folders and the current user, injected so sources can be tested with fakes.</summary>
/// <param name="UserName">Current user as <c>DOMAIN\name</c>.</param>
public sealed record SystemContext(
    string WindowsDirectory,
    string UserStartupFolder,
    string CommonStartupFolder,
    string UserName,
    string UserSid)
{
    public static SystemContext Current()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new SystemContext(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
            identity.Name,
            identity.User?.Value ?? "");
    }

    /// <summary>
    /// Whether a task principal (<c>DOMAIN\name</c>, bare name or SID) is the current user.
    /// </summary>
    public bool IsCurrentUser(string? principal)
    {
        if (string.IsNullOrEmpty(principal))
        {
            return false;
        }

        if (principal.Equals(UserName, StringComparison.OrdinalIgnoreCase) || principal.Equals(UserSid, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        int slash = UserName.IndexOf('\\');
        return !principal.Contains('\\') && slash >= 0 && principal.Equals(UserName[(slash + 1)..], StringComparison.OrdinalIgnoreCase);
    }
}
