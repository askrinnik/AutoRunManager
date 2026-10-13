using AutoRunManager.Core.Model;
using AutoRunManager.Core.Presentation;

using Microsoft.Win32;

namespace AutoRunManager.Core.Tests;

public class EntryScopeTests
{
    private static StartupEntry Entry(EntryKind kind, EntryLocator locator) =>
        new(kind, "App", "app.exe", null, locator, EntryState.Enabled, CanToggle: true, CanDelete: true, RequiresAdmin: false);

    private static string? Tag(EntryKind kind, EntryLocator locator) => EntryScope.Tag(Entry(kind, locator), TestContext.Default);

    [Theory]
    [InlineData(@"C:\Users\me\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\App.lnk", "User")]
    [InlineData(@"c:\programdata\microsoft\windows\start menu\programs\startup\App.lnk", "All users")]
    [InlineData(@"C:\Elsewhere\App.lnk", null)]
    public void Tag_StartupFolderFile_NamesWhoseFolder(string path, string? expected)
    {
        Assert.Equal(expected, Tag(EntryKind.StartupFolder, new FileLocator(path)));
    }

    [Theory]
    [InlineData(EntryKind.RunKey, RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKCU")]
    [InlineData(EntryKind.RunKey, RegistryHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKLM")]
    [InlineData(EntryKind.RunKey, RegistryHive.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "HKLM 32-bit")]
    [InlineData(EntryKind.RunOnce, RegistryHive.LocalMachine, @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\RunOnce", "HKLM 32-bit")]
    [InlineData(EntryKind.PolicyRun, RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer\Run", "HKCU")]
    [InlineData(EntryKind.ActiveSetup, RegistryHive.LocalMachine, @"Software\WOW6432Node\Microsoft\Active Setup\Installed Components\{X}", "HKLM 32-bit")]
    [InlineData(EntryKind.Winlogon, RegistryHive.LocalMachine, @"Software\Microsoft\Windows NT\CurrentVersion\Winlogon", "HKLM")]
    [InlineData(EntryKind.RunKey, RegistryHive.LocalMachine, @"Software\Vendor\NotWOW6432NodeReally", "HKLM")]
    public void Tag_RegistryValue_NamesHiveAnd32BitView(EntryKind kind, RegistryHive hive, string keyPath, string expected)
    {
        Assert.Equal(expected, Tag(kind, new RegistryValueLocator(hive, keyPath, "App")));
    }

    [Fact]
    public void Tag_OtherKinds_HaveNone()
    {
        Assert.Null(Tag(EntryKind.PackagedApp, new PackagedTaskLocator("App_8wekyb3d8bbwe", "Task")));
        Assert.Null(Tag(EntryKind.ScheduledTask, new ScheduledTaskLocator(@"\Vendor\Updater")));
        Assert.Null(Tag(EntryKind.Service, new ServiceLocator("VendorSvc")));
        Assert.Null(Tag(EntryKind.WmiSubscription, new WmiSubscriptionLocator("f", "c", "b")));
    }
}
