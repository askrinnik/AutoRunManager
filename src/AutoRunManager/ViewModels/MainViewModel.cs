using System.Windows;
using System.Windows.Input;

using AutoRunManager.Core.Actions;
using AutoRunManager.Core.Model;
using AutoRunManager.Core.Platform;
using AutoRunManager.Core.Presentation;
using AutoRunManager.Core.Sources;
using AutoRunManager.Services;

namespace AutoRunManager.ViewModels;

/// <summary>A group or section in the side navigation.</summary>
public sealed class NavigationItemViewModel(NavigationItem item)
{
    public NavigationItem Item => item;

    public string Title => item.IsGroup ? item.Title.ToUpperInvariant() : item.Title;

    public string Count => item.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public bool IsGroup => item.IsGroup;

    public bool Matches(NavigationItem other) => other.Group.Title == item.Group.Title && other.Kind == item.Kind;
}

public sealed record FilterOption(EntryFilter Filter, string Label);

public sealed class MainViewModel : ObservableObject
{
    private readonly IDialogs _dialogs;
    private readonly Func<EntryCollector> _createCollector;
    private readonly EntryActions _actions;
    private List<EntryViewModel> _entries = [];
    private IReadOnlyList<NavigationItemViewModel> _navigation = [];
    private NavigationItemViewModel? _selectedNavigation;
    private FilterOption _selectedFilter;
    private string _searchText = "";
    private IReadOnlyList<SectionViewModel> _sections = [];
    private bool _isLoading;
    private string _status = "";

    public MainViewModel(IDialogs dialogs, Func<EntryCollector> createCollector, EntryActions actions, bool isElevated)
    {
        _dialogs = dialogs;
        _createCollector = createCollector;
        _actions = actions;
        IsElevated = isElevated;
        Filters =
        [
            new(EntryFilter.All, "All"),
            new(EntryFilter.On, "On"),
            new(EntryFilter.Off, "Off"),
            new(EntryFilter.NeedsAdmin, "Needs admin"),
        ];
        _selectedFilter = Filters[0];
        RefreshCommand = new AsyncCommand(LoadAsync);
        RestartAsAdminCommand = new RelayCommand(RestartAsAdmin);
        UpdateNavigation();
    }

    public bool IsElevated { get; }

    public bool ShowRestartAsAdmin => !IsElevated;

    public IReadOnlyList<NavigationItemViewModel> Navigation
    {
        get => _navigation;
        private set => Set(ref _navigation, value);
    }

    public NavigationItemViewModel? SelectedNavigation
    {
        get => _selectedNavigation;
        set
        {
            if (value is not null && Set(ref _selectedNavigation, value))
            {
                // Picking a group or section ends a search, so the choice is visible at once.
                if (IsSearching)
                {
                    _searchText = "";
                    OnPropertyChanged(nameof(SearchText));
                    OnPropertyChanged(nameof(IsSearching));
                }

                Rebuild();
            }
        }
    }

    public IReadOnlyList<FilterOption> Filters { get; }

