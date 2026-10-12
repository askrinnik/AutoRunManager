using AutoRunManager.Core.Model;
using AutoRunManager.Core.Parsing;
using AutoRunManager.Core.Platform;
using AutoRunManager.Core.Sources;

using Microsoft.Win32;

namespace AutoRunManager.Core.Tests;

public class LaunchPointRulesTests
{
    private const string Windows = @"C:\Windows";

    [Theory]
    [InlineData(null, true)]
    [InlineData("explorer.exe", true)]
    [InlineData("Explorer.exe", true)]
    [InlineData(@"C:\Windows\explorer.exe", true)]
    [InlineData(@"explorer.exe, C:\evil.exe", false)]
    [InlineData(@"C:\Tools\shell.exe", false)]
    public void IsDefaultShell(string? value, bool expected)
    {
        Assert.Equal(expected, LaunchPointRules.IsDefaultShell(value, Windows));
    }

    [Theory]
    [InlineData(@"C:\WINDOWS\system32\userinit.exe,", true)]
    [InlineData(@"C:\Windows\system32\userinit.exe", true)]
    [InlineData("userinit.exe,", true)]
    [InlineData(null, true)]
    [InlineData(@"C:\Windows\system32\userinit.exe,C:\evil.exe,", false)]
    [InlineData(@"C:\Other\userinit.exe,", false)]
    public void IsDefaultUserinit(string? value, bool expected)
    {
        Assert.Equal(expected, LaunchPointRules.IsDefaultUserinit(value, Windows));
    }

    [Fact]
    public void NonDefaultBootLines_DropsCheckDiskAndBlanks()
    {
        Assert.Equal(["custom.exe arg"], LaunchPointRules.NonDefaultBootLines(["autocheck autochk *", "", " custom.exe arg "]));
        Assert.Empty(LaunchPointRules.NonDefaultBootLines(null));
    }
}

public class RegistryLaunchPointSourceTests
{
    private const string Cv = @"Software\Microsoft\Windows\CurrentVersion";
    private const string NtCv = @"Software\Microsoft\Windows NT\CurrentVersion";

    private static List<StartupEntry> Enumerate(FakeRegistry registry) =>
        new RegistryLaunchPointSource(registry, TestContext.Default).Enumerate().ToList();

    [Fact]
    public void Enumerate_ListsRunOnceAndPolicyValuesAsDeletableReadOnly()
    {
        FakeRegistry registry = new FakeRegistry()
            .Set(RegistryHive.CurrentUser, Cv + @"\RunOnce", "Cleanup", @"C:\Windows\system32\cmd.exe /c del x")
            .Set(RegistryHive.LocalMachine, Cv + @"\Policies\Explorer\Run", "Agent", @"C:\Corp\agent.exe")
            .Set(RegistryHive.LocalMachine, Cv + @"\RunOnceEx\0001", "Step", @"C:\Setup\step.exe /q");

        List<StartupEntry> entries = Enumerate(registry);

        Assert.Equal([EntryKind.RunOnce, EntryKind.PolicyRun, EntryKind.RunOnce], entries.Select(e => e.Kind));
        Assert.Equal(["Cleanup", "Agent", "Step"], entries.Select(e => e.Name));
        Assert.Equal([false, true, true], entries.Select(e => e.RequiresAdmin));
        Assert.All(entries, e => Assert.True(e.State == EntryState.Active && !e.CanToggle && e.CanDelete));
        Assert.Equal(new RegistryValueLocator(RegistryHive.LocalMachine, Cv + @"\RunOnceEx\0001", "Step"), entries[2].Locator);
    }

    [Fact]
    public void Enumerate_ShowsWinlogonOnlyWhenNotDefaultAndNeverDeletesMachineValues()
    {
        FakeRegistry registry = new FakeRegistry()
            .Set(RegistryHive.LocalMachine, NtCv + @"\Winlogon", "Shell", "explorer.exe")
            .Set(RegistryHive.LocalMachine, NtCv + @"\Winlogon", "Userinit", @"C:\Windows\system32\userinit.exe,C:\x\evil.exe,");

        StartupEntry entry = Assert.Single(Enumerate(registry));

        Assert.Equal("Userinit", entry.Name);
        Assert.False(entry.CanDelete);
    }

    [Fact]
    public void Enumerate_UserShellAndWindowsLoadAreListed()
    {
        FakeRegistry registry = new FakeRegistry()
            .Set(RegistryHive.CurrentUser, NtCv + @"\Winlogon", "Shell", @"C:\Tools\shell.exe")
            .Set(RegistryHive.CurrentUser, NtCv + @"\Windows", "Load", @"C:\Tools\load.exe")
            .Set(RegistryHive.CurrentUser, NtCv + @"\Windows", "Run", "");

        Assert.Equal(["Shell (user)", "Load"], Enumerate(registry).Select(e => e.Name));
    }

