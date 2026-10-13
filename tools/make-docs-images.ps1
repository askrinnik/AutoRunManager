<#
.SYNOPSIS
    Regenerates the images of the README and GitHub: docs\screenshots\*.png (the window with made-up
    entries, rendered by the app's --screenshots option) and docs\social-preview.png (1280x640, upload
    it in the repository Settings → General → Social preview).

.EXAMPLE
    .\tools\make-docs-images.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$docs = Join-Path $root 'docs'

# Screenshots: the app renders them off screen and exits; nothing on the machine is read or changed.
dotnet build (Join-Path $root 'src\AutoRunManager') -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$exe = Join-Path $root 'src\AutoRunManager\bin\Release\net10.0-windows\AutoRunManager.exe'
$screenshots = Join-Path $docs 'screenshots'
Start-Process -Wait -FilePath $exe -ArgumentList '--screenshots', "`"$screenshots`""
"Wrote $screenshots"

# Social preview: the icon (docs\icon.png, 256 px) and the name on the app's dark background.
$background = [System.Drawing.ColorTranslator]::FromHtml('#141619')    # WindowBackgroundBrush
$card = [System.Drawing.ColorTranslator]::FromHtml('#1E2228')          # CardBrush
$tagFill = [System.Drawing.ColorTranslator]::FromHtml('#3D4450')       # IconBackgroundBrush
$accent = [System.Drawing.ColorTranslator]::FromHtml('#5E81AC')        # AccentBrush

$W = 1280; $H = 640
$bitmap = New-Object System.Drawing.Bitmap $W, $H
$g = [System.Drawing.Graphics]::FromImage($bitmap)
$g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'; $g.InterpolationMode = 'HighQualityBicubic'

$fill = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.Point]::new(0, 0)), ([System.Drawing.Point]::new($W, $H)), $background, $card
$g.FillRectangle($fill, 0, 0, $W, $H)

# Soft accent glow behind the icon.
$glowPath = New-Object System.Drawing.Drawing2D.GraphicsPath
$glowPath.AddEllipse(50, 110, 420, 420)
$glow = New-Object System.Drawing.Drawing2D.PathGradientBrush $glowPath
$glow.CenterColor = [System.Drawing.Color]::FromArgb(90, $accent)
$glow.SurroundColors = @([System.Drawing.Color]::FromArgb(0, $accent))
$g.FillPath($glow, $glowPath)

$icon = [System.Drawing.Image]::FromFile((Join-Path $docs 'icon.png'))
$g.DrawImage($icon, 130, 190, 260, 260)

$x = 480
$title = New-Object System.Drawing.Font 'Segoe UI Semibold', 76, ([System.Drawing.GraphicsUnit]::Pixel)
$subtitle = New-Object System.Drawing.Font 'Segoe UI', 34, ([System.Drawing.GraphicsUnit]::Pixel)
$tagFont = New-Object System.Drawing.Font 'Segoe UI Semibold', 24, ([System.Drawing.GraphicsUnit]::Pixel)
$small = New-Object System.Drawing.Font 'Segoe UI', 26, ([System.Drawing.GraphicsUnit]::Pixel)
$g.DrawString('AutoRunManager', $title, [System.Drawing.Brushes]::White, $x - 6, 140)
$g.DrawString('Shows everything that starts automatically with Windows and lets you turn it off.',
    $subtitle, (New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(220, 220, 220))),
    (New-Object System.Drawing.RectangleF $x, 250, 760, 110))

# The navigation groups of the app as scope-tag-style pills.
$tx = $x; $ty = 392
$center = New-Object System.Drawing.StringFormat
$center.Alignment = 'Center'; $center.LineAlignment = 'Center'
foreach ($tag in 'Apps', 'Tasks', 'Services', 'Advanced') {
    $tw = [Math]::Ceiling($g.MeasureString($tag, $tagFont).Width) + 28; $th = 46; $r = 23
    $pill = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pill.AddArc($tx, $ty, 2 * $r, 2 * $r, 90, 180); $pill.AddArc($tx + $tw - 2 * $r, $ty, 2 * $r, 2 * $r, 270, 180); $pill.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush $tagFill), $pill)
    $g.DrawString($tag, $tagFont, [System.Drawing.Brushes]::White, (New-Object System.Drawing.RectangleF $tx, $ty, $tw, $th), $center)
    $tx += $tw + 14
}

$g.DrawString('Windows 10 / 11  ·  WPF  ·  .NET 10  ·  MIT', $small, [System.Drawing.Brushes]::Gray, $x, 478)

$g.Dispose()
$preview = Join-Path $docs 'social-preview.png'
$bitmap.Save($preview, [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()
"Wrote $preview"
