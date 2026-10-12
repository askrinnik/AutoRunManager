using System.ComponentModel;
using System.Text;

using AutoRunManager.Core.Actions;
using AutoRunManager.Core.Model;
using AutoRunManager.Core.Parsing;
using AutoRunManager.Core.Platform;
using AutoRunManager.Core.Sources;

using Microsoft.Win32;

namespace AutoRunManager.Core.Tests;

public class EntryActionsTests
{
    private const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Run32 = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string StatePath = @"C:\state\state.json";
    private const string BackupFolder = @"C:\state\backups";
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 15, 0, TimeSpan.Zero);

    private readonly FakeRegistry _registry = new();
    private readonly FakeFileSystem _files = new();
    private readonly FakeTaskScheduler _scheduler = new();
    private readonly FakeServiceControl _services = new();
    private readonly FakeWmi _wmi = new();

    private EntryActions Actions() => new(
        _registry, _files, _scheduler, _services, _wmi, TestContext.Default,
        AppState.Load(_files, StatePath), new BackupStore(_files, BackupFolder, new FixedTime(Now)), new FixedTime(Now));

    private static StartupEntry Entry(EntryKind kind, EntryLocator locator, bool canToggle = true, bool canDelete = true, string name = "App") =>
        new(kind, name, "app.exe", "app.exe", locator, EntryState.Enabled, canToggle, canDelete, RequiresAdmin: false);

    [Fact]
    public void SetEnabled_RunKey_WritesStartupApprovedOfMatchingKey()
    {
        StartupEntry entry = Entry(EntryKind.RunKey, new RegistryValueLocator(RegistryHive.LocalMachine, Run32, "Lightshot"));

        Actions().SetEnabled(entry, false);

        byte[] value = (byte[])_registry.GetValue(RegistryHive.LocalMachine, RunKeySource.StartupApprovedKey + @"\Run32", "Lightshot")!;
        Assert.Equal(StartupApprovedValue.Create(false, Now.UtcDateTime), value);
        Assert.Equal(RegistryValueKind.Binary, _registry.GetValueKind(RegistryHive.LocalMachine, RunKeySource.StartupApprovedKey + @"\Run32", "Lightshot"));

        Actions().SetEnabled(entry, true);

        Assert.True(StartupApprovedValue.IsEnabled((byte[])_registry.GetValue(RegistryHive.LocalMachine, RunKeySource.StartupApprovedKey + @"\Run32", "Lightshot")!));
    }

    [Fact]
    public void SetEnabled_StartupFolder_UsesHiveOfFolderAndFileName()
    {
        string file = TestContext.Default.CommonStartupFolder + @"\Shared.lnk";

        Actions().SetEnabled(Entry(EntryKind.StartupFolder, new FileLocator(file)), false);

        Assert.False(StartupApprovedValue.IsEnabled(_registry.GetValue(RegistryHive.LocalMachine, StartupFolderSource.ApprovedKey, "Shared.lnk") as byte[]));
        Assert.Null(_registry.GetValue(RegistryHive.CurrentUser, StartupFolderSource.ApprovedKey, "Shared.lnk"));
    }

    [Fact]
    public void SetEnabled_PackagedApp_WritesUserState()
    {
        StartupEntry entry = Entry(EntryKind.PackagedApp, new PackagedTaskLocator("Vendor.App_abc", "AppStartup"));
        string key = $@"{PackagedStartupTasks.SystemAppDataKey}\Vendor.App_abc\AppStartup";

        Actions().SetEnabled(entry, false);
        Assert.Equal((int)PackagedTaskState.DisabledByUser, _registry.GetValue(RegistryHive.CurrentUser, key, "State"));

        Actions().SetEnabled(entry, true);
        Assert.Equal((int)PackagedTaskState.Enabled, _registry.GetValue(RegistryHive.CurrentUser, key, "State"));
    }

    [Fact]
    public void SetEnabled_ScheduledTask_UsesFullPath()
    {
        Actions().SetEnabled(Entry(EntryKind.ScheduledTask, new ScheduledTaskLocator(@"\Vendor\Updater")), false);

        Assert.Equal(new Dictionary<string, bool> { [@"\Vendor\Updater"] = false }, _scheduler.EnabledChanges);
    }

    [Fact]
    public void SetEnabled_Service_OffSavesModeAndSetsManual_OnRestoresIt()
    {
        string key = $@"{ServiceSource.ServicesKey}\svc";
        _registry.Set(RegistryHive.LocalMachine, key, "Start", 2).Set(RegistryHive.LocalMachine, key, "DelayedAutostart", 1);
        StartupEntry entry = Entry(EntryKind.Service, new ServiceLocator("svc"), canDelete: false);
        ServiceStartMode delayed = new(ServiceStartType.Automatic, DelayedStart: true);

        Actions().SetEnabled(entry, false);

        Assert.Equal(("svc", ServiceStartMode.Manual), _services.Changes[^1]);
        Assert.Equal(delayed, AppState.Load(_files, StatePath).ServiceStartModes["SVC"]);

        Actions().SetEnabled(entry, true);

        Assert.Equal(("svc", delayed), _services.Changes[^1]);
        Assert.Empty(AppState.Load(_files, StatePath).ServiceStartModes);
    }

    [Fact]
    public void SetEnabled_Service_FromManualDoesNotSaveAndOnFallsBackToAutomatic()
    {
        _registry.Set(RegistryHive.LocalMachine, $@"{ServiceSource.ServicesKey}\svc", "Start", 3);
        StartupEntry entry = Entry(EntryKind.Service, new ServiceLocator("svc"), canDelete: false);

        Actions().SetEnabled(entry, false);
        Assert.False(_files.FileExists(StatePath));

        Actions().SetEnabled(entry, true);
        Assert.Equal(("svc", ServiceStartMode.Automatic), _services.Changes[^1]);
    }

    [Fact]
    public void SetEnabled_ServiceFailure_KeepsNoSavedMode()
    {
        _registry.Set(RegistryHive.LocalMachine, $@"{ServiceSource.ServicesKey}\svc", "Start", 2);
        ThrowingServiceControl services = new();
        EntryActions actions = new(_registry, _files, _scheduler, services, _wmi, TestContext.Default,
            AppState.Load(_files, StatePath), new BackupStore(_files, BackupFolder, new FixedTime(Now)), new FixedTime(Now));

        Assert.Throws<Win32Exception>(() => actions.SetEnabled(Entry(EntryKind.Service, new ServiceLocator("svc"), canDelete: false), false));
        Assert.False(_files.FileExists(StatePath));
    }

    [Fact]
    public void SetEnabled_ReadOnlyEntry_Throws()
    {
        StartupEntry entry = Entry(EntryKind.RunOnce, new RegistryValueLocator(RegistryHive.CurrentUser, Run + "Once", "x"), canToggle: false);

        Assert.Throws<InvalidOperationException>(() => Actions().SetEnabled(entry, false));
    }

    [Fact]
    public void Delete_RunValue_BacksUpValueAndStateThenDeletesBoth()
    {
        _registry.Set(RegistryHive.CurrentUser, Run, "Viber", @"""C:\Viber.exe"" -m", RegistryValueKind.String)
            .Set(RegistryHive.CurrentUser, RunKeySource.StartupApprovedKey + @"\Run", "Viber", StartupApprovedValue.Create(false, Now.UtcDateTime));
        StartupEntry entry = Entry(EntryKind.RunKey, new RegistryValueLocator(RegistryHive.CurrentUser, Run, "Viber"), name: "Viber");

        string backup = Actions().Delete(entry);

        Assert.Equal(@"C:\state\backups\20261012-091500 RunKey Viber.reg", backup);
        Assert.Equal(Encoding.Unicode, _files.Encodings[backup]);
        string reg = _files.Files[backup];
        Assert.Contains(@"[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run]", reg);
        Assert.Contains(@"""Viber""=""\""C:\\Viber.exe\"" -m""", reg);
        Assert.Contains(@"\Explorer\StartupApproved\Run]", reg);
        Assert.Null(_registry.GetValue(RegistryHive.CurrentUser, Run, "Viber"));
        Assert.Null(_registry.GetValue(RegistryHive.CurrentUser, RunKeySource.StartupApprovedKey + @"\Run", "Viber"));
    }

    [Fact]
    public void Delete_StartupFile_CopiesFileThenDeletesIt()
    {
        string file = TestContext.Default.UserStartupFolder + @"\Punto.lnk";
        _files.Add(file, "shortcut");

        string backup = Actions().Delete(Entry(EntryKind.StartupFolder, new FileLocator(file), name: "Punto"));

        Assert.Equal(@"C:\state\backups\20261012-091500 StartupFolder Punto.lnk", backup);
        Assert.Equal("shortcut", _files.Files[backup]);
        Assert.False(_files.FileExists(file));
    }

    [Fact]
    public void Delete_Task_SavesXmlThenDeletes()
    {
        string backup = Actions().Delete(Entry(EntryKind.ScheduledTask, new ScheduledTaskLocator(@"\Vendor\Updater"), name: "Updater"));

        Assert.EndsWith(".xml", backup);
        Assert.Equal(@"<Task path=""\Vendor\Updater"" />", _files.Files[backup]);
        Assert.Equal([@"\Vendor\Updater"], _scheduler.Deleted);
    }

    [Fact]
    public void Delete_WmiSubscription_SavesDescriptionThenDeletesAllParts()
    {
        Actions().Delete(Entry(EntryKind.WmiSubscription, new WmiSubscriptionLocator("F", "C", "B")));

        Assert.Equal([["B", "C", "F"]], _wmi.Deleted);
    }

    [Fact]
    public void Delete_NotDeletable_ThrowsAndChangesNothing()
    {
        _registry.Set(RegistryHive.LocalMachine, @"Software\Microsoft\Windows NT\CurrentVersion\Winlogon", "Userinit", "x");
        StartupEntry entry = Entry(EntryKind.Winlogon,
            new RegistryValueLocator(RegistryHive.LocalMachine, @"Software\Microsoft\Windows NT\CurrentVersion\Winlogon", "Userinit"), canToggle: false, canDelete: false);

        Assert.Throws<InvalidOperationException>(() => Actions().Delete(entry));
        Assert.Equal("x", _registry.GetValue(RegistryHive.LocalMachine, @"Software\Microsoft\Windows NT\CurrentVersion\Winlogon", "Userinit"));
        Assert.Empty(_files.Files);
    }

    private sealed class ThrowingServiceControl : IServiceControl
    {
        public void SetStartMode(string serviceName, ServiceStartMode mode) => throw new Win32Exception(5);
    }
}

public class RegFileTests
{
    [Fact]
    public void Export_GroupsValuesByKeyAndEscapes()
    {
        string text = RegFile.Export(
        [
            (new RegistryValueLocator(RegistryHive.CurrentUser, @"Software\A", "Path"), (object)@"""C:\x.exe"" -a", RegistryValueKind.String),
            (new RegistryValueLocator(RegistryHive.CurrentUser, @"Software\A", ""), (object)"default", RegistryValueKind.String),
            (new RegistryValueLocator(RegistryHive.LocalMachine, @"Software\B", "Flag"), (object)-1, RegistryValueKind.DWord),
        ]);

        Assert.Equal(
            "Windows Registry Editor Version 5.00\r\n" +
            "\r\n[HKEY_CURRENT_USER\\Software\\A]\r\n" +
            "\"Path\"=\"\\\"C:\\\\x.exe\\\" -a\"\r\n" +
            "@=\"default\"\r\n" +
            "\r\n[HKEY_LOCAL_MACHINE\\Software\\B]\r\n" +
            "\"Flag\"=dword:ffffffff\r\n",
            text);
    }

    [Fact]
    public void FormatValue_BinaryExpandAndMulti()
    {
        Assert.Equal("hex:03,00,ff", RegFile.FormatValue(new byte[] { 3, 0, 255 }, RegistryValueKind.Binary));
        Assert.Equal("hex(2):25,00,41,00,25,00,00,00", RegFile.FormatValue("%A%", RegistryValueKind.ExpandString));
        Assert.Equal("hex(7):61,00,00,00,62,00,00,00,00,00", RegFile.FormatValue(new[] { "a", "b" }, RegistryValueKind.MultiString));
    }
}

