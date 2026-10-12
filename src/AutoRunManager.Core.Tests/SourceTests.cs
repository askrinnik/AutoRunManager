using AutoRunManager.Core.Model;
using AutoRunManager.Core.Parsing;
using AutoRunManager.Core.Platform;
using AutoRunManager.Core.Sources;

using Microsoft.Win32;

namespace AutoRunManager.Core.Tests;

public class RunKeySourceTests
{
    private const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Run32 = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string Approved = RunKeySource.StartupApprovedKey;

    [Fact]
    public void Enumerate_ReadsUserMachineAndWow64RunKeys()
    {
        FakeRegistry registry = new FakeRegistry()
            .Set(RegistryHive.CurrentUser, Run, "Viber", @"""C:\Users\me\Viber.exe"" StartMinimized")
            .Set(RegistryHive.LocalMachine, Run, "NetWorx", @"C:\Programs\NetWorx\networx.exe")
            .Set(RegistryHive.LocalMachine, Run32, "Lightshot", @"C:\Program Files (x86)\Lightshot\Lightshot.exe");

        List<StartupEntry> entries = new RunKeySource(registry).Enumerate().ToList();

        Assert.Equal(["Viber", "NetWorx", "Lightshot"], entries.Select(e => e.Name));
        Assert.Equal([false, true, true], entries.Select(e => e.RequiresAdmin));
        Assert.Equal(@"C:\Users\me\Viber.exe", entries[0].ExecutablePath);
        Assert.All(entries, e => Assert.Equal(EntryState.Enabled, e.State));
        Assert.All(entries, e => Assert.True(e.CanToggle && e.CanDelete));
        Assert.Equal(new RegistryValueLocator(RegistryHive.LocalMachine, Run32, "Lightshot"), entries[2].Locator);
    }

    [Fact]
    public void Enumerate_TakesStateFromMatchingStartupApprovedKey()
    {
        FakeRegistry registry = new FakeRegistry()
            .Set(RegistryHive.CurrentUser, Run, "Docker", "docker.exe")
            .Set(RegistryHive.CurrentUser, Approved + @"\Run", "Docker", StartupApprovedValue.Create(false, DateTime.UtcNow))
            .Set(RegistryHive.LocalMachine, Run32, "Lightshot", "Lightshot.exe")
            .Set(RegistryHive.LocalMachine, Approved + @"\Run32", "Lightshot", StartupApprovedValue.Create(false, DateTime.UtcNow))
            // Same name in the wrong StartupApproved key must not count.
            .Set(RegistryHive.LocalMachine, Run, "Other", "other.exe")
            .Set(RegistryHive.LocalMachine, Approved + @"\Run32", "Other", StartupApprovedValue.Create(false, DateTime.UtcNow));

        Dictionary<string, EntryState> states = new RunKeySource(registry).Enumerate().ToDictionary(e => e.Name, e => e.State);

        Assert.Equal(EntryState.Disabled, states["Docker"]);
        Assert.Equal(EntryState.Disabled, states["Lightshot"]);
        Assert.Equal(EntryState.Enabled, states["Other"]);
    }

    [Fact]
    public void Enumerate_SkipsEmptyDefaultValue()
    {
        FakeRegistry registry = new FakeRegistry().Set(RegistryHive.CurrentUser, Run, "", "");

        Assert.Empty(new RunKeySource(registry).Enumerate());
    }

    [Fact]
    public void ApprovedKeyFor_MapsWow64KeyToRun32()
    {
        Assert.Equal(Approved + @"\Run32", RunKeySource.ApprovedKeyFor(new RegistryValueLocator(RegistryHive.LocalMachine, Run32, "x")));
        Assert.Equal(Approved + @"\Run", RunKeySource.ApprovedKeyFor(new RegistryValueLocator(RegistryHive.CurrentUser, Run, "x")));
    }
}

public class StartupFolderSourceTests
{
    private static readonly SystemContext Context = TestContext.Default;

    [Fact]
    public void Enumerate_ListsAllFilesExceptDesktopIni()
    {
        FakeFileSystem files = new FakeFileSystem()
            .Add(Context.UserStartupFolder + @"\Punto Switcher.lnk")
            .Add(Context.UserStartupFolder + @"\script.bat")
            .Add(Context.UserStartupFolder + @"\desktop.ini")
            .Add(Context.CommonStartupFolder + @"\Shared.url");

        List<StartupEntry> entries = new StartupFolderSource(new FakeRegistry(), files, Context).Enumerate().ToList();

        Assert.Equal(["Punto Switcher", "script", "Shared"], entries.Select(e => e.Name));
        Assert.Equal([false, false, true], entries.Select(e => e.RequiresAdmin));
    }

    [Fact]
    public void Enumerate_ReadsStateByFileNameFromHiveOfFolder()
    {
        FakeFileSystem files = new FakeFileSystem()
            .Add(Context.UserStartupFolder + @"\a.lnk")
            .Add(Context.CommonStartupFolder + @"\b.lnk");
        FakeRegistry registry = new FakeRegistry()
            .Set(RegistryHive.CurrentUser, StartupFolderSource.ApprovedKey, "a.lnk", StartupApprovedValue.Create(false, DateTime.UtcNow))
            .Set(RegistryHive.CurrentUser, StartupFolderSource.ApprovedKey, "b.lnk", StartupApprovedValue.Create(false, DateTime.UtcNow));

        StartupFolderSource source = new(registry, files, Context);
        Dictionary<string, EntryState> states = source.Enumerate().ToDictionary(e => e.Name, e => e.State);

        Assert.Equal(EntryState.Disabled, states["a"]);
        Assert.Equal(EntryState.Enabled, states["b"]);
        Assert.Equal(RegistryHive.LocalMachine, source.ApprovedHiveFor(new FileLocator(Context.CommonStartupFolder + @"\b.lnk")));
        Assert.Equal(RegistryHive.CurrentUser, source.ApprovedHiveFor(new FileLocator(Context.UserStartupFolder + @"\a.lnk")));
    }
}

public class PackagedAppSourceTests
{
    private const string FullName = "Vendor.App_1.2.3.0_x64__abc123";
    private const string Family = "Vendor.App_abc123";
    private const string Root = @"C:\Program Files\WindowsApps\" + FullName;

    private const string Manifest = """
        <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
                 xmlns:uap5="http://schemas.microsoft.com/appx/manifest/uap/windows10/5">
          <Applications>
            <Application Id="App" Executable="App.exe">
              <Extensions>
                <uap5:Extension Category="windows.startupTask">
                  <uap5:StartupTask TaskId="AppStartup" Enabled="true" DisplayName="ms-resource:StartupName" />
                </uap5:Extension>
              </Extensions>
            </Application>
          </Applications>
        </Package>
        """;

    private static (FakeRegistry Registry, FakeFileSystem Files) Installed()
    {
        FakeRegistry registry = new FakeRegistry()
            .Set(RegistryHive.CurrentUser, $@"{PackagedAppSource.RepositoryKey}\{FullName}", "PackageRootFolder", Root)
            .Set(RegistryHive.CurrentUser, $@"{PackagedAppSource.RepositoryKey}\{FullName}", "DisplayName", "Vendor App");
        FakeFileSystem files = new FakeFileSystem().Add(Root + @"\AppxManifest.xml", Manifest);
        return (registry, files);
    }

    [Fact]
    public void Enumerate_UsesManifestDefaultWhenNoStateStored()
    {
        (FakeRegistry registry, FakeFileSystem files) = Installed();
        FakeIndirectStrings strings = new(new() { [$"@{{{FullName}?ms-resource://Vendor.App/resources/StartupName}}"] = "App at logon" });

        StartupEntry entry = Assert.Single(new PackagedAppSource(registry, files, strings).Enumerate());

        Assert.Equal("App at logon", entry.Name);
        Assert.Equal(EntryState.Enabled, entry.State);
        Assert.Equal(new PackagedTaskLocator(Family, "AppStartup"), entry.Locator);
        Assert.Equal(Root + @"\App.exe", entry.ExecutablePath);
        Assert.True(entry.CanToggle);
        Assert.False(entry.CanDelete);
        Assert.False(entry.RequiresAdmin);
    }

    [Fact]
    public void Enumerate_ReadsStoredStateAndFallsBackToPackageName()
    {
        (FakeRegistry registry, FakeFileSystem files) = Installed();
        registry.Set(RegistryHive.CurrentUser, $@"{PackagedStartupTasks.SystemAppDataKey}\{Family}\AppStartup", "State", 3);

        StartupEntry entry = Assert.Single(new PackagedAppSource(registry, files, new FakeIndirectStrings()).Enumerate());

        Assert.Equal("Vendor App", entry.Name);
        Assert.Equal(EntryState.Disabled, entry.State);
        Assert.False(entry.CanToggle);
    }

    [Fact]
    public void Enumerate_SkipsPackagesWithoutReadableManifestAndDuplicates()
    {
        (FakeRegistry registry, FakeFileSystem files) = Installed();
        const string otherVersion = "Vendor.App_1.2.4.0_x64__abc123";
        registry.Set(RegistryHive.CurrentUser, $@"{PackagedAppSource.RepositoryKey}\{otherVersion}", "PackageRootFolder", @"C:\Program Files\WindowsApps\" + otherVersion);
        files.Add(@"C:\Program Files\WindowsApps\" + otherVersion + @"\AppxManifest.xml", Manifest);
        registry.Set(RegistryHive.CurrentUser, $@"{PackagedAppSource.RepositoryKey}\Broken_1.0.0.0_x64__zzz", "PackageRootFolder", @"C:\missing");

        Assert.Single(new PackagedAppSource(registry, files, new FakeIndirectStrings()).Enumerate());
    }

    [Theory]
    [InlineData("Plain name", null)]
    [InlineData("ms-resource:AppName", "@{P.Name_1.0.0.0_x64__pub?ms-resource://P.Name/resources/AppName}")]
    [InlineData("ms-resource:Resources/AppName", "@{P.Name_1.0.0.0_x64__pub?ms-resource://P.Name/Resources/AppName}")]
    [InlineData("ms-resource:/Strings/AppName", "@{P.Name_1.0.0.0_x64__pub?ms-resource://P.Name/Strings/AppName}")]
    [InlineData("ms-resource://Other/Strings/AppName", "@{P.Name_1.0.0.0_x64__pub?ms-resource://Other/Strings/AppName}")]
    public void ToIndirectString_BuildsPackageResourceReference(string value, string? expected)
    {
        Assert.Equal(expected, PackagedAppSource.ToIndirectString(value, "P.Name_1.0.0.0_x64__pub"));
    }
}

public class ScheduledTaskSourceTests
{
    private static ScheduledTaskInfo Task(string path, string? userId = @"PC\me", bool enabled = true, TaskAction? action = null) =>
        new(path, TaskNames.Split(path).Name, enabled, [action ?? new TaskAction(@"C:\Program Files\Vendor\upd.exe", "/silent", null)],
            ["At logon"], userId, null, null);

    [Fact]
    public void Enumerate_SkipsMicrosoftFolderWithoutVisitingIt()
    {
        FakeTaskScheduler scheduler = new(Task(@"\Vendor\Updater"), Task(@"\Microsoft\Windows\Defrag\ScheduledDefrag"), Task(@"\Root"));

        List<StartupEntry> entries = new ScheduledTaskSource(scheduler, new FakeRegistry(), TestContext.Default).Enumerate().ToList();

        Assert.Equal(["Updater", "Root"], entries.Select(e => e.Name));
    }

    [Fact]
    public void Enumerate_BuildsCommandAndExecutable()
    {
        FakeTaskScheduler scheduler = new(Task(@"\Updater-S-1-5-21-1-2-3-1001", enabled: false));

        StartupEntry entry = Assert.Single(new ScheduledTaskSource(scheduler, new FakeRegistry(), TestContext.Default).Enumerate());

        Assert.Equal("Updater", entry.Name);
        Assert.Equal(@"""C:\Program Files\Vendor\upd.exe"" /silent", entry.Command);
        Assert.Equal(@"C:\Program Files\Vendor\upd.exe", entry.ExecutablePath);
        Assert.Equal(EntryState.Disabled, entry.State);
        Assert.Equal(new ScheduledTaskLocator(@"\Updater-S-1-5-21-1-2-3-1001"), entry.Locator);
        Assert.Contains("At logon", entry.Details);
    }

    [Theory]
    [InlineData(@"PC\me", false)]
    [InlineData("me", false)]
    [InlineData("S-1-5-21-1-2-3-1001", false)]
    [InlineData("SYSTEM", true)]
    [InlineData(@"PC\other", true)]
    [InlineData(null, true)]
    public void Enumerate_RequiresAdminForTasksOfOtherPrincipals(string? userId, bool requiresAdmin)
    {
        FakeTaskScheduler scheduler = new(Task(@"\T", userId));

        Assert.Equal(requiresAdmin, Assert.Single(new ScheduledTaskSource(scheduler, new FakeRegistry(), TestContext.Default).Enumerate()).RequiresAdmin);
    }

    [Fact]
    public void Enumerate_ResolvesComHandlerServer()
    {
        const string clsid = "{11111111-2222-3333-4444-555555555555}";
        FakeRegistry registry = new FakeRegistry().Set(RegistryHive.ClassesRoot, $@"CLSID\{clsid}\InprocServer32", "", @"C:\Vendor\handler.dll");
        FakeTaskScheduler scheduler = new(Task(@"\Com", action: new TaskAction(null, null, clsid)));

        StartupEntry entry = Assert.Single(new ScheduledTaskSource(scheduler, registry, TestContext.Default).Enumerate());

        Assert.Equal(@"C:\Vendor\handler.dll", entry.ExecutablePath);
        Assert.Contains(clsid, entry.Command);
    }
}

public class ServiceSourceTests
{
    private const string Services = ServiceSource.ServicesKey;

