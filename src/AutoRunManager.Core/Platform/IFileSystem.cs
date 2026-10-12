namespace AutoRunManager.Core.Platform;

public interface IFileSystem
{
    /// <summary>Full paths of the files directly in a folder; empty if it does not exist.</summary>
    IReadOnlyList<string> GetFiles(string directory);

    bool FileExists(string path);

    /// <summary>File text, or <see langword="null"/> if it cannot be read.</summary>
    string? ReadAllText(string path);
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
}
