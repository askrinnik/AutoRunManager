using System.Text.RegularExpressions;

namespace AutoRunManager.Core.Parsing;

/// <summary>Values of the <c>Start</c> registry value of a service.</summary>
public enum ServiceStartType
{
    Boot = 0,
    System = 1,
    Automatic = 2,
    Manual = 3,
    Disabled = 4,
}

public readonly record struct ServiceStartMode(ServiceStartType Type, bool DelayedStart = false)
{
    public static readonly ServiceStartMode Automatic = new(ServiceStartType.Automatic);
    public static readonly ServiceStartMode Manual = new(ServiceStartType.Manual);

    /// <summary>Starts with Windows: the toggle shows "on".</summary>
    public bool IsAutoStart => Type is ServiceStartType.Automatic or ServiceStartType.Boot or ServiceStartType.System;

    public string Display => Type switch
    {
        ServiceStartType.Automatic when DelayedStart => "Automatic (delayed start)",
        ServiceStartType.Automatic => "Automatic",
        ServiceStartType.Manual => "Manual",
        ServiceStartType.Disabled => "Disabled",
        ServiceStartType.Boot => "Boot",
        ServiceStartType.System => "System",
        _ => Type.ToString(),
    };
}

public static partial class ServiceRules
{
    /// <summary>Win32 own-process, share-process and per-user services (<c>Type</c> ≥ 0x10); drivers are lower.</summary>
    public const int MinWin32ServiceType = 0x10;

    /// <summary>
    /// Whether a service is listed: a Win32 service whose program lies outside the Windows folder
    /// and is not part of Microsoft Defender (same rule as the prototype).
    /// </summary>
    public static bool IsListed(string serviceName, string? imagePath, int type, string windowsDirectory)
    {
        if (type < MinWin32ServiceType || string.IsNullOrWhiteSpace(imagePath) || DefenderPattern().IsMatch(serviceName))
        {
            return false;
        }

        string? path = CommandLine.GetExecutablePath(imagePath, windowsDirectory);
        if (path is null)
        {
            return false;
        }

        string windows = windowsDirectory.TrimEnd('\\') + @"\";
        return !path.StartsWith(windows, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Start mode set when the user turns a service off: it no longer starts with Windows but can still be started on demand.</summary>
    public static ServiceStartMode OffMode => ServiceStartMode.Manual;

    /// <summary>Start mode set when the user turns a service on: the one saved when it was turned off, else Automatic.</summary>
    public static ServiceStartMode OnMode(ServiceStartMode? saved) =>
        saved is { IsAutoStart: true } mode && mode.Type == ServiceStartType.Automatic ? mode : ServiceStartMode.Automatic;

    /// <summary>Display name without a trailing parenthesised note, as the prototype showed it.</summary>
    public static string CleanDisplayName(string? displayName, string serviceName)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.StartsWith('@'))
        {
            return serviceName;
        }

        string name = TrailingParenthesesPattern().Replace(displayName, "").Trim();
        return name.Length > 0 ? name : serviceName;
    }

    [GeneratedRegex("^(WinDefend|Sense|WdBoot|WdFilter|WdNisSvc|WdNisDrv|MsSecCore|MsSecFlt|MDCoreSvc)$", RegexOptions.IgnoreCase)]
    private static partial Regex DefenderPattern();

    [GeneratedRegex(@"\s\(.*?\)$")]
    private static partial Regex TrailingParenthesesPattern();
}
