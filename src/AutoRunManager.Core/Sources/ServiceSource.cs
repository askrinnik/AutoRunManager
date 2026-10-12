using AutoRunManager.Core.Model;
using AutoRunManager.Core.Parsing;
using AutoRunManager.Core.Platform;

using Microsoft.Win32;

namespace AutoRunManager.Core.Sources;

/// <summary>Third-party Win32 services (see <see cref="ServiceRules.IsListed"/>).</summary>
public sealed class ServiceSource(IRegistry registry, IIndirectStrings strings, SystemContext context) : IEntrySource
{
    public const string ServicesKey = @"SYSTEM\CurrentControlSet\Services";

    public string Name => "Services";

    public IEnumerable<StartupEntry> Enumerate()
    {
        foreach (string serviceName in registry.GetSubKeyNames(RegistryHive.LocalMachine, ServicesKey))
        {
            string key = $@"{ServicesKey}\{serviceName}";
            string? imagePath = registry.GetString(RegistryHive.LocalMachine, key, "ImagePath");
            int type = registry.GetInt(RegistryHive.LocalMachine, key, "Type") ?? 0;
            if (!ServiceRules.IsListed(serviceName, imagePath, type, context.WindowsDirectory))
            {
                continue;
            }

            ServiceStartMode mode = ReadStartMode(registry, serviceName);
            string displayName = ServiceRules.CleanDisplayName(Resolve(registry.GetString(RegistryHive.LocalMachine, key, "DisplayName")), serviceName);
            string? description = Resolve(registry.GetString(RegistryHive.LocalMachine, key, "Description"));

            yield return new StartupEntry(
                EntryKind.Service,
                displayName,
                imagePath!,
                CommandLine.GetExecutablePath(imagePath),
                new ServiceLocator(serviceName),
                mode.IsAutoStart ? EntryState.Enabled : EntryState.Disabled,
                CanToggle: true,
                CanDelete: false,
                RequiresAdmin: true,
                Details: $"Service: {serviceName}{Environment.NewLine}Start type: {mode.Display}"
                    + (string.IsNullOrWhiteSpace(description) ? "" : Environment.NewLine + description.Trim()));
        }
    }

    public static ServiceStartMode ReadStartMode(IRegistry registry, string serviceName)
    {
        string key = $@"{ServicesKey}\{serviceName}";
        int start = registry.GetInt(RegistryHive.LocalMachine, key, "Start") ?? (int)ServiceStartType.Manual;
        bool delayed = registry.GetInt(RegistryHive.LocalMachine, key, "DelayedAutostart") == 1;
        ServiceStartType type = Enum.IsDefined((ServiceStartType)start) ? (ServiceStartType)start : ServiceStartType.Manual;
        return new ServiceStartMode(type, type == ServiceStartType.Automatic && delayed);
    }

    private string? Resolve(string? text) =>
        text is not null && text.StartsWith('@') ? strings.Resolve(text) : text;
}
