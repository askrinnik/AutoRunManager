using AutoRunManager.Core.Model;
using AutoRunManager.Core.Parsing;
using AutoRunManager.Core.Platform;

namespace AutoRunManager.Core.Sources;

/// <summary>WMI permanent event subscriptions that run a command or a script.</summary>
public sealed class WmiSubscriptionSource(IWmiSubscriptions wmi) : IEntrySource
{
    public string Name => "WMI subscriptions";

    public IEnumerable<StartupEntry> Enumerate() =>
        wmi.GetSubscriptions().Select(s => new StartupEntry(
            EntryKind.WmiSubscription,
            s.ConsumerName,
            s.Command,
            s.ConsumerClass == "CommandLineEventConsumer" ? CommandLine.GetExecutablePath(s.Command) : null,
            new WmiSubscriptionLocator(s.FilterPath, s.ConsumerPath, s.BindingPath),
            EntryState.Active,
            CanToggle: false,
            CanDelete: true,
            RequiresAdmin: true,
            Details: $"{s.ConsumerClass}" + (s.Query is null ? "" : $"{Environment.NewLine}Trigger: {s.Query}")));
}
