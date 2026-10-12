using System.Text.Json;
using System.Text.Json.Serialization;

using AutoRunManager.Core.Parsing;
using AutoRunManager.Core.Platform;

namespace AutoRunManager.Core.Actions;

/// <summary>
/// What the app remembers between runs: the start mode a service had before the user turned it
/// off. Stored as JSON in <c>%LOCALAPPDATA%\AutoRunManager\state.json</c>.
/// </summary>
public sealed class AppState
{
    private static readonly JsonSerializerOptions s_json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IFileSystem _fileSystem;
    private readonly string _path;

    private AppState(IFileSystem fileSystem, string path, Dictionary<string, ServiceStartMode> serviceStartModes)
    {
        _fileSystem = fileSystem;
        _path = path;
        ServiceStartModes = serviceStartModes;
    }

    public static string DefaultFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoRunManager");

    /// <summary>Saved start modes by service name (case-insensitive).</summary>
    public Dictionary<string, ServiceStartMode> ServiceStartModes { get; }

    /// <summary>Loads the state; a missing or unreadable file gives an empty state.</summary>
    public static AppState Load(IFileSystem fileSystem, string path)
    {
        Dictionary<string, ServiceStartMode> modes = new(StringComparer.OrdinalIgnoreCase);
        if (fileSystem.ReadAllText(path) is { } json)
        {
            try
            {
                if (JsonSerializer.Deserialize<StateFile>(json, s_json)?.ServiceStartModes is { } stored)
                {
                    foreach ((string name, ServiceStartMode mode) in stored)
                    {
                        modes[name] = mode;
                    }
                }
            }
            catch (JsonException)
            {
            }
        }

        return new AppState(fileSystem, path, modes);
    }

    public void Save() =>
        _fileSystem.WriteAllText(_path, JsonSerializer.Serialize(new StateFile(ServiceStartModes), s_json));

    private sealed record StateFile(Dictionary<string, ServiceStartMode>? ServiceStartModes);
}
