using System.Text;

using AutoRunManager.Core.Model;
using AutoRunManager.Core.Platform;

using Microsoft.Win32;

namespace AutoRunManager.Core.Actions;

/// <summary>
/// Saves the definition of an entry before it is deleted, in a form the user can restore by hand:
/// <c>.reg</c> for registry values, the file itself for Startup folder items, task XML for
/// scheduled tasks, a text description for WMI subscriptions.
/// </summary>
public sealed class BackupStore(IFileSystem fileSystem, string folder, TimeProvider time)
{
    public static string DefaultFolder => Path.Combine(AppState.DefaultFolder, "backups");

    public string Folder => folder;

    public string SaveRegistry(StartupEntry entry, IEnumerable<(RegistryValueLocator Locator, object Value, RegistryValueKind Kind)> values)
    {
        string path = PathFor(entry, ".reg");
        fileSystem.WriteAllText(path, RegFile.Export(values), Encoding.Unicode);
        return path;
    }

    public string SaveFile(StartupEntry entry, string file)
    {
        string path = PathFor(entry, Path.GetExtension(file));
        fileSystem.Copy(file, path);
        return path;
    }

    public string SaveText(StartupEntry entry, string extension, string text)
    {
        string path = PathFor(entry, extension);
        fileSystem.WriteAllText(path, text);
        return path;
    }

    internal string PathFor(StartupEntry entry, string extension)
    {
        string stamp = time.GetLocalNow().ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        return Path.Combine(folder, $"{stamp} {entry.Kind} {SafeName(entry.Name)}{extension}");
    }

    internal static string SafeName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string safe = new(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        safe = safe.Trim().TrimEnd('.');
        return safe.Length > 60 ? safe[..60] : safe.Length > 0 ? safe : "entry";
    }
}
