# Design

## Goal

A WPF app (.NET 10) that shows every place where Windows 11 starts apps and services automatically and lets the user turn entries off and on, delete them, or jump to where they are defined. It replaces the PowerShell prototype `prototype/AutoRun.Resources.AutoRun.ps1`: same look and interaction, fixed bugs, more autostart locations.

## Requirements

1. Same visual design as the prototype: borderless dark window, cards per entry grouped by category, animated toggle, fade-in/out, scroll fade mask, loading message, icons of the target executables.
2. UI language: English.
3. Runs without elevation (`asInvoker`). Entries that need admin rights to change are shown, their controls are disabled with a tooltip, and the window offers "Restart as administrator".
4. Data is collected off the UI thread; the window never freezes while loading.
5. Toggle and delete per entry; clicking a category header toggles all its entries after confirmation.
6. Services can be turned off (`Manual`) and on (previous start type), never deleted.
7. Before deleting an entry, its definition is saved to a backup in `%LOCALAPPDATA%\AutoRunManager\backups`.
8. No links to the prototype author's social accounts.

## Autostart locations

Grouped by how Windows itself treats them. "Native toggle" means Windows has its own enabled/disabled state for the entry (the one Task Manager and Settings use). Entries without a native toggle are read-only: the app shows them, opens their location and deletes them with a backup, but does not turn them off.

### Group A: what Task Manager and Settings → Apps → Startup show (native toggle)

| Location | Toggle | In prototype |
|---|---|---|
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` | `HKCU\…\Explorer\StartupApproved\Run` | yes |
| `HKLM\Software\Microsoft\Windows\CurrentVersion\Run` | `HKLM\…\Explorer\StartupApproved\Run` | yes |
| `HKLM\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run` (32-bit apps) | `HKLM\…\Explorer\StartupApproved\Run32` | no |
| `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup` (all files except `desktop.ini`) | `HKCU\…\Explorer\StartupApproved\StartupFolder` | `.lnk` only |
| `%ProgramData%\Microsoft\Windows\Start Menu\Programs\Startup` | `HKLM\…\Explorer\StartupApproved\StartupFolder` | `.lnk` only |
| Packaged (MSIX/Store) app startup tasks (`windows.startupTask` in the manifest) | `HKCU\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData\<package>\<task>\State` | no |

`StartupApproved` values are 12 bytes: byte 0 even (`02`, `06`) = enabled, odd (`03`, `07`) = disabled; bytes 4–11 hold the FILETIME when it was disabled.

### Group B: other places that start programs at boot or logon

| Location | Toggle | In prototype |
|---|---|---|
| Task Scheduler, all tasks outside `\Microsoft\` (task path, actions, triggers) | native (task enabled flag) | yes |
| Services (`HKLM\SYSTEM\CurrentControlSet\Services`, Win32 services outside Windows and Defender) | start type | yes |
| `RunOnce` / `RunOnceEx` (HKCU, HKLM, WOW6432Node) | none | no |
| `…\Policies\Explorer\Run` (HKCU, HKLM) | none | no |
| `HKLM\…\Windows NT\CurrentVersion\Winlogon` `Shell`, `Userinit` (only non-default values) | none | no |
| `HKCU\…\Windows NT\CurrentVersion\Windows` `Load`, `Run` | none | no |
| Active Setup `StubPath` (`HKLM\Software\Microsoft\Active Setup\Installed Components`, also WOW6432Node) | none | no |
| `HKLM\…\Terminal Server\Install\…\Run`, `RunOnce` | none | no |
| `HKLM\SYSTEM\CurrentControlSet\Control\Session Manager` `BootExecute` (only non-default) | none | no |
| Image File Execution Options `Debugger` (launch hijack) | none | no |
| `AppInit_DLLs` (only if enabled) | none | no |
| WMI permanent event subscriptions (`root\subscription`: `CommandLineEventConsumer`) | none | no |

### Group C: deep system extension points (Sysinternals Autoruns level)

Drivers, Explorer shell extensions, browser helper objects, codecs, Winsock/LSA/network/print providers, KnownDLLs, Office add-ins. Hundreds of entries on a normal machine, mostly Microsoft; undecided (open question 1).

## Prototype behavior and fixes

The prototype is plain PowerShell building WPF from code (no embedded C#). Defects fixed in the port:

1. No elevation check: writes to HKLM, services and tasks fail silently.
2. Services: off sets `Disabled`, on always sets `Automatic`; the original start type is lost. Now off sets `Manual` and on restores the start type saved when it was turned off (`Automatic` if none was saved).
3. Tasks are enabled, disabled and deleted by name only, without the task path; wrong or no task in subfolders.
4. Startup folders list only `.lnk` files.
5. Category header click toggles all entries without confirmation.
6. Jump to regedit writes a localized `LastKey` ("Компьютер\…"); use the language-neutral form.
7. `DoEvents` and synchronous loading freeze the window.
8. Deletion is irreversible and has no backup.

## Design decisions

- WPF, not WinForms: the prototype is WPF, and its templates (toggle, button, fades) port directly to XAML.
- Window layout: tabs with entry counts (Apps: Run keys, Startup folders, Store apps; Tasks; Services; Advanced: the rest of group B), a search box and a filter (All, On, Off, Needs admin) under the tabs, entries grouped into sections per location inside a tab. Fixed height (650) so switching tabs does not resize the window.
- `src/AutoRunManager.Core` holds the model, pure parsing helpers and one source class per location behind thin platform interfaces (registry, file system, Task Scheduler COM, Service Control Manager P/Invoke). Pure parts are unit-tested with fakes.
- Group B entries without a native toggle are read-only (open location, delete with backup). Turning them off through a store of our own (as Autoruns does with its `AutorunsDisabled` keys) may come later.
- Services: "off" sets `Manual`, not `Disabled`, so apps that start their service on demand keep working. The previous start type is saved in `%LOCALAPPDATA%\AutoRunManager\state.json` and restored by "on".
- `src/AutoRunManager` is the WPF shell: XAML themes, view models, dialogs, icon loading, shell launching, elevation.

## Open questions

1. Group C: postponed until the app with groups A and B is released (on this machine: third-party drivers such as NoMachine, VeraCrypt, ViGEmBus, FortiFilter; Explorer extensions such as 7-Zip, TortoiseGit, TortoiseSVN, NVIDIA).

## Roadmap

0. Harness: repository, docs, settings. **Done.**
1. Solution skeleton: `.slnx`, shared props, WPF window, empty Core and tests. **Done.**
2. Core model and pure helpers with tests: command-line → executable path, `StartupApproved` encode/decode, task name cleanup, service filter. **Done.**
3. Read-only sources for group A and the prototype's tasks and services; compare the list with the prototype and Task Manager on the real machine. **Done** (checked with `--list`: Run32 and Store app entries appear; a task with doubled quotes around its program path is parsed).
4. Read-only sources for group B. **Done.** Winlogon, BootExecute and AppInit_DLLs entries are listed only when they differ from the default and are never deleted.
5. Actions: toggle, delete with backup; admin-rights detection per entry. **Done.** Verified on the machine with test entries only (HKCU Run value, user Startup file, user task): toggle, delete, backup, restore of the `.reg` backup. Not verified on the machine: Store app toggle, service start type change, WMI deletion.
6. WPF UI port of the prototype design; async loading. **Done.** Tabs, search and filter as decided; checked with screenshots.
7. Elevation: disabled controls, "Restart as administrator". **Done.** Checked only with `--preview-unelevated`: UAC is off on the development machine.
8. Manual check on the real machine with test entries only; publish script, icon, README. **Done** except the user's own check of the window.