    [Fact]
    public void Enumerate_ActiveSetupSkipsWindowsAndUninstalledComponents()
    {
        const string root = @"Software\Microsoft\Active Setup\Installed Components";
        FakeRegistry registry = new FakeRegistry()
            .Set(RegistryHive.LocalMachine, root + @"\{A}", "StubPath", @"C:\Program Files\Chrome\chrmstp.exe --configure")
            .Set(RegistryHive.LocalMachine, root + @"\{A}", "", "Google Chrome")
            .Set(RegistryHive.LocalMachine, root + @"\{B}", "StubPath", @"%SystemRoot%\system32\ie4uinit.exe -UserConfig")
            .Set(RegistryHive.LocalMachine, root + @"\{C}", "StubPath", "rundll32.exe advpack.dll,LaunchINFSection")
            .Set(RegistryHive.LocalMachine, root + @"\{D}", "StubPath", @"C:\Vendor\old.exe")
            .Set(RegistryHive.LocalMachine, root + @"\{D}", "IsInstalled", 0)
            .Set(RegistryHive.LocalMachine, root + @"\{E}", "StubPath", @"C:\Vendor\tool.exe");

        Assert.Equal(["Google Chrome", "{E}"], Enumerate(registry).Select(e => e.Name));
    }

    [Fact]
    public void Enumerate_ImageHijackBootExecuteAndAppInit()
    {
        FakeRegistry registry = new FakeRegistry()
            .Set(RegistryHive.LocalMachine, NtCv + @"\Image File Execution Options\taskmgr.exe", "Debugger", @"C:\Tools\procexp.exe")
            .Set(RegistryHive.LocalMachine, NtCv + @"\Image File Execution Options\notepad.exe", "GlobalFlag", 2)
            .Set(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager", "BootExecute", new[] { "autocheck autochk *" })
            .Set(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager", "SetupExecute", new[] { "custom.exe" })
            .Set(RegistryHive.LocalMachine, NtCv + @"\Windows", "AppInit_DLLs", @"C:\x\hook.dll")
            .Set(RegistryHive.LocalMachine, NtCv + @"\Windows", "LoadAppInit_DLLs", 1)
            .Set(RegistryHive.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows NT\CurrentVersion\Windows", "AppInit_DLLs", @"C:\x\hook32.dll")
            .Set(RegistryHive.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows NT\CurrentVersion\Windows", "LoadAppInit_DLLs", 0);

        List<StartupEntry> entries = Enumerate(registry);

        Assert.Equal([EntryKind.BootExecute, EntryKind.ImageHijack, EntryKind.AppInitDll], entries.Select(e => e.Kind));
        Assert.Equal(["SetupExecute", "taskmgr.exe → debugger", "AppInit_DLLs"], entries.Select(e => e.Name));
        Assert.Equal([false, true, false], entries.Select(e => e.CanDelete));
        Assert.Equal(@"C:\Tools\procexp.exe", entries[1].ExecutablePath);
    }
}

public class WmiSubscriptionSourceTests
{
    private sealed class FakeWmi(params WmiSubscriptionInfo[] subscriptions) : IWmiSubscriptions
    {
        public IReadOnlyList<WmiSubscriptionInfo> GetSubscriptions() => subscriptions;
    }

    [Fact]
    public void Enumerate_MapsCommandLineAndScriptConsumers()
    {
        FakeWmi wmi = new(
            new WmiSubscriptionInfo("Updater", "CommandLineEventConsumer", @"C:\x\run.exe -a", "SELECT * FROM __InstanceModificationEvent", "F1", "C1", "B1"),
            new WmiSubscriptionInfo("Script", "ActiveScriptEventConsumer", "Set x = ...", null, "F2", "C2", "B2"));

        List<StartupEntry> entries = new WmiSubscriptionSource(wmi).Enumerate().ToList();

        Assert.Equal(["Updater", "Script"], entries.Select(e => e.Name));
        Assert.Equal(@"C:\x\run.exe", entries[0].ExecutablePath);
        Assert.Null(entries[1].ExecutablePath);
        Assert.Equal(new WmiSubscriptionLocator("F1", "C1", "B1"), entries[0].Locator);
        Assert.All(entries, e => Assert.True(e.RequiresAdmin && e.CanDelete && !e.CanToggle));
        Assert.Contains("__InstanceModificationEvent", entries[0].Details);
    }
}
