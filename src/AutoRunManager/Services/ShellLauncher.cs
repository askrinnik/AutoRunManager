using System.ComponentModel;
using System.Diagnostics;
using System.IO;

using AutoRunManager.Core.Model;

using Microsoft.Win32;

namespace AutoRunManager.Services;

/// <summary>Opens the place where an entry is defined, or its program file, in the matching Windows tool.</summary>
public static class ShellLauncher
{
    private const string RegeditApplet = @"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit";

    public static void OpenLocation(StartupEntry entry)
    {
        switch (entry.Locator)
        {
            case RegistryValueLocator value:
                OpenRegedit(value);
                break;
            case FileLocator file:
                ShowInExplorer(file.Path);
                break;
            case PackagedTaskLocator:
                Start("ms-settings:startupapps");
                break;
            case ScheduledTaskLocator:
                Start("taskschd.msc");
                break;
            case ServiceLocator:
                Start("services.msc");
                break;
            default:
                if (entry.ExecutablePath is { } path)
                {
                    ShowInExplorer(path);
                }

                break;
        }
    }

    /// <summary>Selects the file in Explorer; does nothing if it does not exist.</summary>
    public static void ShowInExplorer(string? path)
    {
        if (path is not null && File.Exists(path))
        {
            Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
    }

    /// <summary>Starts this app again elevated; returns false if the user declined the UAC prompt.</summary>
    public static bool RestartAsAdmin()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" });
            return true;
        }
        catch (Win32Exception e) when (e.NativeErrorCode == 1223)
        {
            return false;
        }
    }

    // Regedit opens at its LastKey value; it ignores it when already running.
    private static void OpenRegedit(RegistryValueLocator value)
    {
        using (RegistryKey applet = Registry.CurrentUser.CreateSubKey(RegeditApplet))
        {
            applet.SetValue("LastKey", value.RegeditKeyName);
        }

        Start("regedit.exe");
    }

    private static void Start(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Win32Exception e) when (e.NativeErrorCode == 1223)
        {
            // The user declined the UAC prompt.
        }
    }
}
