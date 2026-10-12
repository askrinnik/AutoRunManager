namespace AutoRunManager.Core.Model;

public enum EntryState
{
    Enabled,
    Disabled,

    /// <summary>Runs whenever it is present; Windows has no switch for it.</summary>
    Active,
}

/// <summary>One autostart entry, as collected by a source.</summary>
/// <param name="Name">Display name.</param>
/// <param name="Command">Command line or path as stored by Windows.</param>
/// <param name="ExecutablePath">Program file extracted from <paramref name="Command"/>, if any; used for the icon.</param>
/// <param name="CanToggle">Whether the app can turn the entry off and on.</param>
/// <param name="CanDelete">Whether the app can delete the entry.</param>
/// <param name="RequiresAdmin">Whether changing the entry needs an elevated process.</param>
/// <param name="Details">Extra text for the tooltip (task triggers, service start type, ...).</param>
public sealed record StartupEntry(
    EntryKind Kind,
    string Name,
    string Command,
    string? ExecutablePath,
    EntryLocator Locator,
    EntryState State,
    bool CanToggle,
    bool CanDelete,
    bool RequiresAdmin,
    string? Details = null)
{
    public string Id => $"{Kind}|{Locator.Display}";

    public bool IsEnabled => State != EntryState.Disabled;
}
