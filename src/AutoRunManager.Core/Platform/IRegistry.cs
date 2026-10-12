using Microsoft.Win32;

namespace AutoRunManager.Core.Platform;

/// <summary>
/// Registry access through the 64-bit view. Reads of missing or inaccessible keys return empty
/// results instead of throwing; writes throw on failure.
/// </summary>
public interface IRegistry
{
    bool KeyExists(RegistryHive hive, string keyPath);

    IReadOnlyList<string> GetSubKeyNames(RegistryHive hive, string keyPath);

    IReadOnlyList<string> GetValueNames(RegistryHive hive, string keyPath);

    /// <summary>The value as stored: environment variables in <c>REG_EXPAND_SZ</c> are not expanded.</summary>
    object? GetValue(RegistryHive hive, string keyPath, string valueName);

    RegistryValueKind? GetValueKind(RegistryHive hive, string keyPath, string valueName);

    /// <summary>Sets a value, creating the key if needed.</summary>
    void SetValue(RegistryHive hive, string keyPath, string valueName, object value, RegistryValueKind kind);

    /// <summary>Deletes a value; does nothing if it does not exist.</summary>
    void DeleteValue(RegistryHive hive, string keyPath, string valueName);
}

public static class RegistryExtensions
{
    public static string? GetString(this IRegistry registry, RegistryHive hive, string keyPath, string valueName) =>
        registry.GetValue(hive, keyPath, valueName) switch
        {
            string text => text,
            string[] lines => string.Join(" ", lines),
            _ => null,
        };

    public static int? GetInt(this IRegistry registry, RegistryHive hive, string keyPath, string valueName) =>
        registry.GetValue(hive, keyPath, valueName) is int number ? number : null;
}
