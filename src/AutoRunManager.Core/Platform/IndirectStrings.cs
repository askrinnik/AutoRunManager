using System.Runtime.InteropServices;

namespace AutoRunManager.Core.Platform;

/// <summary>Resolves indirect strings: <c>@file.dll,-123</c> and <c>@{PackageFullName?ms-resource://...}</c>.</summary>
public interface IIndirectStrings
{
    /// <summary>The resolved text, or <see langword="null"/> if it cannot be resolved.</summary>
    string? Resolve(string indirect);
}

public sealed partial class ShellIndirectStrings : IIndirectStrings
{
    public string? Resolve(string indirect)
    {
        char[] buffer = new char[1024];
        int hr = SHLoadIndirectString(indirect, buffer, buffer.Length, IntPtr.Zero);
        if (hr != 0)
        {
            return null;
        }

        string text = new(buffer, 0, Array.IndexOf(buffer, '\0') is var end and >= 0 ? end : buffer.Length);
        return text.Length > 0 ? text : null;
    }

    [LibraryImport("shlwapi.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SHLoadIndirectString(string source, [Out] char[] output, int outputLength, IntPtr reserved);
}
