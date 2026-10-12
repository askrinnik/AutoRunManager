<#
.SYNOPSIS
    Builds the release AutoRunManager.exe and optionally installs it into a folder.

.DESCRIPTION
    Publishes a framework-dependent, single-file win-x64 build (needs the .NET 10 Desktop Runtime
    on the machine) to <repo>\publish\AutoRunManager.exe. Not trimmed: the app uses built-in COM
    and WMI.

    With -InstallDir the executable is copied into that folder; a running copy is stopped first.
    The app is portable: AutoRunManager.state.json and the Backups folder live next to the
    executable and are left untouched. The folder must be writable, so not Program Files.

.EXAMPLE
    .\tools\publish.ps1

.EXAMPLE
    .\tools\publish.ps1 -InstallDir C:\Programs\AutoRunManager
#>
[CmdletBinding()]
param(
    [string]$InstallDir
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$publishDir = Join-Path $root 'publish'

dotnet publish (Join-Path $root 'src\AutoRunManager') `
    -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -p:DebugType=embedded `
    -o $publishDir --nologo
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

$exe = Join-Path $publishDir 'AutoRunManager.exe'
"Published $exe ($([math]::Round((Get-Item $exe).Length / 1KB)) KB)."

if (-not $InstallDir) { return }

$InstallDir = [IO.Path]::GetFullPath($InstallDir)
if ($InstallDir.StartsWith($env:ProgramFiles, 'OrdinalIgnoreCase') -or
    ($env:ProgramW6432 -and $InstallDir.StartsWith($env:ProgramW6432, 'OrdinalIgnoreCase')) -or
    (${env:ProgramFiles(x86)} -and $InstallDir.StartsWith(${env:ProgramFiles(x86)}, 'OrdinalIgnoreCase'))) {
    throw "Program Files is not writable without admin rights; the portable app keeps its state and backups next to the exe."
}
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
$target = Join-Path $InstallDir 'AutoRunManager.exe'

$running = @(Get-Process AutoRunManager -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path -ieq $target })
if ($running) {
    "Stopping running AutoRunManager."
    $running | Stop-Process
    $running | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
}

# Windows may keep the file locked for a moment after the process exits.
for ($attempt = 1; ; $attempt++) {
    try {
        Copy-Item $exe $target -Force
        break
    }
    catch [System.IO.IOException] {
        if ($attempt -ge 20) { throw }
        Start-Sleep -Milliseconds 250
    }
}
"Installed $target."
