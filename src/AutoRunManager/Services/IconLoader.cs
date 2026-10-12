using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AutoRunManager.Services;

/// <summary>Icons of program files through the shell (works for .exe, .lnk, .dll and documents); cached per path.</summary>
public static partial class IconLoader
{
    private const uint ShgfiIcon = 0x100;
    private const uint ShgfiLargeIcon = 0x0;

    private static readonly Dictionary<string, ImageSource?> s_cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The icon, or <see langword="null"/> if the file cannot be found. Call on the UI thread.</summary>
    public static ImageSource? Load(string? path)
    {
        string? file = Resolve(path);
        if (file is null)
        {
            return null;
        }

        if (s_cache.TryGetValue(file, out ImageSource? cached))
        {
            return cached;
        }

        ImageSource? icon = null;
        ShFileInfo info = default;
        if (SHGetFileInfoW(file, 0, ref info, (uint)Marshal.SizeOf<ShFileInfo>(), ShgfiIcon | ShgfiLargeIcon) != IntPtr.Zero && info.Icon != IntPtr.Zero)
        {
            try
            {
                BitmapSource bitmap = Imaging.CreateBitmapSourceFromHIcon(info.Icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bitmap.Freeze();
                icon = bitmap;
            }
            finally
            {
                DestroyIcon(info.Icon);
            }
        }

        s_cache[file] = icon;
        return icon;
    }

    /// <summary>The file itself, or for a bare name (<c>rundll32.exe</c>) the copy in System32, Windows or PATH.</summary>
    private static string? Resolve(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (File.Exists(path))
        {
            return path;
        }

        if (path.Contains('\\'))
        {
            return null;
        }

        string name = Path.HasExtension(path) ? path : path + ".exe";
        IEnumerable<string> folders = new[] { Environment.SystemDirectory, Environment.GetFolderPath(Environment.SpecialFolder.Windows) }
            .Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries));
        return folders.Select(folder => Path.Combine(folder, name)).FirstOrDefault(File.Exists);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr Icon;
        public int IconIndex;
        public uint Attributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string DisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string TypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfoW(string path, uint fileAttributes, ref ShFileInfo info, uint size, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(IntPtr icon);
}
