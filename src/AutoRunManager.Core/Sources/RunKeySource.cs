using AutoRunManager.Core.Model;
using AutoRunManager.Core.Parsing;
using AutoRunManager.Core.Platform;

using Microsoft.Win32;

namespace AutoRunManager.Core.Sources;

/// <summary><c>Run</c> keys that Task Manager shows, with their <c>StartupApproved</c> state.</summary>
public sealed class RunKeySource(IRegistry registry) : IEntrySource
{
    public const string StartupApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

    /// <param name="ApprovedSubKey">Subkey of <see cref="StartupApprovedKey"/> in the same hive.</param>
    public sealed record Location(RegistryHive Hive, string KeyPath, string ApprovedSubKey);

    public static readonly IReadOnlyList<Location> Locations =
    [
        new(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "Run"),
        new(RegistryHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "Run"),
        new(RegistryHive.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "Run32"),
    ];

    public string Name => "Run keys";

    public IEnumerable<StartupEntry> Enumerate()
    {
        foreach (Location location in Locations)
        {
            string approvedKey = $@"{StartupApprovedKey}\{location.ApprovedSubKey}";
            foreach (string valueName in registry.GetValueNames(location.Hive, location.KeyPath))
            {
                string? command = registry.GetString(location.Hive, location.KeyPath, valueName);
                if (valueName.Length == 0 && string.IsNullOrWhiteSpace(command))
                {
                    continue;
                }

                byte[]? approved = registry.GetValue(location.Hive, approvedKey, valueName) as byte[];
                yield return new StartupEntry(
                    EntryKind.RunKey,
                    valueName.Length > 0 ? valueName : "(Default)",
                    command ?? "",
                    CommandLine.GetExecutablePath(command),
                    new RegistryValueLocator(location.Hive, location.KeyPath, valueName),
                    StartupApprovedValue.IsEnabled(approved) ? EntryState.Enabled : EntryState.Disabled,
                    CanToggle: true,
                    CanDelete: true,
                    RequiresAdmin: location.Hive == RegistryHive.LocalMachine,
                    Details: DisabledNote(approved));
            }
        }
    }

    /// <summary>The <c>StartupApproved</c> key that holds the state of a Run value.</summary>
    public static string ApprovedKeyFor(RegistryValueLocator locator)
    {
        Location location = Locations.First(l => l.Hive == locator.Hive && l.KeyPath.Equals(locator.KeyPath, StringComparison.OrdinalIgnoreCase));
        return $@"{StartupApprovedKey}\{location.ApprovedSubKey}";
    }

    internal static string? DisabledNote(byte[]? approved) =>
        StartupApprovedValue.DisabledAtUtc(approved) is { } at ? $"Disabled on {at.ToLocalTime():g}" : null;
}
