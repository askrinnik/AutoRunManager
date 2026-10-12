# AutoRunManager

A Windows 11 app that lists every place where Windows starts apps and services automatically: the Run keys, Startup folders and Store app startup tasks that Task Manager shows, plus scheduled tasks, services and the less visible launch points (RunOnce, policies, Winlogon, Active Setup and more). Entries can be turned off and on, deleted (with a backup) or opened where they are defined.

It is a C# port of a PowerShell/WPF script (`prototype/`), keeping its look and fixing its defects. Details: [`docs/design.md`](docs/design.md).

## Status

Skeleton only: the solution builds and shows an empty window. See the roadmap in [`docs/design.md`](docs/design.md).

## Build and run

Requires the .NET 10 SDK, Windows 10/11.

```bash
dotnet build src
dotnet test src
dotnet run --project src/AutoRunManager
```

## Repository layout

```
src/                            Self-contained C# solution: AutoRunManager.slnx, Directory.Build.props, .editorconfig
src/AutoRunManager.Core/        Model, parsing helpers, autostart sources; no UI
src/AutoRunManager/             WPF app (WinExe)
src/AutoRunManager.Core.Tests/  xUnit tests for Core
docs/design.md                  Requirements, autostart locations, decisions, roadmap
prototype/                      Original PowerShell script (reference only)
```
