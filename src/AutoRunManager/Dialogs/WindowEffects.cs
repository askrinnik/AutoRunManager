using System.Windows;
using System.Windows.Media.Animation;

namespace AutoRunManager.Dialogs;

/// <summary>The prototype's 200 ms fades.</summary>
public static class WindowEffects
{
    private static readonly Duration s_duration = new(TimeSpan.FromMilliseconds(200));

    public static void FadeIn(Window window)
    {
        window.Opacity = 0;
        window.Loaded += (_, _) => window.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, s_duration) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
    }

    public static void FadeOut(Window window, Action completed)
    {
        DoubleAnimation fade = new(window.Opacity, 0, s_duration) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
        fade.Completed += (_, _) => completed();
        window.BeginAnimation(UIElement.OpacityProperty, fade);
    }
}