    public FilterOption SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (value is not null && Set(ref _selectedFilter, value))
            {
                Rebuild();
            }
        }
    }

    /// <summary>Non-empty text searches every group, not only the selected one.</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (Set(ref _searchText, value ?? ""))
            {
                OnPropertyChanged(nameof(IsSearching));
                Rebuild();
            }
        }
    }

    public bool IsSearching => !string.IsNullOrWhiteSpace(_searchText);

    public IReadOnlyList<SectionViewModel> Sections
    {
        get => _sections;
        private set
        {
            Set(ref _sections, value);
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(EmptyText));
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            Set(ref _isLoading, value);
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    public bool IsEmpty => !IsLoading && Sections.Count == 0;

    public string EmptyText =>
        IsSearching ? "Nothing matches the search"
        : _entries.Any(e => CurrentKinds.Contains(e.Entry.Kind)) ? "No entries match the filter"
        : "Nothing starts from here";

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    public ICommand RefreshCommand { get; }

    public ICommand RestartAsAdminCommand { get; }

    private IReadOnlyList<EntryKind> CurrentKinds =>
        IsSearching ? EntryTabs.AllKinds : _selectedNavigation?.Item.Kinds ?? EntryTabs.All[0].Kinds;

    public async Task LoadAsync()
    {
        IsLoading = true;
        Sections = [];
        try
        {
            CollectResult result = await Task.Run(() => _createCollector().Collect());
            _entries = result.Entries.Select(e => new EntryViewModel(this, e)).ToList();
            Status = result.Errors.Count > 0 ? "Could not read: " + string.Join("; ", result.Errors) : "";
        }
        catch (Exception e)
        {
            _entries = [];
            Status = "Could not read the startup entries: " + e.Message;
        }
        finally
        {
            IsLoading = false;
        }

        UpdateNavigation();
        Rebuild();
    }

    public async Task ToggleAsync(EntryViewModel entry)
    {
        bool enable = !entry.Entry.IsEnabled;
        if (!entry.CanChange)
        {
            entry.RefreshState();
            return;
        }

        entry.Busy = true;
        try
        {
            await Task.Run(() => _actions.SetEnabled(entry.Entry, enable));
            entry.Entry = entry.Entry with { State = enable ? EntryState.Enabled : EntryState.Disabled };
            Status = $"{entry.Name} turned {(enable ? "on" : "off")}.";
        }
        catch (Exception e)
        {
            entry.RefreshState();
            _dialogs.Message(ErrorText(e));
        }
        finally
        {
            entry.Busy = false;
        }
    }

    public async Task DeleteAsync(EntryViewModel entry)
    {
        if (!entry.CanChange || !_dialogs.Confirm($"Delete \"{entry.Name}\" from startup?\nA backup is saved first."))
        {
            return;
        }

        entry.Busy = true;
        try
        {
            string backup = await Task.Run(() => _actions.Delete(entry.Entry));
            _entries.Remove(entry);
            UpdateNavigation();
            Rebuild();
            Status = $"{entry.Name} deleted. Backup: {backup}";
        }
        catch (Exception e)
        {
            _dialogs.Message(ErrorText(e));
        }
        finally
        {
            entry.Busy = false;
        }
    }

    /// <summary>Turns all switchable entries of a section to the opposite of the first one, after confirmation.</summary>
    public async Task ToggleSectionAsync(SectionViewModel section)
    {
        List<EntryViewModel> toggles = section.Entries.Where(e => e.ShowToggle).ToList();
        if (toggles.Count == 0)
        {
            return;
        }

        List<EntryViewModel> changeable = toggles.Where(e => e.CanChange).ToList();
        if (changeable.Count == 0)
        {
            _dialogs.Message("These entries need administrator rights. Use \"Restart as administrator\".");
            return;
        }

        bool enable = !changeable[0].IsEnabled;
        List<EntryViewModel> targets = changeable.Where(e => e.IsEnabled != enable).ToList();
        string skipped = changeable.Count < toggles.Count ? $"\n{toggles.Count - changeable.Count} entries that need administrator rights are skipped." : "";
        if (!_dialogs.Confirm($"Turn {(enable ? "on" : "off")} all {targets.Count} entries in {section.Kind.DisplayName()}?{skipped}"))
        {
            return;
        }

        List<string> failures = [];
        foreach (EntryViewModel entry in targets)
        {
            entry.Busy = true;
            try
            {
                await Task.Run(() => _actions.SetEnabled(entry.Entry, enable));
                entry.Entry = entry.Entry with { State = enable ? EntryState.Enabled : EntryState.Disabled };
            }
            catch (Exception e)
            {
                entry.RefreshState();
                failures.Add($"{entry.Name}: {ErrorText(e)}");
            }
            finally
            {
                entry.Busy = false;
            }
        }

        Status = $"{targets.Count - failures.Count} entries turned {(enable ? "on" : "off")}.";
        if (failures.Count > 0)
        {
            _dialogs.Message(string.Join("\n", failures));
        }
    }

    /// <summary>Runs a quick UI action (opening a tool) and reports failures.</summary>
    public void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            _dialogs.Message(e.Message);
        }
    }

    private void RestartAsAdmin()
    {
        if (ShellLauncher.RestartAsAdmin())
        {
            Application.Current.Shutdown();
        }
    }

    /// <summary>Rebuilds the navigation with current counts, keeping the selected item if it still exists.</summary>
    private void UpdateNavigation()
    {
        NavigationItem? selected = _selectedNavigation?.Item;
        Navigation = EntryTabs.Navigation(_entries.Select(e => e.Entry).ToList())
            .Select(i => new NavigationItemViewModel(i))
            .ToList();
        _selectedNavigation = (selected is null ? null : Navigation.FirstOrDefault(n => n.Matches(selected)))
            ?? Navigation.FirstOrDefault(n => n.IsGroup && n.Item.Group.Title == selected?.Group.Title)
            ?? Navigation[0];
        OnPropertyChanged(nameof(SelectedNavigation));
    }

    private void Rebuild()
    {
        Dictionary<StartupEntry, EntryViewModel> byEntry = _entries.ToDictionary<EntryViewModel, StartupEntry>(e => e.Entry, ReferenceEqualityComparer.Instance);
        Sections = EntryTabs.Sections(CurrentKinds, byEntry.Keys, SelectedFilter.Filter, SearchText)
            .Select(s => new SectionViewModel(this, s.Kind, s.Entries.Select(e => byEntry[e]).ToList()))
            .ToList();
    }

    private static string ErrorText(Exception e) =>
        Elevation.IsAccessDenied(e) ? "Access denied. Restart as administrator to change this entry." : e.Message;
}