public class AppStateTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsStartModes()
    {
        FakeFileSystem files = new();
        AppState state = AppState.Load(files, @"C:\s.json");
        state.ServiceStartModes["Svc"] = new ServiceStartMode(ServiceStartType.Automatic, DelayedStart: true);
        state.Save();

        AppState loaded = AppState.Load(files, @"C:\s.json");

        Assert.Equal(new ServiceStartMode(ServiceStartType.Automatic, true), loaded.ServiceStartModes["svc"]);
        Assert.Contains("\"Automatic\"", files.Files[@"C:\s.json"]);
    }

    [Fact]
    public void Load_InvalidFile_GivesEmptyState()
    {
        FakeFileSystem files = new FakeFileSystem().Add(@"C:\s.json", "{ not json");

        Assert.Empty(AppState.Load(files, @"C:\s.json").ServiceStartModes);
    }
}

public class BackupStoreTests
{
    [Theory]
    [InlineData("Viber", "Viber")]
    [InlineData(@"taskmgr.exe → debugger", "taskmgr.exe → debugger")]
    [InlineData(@"a\b:c*d?", "a_b_c_d_")]
    [InlineData("...", "entry")]
    public void SafeName_ReplacesInvalidCharacters(string name, string expected)
    {
        Assert.Equal(expected, BackupStore.SafeName(name));
    }
}

