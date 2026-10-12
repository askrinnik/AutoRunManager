using System.Buffers.Binary;

namespace AutoRunManager.Core.Parsing;

/// <summary>
/// Values under <c>...\Explorer\StartupApproved\{Run,Run32,StartupFolder}</c>: 12 bytes, the first
/// byte even (<c>02</c>, <c>06</c>) when the entry is enabled and odd (<c>03</c>, <c>07</c>) when
/// disabled; bytes 4–11 hold the FILETIME (UTC) when it was disabled. A missing value means enabled.
/// </summary>
public static class StartupApprovedValue
{
    public const int Length = 12;

    public static bool IsEnabled(byte[]? data) => data is not { Length: > 0 } || (data[0] & 1) == 0;

    /// <summary>The value Task Manager writes when the user enables or disables the entry.</summary>
    public static byte[] Create(bool enabled, DateTime utcNow)
    {
        byte[] data = new byte[Length];
        data[0] = enabled ? (byte)0x02 : (byte)0x03;
        if (!enabled)
        {
            BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(4), utcNow.ToFileTimeUtc());
        }

        return data;
    }

    /// <summary>When the entry was disabled, if the value records it.</summary>
    public static DateTime? DisabledAtUtc(byte[]? data)
    {
        if (IsEnabled(data) || data!.Length < Length)
        {
            return null;
        }

        long fileTime = BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(4));
        return fileTime > 0 ? DateTime.FromFileTimeUtc(fileTime) : null;
    }
}
