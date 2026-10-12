using System.IO;
using System.Text;
using System.Windows;

using AutoRunManager.Core.Model;
using AutoRunManager.Core.Sources;

namespace AutoRunManager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // --list <file>: write all entries as tab-separated text and exit (diagnostics, no window).
        if (e.Args is ["--list", string file])
        {
            WriteList(file);
            Shutdown();
            return;
        }

        new MainWindow().Show();
    }

    private static void WriteList(string file)
    {
        CollectResult result = EntryCollector.CreateDefault().Collect();
        StringBuilder text = new();
        text.AppendLine("Kind\tState\tName\tCanToggle\tCanDelete\tAdmin\tExecutable\tCommand\tLocation\tDetails");
        foreach (StartupEntry entry in result.Entries)
        {
            text.AppendJoin('\t',
                entry.Kind, entry.State, entry.Name, entry.CanToggle, entry.CanDelete, entry.RequiresAdmin,
                entry.ExecutablePath, entry.Command, entry.Locator.Display, entry.Details?.ReplaceLineEndings(" | "));
            text.AppendLine();
        }

        foreach (string error in result.Errors)
        {
            text.AppendLine("ERROR\t" + error);
        }

        File.WriteAllText(file, text.ToString());
    }
}
