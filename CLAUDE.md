# AutoRunManager

Windows 11 app (.NET 10, WPF) that shows every place where Windows starts apps and services automatically and lets the user turn entries off and on, delete them, or open their location. Port of the PowerShell prototype in `prototype/`. Requirements, autostart locations, decisions, open questions, roadmap: `docs/design.md`. Read it before starting a task.

## Commands

```bash
dotnet build src                              # whole solution (src/AutoRunManager.slnx); warnings are errors
dotnet test src                               # xUnit tests for Core
dotnet run --project src/AutoRunManager       # run the app
dotnet format src --verify-no-changes         # check formatting against src/.editorconfig
src\AutoRunManager\bin\Debug\net10.0-windows\AutoRunManager.exe --list <file.tsv>   # dump all entries as TSV, no window
src\AutoRunManager\bin\Debug\net10.0-windows\AutoRunManager.exe --preview-unelevated   # UI as a non-elevated process shows it
```

`--list` runs the same collector as the window and is the quickest way to check sources against the real machine (start it with `Start-Process -Wait`; it is a WinExe and prints nothing to the console).

UAC is off on this machine (`EnableLUA = 0`): every process of the user is elevated, so the non-elevated UI never appears by itself. Use `--preview-unelevated` to see it (actions themselves still run elevated).

## Layout

`src/` is a self-contained C# solution: `AutoRunManager.slnx` (flat, no solution folders), `Directory.Build.props`, `.editorconfig` and all projects, including tests.

- `src/AutoRunManager.Core`: no UI.
  - `Model/`: `StartupEntry`, `EntryKind` (+ group and display name), `EntryLocator` subtypes that identify where an entry is defined.
  - `Parsing/`: pure helpers: `CommandLine` (command → program path), `StartupApprovedValue`, `TaskNames`, `ServiceRules` (listing filter, start modes, off/on policy), `PackagedStartupTasks` (manifest parsing, `State` values), `LaunchPointRules` (defaults of Winlogon and BootExecute).
  - `Platform/`: thin wrappers: `IRegistry` (64-bit view, raw values), `IFileSystem`, `ITaskScheduler` (COM `Schedule.Service`), `IIndirectStrings` (`SHLoadIndirectString`), `IWmiSubscriptions` (System.Management), `IServiceControl` (`ChangeServiceConfig`), `Elevation`, `SystemContext` (folders, current user).
  - `Actions/`: `EntryActions` (toggle per location; delete after a backup), `BackupStore` (`%LOCALAPPDATA%\AutoRunManager\backups`: `.reg`, file copy, task XML, text), `RegFile` (regedit export format, UTF-16), `AppState` (`state.json`: saved service start modes).
  - `Presentation/`: `EntryTabs` (tabs and their sections), `EntryQuery` (filter and search).
  - `Sources/`: one `IEntrySource` per location (`RunKeySource`, `StartupFolderSource`, `PackagedAppSource`, `ScheduledTaskSource`, `ServiceSource`, `RegistryLaunchPointSource` for all group B registry locations, `WmiSubscriptionSource`); `EntryCollector` runs them and reports failing sources.
- `src/AutoRunManager`: WinExe, WPF, manifest `asInvoker`.
  - `Themes/`: `Colors.xaml` (prototype palette), `Controls.xaml` (accent button, toggle switch, tabs, filter chips, search box).
  - `ViewModels/`: `MainViewModel` (load off the UI thread, tabs, search, filter, toggle, delete, toggle a whole section), `EntryViewModel`, `SectionViewModel`, `AsyncCommand`.
  - `Dialogs/`: `DialogWindow` (confirm/message in the prototype style), `WindowEffects` (fades).
  - `Services/`: `IconLoader` (`SHGetFileInfo`, cached), `ShellLauncher` (regedit at the key, Explorer select, taskschd.msc, services.msc, Settings startup page, restart elevated).
  - `MainWindow`: tabs from `Core/Presentation/EntryTabs`, search (Ctrl+F), F5 reloads.
- `src/AutoRunManager.Core.Tests`: xUnit tests with fake platform interfaces.
- `tools/`: `publish.ps1` (framework-dependent single-file win-x64; `-InstallDir` copies the exe, stopping a running copy), `make-icon.ps1` (power symbol in the accent color).
- `prototype/AutoRun.Resources.AutoRun.ps1`: working PowerShell/WPF prototype. Reference for look and behavior; not built or shipped.

## Conventions

- All projects target `net10.0-windows`. Shared settings live in `src/Directory.Build.props` (nullable, implicit usings, `TreatWarningsAsErrors`).
- Core uses built-in COM (`dynamic` Task Scheduler) and WMI: never publish trimmed or AOT. File-based test scripts (`dotnet run x.cs`) need `#:property TargetFramework=net10.0-windows` and `#:property PublishAot=false`.
- Keep decision and parsing logic in pure classes in Core with unit tests; keep registry, COM and P/Invoke code thin and separate.
- Never block the UI thread with data collection.
- UI text in English. Code, comments, commit messages and repository docs in English. Talk to the user in Russian.

## Testing on the real machine

Unit tests use fakes. Reading real autostart locations is safe and can be done freely. Changing them is not:

- Test toggle and delete only on entries created for the test (e.g. a value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` pointing at `notepad.exe`), and remove them afterwards.
- Claude Code on this machine runs elevated (`Elevation.IsElevated()` is true), so HKLM writes succeed from Claude's processes; be careful, and ask the user to check the non-elevated behavior.
- Report what was verified on the machine and what only in tests.

## Do not

- Toggle, delete or otherwise change the user's real autostart entries, services or scheduled tasks. Ask first, every time.
- Write under `HKLM`, change service configuration or run anything elevated without asking.
- Delete services: the app only changes their start type.
