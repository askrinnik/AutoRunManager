using AutoRunManager.Core.Model;
using AutoRunManager.Core.Parsing;
using AutoRunManager.Core.Platform;

using Microsoft.Win32;

namespace AutoRunManager.Core.Sources;

/// <summary>
/// Group B registry launch points without a native toggle. Entries are read-only; most can be
/// deleted, but system values (Winlogon, BootExecute, AppInit_DLLs) only differ from their defaults
/// and are never deleted.
/// </summary>
public sealed class RegistryLaunchPointSource(IRegistry registry, SystemContext context) : IEntrySource
{
    private const string CurrentVersion = @"Software\Microsoft\Windows\CurrentVersion";
    private const string Wow64CurrentVersion = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion";
    private const string NtCurrentVersion = @"Software\Microsoft\Windows NT\CurrentVersion";
    private const string Wow64NtCurrentVersion = @"Software\WOW6432Node\Microsoft\Windows NT\CurrentVersion";
    private const string TerminalServerInstall = NtCurrentVersion + @"\Terminal Server\Install\" + CurrentVersion;

    /// <summary>Keys whose every value is a command.</summary>
    internal static readonly IReadOnlyList<(EntryKind Kind, RegistryHive Hive, string KeyPath)> ValueKeys =
    [
        (EntryKind.RunOnce, RegistryHive.CurrentUser, CurrentVersion + @"\RunOnce"),
        (EntryKind.RunOnce, RegistryHive.LocalMachine, CurrentVersion + @"\RunOnce"),
        (EntryKind.RunOnce, RegistryHive.LocalMachine, Wow64CurrentVersion + @"\RunOnce"),
        (EntryKind.RunServices, RegistryHive.CurrentUser, CurrentVersion + @"\RunServices"),
        (EntryKind.RunServices, RegistryHive.CurrentUser, CurrentVersion + @"\RunServicesOnce"),
        (EntryKind.RunServices, RegistryHive.LocalMachine, CurrentVersion + @"\RunServices"),
        (EntryKind.RunServices, RegistryHive.LocalMachine, CurrentVersion + @"\RunServicesOnce"),
        (EntryKind.RunServices, RegistryHive.LocalMachine, Wow64CurrentVersion + @"\RunServices"),
        (EntryKind.RunServices, RegistryHive.LocalMachine, Wow64CurrentVersion + @"\RunServicesOnce"),
        (EntryKind.PolicyRun, RegistryHive.CurrentUser, CurrentVersion + @"\Policies\Explorer\Run"),
        (EntryKind.PolicyRun, RegistryHive.LocalMachine, CurrentVersion + @"\Policies\Explorer\Run"),
        (EntryKind.PolicyRun, RegistryHive.LocalMachine, Wow64CurrentVersion + @"\Policies\Explorer\Run"),
        (EntryKind.TerminalServerRun, RegistryHive.LocalMachine, TerminalServerInstall + @"\Run"),
        (EntryKind.TerminalServerRun, RegistryHive.LocalMachine, TerminalServerInstall + @"\RunOnce"),
    ];

    /// <summary>Keys whose subkeys hold command values (<c>RunOnceEx\0001\...</c>).</summary>
    internal static readonly IReadOnlyList<(RegistryHive Hive, string KeyPath)> RunOnceExKeys =
    [
        (RegistryHive.CurrentUser, CurrentVersion + @"\RunOnceEx"),
        (RegistryHive.LocalMachine, CurrentVersion + @"\RunOnceEx"),
        (RegistryHive.LocalMachine, Wow64CurrentVersion + @"\RunOnceEx"),
    ];

    /// <summary>RunServices keys come from Windows 9x; listed for completeness only.</summary>
    internal const string LegacyNote = "Legacy Windows 9x key: Windows 10 and 11 do not run entries from here.";

    public string Name => "Registry launch points";

    public IEnumerable<StartupEntry> Enumerate()
    {
        foreach ((EntryKind kind, RegistryHive hive, string keyPath) in ValueKeys)
        {
            foreach (StartupEntry entry in AllValues(kind, hive, keyPath))
            {
                yield return entry;
            }
        }

        foreach ((RegistryHive hive, string keyPath) in RunOnceExKeys)
        {
            foreach (string subKey in registry.GetSubKeyNames(hive, keyPath))
            {
                foreach (StartupEntry entry in AllValues(EntryKind.RunOnce, hive, $@"{keyPath}\{subKey}"))
                {
                    yield return entry;
                }
            }
        }

        foreach (StartupEntry entry in Winlogon().Concat(WindowsLoad()).Concat(ActiveSetup()).Concat(BootExecute())
            .Concat(ImageHijacks()).Concat(AppInitDlls()))
        {
            yield return entry;
        }
    }

    private IEnumerable<StartupEntry> AllValues(EntryKind kind, RegistryHive hive, string keyPath)
    {
        foreach (string valueName in registry.GetValueNames(hive, keyPath))
        {
            string? command = registry.GetString(hive, keyPath, valueName);
            if (string.IsNullOrWhiteSpace(command))
            {
                continue;
            }

            yield return Entry(kind, valueName.Length > 0 ? valueName : "(Default)", command, hive, keyPath, valueName, canDelete: true,
                details: kind == EntryKind.RunServices ? LegacyNote : null);
        }
    }

