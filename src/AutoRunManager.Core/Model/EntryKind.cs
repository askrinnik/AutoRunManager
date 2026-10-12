namespace AutoRunManager.Core.Model;

/// <summary>Where an autostart entry is defined. See docs/design.md, "Autostart locations".</summary>
public enum EntryKind
{
    // Group A: shown by Task Manager and Settings, native toggle.
    RunKey,
    StartupFolder,
    PackagedApp,

    // Group B: other boot and logon launch points.
    ScheduledTask,
    Service,
    RunOnce,
    PolicyRun,
    Winlogon,
    WindowsLoad,
    ActiveSetup,
    TerminalServerRun,
    BootExecute,
    ImageHijack,
    AppInitDll,
    WmiSubscription,
}

public enum EntryGroup
{
    /// <summary>Group A: what Task Manager and Settings → Apps → Startup show.</summary>
    StartupApps,

    /// <summary>Group B: other places that start programs at boot or logon.</summary>
    LaunchPoints,
}

public static class EntryKindExtensions
{
    public static EntryGroup Group(this EntryKind kind) => kind switch
    {
        EntryKind.RunKey or EntryKind.StartupFolder or EntryKind.PackagedApp => EntryGroup.StartupApps,
        _ => EntryGroup.LaunchPoints,
    };

    public static string DisplayName(this EntryKind kind) => kind switch
    {
        EntryKind.RunKey => "Registry",
        EntryKind.StartupFolder => "Startup folder",
        EntryKind.PackagedApp => "Store apps",
        EntryKind.ScheduledTask => "Task Scheduler",
        EntryKind.Service => "Services",
        EntryKind.RunOnce => "RunOnce",
        EntryKind.PolicyRun => "Policy Run",
        EntryKind.Winlogon => "Winlogon",
        EntryKind.WindowsLoad => "Windows Load/Run",
        EntryKind.ActiveSetup => "Active Setup",
        EntryKind.TerminalServerRun => "Terminal Server Run",
        EntryKind.BootExecute => "Boot Execute",
        EntryKind.ImageHijack => "Image hijacks",
        EntryKind.AppInitDll => "AppInit DLLs",
        EntryKind.WmiSubscription => "WMI subscriptions",
        _ => kind.ToString(),
    };
}
