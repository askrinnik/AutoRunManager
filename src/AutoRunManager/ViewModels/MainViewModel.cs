using System.Windows;
using System.Windows.Input;

using AutoRunManager.Core.Actions;
using AutoRunManager.Core.Model;
using AutoRunManager.Core.Platform;
using AutoRunManager.Core.Presentation;
using AutoRunManager.Core.Sources;
using AutoRunManager.Services;

namespace AutoRunManager.ViewModels;

public sealed class TabViewModel(TabDefinition definition) : ObservableObject
{
    private int _count;

    public TabDefinition Definition => definition;

    public string Label => _count > 0 ? $"{definition.Title}  {_count}" : definition.Title;

    public int Count
    {
        get => _count;
        set
        {
            if (Set(ref _count, value))
            {
                OnPropertyChanged(nameof(Label));
            }
        }
    }
}

public sealed record FilterOption(EntryFilter Filter, string Label);

public sealed class MainViewModel : ObservableObject
{
    private readonly IDialogs _dialogs;
    private readonly Func<EntryCollector> _createCollector;
    private readonly EntryActions _actions;
    private List<EntryViewModel> _entries = [];
    private TabViewModel _selectedTab;
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
        Tabs = EntryTabs.All.Select(t => new TabViewModel(t)).ToList();
        _selectedTab = Tabs[0];
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
    }

    public bool IsElevated { get; }

    public bool ShowRestartAsAdmin => !IsElevated;

    public IReadOnlyList<TabViewModel> Tabs { get; }

    public IReadOnlyList<FilterOption> Filters { get; }

    public TabViewModel SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (value is not null && Set(ref _selectedTab, value))
            {
                Rebuild();
            }
        }
    }

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

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (Set(ref _searchText, value ?? ""))
            {
                Rebuild();
            }
        }
    }

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
        _entries.Any(e => SelectedTab.Definition.Kinds.Contains(e.Entry.Kind)) ? "No entries match" : "Nothing starts from here";

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    public ICommand RefreshCommand { get; }

    public ICommand RestartAsAdminCommand { get; }

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

        UpdateCounts();
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
            UpdateCounts();
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

    private void UpdateCounts()
    {
        foreach (TabViewModel tab in Tabs)
        {
            tab.Count = EntryTabs.Count(tab.Definition, _entries.Select(e => e.Entry));
        }
    }

    private void Rebuild()
    {
        Dictionary<StartupEntry, EntryViewModel> byEntry = _entries.ToDictionary<EntryViewModel, StartupEntry>(e => e.Entry, ReferenceEqualityComparer.Instance);
        Sections = EntryTabs.Sections(SelectedTab.Definition, byEntry.Keys, SelectedFilter.Filter, SearchText)
            .Select(s => new SectionViewModel(this, s.Kind, s.Entries.Select(e => byEntry[e]).ToList()))
            .ToList();
    }

    private static string ErrorText(Exception e) =>
        Elevation.IsAccessDenied(e) ? "Access denied. Restart as administrator to change this entry." : e.Message;
}
