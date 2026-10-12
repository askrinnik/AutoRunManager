# AutoRunManager

A Windows 11 app that lists every place where Windows starts apps and services automatically: the Run keys, Startup folders and Store app startup tasks that Task Manager shows, plus scheduled tasks, services and the less visible launch points (RunOnce, policies, Winlogon, Active Setup, image hijacks, BootExecute, AppInit_DLLs, WMI subscriptions). Entries can be turned off and on, deleted (after a backup) or opened where they are defined.

It is a C# port of a PowerShell/WPF script (`prototype/`), keeping its look and fixing its defects. Details: [`docs/design.md`](docs/design.md).

## Using it

- Side navigation: **Apps** (what Task Manager shows) with Registry, Startup folder and Store apps; **Tasks**; **Services**; **Advanced** (other launch points). Search (Ctrl+F) looks through all groups; filter by On / Off / Needs admin, reload with F5.
- Click a name to open where the entry is defined (regedit at the key, Explorer, Task Scheduler, Services, Settings → Startup apps). Click the path to show the program in Explorer.
- The switch turns an entry off and on the way Windows does (`StartupApproved`, Store app state, task enabled flag). Services are set to Manual when off and get their previous start type back when on.
- Entries marked "read-only" have no switch in Windows; they can only be deleted.
- Delete saves a backup first in `%LOCALAPPDATA%\AutoRunManager\backups`: a `.reg` file (double-click to restore), the Startup folder file, or the task XML (`schtasks /create /xml <file> /tn <name>`).
- Click a section header to turn all its entries off or on (asks first).
- Without admin rights, machine-wide entries, services and tasks of other users are locked; "Restart as admin" restarts the app elevated.

## Build and run

Requires the .NET 10 SDK, Windows 10/11.

```bash
dotnet build src
dotnet test src
dotnet run --project src/AutoRunManager
```

## Install

The app is a single `AutoRunManager.exe` (framework-dependent, needs the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)).

```powershell
.\tools\publish.ps1                                          # builds publish\AutoRunManager.exe
.\tools\publish.ps1 -InstallDir C:\Programs\AutoRunManager   # also copies it there
```

## Repository layout

```
src/                            Self-contained C# solution: AutoRunManager.slnx, Directory.Build.props, .editorconfig
src/AutoRunManager.Core/        Model, parsing helpers, autostart sources, actions; no UI
src/AutoRunManager/             WPF app (WinExe)
src/AutoRunManager.Core.Tests/  xUnit tests for Core
tools/                          publish.ps1, make-icon.ps1
docs/design.md                  Requirements, autostart locations, decisions, roadmap
prototype/                      Original PowerShell script (reference only)
```
