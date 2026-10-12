using System.Globalization;
using System.Text;

using AutoRunManager.Core.Model;

using Microsoft.Win32;

namespace AutoRunManager.Core.Actions;

/// <summary>Writes registry values in the format of regedit exports, so a backup can be restored by double-clicking it.</summary>
public static class RegFile
{
    public const string Header = "Windows Registry Editor Version 5.00";

    public static string Export(IEnumerable<(RegistryValueLocator Locator, object Value, RegistryValueKind Kind)> values)
    {
        StringBuilder text = new();
        text.Append(Header).Append("\r\n");
        foreach (IGrouping<string, (RegistryValueLocator Locator, object Value, RegistryValueKind Kind)> key in values.GroupBy(v => v.Locator.RegeditKeyName))
        {
            text.Append("\r\n[").Append(key.Key).Append("]\r\n");
            foreach ((RegistryValueLocator locator, object value, RegistryValueKind kind) in key)
            {
                string name = locator.ValueName.Length == 0 ? "@" : Quote(locator.ValueName);
                text.Append(name).Append('=').Append(FormatValue(value, kind)).Append("\r\n");
            }
        }

        return text.ToString();
    }

    internal static string FormatValue(object value, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.String => Quote((string)value),
        RegistryValueKind.DWord => "dword:" + unchecked((uint)(int)value).ToString("x8", CultureInfo.InvariantCulture),
        RegistryValueKind.QWord => "hex(b):" + Hex(BitConverter.GetBytes((long)value)),
        RegistryValueKind.Binary => "hex:" + Hex((byte[])value),
        RegistryValueKind.ExpandString => "hex(2):" + Hex(Utf16Z((string)value)),
        RegistryValueKind.MultiString => "hex(7):" + Hex(MultiUtf16((string[])value)),
        _ => throw new NotSupportedException($"Registry value kind {kind} is not exported."),
    };

    private static string Quote(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static string Hex(byte[] bytes) => string.Join(',', bytes.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));

    private static byte[] Utf16Z(string text) => Encoding.Unicode.GetBytes(text + "\0");

    private static byte[] MultiUtf16(string[] lines) => Encoding.Unicode.GetBytes(string.Concat(lines.Select(l => l + "\0")) + "\0");
}
