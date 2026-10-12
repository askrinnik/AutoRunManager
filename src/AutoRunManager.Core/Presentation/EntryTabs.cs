using AutoRunManager.Core.Model;

namespace AutoRunManager.Core.Presentation;

/// <summary>A tab of the main window: the entry kinds it shows, in section order.</summary>
public sealed record TabDefinition(string Title, IReadOnlyList<EntryKind> Kinds);

public static class EntryTabs
{
    public static readonly IReadOnlyList<TabDefinition> All =
    [
        new("Apps", [EntryKind.RunKey, EntryKind.StartupFolder, EntryKind.PackagedApp]),
        new("Tasks", [EntryKind.ScheduledTask]),
        new("Services", [EntryKind.Service]),
        new("Advanced",
        [
            EntryKind.RunOnce, EntryKind.PolicyRun, EntryKind.Winlogon, EntryKind.WindowsLoad, EntryKind.ActiveSetup,
            EntryKind.TerminalServerRun, EntryKind.BootExecute, EntryKind.ImageHijack, EntryKind.AppInitDll, EntryKind.WmiSubscription,
        ]),
    ];

    /// <summary>
    /// Sections of a tab: one per kind that has matching entries, in the tab's order; entries keep
    /// the order in which the sources produced them.
    /// </summary>
    public static IReadOnlyList<(EntryKind Kind, IReadOnlyList<StartupEntry> Entries)> Sections(
        TabDefinition tab, IEnumerable<StartupEntry> entries, EntryFilter filter, string? search)
    {
        ILookup<EntryKind, StartupEntry> byKind = entries
            .Where(e => tab.Kinds.Contains(e.Kind) && EntryQuery.Matches(e, filter, search))
            .ToLookup(e => e.Kind);
        return tab.Kinds
            .Where(byKind.Contains)
            .Select(kind => (kind, (IReadOnlyList<StartupEntry>)byKind[kind].ToList()))
            .ToList();
    }

    public static int Count(TabDefinition tab, IEnumerable<StartupEntry> entries) =>
        entries.Count(e => tab.Kinds.Contains(e.Kind));
}

public enum EntryFilter
{
    All,
    On,
    Off,
    NeedsAdmin,
}

public static class EntryQuery
{
    /// <summary>
    /// Filter: On = enabled or always active, Off = disabled, NeedsAdmin = changing it needs
    /// elevation. Search: case-insensitive substring of name, command, location or details; all
    /// words must match.
    /// </summary>
    public static bool Matches(StartupEntry entry, EntryFilter filter, string? search)
    {
        bool filtered = filter switch
        {
            EntryFilter.On => entry.IsEnabled,
            EntryFilter.Off => !entry.IsEnabled,
            EntryFilter.NeedsAdmin => entry.RequiresAdmin,
            _ => true,
        };
        if (!filtered)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        string haystack = string.Join('\n', entry.Name, entry.Command, entry.Locator.Display, entry.Details ?? "", entry.Kind.DisplayName());
        return search.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .All(word => haystack.Contains(word, StringComparison.OrdinalIgnoreCase));
    }
}