    private static FakeRegistry Service(FakeRegistry registry, string name, string imagePath, int start, int type = 0x10, bool delayed = false, string? displayName = null)
    {
        registry.Set(RegistryHive.LocalMachine, $@"{Services}\{name}", "ImagePath", imagePath, RegistryValueKind.ExpandString)
            .Set(RegistryHive.LocalMachine, $@"{Services}\{name}", "Start", start)
            .Set(RegistryHive.LocalMachine, $@"{Services}\{name}", "Type", type);
        if (delayed)
        {
            registry.Set(RegistryHive.LocalMachine, $@"{Services}\{name}", "DelayedAutostart", 1);
        }

        if (displayName is not null)
        {
            registry.Set(RegistryHive.LocalMachine, $@"{Services}\{name}", "DisplayName", displayName);
        }

        return registry;
    }

    [Fact]
    public void Enumerate_ListsThirdPartyServicesWithStartState()
    {
        FakeRegistry registry = new();
        Service(registry, "TeamViewer", @"""C:\Program Files\TeamViewer\TeamViewer_Service.exe""", 2, displayName: "TeamViewer");
        Service(registry, "nxservice", @"C:\Program Files\NoMachine\bin\nxservice64.exe", 3, displayName: "NoMachine Device Server (nxservice)");
        Service(registry, "Spooler", @"%SystemRoot%\System32\spoolsv.exe", 2);
        Service(registry, "veracrypt", @"System32\drivers\veracrypt.sys", 1, type: 1);

        List<StartupEntry> entries = new ServiceSource(registry, new FakeIndirectStrings(), TestContext.Default).Enumerate().ToList();

        Assert.Equal(["TeamViewer", "NoMachine Device Server"], entries.Select(e => e.Name));
        Assert.Equal([EntryState.Enabled, EntryState.Disabled], entries.Select(e => e.State));
        Assert.All(entries, e => Assert.True(e.CanToggle && !e.CanDelete && e.RequiresAdmin));
        Assert.Equal(new ServiceLocator("nxservice"), entries[1].Locator);
    }

