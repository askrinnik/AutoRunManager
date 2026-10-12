<#
.SYNOPSIS
    Draws src\AutoRunManager\AutoRunManager.ico: a power symbol in the accent color on the dark card
    color of the app, as PNG frames of 16-256 px in one .ico file.

.EXAMPLE
    .\tools\make-icon.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$target = Join-Path $root 'src\AutoRunManager\AutoRunManager.ico'
$sizes = 16, 24, 32, 48, 64, 128, 256

$background = [System.Drawing.Color]::FromArgb(255, 30, 34, 40)    # CardBrush
$accent = [System.Drawing.Color]::FromArgb(255, 94, 129, 172)      # AccentBrush

function New-Frame([int]$size) {
    $bitmap = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # Rounded square with an accent outline.
    $inset = [Math]::Max(0.5, $size * 0.03)
    $edge = $size - 2 * $inset
    $radius = $size * 0.22
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($inset, $inset, $radius * 2, $radius * 2, 180, 90)
    $path.AddArc($inset + $edge - $radius * 2, $inset, $radius * 2, $radius * 2, 270, 90)
    $path.AddArc($inset + $edge - $radius * 2, $inset + $edge - $radius * 2, $radius * 2, $radius * 2, 0, 90)
    $path.AddArc($inset, $inset + $edge - $radius * 2, $radius * 2, $radius * 2, 90, 90)
    $path.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush $background), $path)
    $g.DrawPath((New-Object System.Drawing.Pen $accent, ([Math]::Max(1, $size * 0.04))), $path)

    # Power symbol: open ring and a vertical bar.
    $stroke = [Math]::Max(1.6, $size * 0.09)
    $pen = New-Object System.Drawing.Pen $accent, $stroke
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $ring = $size * 0.5
    $offset = ($size - $ring) / 2
    $g.DrawArc($pen, $offset, $offset + $size * 0.04, $ring, $ring, -60, 300)
    $g.DrawLine($pen, $size / 2, $size * 0.2, $size / 2, $size * 0.5)

    $g.Dispose()
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    return , $stream.ToArray()
}

$frames = foreach ($size in $sizes) { , (New-Frame $size) }

# ICONDIR + ICONDIRENTRY per frame, then the PNG data.
$out = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $out
$writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dimension = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([UInt16]1); $writer.Write([UInt16]32)
    $writer.Write([UInt32]$frames[$i].Length); $writer.Write([UInt32]$offset)
    $offset += $frames[$i].Length
}
foreach ($frame in $frames) { $writer.Write($frame) }
$writer.Flush()
[System.IO.File]::WriteAllBytes($target, $out.ToArray())
"Wrote $target ($($sizes -join ', ') px)."
