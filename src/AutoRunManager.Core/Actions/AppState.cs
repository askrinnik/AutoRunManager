using System.Text.Json;
using System.Text.Json.Serialization;

using AutoRunManager.Core.Parsing;
using AutoRunManager.Core.Platform;

namespace AutoRunManager.Core.Actions;

/// <summary>Position and size of the main window in device-independent pixels.</summary>
public sealed record WindowBounds(double Left, double Top, double Width, double Height)
{
    /// <summary>
    /// Whether the top of the window (where it can be dragged) lies at least partly on the given
    /// screen area, so a window saved on a monitor that is gone is not restored off-screen.
    /// </summary>
    public bool IsReachableOn(double screenLeft, double screenTop, double screenWidth, double screenHeight)
    {
        const double grip = 40;
        return Width > 0 && Height > 0
            && Left + Width - grip >= screenLeft && Left + grip <= screenLeft + screenWidth
            && Top >= screenTop && Top + grip <= screenTop + screenHeight;
    }
}

/// <summary>
/// What the app remembers between runs: the start mode a service had before the user turned it
/// off, and the main window bounds. Stored as JSON in <see cref="FileName"/> next to the executable,
/// so the app stays portable.
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

    public const string FileName = "AutoRunManager.state.json";

    /// <summary>The folder of the executable; state and backups live there (portable app).</summary>
    public static string DefaultFolder => AppContext.BaseDirectory;

    /// <summary>Loads <see cref="FileName"/> from <see cref="DefaultFolder"/>.</summary>
    public static AppState LoadDefault() => Load(new LocalFileSystem(), Path.Combine(DefaultFolder, FileName));

    /// <summary>Saved start modes by service name (case-insensitive).</summary>
    public Dictionary<string, ServiceStartMode> ServiceStartModes { get; }

    public WindowBounds? Window { get; set; }

    /// <summary>Loads the state; a missing or unreadable file gives an empty state.</summary>
    public static AppState Load(IFileSystem fileSystem, string path)
    {
        Dictionary<string, ServiceStartMode> modes = new(StringComparer.OrdinalIgnoreCase);
        WindowBounds? window = null;
        if (fileSystem.ReadAllText(path) is { } json)
        {
            try
            {
                StateFile? file = JsonSerializer.Deserialize<StateFile>(json, s_json);
                window = file?.Window;
                if (file?.ServiceStartModes is { } stored)
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

        return new AppState(fileSystem, path, modes) { Window = window };
    }

    public void Save() =>
        _fileSystem.WriteAllText(_path, JsonSerializer.Serialize(new StateFile(ServiceStartModes, Window), s_json));

    private sealed record StateFile(Dictionary<string, ServiceStartMode>? ServiceStartModes, WindowBounds? Window);
}