public class ElevationTests
{
    [Fact]
    public void IsAccessDenied_RecognisesCommonForms()
    {
        Assert.True(Elevation.IsAccessDenied(new UnauthorizedAccessException()));
        Assert.True(Elevation.IsAccessDenied(new Win32Exception(5)));
        Assert.True(Elevation.IsAccessDenied(new System.Runtime.InteropServices.COMException("x", unchecked((int)0x80070005))));
        Assert.True(Elevation.IsAccessDenied(new InvalidOperationException("x", new UnauthorizedAccessException())));
        Assert.False(Elevation.IsAccessDenied(new Win32Exception(2)));
    }
}

public class WindowBoundsTests
{
    [Theory]
    [InlineData(100, 100, 800, 600, true)]
    [InlineData(-760, 100, 800, 600, true)]
    [InlineData(-790, 100, 800, 600, false)]
    [InlineData(1900, 100, 800, 600, false)]
    [InlineData(100, -5, 800, 600, false)]
    [InlineData(100, 1050, 800, 600, false)]
    [InlineData(100, 100, 0, 600, false)]
    public void IsReachableOn_RequiresTheTopEdgeOnScreen(double left, double top, double width, double height, bool expected)
    {
        Assert.Equal(expected, new WindowBounds(left, top, width, height).IsReachableOn(0, 0, 1920, 1080));
    }

    [Fact]
    public void AppState_RoundTripsWindowBounds()
    {
        FakeFileSystem files = new();
        AppState state = AppState.Load(files, @"C:\s.json");
        state.Window = new WindowBounds(10, 20, 820, 650);
        state.Save();

        Assert.Equal(new WindowBounds(10, 20, 820, 650), AppState.Load(files, @"C:\s.json").Window);
    }
}
