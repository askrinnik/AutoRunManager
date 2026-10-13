using AutoRunManager.Core.Model;
using AutoRunManager.Core.Platform;
using AutoRunManager.Core.Presentation;

namespace AutoRunManager.Core.Sources;

public sealed record CollectResult(IReadOnlyList<StartupEntry> Entries, IReadOnlyList<string> Errors);

/// <summary>
/// Runs all sources and sets the scope tag of each entry; a failing source is reported, the others
/// still contribute.
/// </summary>
public sealed class EntryCollector(IReadOnlyList<IEntrySource> sources, SystemContext context)
{
    public static EntryCollector CreateDefault()
    {
        WindowsRegistry registry = new();
        LocalFileSystem fileSystem = new();
        ShellIndirectStrings strings = new();
        SystemContext context = SystemContext.Current();
        return new EntryCollector(
        [
            new RunKeySource(registry),
            new StartupFolderSource(registry, fileSystem, context),
            new PackagedAppSource(registry, fileSystem, strings),
            new ScheduledTaskSource(new ComTaskScheduler(), registry, context),
            new ServiceSource(registry, strings, context),
            new RegistryLaunchPointSource(registry, context),
            new WmiSubscriptionSource(new WmiSubscriptions()),
        ], context);
    }

    public IReadOnlyList<IEntrySource> Sources => sources;

    public CollectResult Collect()
    {
        List<StartupEntry> entries = [];
        List<string> errors = [];
        foreach (IEntrySource source in sources)
        {
            try
            {
                entries.AddRange(source.Enumerate().Select(e => e with { Scope = EntryScope.Tag(e, context) }));
            }
            catch (Exception e)
            {
                errors.Add($"{source.Name}: {e.Message}");
            }
        }

        return new CollectResult(entries, errors);
    }
}