    [Fact]
    public void Enumerate_ResolvesIndirectDisplayName()
    {
        FakeRegistry registry = Service(new FakeRegistry(), "svc", @"C:\Vendor\svc.exe", 2, displayName: "@C:\\Vendor\\res.dll,-1");
        FakeIndirectStrings strings = new(new() { ["@C:\\Vendor\\res.dll,-1"] = "Vendor Service" });

        Assert.Equal("Vendor Service", Assert.Single(new ServiceSource(registry, strings, TestContext.Default).Enumerate()).Name);
    }

    [Theory]
    [InlineData(2, false, ServiceStartType.Automatic, false)]
    [InlineData(2, true, ServiceStartType.Automatic, true)]
    [InlineData(3, true, ServiceStartType.Manual, false)]
    [InlineData(4, false, ServiceStartType.Disabled, false)]
    [InlineData(9, false, ServiceStartType.Manual, false)]
    public void ReadStartMode_CombinesStartAndDelayedFlag(int start, bool delayed, ServiceStartType type, bool expectedDelayed)
    {
        FakeRegistry registry = Service(new FakeRegistry(), "svc", @"C:\Vendor\svc.exe", start, delayed: delayed);

        Assert.Equal(new ServiceStartMode(type, expectedDelayed), ServiceSource.ReadStartMode(registry, "svc"));
    }
}

public class EntryCollectorTests
{
    private sealed class ThrowingSource : IEntrySource
    {
        public string Name => "Broken";

        public IEnumerable<StartupEntry> Enumerate() => throw new InvalidOperationException("boom");
    }

    [Fact]
    public void Collect_ReportsFailingSourceAndKeepsOthers()
    {
        FakeRegistry registry = new FakeRegistry().Set(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "App", "app.exe");

        CollectResult result = new EntryCollector([new ThrowingSource(), new RunKeySource(registry)]).Collect();

        Assert.Single(result.Entries);
        Assert.Equal(["Broken: boom"], result.Errors);
    }
}
