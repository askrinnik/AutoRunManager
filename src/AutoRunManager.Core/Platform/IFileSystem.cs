namespace AutoRunManager.Core.Platform;

public interface IFileSystem
{
    /// <summary>Full paths of the files directly in a folder; empty if it does not exist.</summary>
    IReadOnlyList<string> GetFiles(string directory);

    bool FileExists(string path);

    /// <summary>File text, or <see langword="null"/> if it cannot be read.</summary>
    string? ReadAllText(string path);

    /// <summary>Writes a file (UTF-8 unless <paramref name="encoding"/> is given), creating its folder if needed.</summary>
    void WriteAllText(string path, string text, System.Text.Encoding? encoding = null);

    /// <summary>Copies a file, creating the target folder if needed.</summary>
    void Copy(string source, string target);

    void Delete(string path);
}

public sealed class LocalFileSystem : IFileSystem
{
    public IReadOnlyList<string> GetFiles(string directory)
    {
        try
        {
            return Directory.Exists(directory) ? Directory.GetFiles(directory) : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public bool FileExists(string path) => File.Exists(path);

    public string? ReadAllText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void WriteAllText(string path, string text, System.Text.Encoding? encoding = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text, encoding ?? System.Text.Encoding.UTF8);
    }

    public void Copy(string source, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source, target, overwrite: true);
    }

    public void Delete(string path) => File.Delete(path);
}
