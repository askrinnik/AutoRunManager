using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using AutoRunManager.Core.Actions;
using AutoRunManager.Core.Sources;
using AutoRunManager.Dialogs;
using AutoRunManager.ViewModels;

namespace AutoRunManager;

public partial class MainWindow : Window
{
    // Edges of the list fade out while there is more content in that direction (prototype behavior).
    private readonly GradientStop _topStop = new(Colors.Black, 0.0);
    private readonly GradientStop _bottomStop = new(Colors.Black, 1.0);
    private readonly MainViewModel _model;
    private readonly AppState _state;
    private readonly bool _rememberBounds;
    private readonly TaskCompletionSource _firstLoad = new();
    private bool _closing;

    public MainWindow(bool elevated, AppState state)
        : this(elevated, state, EntryCollector.CreateDefault, rememberBounds: true)
    {
    }

    /// <param name="rememberBounds">Whether to use and save the window bounds in <paramref name="state"/>; off for screenshots.</param>
    internal MainWindow(bool elevated, AppState state, Func<EntryCollector> createCollector, bool rememberBounds)
    {
        InitializeComponent();
        _state = state;
        _rememberBounds = rememberBounds;
        if (rememberBounds)
        {
            ApplySavedBounds(state.Window);
        }

        _model = new MainViewModel(new WindowDialogs(this), createCollector, EntryActions.CreateDefault(state), elevated);
        DataContext = _model;

        EntriesScroll.OpacityMask = new LinearGradientBrush(
            [_topStop, new GradientStop(Colors.Black, 0.05), new GradientStop(Colors.Black, 0.95), _bottomStop],
            new Point(0, 0),
            new Point(0, 1));

        InputBindings.Add(new KeyBinding(new RelayCommand(() => SearchBox.Focus()), Key.F, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(_model.RefreshCommand, Key.F5, ModifierKeys.None));
        WindowEffects.FadeIn(this);
        Loaded += async (_, _) =>
        {
            await _model.LoadAsync();
            _firstLoad.TrySetResult();
        };
        _model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.SelectedNavigation) or nameof(MainViewModel.SelectedFilter) or nameof(MainViewModel.SearchText))
            {
                EntriesScroll.ScrollToTop();
            }
        };
    }

    internal MainViewModel Model => _model;

    /// <summary>Completes when the entries have been read for the first time.</summary>
    internal Task FirstLoad => _firstLoad.Task;

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        if (!_rememberBounds)
        {
            return;
        }

        Rect bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        _state.Window = new WindowBounds(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        try
        {
            _state.Save();
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            // Losing the window position is not worth an error on exit.
        }
    }

    /// <summary>Uses the saved bounds if the window would still be reachable; otherwise keeps the centered default.</summary>
    private void ApplySavedBounds(WindowBounds? saved)
    {
        if (saved is not null
            && saved.IsReachableOn(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = saved.Left;
            Top = saved.Top;
            Width = Math.Max(saved.Width, MinWidth);
            Height = Math.Max(saved.Height, MinHeight);
        }
    }

    private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnCloseClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (!_closing)
        {
            _closing = true;
            WindowEffects.FadeOut(this, Close);
        }
    }

    private void OnEntriesScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        ScrollViewer scroll = (ScrollViewer)sender;
        _topStop.Color = scroll.VerticalOffset > 0 ? Colors.Transparent : Colors.Black;
        _bottomStop.Color = scroll.VerticalOffset < scroll.ScrollableHeight ? Colors.Transparent : Colors.Black;
    }
}
