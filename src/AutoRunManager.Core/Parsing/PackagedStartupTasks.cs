using System.Xml.Linq;

namespace AutoRunManager.Core.Parsing;

/// <summary>A <c>windows.startupTask</c> extension declared in a package manifest.</summary>
/// <param name="Executable">Path relative to the package folder.</param>
/// <param name="DisplayName">Literal name or an <c>ms-resource:</c> reference.</param>
public sealed record ManifestStartupTask(string TaskId, string Executable, string? DisplayName, bool EnabledByDefault);

/// <summary>
/// <c>State</c> value under <c>...\AppModel\SystemAppData\&lt;package family&gt;\&lt;task id&gt;</c>.
/// Missing until the task is first changed; then the manifest default applies.
/// </summary>
public enum PackagedTaskState
{
    Disabled = 0,
    DisabledByUser = 1,
    Enabled = 2,
    DisabledByPolicy = 3,
    EnabledByPolicy = 4,
}

public static class PackagedStartupTasks
{
    public const string SystemAppDataKey =
        @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData";

    /// <summary>Reads the startup tasks and the package display name from <c>AppxManifest.xml</c>.</summary>
    public static (string? PackageDisplayName, IReadOnlyList<ManifestStartupTask> Tasks) ParseManifest(string manifestXml)
    {
        XDocument doc = XDocument.Parse(manifestXml);
        string? packageName = doc.Root?
            .Elements().FirstOrDefault(e => e.Name.LocalName == "Properties")?
            .Elements().FirstOrDefault(e => e.Name.LocalName == "DisplayName")?
            .Value.Trim();

        List<ManifestStartupTask> tasks = [];
        foreach (XElement extension in doc.Descendants().Where(IsStartupTaskExtension))
        {
            XElement? task = extension.Elements().FirstOrDefault(e => e.Name.LocalName == "StartupTask");
            string? taskId = (string?)task?.Attribute("TaskId");
            if (task is null || string.IsNullOrEmpty(taskId))
            {
                continue;
            }

            string? executable = (string?)extension.Attribute("Executable")
                ?? (string?)extension.Ancestors().FirstOrDefault(e => e.Name.LocalName == "Application")?.Attribute("Executable");
            bool enabled = string.Equals((string?)task.Attribute("Enabled"), "true", StringComparison.OrdinalIgnoreCase);
            tasks.Add(new ManifestStartupTask(taskId, executable ?? "", (string?)task.Attribute("DisplayName"), enabled));
        }

        return (packageName, tasks);
    }

    /// <summary>Whether the task runs at logon, and whether the user may change that.</summary>
    public static (bool Enabled, bool UserCanToggle) Evaluate(int? state, bool enabledByDefault) => (PackagedTaskState?)state switch
    {
        null => (enabledByDefault, true),
        PackagedTaskState.Enabled => (true, true),
        PackagedTaskState.Disabled or PackagedTaskState.DisabledByUser => (false, true),
        PackagedTaskState.DisabledByPolicy => (false, false),
        PackagedTaskState.EnabledByPolicy => (true, false),
        _ => (false, true),
    };

    /// <summary>The state Settings writes when the user switches the task.</summary>
    public static PackagedTaskState StateFor(bool enabled) =>
        enabled ? PackagedTaskState.Enabled : PackagedTaskState.DisabledByUser;

    /// <summary>Package family name from a full name: <c>Name_Version_Arch_ResourceId_PublisherId</c> → <c>Name_PublisherId</c>.</summary>
    public static string? FamilyNameFromFullName(string packageFullName)
    {
        string[] parts = packageFullName.Split('_');
        return parts.Length == 5 ? $"{parts[0]}_{parts[4]}" : null;
    }

    private static bool IsStartupTaskExtension(XElement element) =>
        element.Name.LocalName == "Extension"
        && string.Equals((string?)element.Attribute("Category"), "windows.startupTask", StringComparison.Ordinal);
}
