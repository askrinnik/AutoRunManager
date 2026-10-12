# AutoRunManager

Windows 11 app (.NET 10, WPF) that shows every place where Windows starts apps and services automatically and lets the user turn entries off and on, delete them, or open their location. Port of the PowerShell prototype in `prototype/`. Requirements, autostart locations, decisions, open questions, roadmap: `docs/design.md`. Read it before starting a task.

## Commands

```bash
dotnet build src                              # whole solution (src/AutoRunManager.slnx); warnings are errors
dotnet test src                               # xUnit tests for Core
dotnet run --project src/AutoRunManager       # run the app
dotnet format src --verify-no-changes         # check formatting against src/.editorconfig
```

## Layout

`src/` is a self-contained C# solution: `AutoRunManager.slnx` (flat, no solution folders), `Directory.Build.props`, `.editorconfig` and all projects, including tests.

- `src/AutoRunManager.Core`: model, pure helpers, one source class per autostart location behind thin platform interfaces (registry, file system, Task Scheduler, services). No UI.
- `src/AutoRunManager`: WinExe, WPF. `Themes/` (colors and control templates in XAML), main window, dialogs. Manifest `asInvoker`.
- `src/AutoRunManager.Core.Tests`: xUnit tests with fake platform interfaces.
- `prototype/AutoRun.Resources.AutoRun.ps1`: working PowerShell/WPF prototype. Reference for look and behavior; not built or shipped.

## Conventions

- All projects target `net10.0-windows`. Shared settings live in `src/Directory.Build.props` (nullable, implicit usings, `TreatWarningsAsErrors`).
- Keep decision and parsing logic in pure classes in Core with unit tests; keep registry, COM and P/Invoke code thin and separate.
- Never block the UI thread with data collection.
- UI text in English. Code, comments, commit messages and repository docs in English. Talk to the user in Russian.

## Testing on the real machine

Unit tests use fakes. Reading real autostart locations is safe and can be done freely. Changing them is not:

- Test toggle and delete only on entries created for the test (e.g. a value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` pointing at `notepad.exe`), and remove them afterwards.
- Report what was verified on the machine and what only in tests.

## Do not

- Toggle, delete or otherwise change the user's real autostart entries, services or scheduled tasks. Ask first, every time.
- Write under `HKLM`, change service configuration or run anything elevated without asking.
- Delete services: the app only changes their start type.
