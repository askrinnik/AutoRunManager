using AutoRunManager.Core.Model;
using AutoRunManager.Core.Parsing;
using AutoRunManager.Core.Platform;

using Microsoft.Win32;

namespace AutoRunManager.Core.Sources;

/// <summary>Files in the user's and the common Startup folder, with their <c>StartupApproved</c> state.</summary>
public sealed class StartupFolderSource(IRegistry registry, IFileSystem fileSystem, SystemContext context) : IEntrySource
{
    public const string ApprovedKey = RunKeySource.StartupApprovedKey + @"\StartupFolder";

    public string Name => "Startup folders";

    public IEnumerable<StartupEntry> Enumerate()
    {
        foreach ((string folder, RegistryHive hive) in Folders())
        {
            foreach (string file in fileSystem.GetFiles(folder))
            {
                string fileName = Path.GetFileName(file);
                if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                byte[]? approved = registry.GetValue(hive, ApprovedKey, fileName) as byte[];
                yield return new StartupEntry(
                    EntryKind.StartupFolder,
                    Path.GetFileNameWithoutExtension(fileName),
                    file,
                    file,
                    new FileLocator(file),
                    StartupApprovedValue.IsEnabled(approved) ? EntryState.Enabled : EntryState.Disabled,
                    CanToggle: true,
                    CanDelete: true,
                    RequiresAdmin: hive == RegistryHive.LocalMachine,
                    Details: RunKeySource.DisabledNote(approved));
            }
        }
    }

    /// <summary>The hive whose <c>StartupApproved\StartupFolder</c> key holds the state of a file.</summary>
    public static RegistryHive ApprovedHiveFor(SystemContext context, FileLocator locator) =>
        IsIn(locator.Path, context.CommonStartupFolder) ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;

    private IEnumerable<(string Folder, RegistryHive Hive)> Folders()
    {
        yield return (context.UserStartupFolder, RegistryHive.CurrentUser);
        yield return (context.CommonStartupFolder, RegistryHive.LocalMachine);
    }

    private static bool IsIn(string path, string folder) =>
        string.Equals(Path.GetDirectoryName(path)?.TrimEnd('\\'), folder.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
}
