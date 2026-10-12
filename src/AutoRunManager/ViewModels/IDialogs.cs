namespace AutoRunManager.ViewModels;

/// <summary>Modal dialogs in the app's style, owned by the main window.</summary>
public interface IDialogs
{
    bool Confirm(string text);

    void Message(string text);
}
