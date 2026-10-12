using System.Windows;

using AutoRunManager.ViewModels;

namespace AutoRunManager.Dialogs;

/// <summary>Borderless confirm and message dialog of the prototype, with fade in and out.</summary>
public partial class DialogWindow : Window
{
    private bool _result;
    private bool _closing;

    private DialogWindow(Window owner, string text, bool confirm)
    {
        InitializeComponent();
        Owner = owner;
        MessageText.Text = text;
        if (!confirm)
        {
            YesButton.Content = "OK";
            YesButton.IsCancel = true;
            YesButton.Margin = new Thickness(0);
            NoButton.Visibility = Visibility.Collapsed;
        }

        WindowEffects.FadeIn(this);
    }

    public static bool Confirm(Window owner, string text)
    {
        DialogWindow dialog = new(owner, text, confirm: true);
        dialog.ShowDialog();
        return dialog._result;
    }

    public static void Message(Window owner, string text) => new DialogWindow(owner, text, confirm: false).ShowDialog();

    private void OnYes(object sender, RoutedEventArgs e)
    {
        _result = NoButton.Visibility == Visibility.Visible;
        FadeOutAndClose();
    }

    private void OnNo(object sender, RoutedEventArgs e) => FadeOutAndClose();

    private void FadeOutAndClose()
    {
        if (!_closing)
        {
            _closing = true;
            WindowEffects.FadeOut(this, Close);
        }
    }
}

/// <summary><see cref="IDialogs"/> owned by a window.</summary>
public sealed class WindowDialogs(Window owner) : IDialogs
{
    public bool Confirm(string text) => DialogWindow.Confirm(owner, text);

    public void Message(string text) => DialogWindow.Message(owner, text);
}
