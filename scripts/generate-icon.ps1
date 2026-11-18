$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$pngPath = Join-Path $root 'assets\harrow-studio.png'
$icoPath = Join-Path $root 'assets\harrow-studio.ico'

Add-Type -AssemblyName System.Drawing
$bitmap = New-Object System.Drawing.Bitmap(256, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)

$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$radius = 56
$path.AddArc(8, 8, $radius, $radius, 180, 90)
$path.AddArc(248 - $radius, 8, $radius, $radius, 270, 90)
$path.AddArc(248 - $radius, 248 - $radius, $radius, $radius, 0, 90)
$path.AddArc(8, 248 - $radius, $radius, $radius, 90, 90)
$path.CloseFigure()

$backgroundBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(231, 71, 86))
$graphics.FillPath($backgroundBrush, $path)
$linePen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 27)
$linePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$linePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$graphics.DrawLine($linePen, 83, 75, 83, 181)
$graphics.DrawLine($linePen, 173, 75, 173, 181)
$graphics.DrawLine($linePen, 83, 128, 173, 128)

$bitmap.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose(); $graphics.Dispose(); $path.Dispose(); $backgroundBrush.Dispose(); $linePen.Dispose()

$pngBytes = [System.IO.File]::ReadAllBytes($pngPath)
$stream = [System.IO.File]::Open($icoPath, [System.IO.FileMode]::Create)
$writer = New-Object System.IO.BinaryWriter($stream)
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]1)
$writer.Write([Byte]0)
$writer.Write([Byte]0)
$writer.Write([Byte]0)
$writer.Write([Byte]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]32)
$writer.Write([UInt32]$pngBytes.Length)
$writer.Write([UInt32]22)
$writer.Write($pngBytes)
$writer.Dispose(); $stream.Dispose()
