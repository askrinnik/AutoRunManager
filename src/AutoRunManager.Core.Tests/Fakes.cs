using AutoRunManager.Core.Platform;

using Microsoft.Win32;

namespace AutoRunManager.Core.Tests;

/// <summary>In-memory registry; key paths are case-insensitive like the real one.</summary>
internal sealed class FakeRegistry : IRegistry
{
    private readonly Dictionary<string, Dictionary<string, (object Value, RegistryValueKind Kind)>> _keys = new(StringComparer.OrdinalIgnoreCase);

    public FakeRegistry Set(RegistryHive hive, string keyPath, string valueName, object value, RegistryValueKind? kind = null)
    {
        SetValue(hive, keyPath, valueName, value, kind ?? KindOf(value));
        return this;
    }

    public FakeRegistry AddKey(RegistryHive hive, string keyPath)
    {
        Key(hive, keyPath, create: true);
        return this;
    }

    public bool KeyExists(RegistryHive hive, string keyPath) => Key(hive, keyPath, create: false) is not null;

    public IReadOnlyList<string> GetSubKeyNames(RegistryHive hive, string keyPath)
    {
        string prefix = Full(hive, keyPath) + @"\";
        return _keys.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(k => k[prefix.Length..].Split('\\')[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> GetValueNames(RegistryHive hive, string keyPath) =>
        Key(hive, keyPath, create: false)?.Keys.ToList() ?? [];

    public object? GetValue(RegistryHive hive, string keyPath, string valueName) =>
        Key(hive, keyPath, create: false) is { } key && key.TryGetValue(valueName, out var value) ? value.Value : null;

    public RegistryValueKind? GetValueKind(RegistryHive hive, string keyPath, string valueName) =>
        Key(hive, keyPath, create: false) is { } key && key.TryGetValue(valueName, out var value) ? value.Kind : null;

    public void SetValue(RegistryHive hive, string keyPath, string valueName, object value, RegistryValueKind kind) =>
        Key(hive, keyPath, create: true)![valueName] = (value, kind);

    public void DeleteValue(RegistryHive hive, string keyPath, string valueName) =>
        Key(hive, keyPath, create: false)?.Remove(valueName);

    private Dictionary<string, (object Value, RegistryValueKind Kind)>? Key(RegistryHive hive, string keyPath, bool create)
    {
        string full = Full(hive, keyPath);
        if (_keys.TryGetValue(full, out var key))
        {
            return key;
        }

        if (!create)
        {
            return null;
        }

        // Create the parents too, so GetSubKeyNames sees intermediate keys.
        string[] parts = full.Split('\\');
        for (int i = 2; i <= parts.Length; i++)
        {
            string path = string.Join('\\', parts[..i]);
            if (!_keys.ContainsKey(path))
            {
                _keys[path] = new(StringComparer.OrdinalIgnoreCase);
            }
        }

        return _keys[full];
    }

    private static string Full(RegistryHive hive, string keyPath) => $@"{hive}\{keyPath.Trim('\\')}";

    private static RegistryValueKind KindOf(object value) => value switch
    {
        int => RegistryValueKind.DWord,
        byte[] => RegistryValueKind.Binary,
        string[] => RegistryValueKind.MultiString,
        _ => RegistryValueKind.String,
    };
}

internal sealed class FakeFileSystem : IFileSystem
{
    public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

    public FakeFileSystem Add(string path, string text = "")
    {
        Files[path] = text;
        return this;
    }

    public IReadOnlyList<string> GetFiles(string directory) =>
        Files.Keys.Where(f => string.Equals(Path.GetDirectoryName(f), directory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)).ToList();

    public bool FileExists(string path) => Files.ContainsKey(path);

    public string? ReadAllText(string path) => Files.TryGetValue(path, out string? text) ? text : null;

    public Dictionary<string, System.Text.Encoding?> Encodings { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void WriteAllText(string path, string text, System.Text.Encoding? encoding = null)
    {
        Files[path] = text;
        Encodings[path] = encoding;
    }

    public void Copy(string source, string target) =>
        Files[target] = Files.TryGetValue(source, out string? text) ? text : throw new FileNotFoundException(source);

    public void Delete(string path) => Files.Remove(path);
}

internal sealed class FakeTaskScheduler(params ScheduledTaskInfo[] tasks) : ITaskScheduler
{
    public List<string> VisitedFolders { get; } = [];

    public IReadOnlyList<ScheduledTaskInfo> GetTasks(Func<string, bool> includeFolder)
    {
        List<ScheduledTaskInfo> result = [];
        foreach (ScheduledTaskInfo task in tasks)
        {
            string folder = Parsing.TaskNames.Split(task.Path).Folder;
            VisitedFolders.Add(folder);
            if (includeFolder(folder))
            {
                result.Add(task);
            }
        }

        return result;
    }

    public Dictionary<string, bool> EnabledChanges { get; } = [];

    public List<string> Deleted { get; } = [];

    public void SetEnabled(string taskPath, bool enabled) => EnabledChanges[taskPath] = enabled;

    public string GetXml(string taskPath) => $"<Task path=\"{taskPath}\" />";

    public void Delete(string taskPath) => Deleted.Add(taskPath);
}

internal sealed class FakeIndirectStrings(Dictionary<string, string>? known = null) : IIndirectStrings
{
    public List<string> Requests { get; } = [];

    public string? Resolve(string indirect)
    {
        Requests.Add(indirect);
        return known is not null && known.TryGetValue(indirect, out string? text) ? text : null;
    }
}

internal static class TestContext
{
    public static readonly SystemContext Default = new(
        @"C:\Windows",
        @"C:\Users\me\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup",
        @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Startup",
        @"PC\me",
        "S-1-5-21-1-2-3-1001");
}

internal sealed class FakeWmi(params WmiSubscriptionInfo[] subscriptions) : IWmiSubscriptions
{
    public List<string[]> Deleted { get; } = [];

    public IReadOnlyList<WmiSubscriptionInfo> GetSubscriptions() => subscriptions;

    public void Delete(string bindingPath, string consumerPath, string filterPath) => Deleted.Add([bindingPath, consumerPath, filterPath]);
}

internal sealed class FakeServiceControl : IServiceControl
{
    public List<(string Name, Parsing.ServiceStartMode Mode)> Changes { get; } = [];

    public void SetStartMode(string serviceName, Parsing.ServiceStartMode mode) => Changes.Add((serviceName, mode));
}

internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
