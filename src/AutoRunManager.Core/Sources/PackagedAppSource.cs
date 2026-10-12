using AutoRunManager.Core.Model;
using AutoRunManager.Core.Parsing;
using AutoRunManager.Core.Platform;

using Microsoft.Win32;

namespace AutoRunManager.Core.Sources;

/// <summary>Startup tasks of packaged (MSIX/Store) apps installed for the current user.</summary>
public sealed class PackagedAppSource(IRegistry registry, IFileSystem fileSystem, IIndirectStrings strings) : IEntrySource
{
    public const string RepositoryKey =
        @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";

    public string Name => "Store apps";

    public IEnumerable<StartupEntry> Enumerate()
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (string fullName in registry.GetSubKeyNames(RegistryHive.CurrentUser, RepositoryKey))
        {
            string packageKey = $@"{RepositoryKey}\{fullName}";
            string? root = registry.GetString(RegistryHive.CurrentUser, packageKey, "PackageRootFolder");
            string? family = PackagedStartupTasks.FamilyNameFromFullName(fullName);
            if (root is null || family is null || fileSystem.ReadAllText(Path.Combine(root, "AppxManifest.xml")) is not { } manifest)
            {
                continue;
            }

            IReadOnlyList<ManifestStartupTask> tasks;
            try
            {
                tasks = PackagedStartupTasks.ParseManifest(manifest).Tasks;
            }
            catch (System.Xml.XmlException)
            {
                continue;
            }

            foreach (ManifestStartupTask task in tasks)
            {
                if (!seen.Add($"{family}|{task.TaskId}"))
                {
                    continue;
                }

                int? state = registry.GetInt(RegistryHive.CurrentUser, $@"{PackagedStartupTasks.SystemAppDataKey}\{family}\{task.TaskId}", "State");
                (bool enabled, bool canToggle) = PackagedStartupTasks.Evaluate(state, task.EnabledByDefault);
                string packageName = ResolveText(registry.GetString(RegistryHive.CurrentUser, packageKey, "DisplayName"), fullName)
                    ?? fullName.Split('_')[0];
                string name = ResolveText(task.DisplayName, fullName) ?? packageName;
                string executable = task.Executable.Length > 0 ? Path.Combine(root, task.Executable) : root;

                yield return new StartupEntry(
                    EntryKind.PackagedApp,
                    name,
                    executable,
                    executable,
                    new PackagedTaskLocator(family, task.TaskId),
                    enabled ? EntryState.Enabled : EntryState.Disabled,
                    CanToggle: canToggle,
                    CanDelete: false,
                    RequiresAdmin: false,
                    Details: name == packageName ? fullName : $"{packageName} · {fullName}");
            }
        }
    }

    private string? ResolveText(string? value, string packageFullName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string? indirect = value.StartsWith('@') ? value : ToIndirectString(value, packageFullName);
        return indirect is null ? value : strings.Resolve(indirect);
    }

    /// <summary>
    /// Turns a manifest <c>ms-resource:</c> reference into an indirect string for
    /// <c>SHLoadIndirectString</c>; <see langword="null"/> for literal text.
    /// </summary>
    internal static string? ToIndirectString(string value, string packageFullName)
    {
        const string prefix = "ms-resource:";
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string rest = value[prefix.Length..];
        string identity = packageFullName.Split('_')[0];
        string uri = rest.StartsWith("//", StringComparison.Ordinal) ? prefix + rest
            : rest.StartsWith('/') ? $"{prefix}//{identity}{rest}"
            : rest.Contains('/') ? $"{prefix}//{identity}/{rest}"
            : $"{prefix}//{identity}/resources/{rest}";
        return $"@{{{packageFullName}?{uri}}}";
    }
}