    private IEnumerable<StartupEntry> Winlogon()
    {
        string key = NtCurrentVersion + @"\Winlogon";
        string? shell = registry.GetString(RegistryHive.LocalMachine, key, "Shell");
        if (!LaunchPointRules.IsDefaultShell(shell, context.WindowsDirectory))
        {
            yield return Entry(EntryKind.Winlogon, "Shell", shell!, RegistryHive.LocalMachine, key, "Shell", canDelete: false);
        }

        string? userinit = registry.GetString(RegistryHive.LocalMachine, key, "Userinit");
        if (!LaunchPointRules.IsDefaultUserinit(userinit, context.WindowsDirectory))
        {
            yield return Entry(EntryKind.Winlogon, "Userinit", userinit!, RegistryHive.LocalMachine, key, "Userinit", canDelete: false);
        }

        // Any per-user shell replaces Explorer for this user.
        string? userShell = registry.GetString(RegistryHive.CurrentUser, key, "Shell");
        if (!string.IsNullOrWhiteSpace(userShell))
        {
            yield return Entry(EntryKind.Winlogon, "Shell (user)", userShell, RegistryHive.CurrentUser, key, "Shell", canDelete: true);
        }
    }

    private IEnumerable<StartupEntry> WindowsLoad()
    {
        string key = NtCurrentVersion + @"\Windows";
        foreach (string valueName in (string[])["Load", "Run"])
        {
            string? command = registry.GetString(RegistryHive.CurrentUser, key, valueName);
            if (!string.IsNullOrWhiteSpace(command))
            {
                yield return Entry(EntryKind.WindowsLoad, valueName, command, RegistryHive.CurrentUser, key, valueName, canDelete: true);
            }
        }
    }

    private IEnumerable<StartupEntry> ActiveSetup()
    {
        foreach (string root in (string[])[@"Software\Microsoft\Active Setup\Installed Components", @"Software\WOW6432Node\Microsoft\Active Setup\Installed Components"])
        {
            foreach (string component in registry.GetSubKeyNames(RegistryHive.LocalMachine, root))
            {
                string key = $@"{root}\{component}";
                string? stubPath = registry.GetString(RegistryHive.LocalMachine, key, "StubPath");
                if (string.IsNullOrWhiteSpace(stubPath) || registry.GetInt(RegistryHive.LocalMachine, key, "IsInstalled") == 0 || IsInWindows(stubPath))
                {
                    continue;
                }

                string? title = registry.GetString(RegistryHive.LocalMachine, key, "");
                string name = string.IsNullOrWhiteSpace(title) || title.StartsWith('@') ? component : title;
                yield return Entry(EntryKind.ActiveSetup, name, stubPath, RegistryHive.LocalMachine, key, "StubPath", canDelete: true);
            }
        }
    }

    private IEnumerable<StartupEntry> BootExecute()
    {
        const string key = @"SYSTEM\CurrentControlSet\Control\Session Manager";
        foreach (string valueName in (string[])["BootExecute", "SetupExecute", "Execute"])
        {
            IReadOnlyList<string> lines = LaunchPointRules.NonDefaultBootLines(registry.GetValue(RegistryHive.LocalMachine, key, valueName) switch
            {
                string[] multi => multi,
                string single => [single],
                _ => null,
            });
            if (lines.Count > 0)
            {
                yield return Entry(EntryKind.BootExecute, valueName, string.Join("; ", lines), RegistryHive.LocalMachine, key, valueName, canDelete: false);
            }
        }
    }

    private IEnumerable<StartupEntry> ImageHijacks()
    {
        foreach (string root in (string[])[NtCurrentVersion + @"\Image File Execution Options", Wow64NtCurrentVersion + @"\Image File Execution Options"])
        {
            foreach (string image in registry.GetSubKeyNames(RegistryHive.LocalMachine, root))
            {
                string key = $@"{root}\{image}";
                string? debugger = registry.GetString(RegistryHive.LocalMachine, key, "Debugger");
                if (!string.IsNullOrWhiteSpace(debugger))
                {
                    yield return Entry(EntryKind.ImageHijack, $"{image} → debugger", debugger, RegistryHive.LocalMachine, key, "Debugger", canDelete: true,
                        details: $"Starting {image} runs this program instead.");
                }
            }
        }
    }

    private IEnumerable<StartupEntry> AppInitDlls()
    {
        foreach (string key in (string[])[NtCurrentVersion + @"\Windows", Wow64NtCurrentVersion + @"\Windows"])
        {
            string? dlls = registry.GetString(RegistryHive.LocalMachine, key, "AppInit_DLLs");
            if (registry.GetInt(RegistryHive.LocalMachine, key, "LoadAppInit_DLLs") == 1 && !string.IsNullOrWhiteSpace(dlls))
            {
                yield return Entry(EntryKind.AppInitDll, "AppInit_DLLs", dlls, RegistryHive.LocalMachine, key, "AppInit_DLLs", canDelete: false,
                    details: "Loaded into every process that loads user32.dll.");
            }
        }
    }

    private StartupEntry Entry(EntryKind kind, string name, string command, RegistryHive hive, string keyPath, string valueName, bool canDelete, string? details = null) =>
        new(
            kind,
            name,
            command,
            CommandLine.GetExecutablePath(command, context.WindowsDirectory),
            new RegistryValueLocator(hive, keyPath, valueName),
            EntryState.Active,
            CanToggle: false,
            CanDelete: canDelete,
            RequiresAdmin: hive == RegistryHive.LocalMachine,
            Details: details);

    // Bare names (rundll32.exe, regsvr32.exe) resolve to System32 as well.
    private bool IsInWindows(string command) =>
        CommandLine.GetExecutablePath(command, context.WindowsDirectory) is { } path
        && (!path.Contains('\\') || path.StartsWith(context.WindowsDirectory.TrimEnd('\\') + @"\", StringComparison.OrdinalIgnoreCase));
}
