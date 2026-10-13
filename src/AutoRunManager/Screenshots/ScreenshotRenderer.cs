using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using AutoRunManager.Core.Actions;
using AutoRunManager.Core.Sources;
using AutoRunManager.ViewModels;

namespace AutoRunManager.Screenshots;

/// <summary>
/// Renders the main window with <see cref="DemoSource"/> entries to PNG files, one per navigation
/// group, for the README. The window stays off screen; nothing is changed.
/// </summary>
internal static class ScreenshotRenderer
{
    private const double Scale = 2;

    // Switches animate into their state when the cards appear (150 ms in Controls.xaml).
    private static readonly TimeSpan s_switchAnimation = TimeSpan.FromMilliseconds(400);

    public static async Task RenderAsync(string folder)
    {
        Directory.CreateDirectory(folder);
        MainWindow window = new(elevated: true, AppState.LoadDefault(), () => new EntryCollector([new DemoSource()], DemoSource.Context), rememberBounds: false)
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -10000,
            Top = -10000,
            ShowActivated = false,
            ShowInTaskbar = false,
        };
        window.Show();
        await window.FirstLoad;

        foreach (NavigationItemViewModel group in window.Model.Navigation.Where(n => n.IsGroup))
        {
            window.Model.SelectedNavigation = group;
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await Task.Delay(s_switchAnimation);
            Save((FrameworkElement)window.Content, Path.Combine(folder, group.Item.Group.Title.ToLowerInvariant() + ".png"));
        }

        window.Close();
    }

    /// <summary>Renders the window content (not the window, whose opacity fades in) at <see cref="Scale"/>.</summary>
    private static void Save(FrameworkElement content, string file)
    {
        RenderTargetBitmap bitmap = new(
            (int)Math.Ceiling(content.ActualWidth * Scale), (int)Math.Ceiling(content.ActualHeight * Scale), 96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
        bitmap.Render(content);
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(file);
        encoder.Save(stream);
    }
}
