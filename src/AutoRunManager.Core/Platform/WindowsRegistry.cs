using System.Security;

using Microsoft.Win32;

namespace AutoRunManager.Core.Platform;

public sealed class WindowsRegistry : IRegistry
{
    public bool KeyExists(RegistryHive hive, string keyPath)
    {
        using RegistryKey? key = Open(hive, keyPath);
        return key is not null;
    }

    public IReadOnlyList<string> GetSubKeyNames(RegistryHive hive, string keyPath)
    {
        using RegistryKey? key = Open(hive, keyPath);
        return Read(() => key?.GetSubKeyNames(), []);
    }

    public IReadOnlyList<string> GetValueNames(RegistryHive hive, string keyPath)
    {
        using RegistryKey? key = Open(hive, keyPath);
        return Read(() => key?.GetValueNames(), []);
    }

    public object? GetValue(RegistryHive hive, string keyPath, string valueName)
    {
        using RegistryKey? key = Open(hive, keyPath);
        return Read(() => key?.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames), null);
    }

    public RegistryValueKind? GetValueKind(RegistryHive hive, string keyPath, string valueName)
    {
        using RegistryKey? key = Open(hive, keyPath);
        return Read<RegistryValueKind?>(() => key?.GetValueKind(valueName), null);
    }

    public void SetValue(RegistryHive hive, string keyPath, string valueName, object value, RegistryValueKind kind)
    {
        using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        using RegistryKey key = baseKey.CreateSubKey(keyPath, writable: true);
        key.SetValue(valueName, value, kind);
    }

    public void DeleteValue(RegistryHive hive, string keyPath, string valueName)
    {
        using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        using RegistryKey? key = baseKey.OpenSubKey(keyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }

    private static RegistryKey? Open(RegistryHive hive, string keyPath)
    {
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            return baseKey.OpenSubKey(keyPath, writable: false);
        }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private static T Read<T>(Func<T?> read, T fallback)
    {
        try
        {
            return read() ?? fallback;
        }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
        {
            return fallback;
        }
    }
}
