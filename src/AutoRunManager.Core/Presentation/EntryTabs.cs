using AutoRunManager.Core.Model;

namespace AutoRunManager.Core.Presentation;

/// <summary>A group of the navigation: the entry kinds it shows, in section order.</summary>
public sealed record TabDefinition(string Title, IReadOnlyList<EntryKind> Kinds);

/// <summary>
/// An item of the side navigation: a whole group (<see cref="Kind"/> is null) or one section of it.
/// </summary>
public sealed record NavigationItem(TabDefinition Group, EntryKind? Kind, int Count)
{
    public bool IsGroup => Kind is null;

    public string Title => Kind is { } kind ? kind.DisplayName() : Group.Title;

    public IReadOnlyList<EntryKind> Kinds => Kind is { } kind ? [kind] : Group.Kinds;
}

public static class EntryTabs
{
    public static readonly IReadOnlyList<TabDefinition> All =
    [
        new("Apps", [EntryKind.RunKey, EntryKind.StartupFolder, EntryKind.PackagedApp]),
        new("Tasks", [EntryKind.ScheduledTask]),
        new("Services", [EntryKind.Service]),
        new("Advanced",
        [
            EntryKind.RunOnce, EntryKind.RunServices, EntryKind.PolicyRun, EntryKind.Winlogon, EntryKind.WindowsLoad, EntryKind.ActiveSetup,
            EntryKind.TerminalServerRun, EntryKind.BootExecute, EntryKind.ImageHijack, EntryKind.AppInitDll, EntryKind.WmiSubscription,
        ]),
    ];

    /// <summary>All kinds in navigation order; used when searching across every group.</summary>
    public static IReadOnlyList<EntryKind> AllKinds { get; } = All.SelectMany(t => t.Kinds).ToList();

    /// <summary>
    /// Side navigation: every group, followed by its sections that have entries. Groups with a
    /// single kind (Tasks, Services) get no section items.
    /// </summary>
    public static IReadOnlyList<NavigationItem> Navigation(IReadOnlyCollection<StartupEntry> entries)
    {
        List<NavigationItem> items = [];
        foreach (TabDefinition group in All)
        {
            items.Add(new NavigationItem(group, null, Count(group, entries)));
            if (group.Kinds.Count < 2)
            {
                continue;
            }

            foreach (EntryKind kind in group.Kinds)
            {
                int count = entries.Count(e => e.Kind == kind);
                if (count > 0)
                {
                    items.Add(new NavigationItem(group, kind, count));
                }
            }
        }

        return items;
    }

    /// <summary>
    /// Sections for the given kinds: one per kind that has matching entries, in the given order;
    /// entries keep the order in which the sources produced them.
    /// </summary>
    public static IReadOnlyList<(EntryKind Kind, IReadOnlyList<StartupEntry> Entries)> Sections(
        IReadOnlyList<EntryKind> kinds, IEnumerable<StartupEntry> entries, EntryFilter filter, string? search)
    {
        ILookup<EntryKind, StartupEntry> byKind = entries
            .Where(e => kinds.Contains(e.Kind) && EntryQuery.Matches(e, filter, search))
            .ToLookup(e => e.Kind);
        return kinds
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
