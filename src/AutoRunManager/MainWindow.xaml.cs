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
    private bool _closing;

    public MainWindow(bool elevated)
    {
        InitializeComponent();
        _model = new MainViewModel(new WindowDialogs(this), EntryCollector.CreateDefault, EntryActions.CreateDefault(), elevated);
        DataContext = _model;

        EntriesScroll.OpacityMask = new LinearGradientBrush(
            [_topStop, new GradientStop(Colors.Black, 0.05), new GradientStop(Colors.Black, 0.95), _bottomStop],
            new Point(0, 0),
            new Point(0, 1));

        InputBindings.Add(new KeyBinding(new RelayCommand(() => SearchBox.Focus()), Key.F, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(_model.RefreshCommand, Key.F5, ModifierKeys.None));
        WindowEffects.FadeIn(this);
        Loaded += async (_, _) => await _model.LoadAsync();
        _model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.SelectedTab) or nameof(MainViewModel.SelectedFilter) or nameof(MainViewModel.SearchText))
            {
                EntriesScroll.ScrollToTop();
            }
        };
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
