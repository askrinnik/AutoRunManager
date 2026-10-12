using System.Text;

using AutoRunManager.Core.Model;
using AutoRunManager.Core.Parsing;
using AutoRunManager.Core.Platform;
using AutoRunManager.Core.Sources;

using Microsoft.Win32;

namespace AutoRunManager.Core.Actions;

/// <summary>Turns entries off and on and deletes them (after a backup).</summary>
public sealed class EntryActions(
    IRegistry registry,
    IFileSystem fileSystem,
    ITaskScheduler scheduler,
    IServiceControl services,
    IWmiSubscriptions wmi,
    SystemContext context,
    AppState state,
    BackupStore backups,
    TimeProvider time)
{
    public static EntryActions CreateDefault(AppState state)
    {
        LocalFileSystem fileSystem = new();
        return new EntryActions(
            new WindowsRegistry(),
            fileSystem,
            new ComTaskScheduler(),
            new ServiceControl(),
            new WmiSubscriptions(),
            SystemContext.Current(),
            state,
            new BackupStore(fileSystem, BackupStore.DefaultFolder, TimeProvider.System),
            TimeProvider.System);
    }

    public void SetEnabled(StartupEntry entry, bool enabled)
    {
        if (!entry.CanToggle)
        {
            throw new InvalidOperationException($"'{entry.Name}' cannot be turned on or off.");
        }

        switch (entry.Locator)
        {
            case RegistryValueLocator run when entry.Kind == EntryKind.RunKey:
                SetApproved(run.Hive, RunKeySource.ApprovedKeyFor(run), run.ValueName, enabled);
                break;
            case FileLocator file when entry.Kind == EntryKind.StartupFolder:
                SetApproved(StartupFolderSource.ApprovedHiveFor(context, file), StartupFolderSource.ApprovedKey, Path.GetFileName(file.Path), enabled);
                break;
            case PackagedTaskLocator package:
                registry.SetValue(RegistryHive.CurrentUser, $@"{PackagedStartupTasks.SystemAppDataKey}\{package.PackageFamilyName}\{package.TaskId}",
                    "State", (int)PackagedStartupTasks.StateFor(enabled), RegistryValueKind.DWord);
                break;
            case ScheduledTaskLocator task:
                scheduler.SetEnabled(task.TaskPath, enabled);
                break;
            case ServiceLocator service:
                SetServiceEnabled(service.ServiceName, enabled);
                break;
            default:
                throw new InvalidOperationException($"No toggle for {entry.Kind}.");
        }
    }

    /// <summary>Deletes the entry and returns the path of its backup.</summary>
    public string Delete(StartupEntry entry)
    {
        if (!entry.CanDelete)
        {
            throw new InvalidOperationException($"'{entry.Name}' cannot be deleted.");
        }

        switch (entry.Locator)
        {
            case RegistryValueLocator value:
                {
                    List<RegistryValueLocator> values = [value];
                    if (entry.Kind == EntryKind.RunKey)
                    {
                        values.Add(new RegistryValueLocator(value.Hive, RunKeySource.ApprovedKeyFor(value), value.ValueName));
                    }

                    string backup = backups.SaveRegistry(entry, ReadExisting(values));
                    foreach (RegistryValueLocator locator in values)
                    {
                        registry.DeleteValue(locator.Hive, locator.KeyPath, locator.ValueName);
                    }

                    return backup;
                }

            case FileLocator file:
                {
                    RegistryValueLocator approved = new(StartupFolderSource.ApprovedHiveFor(context, file), StartupFolderSource.ApprovedKey, Path.GetFileName(file.Path));
                    string backup = backups.SaveFile(entry, file.Path);
                    if (ReadExisting([approved]).ToList() is [_, ..] approvedValue)
                    {
                        backups.SaveRegistry(entry, approvedValue);
                    }

                    fileSystem.Delete(file.Path);
                    registry.DeleteValue(approved.Hive, approved.KeyPath, approved.ValueName);
                    return backup;
                }

            case ScheduledTaskLocator task:
                {
                    string backup = backups.SaveText(entry, ".xml", scheduler.GetXml(task.TaskPath));
                    scheduler.Delete(task.TaskPath);
                    return backup;
                }

            case WmiSubscriptionLocator subscription:
                {
                    string backup = backups.SaveText(entry, ".txt", Describe(entry, subscription));
                    wmi.Delete(subscription.BindingPath, subscription.ConsumerPath, subscription.FilterPath);
                    return backup;
                }

            default:
                throw new InvalidOperationException($"{entry.Kind} entries cannot be deleted.");
        }
    }

    private void SetApproved(RegistryHive hive, string key, string valueName, bool enabled) =>
        registry.SetValue(hive, key, valueName, StartupApprovedValue.Create(enabled, time.GetUtcNow().UtcDateTime), RegistryValueKind.Binary);

    private void SetServiceEnabled(string serviceName, bool enabled)
    {
        if (enabled)
        {
            ServiceStartMode? saved = state.ServiceStartModes.TryGetValue(serviceName, out ServiceStartMode mode) ? mode : null;
            services.SetStartMode(serviceName, ServiceRules.OnMode(saved));
            if (state.ServiceStartModes.Remove(serviceName))
            {
                state.Save();
            }

            return;
        }

        ServiceStartMode current = ServiceSource.ReadStartMode(registry, serviceName);
        services.SetStartMode(serviceName, ServiceRules.OffMode);
        if (current.IsAutoStart)
        {
            state.ServiceStartModes[serviceName] = current;
            state.Save();
        }
    }

    private IEnumerable<(RegistryValueLocator, object, RegistryValueKind)> ReadExisting(IEnumerable<RegistryValueLocator> locators)
    {
        foreach (RegistryValueLocator locator in locators)
        {
            if (registry.GetValue(locator.Hive, locator.KeyPath, locator.ValueName) is { } value
                && registry.GetValueKind(locator.Hive, locator.KeyPath, locator.ValueName) is { } kind)
            {
                yield return (locator, value, kind);
            }
        }
    }

    private static string Describe(StartupEntry entry, WmiSubscriptionLocator subscription)
    {
        StringBuilder text = new();
        text.AppendLine($"Name: {entry.Name}");
        text.AppendLine($"Command: {entry.Command}");
        text.AppendLine($"Details: {entry.Details}");
        text.AppendLine($"Filter: {subscription.FilterPath}");
        text.AppendLine($"Consumer: {subscription.ConsumerPath}");
        text.AppendLine($"Binding: {subscription.BindingPath}");
        return text.ToString();
    }
}
