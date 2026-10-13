using System.IO;

using AutoRunManager.Core.Model;
using AutoRunManager.Core.Platform;
using AutoRunManager.Core.Presentation;
using AutoRunManager.Core.Sources;

using Microsoft.Win32;

namespace AutoRunManager.Screenshots;

/// <summary>
/// Made-up entries for the README screenshots, so they show nothing of the machine they are taken on.
/// Executable paths point at files every Windows has, only to get their icons.
/// </summary>
internal sealed class DemoSource : IEntrySource
{
    private const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Winlogon = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
    private const string System32 = @"C:\Windows\System32\";
    private const string EdgeFolder = @"C:\Program Files (x86)\Microsoft\Edge\Application\";
    private const string EdgeUpdate = @"C:\Program Files (x86)\Microsoft\EdgeUpdate\MicrosoftEdgeUpdate.exe";

    public static readonly SystemContext Context = new(
        @"C:\Windows",
        @"C:\Users\Alex\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup",
        @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\StartUp",
        @"DEMO\Alex",
        "S-1-5-21-1000-1000-1000-1001");

    public string Name => "Demo";

    public IEnumerable<StartupEntry> Enumerate() =>
    [
        RunValue(RegistryHive.CurrentUser, Run, "MicrosoftEdgeAutoLaunch", $"\"{EdgeFolder}msedge.exe\" --no-startup-window --win-session-start", EdgeFolder + "msedge.exe", EntryState.Enabled),
        RunValue(RegistryHive.CurrentUser, Run, "OneDrive", @"""C:\Users\Alex\AppData\Local\Microsoft\OneDrive\OneDrive.exe"" /background", System32 + "OneDriveSetup.exe", EntryState.Disabled),
        RunValue(RegistryHive.LocalMachine, Run, "SecurityHealth", @"%windir%\system32\SecurityHealthSystray.exe", System32 + "SecurityHealthSystray.exe", EntryState.Enabled),
        RunValue(RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "Contoso Updater", @"""C:\Program Files (x86)\Contoso\Updater\updater.exe"" /silent", System32 + "msiexec.exe", EntryState.Disabled),
        StartupFile(Context.UserStartupFolder, "Command Prompt.lnk", System32 + "cmd.exe", EntryState.Enabled, admin: false),
        StartupFile(Context.CommonStartupFolder, "Notepad.lnk", System32 + "notepad.exe", EntryState.Disabled, admin: true),
        new(EntryKind.PackagedApp, "Windows Terminal", "Microsoft.WindowsTerminal_8wekyb3d8bbwe", System32 + "cmd.exe",
            new PackagedTaskLocator("Microsoft.WindowsTerminal_8wekyb3d8bbwe", "StartTerminalOnLoginTask"), EntryState.Disabled,
            CanToggle: true, CanDelete: false, RequiresAdmin: false),

        Task(@"\MicrosoftEdgeUpdateTaskMachineCore", "MicrosoftEdgeUpdateTaskMachineCore", $"\"{EdgeUpdate}\" /c", EdgeUpdate, EntryState.Enabled, EntryScope.System),
        Task(@"\MicrosoftEdgeUpdateTaskMachineUA", "MicrosoftEdgeUpdateTaskMachineUA", $"\"{EdgeUpdate}\" /ua /installsource scheduler", EdgeUpdate, EntryState.Enabled, EntryScope.System),
        Task(@"\Backup documents", "Backup documents", @"powershell.exe -NoProfile -File C:\Scripts\backup.ps1", System32 + @"WindowsPowerShell\v1.0\powershell.exe", EntryState.Enabled, EntryScope.User),
        Task(@"\Cleanup downloads", "Cleanup downloads", @"cmd.exe /c del /q C:\Temp\*.tmp", System32 + "cmd.exe", EntryState.Disabled, EntryScope.User),
        Task(@"\Contoso\Update check", "Update check", @"""C:\Program Files (x86)\Contoso\Updater\updater.exe"" /check", System32 + "msiexec.exe", EntryState.Enabled, EntryScope.AllUsers),

        Service("edgeupdate", "Microsoft Edge Update Service (edgeupdate)", $"\"{EdgeUpdate}\" /svc", EdgeUpdate, EntryState.Enabled, "Automatic (delayed start)"),
        Service("MicrosoftEdgeElevationService", "Microsoft Edge Elevation Service", $"\"{EdgeFolder}elevation_service.exe\"", EdgeFolder + "msedge.exe", EntryState.Disabled, "Manual"),
        Service("ContosoSync", "Contoso Sync Agent", @"""C:\Program Files\Contoso\Sync\SyncAgent.exe"" -service", System32 + "msiexec.exe", EntryState.Enabled, "Automatic"),
        Service("ContosoLicensing", "Contoso Licensing", @"""C:\Program Files\Contoso\Licensing\licsvc.exe""", System32 + "msiexec.exe", EntryState.Disabled, "Manual"),

        LaunchPoint(EntryKind.Winlogon, "Shell", "explorer.exe", @"C:\Windows\explorer.exe", Winlogon, "Shell", canDelete: false),
        LaunchPoint(EntryKind.Winlogon, "Userinit", @"C:\Windows\system32\userinit.exe,", System32 + "userinit.exe", Winlogon, "Userinit", canDelete: false),
        LaunchPoint(EntryKind.ActiveSetup, "Windows Desktop Update", @"""C:\Windows\System32\rundll32.exe"" C:\Windows\System32\iesetup.dll,IEHardenUser", System32 + "rundll32.exe",
            @"SOFTWARE\Microsoft\Active Setup\Installed Components\{89820200-ECBD-11cf-8B85-00AA005B4340}", "StubPath", canDelete: true),
        LaunchPoint(EntryKind.BootExecute, "BootExecute", "autocheck autochk *", System32 + "autochk.exe",
            @"SYSTEM\CurrentControlSet\Control\Session Manager", "BootExecute", canDelete: false),
    ];

    private static StartupEntry RunValue(RegistryHive hive, string key, string name, string command, string icon, EntryState state) =>
        new(EntryKind.RunKey, name, command, icon, new RegistryValueLocator(hive, key, name), state,
            CanToggle: true, CanDelete: true, RequiresAdmin: hive == RegistryHive.LocalMachine);

    private static StartupEntry StartupFile(string folder, string fileName, string icon, EntryState state, bool admin)
    {
        string file = Path.Combine(folder, fileName);
        return new(EntryKind.StartupFolder, Path.GetFileNameWithoutExtension(fileName), file, icon, new FileLocator(file), state,
            CanToggle: true, CanDelete: true, RequiresAdmin: admin);
    }

    private static StartupEntry Task(string path, string name, string command, string icon, EntryState state, string scope) =>
        new(EntryKind.ScheduledTask, name, command, icon, new ScheduledTaskLocator(path), state,
            CanToggle: true, CanDelete: true, RequiresAdmin: scope != EntryScope.User)
        {
            Scope = scope,
        };

    private static StartupEntry Service(string serviceName, string displayName, string command, string icon, EntryState state, string startType) =>
        new(EntryKind.Service, displayName, command, icon, new ServiceLocator(serviceName), state,
            CanToggle: true, CanDelete: false, RequiresAdmin: true, Details: $"Service: {serviceName}{Environment.NewLine}Start type: {startType}");

    private static StartupEntry LaunchPoint(EntryKind kind, string name, string command, string icon, string key, string valueName, bool canDelete) =>
        new(kind, name, command, icon, new RegistryValueLocator(RegistryHive.LocalMachine, key, valueName), EntryState.Active,
            CanToggle: false, CanDelete: canDelete, RequiresAdmin: true);
}
