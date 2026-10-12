using AutoRunManager.Core.Model;
using AutoRunManager.Core.Presentation;

using Microsoft.Win32;

namespace AutoRunManager.Core.Tests;

public class EntryTabsTests
{
    private static StartupEntry Entry(EntryKind kind, string name, EntryState state = EntryState.Enabled, bool admin = false, string command = "app.exe") =>
        new(kind, name, command, null, new RegistryValueLocator(RegistryHive.CurrentUser, @"Software\X", name), state,
            CanToggle: true, CanDelete: true, RequiresAdmin: admin, Details: "Task: details");

    private static readonly StartupEntry[] Entries =
    [
        Entry(EntryKind.PackagedApp, "WhatsApp"),
        Entry(EntryKind.RunKey, "Viber", command: @"C:\Viber\Viber.exe"),
        Entry(EntryKind.RunKey, "Docker", EntryState.Disabled),
        Entry(EntryKind.Service, "TeamViewer", admin: true),
        Entry(EntryKind.RunOnce, "Cleanup", EntryState.Active),
    ];

    [Fact]
    public void All_CoversEveryKindExactlyOnce()
    {
        Assert.Equal(Enum.GetValues<EntryKind>().Order(), EntryTabs.All.SelectMany(t => t.Kinds).Order());
    }

    [Fact]
    public void Sections_FollowTabOrderAndKeepEntryOrder()
    {
        var sections = EntryTabs.Sections(EntryTabs.All[0], Entries, EntryFilter.All, null);

        Assert.Equal([EntryKind.RunKey, EntryKind.PackagedApp], sections.Select(s => s.Kind));
        Assert.Equal(["Viber", "Docker"], sections[0].Entries.Select(e => e.Name));
    }

    [Fact]
    public void Count_IgnoresFilter()
    {
        Assert.Equal(3, EntryTabs.Count(EntryTabs.All[0], Entries));
        Assert.Equal(1, EntryTabs.Count(EntryTabs.All[3], Entries));
    }

    [Theory]
    [InlineData(EntryFilter.All, null, "WhatsApp,Viber,Docker,TeamViewer,Cleanup")]
    [InlineData(EntryFilter.On, null, "WhatsApp,Viber,TeamViewer,Cleanup")]
    [InlineData(EntryFilter.Off, null, "Docker")]
    [InlineData(EntryFilter.NeedsAdmin, null, "TeamViewer")]
    [InlineData(EntryFilter.All, "viber", "Viber")]
    [InlineData(EntryFilter.All, @"C:\Viber", "Viber")]
    [InlineData(EntryFilter.All, "services team", "TeamViewer")]
    [InlineData(EntryFilter.All, "details", "WhatsApp,Viber,Docker,TeamViewer,Cleanup")]
    [InlineData(EntryFilter.Off, "viber", "")]
    public void Matches_CombinesFilterAndSearchWords(EntryFilter filter, string? search, string expected)
    {
        string actual = string.Join(',', Entries.Where(e => EntryQuery.Matches(e, filter, search)).Select(e => e.Name));

        Assert.Equal(expected, actual);
    }
}
