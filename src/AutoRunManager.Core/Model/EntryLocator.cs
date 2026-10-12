using Microsoft.Win32;

namespace AutoRunManager.Core.Model;

/// <summary>Identifies the definition of an entry precisely enough to change or delete it.</summary>
public abstract record EntryLocator
{
    /// <summary>Human-readable location shown in the UI and used as the entry identity.</summary>
    public abstract string Display { get; }
}

/// <summary>
/// A registry value. Always read through the 64-bit view; 32-bit locations are addressed by their
/// explicit <c>WOW6432Node</c> path.
/// </summary>
public sealed record RegistryValueLocator(RegistryHive Hive, string KeyPath, string ValueName) : EntryLocator
{
    public override string Display => $@"{HiveName(Hive)}\{KeyPath} [{ValueName}]";

    public string KeyDisplay => $@"{HiveName(Hive)}\{KeyPath}";

    public static string HiveName(RegistryHive hive) => hive switch
    {
        RegistryHive.CurrentUser => "HKCU",
        RegistryHive.LocalMachine => "HKLM",
        RegistryHive.ClassesRoot => "HKCR",
        RegistryHive.Users => "HKU",
        _ => hive.ToString(),
    };

    /// <summary>Full key name as regedit shows it, e.g. <c>HKEY_CURRENT_USER\Software\...</c>.</summary>
    public string RegeditKeyName => Hive switch
    {
        RegistryHive.CurrentUser => @"HKEY_CURRENT_USER\" + KeyPath,
        RegistryHive.LocalMachine => @"HKEY_LOCAL_MACHINE\" + KeyPath,
        RegistryHive.ClassesRoot => @"HKEY_CLASSES_ROOT\" + KeyPath,
        RegistryHive.Users => @"HKEY_USERS\" + KeyPath,
        _ => KeyPath,
    };
}

public sealed record FileLocator(string Path) : EntryLocator
{
    public override string Display => Path;
}

/// <summary>A scheduled task by its full path, e.g. <c>\Vendor\Updater</c>.</summary>
public sealed record ScheduledTaskLocator(string TaskPath) : EntryLocator
{
    public override string Display => "Task Scheduler " + TaskPath;
}

public sealed record ServiceLocator(string ServiceName) : EntryLocator
{
    public override string Display => @"HKLM\SYSTEM\CurrentControlSet\Services\" + ServiceName;
}

public sealed record PackagedTaskLocator(string PackageFamilyName, string TaskId) : EntryLocator
{
    public override string Display => $"{PackageFamilyName} [{TaskId}]";
}

/// <summary>A WMI permanent event subscription: filter, consumer and the binding between them.</summary>
public sealed record WmiSubscriptionLocator(string FilterPath, string ConsumerPath, string BindingPath) : EntryLocator
{
    public override string Display => @"WMI root\subscription " + ConsumerPath;
}
