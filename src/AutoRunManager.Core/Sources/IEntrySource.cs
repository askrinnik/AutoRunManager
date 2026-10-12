using AutoRunManager.Core.Model;

namespace AutoRunManager.Core.Sources;

/// <summary>Collects the entries of one or more autostart locations.</summary>
public interface IEntrySource
{
    string Name { get; }

    IEnumerable<StartupEntry> Enumerate();
}
