using System.Windows.Input;
using System.Windows.Media;

using AutoRunManager.Core.Model;
using AutoRunManager.Services;

namespace AutoRunManager.ViewModels;

public sealed class EntryViewModel : ObservableObject
{
    private const string AdminHint = "Needs administrator rights. Use \"Restart as administrator\".";

    private readonly MainViewModel _owner;
    private StartupEntry _entry;
    private ImageSource? _icon;
    private bool _iconLoaded;
    private bool _busy;

    public EntryViewModel(MainViewModel owner, StartupEntry entry)
    {
        _owner = owner;
        _entry = entry;
        ToggleCommand = new AsyncCommand(() => owner.ToggleAsync(this));
        DeleteCommand = new AsyncCommand(() => owner.DeleteAsync(this));
        OpenLocationCommand = new RelayCommand(() => owner.Run(() => ShellLauncher.OpenLocation(Entry)));
        ShowFileCommand = new RelayCommand(() => owner.Run(() => ShellLauncher.ShowInExplorer(Entry.ExecutablePath)));
    }

    public StartupEntry Entry
    {
        get => _entry;
        set
        {
            if (Set(ref _entry, value))
            {
                OnPropertyChanged(nameof(IsEnabled));
                OnPropertyChanged(nameof(Tooltip));
            }
        }
    }

    public string Name => _entry.Name;

    /// <summary>Scope tag (User, All users, HKCU, HKLM, HKLM 32-bit); null hides the tag.</summary>
    public string? Scope => _entry.Scope;

    public string PathText => _entry.Command.Replace("\"", "");

    public bool IsEnabled => _entry.IsEnabled;

    /// <summary>Whether the switch is shown; read-only entries have none.</summary>
    public bool ShowToggle => _entry.CanToggle;

    public bool ShowDelete => _entry.CanDelete;

    public bool IsLocked => _entry.RequiresAdmin && !_owner.IsElevated;

    public bool CanChange => !IsLocked && !_busy;

    public bool Busy
    {
        get => _busy;
        set
        {
            if (Set(ref _busy, value))
            {
                OnPropertyChanged(nameof(CanChange));
            }
        }
    }

    public string ToggleTooltip => IsLocked ? AdminHint : "Turn on or off";

    public string DeleteTooltip => IsLocked ? AdminHint : "Delete (a backup is saved first)";

    public string Tooltip
    {
        get
        {
            List<string> lines = [_entry.Locator.Display];
            if (!string.IsNullOrWhiteSpace(_entry.Details))
            {
                lines.Add(_entry.Details);
            }

            if (_entry.State == EntryState.Active)
            {
                lines.Add("Windows has no switch for this entry.");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>Loaded on first use, so only visible cards pay for the shell call.</summary>
    public ImageSource? Icon
    {
        get
        {
            if (!_iconLoaded)
            {
                _iconLoaded = true;
                _icon = IconLoader.Load(_entry.ExecutablePath);
            }

            return _icon;
        }
    }

    public ICommand ToggleCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand OpenLocationCommand { get; }

    public ICommand ShowFileCommand { get; }

    /// <summary>Makes the switch show the model state again after a failed change.</summary>
    public void RefreshState() => OnPropertyChanged(nameof(IsEnabled));
}
