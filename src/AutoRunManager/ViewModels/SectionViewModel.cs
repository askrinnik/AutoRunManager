using System.Windows.Input;

using AutoRunManager.Core.Model;

namespace AutoRunManager.ViewModels;

/// <summary>Entries of one location inside a tab, under a clickable header.</summary>
public sealed class SectionViewModel
{
    public SectionViewModel(MainViewModel owner, EntryKind kind, IReadOnlyList<EntryViewModel> entries)
    {
        Kind = kind;
        Entries = entries;
        ToggleAllCommand = new AsyncCommand(() => owner.ToggleSectionAsync(this));
    }

    public EntryKind Kind { get; }

    public string Title => Kind.DisplayName().ToUpperInvariant() + $"  {Entries.Count}";

    public IReadOnlyList<EntryViewModel> Entries { get; }

    public bool HasToggles => Entries.Any(e => e.ShowToggle);

    public string HeaderTooltip => HasToggles ? "Click to turn all entries of this section on or off" : "";

    public ICommand ToggleAllCommand { get; }
}
