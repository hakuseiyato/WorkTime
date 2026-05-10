# WorkTime app icon generator
# Produces src/WorkTime/Resources/WorkTime.ico (multi-size: 16/32/48/64/128/256)
#
# Design: dark rounded square + cyan vertical bar + white pip (flip-clock motif).
# Run once after icon design changes.

param(
    [string]$OutPath = (Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) 'src\WorkTime\Resources\WorkTime.ico')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    # rounded background
    $bgColor = [System.Drawing.Color]::FromArgb(255, 26, 27, 31)
    $bg = New-Object System.Drawing.SolidBrush($bgColor)
    $r = [int]([Math]::Max(2, $size * 0.18))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $r * 2, $r * 2, 180, 90) | Out-Null
    $path.AddArc($size - $r * 2, 0, $r * 2, $r * 2, 270, 90) | Out-Null
    $path.AddArc($size - $r * 2, $size - $r * 2, $r * 2, $r * 2, 0, 90) | Out-Null
    $path.AddArc(0, $size - $r * 2, $r * 2, $r * 2, 90, 90) | Out-Null
    $path.CloseFigure()
    $g.FillPath($bg, $path)

    # subtle inner border
    if ($size -ge 32) {
        $bd = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(60, 91, 184, 209), 1)
        $g.DrawPath($bd, $path)
        $bd.Dispose()
    }

    # mid divider line (flip-clock)
    if ($size -ge 24) {
        $divHeight = [Math]::Max(1, [int]($size * 0.025))
        $divBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(180, 0, 0, 0))
        $g.FillRectangle($divBrush, [int]($size * 0.18), [int]($size / 2 - $divHeight / 2), [int]($size * 0.64), $divHeight)
        $divBrush.Dispose()
    }

    # cyan vertical bar
    $cyan = [System.Drawing.Color]::FromArgb(255, 91, 184, 209)
    $bar = New-Object System.Drawing.SolidBrush($cyan)
    $bw = [Math]::Max(2, [int]($size * 0.14))
    $bh = [int]($size * 0.62)
    $bx = [int]($size * 0.30)
    $by = [int]($size * 0.19)
    $g.FillRectangle($bar, $bx, $by, $bw, $bh)

    # white pip (top-right of clock face)
    if ($size -ge 24) {
        $pip = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 235, 240, 245))
        $pw = [Math]::Max(2, [int]($size * 0.16))
        $ph = [Math]::Max(2, [int]($size * 0.09))
        $px = [int]($size * 0.55)
        $py = [int]($size * 0.27)
        $g.FillRectangle($pip, $px, $py, $pw, $ph)
        $pip.Dispose()
    }

    $bg.Dispose()
    $bar.Dispose()
    $g.Dispose()
    return $bmp
}

# Render PNGs at each size and assemble ICO
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngs = @()
foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , @{ Size = $s; Data = $ms.ToArray() }
    $bmp.Dispose()
    $ms.Dispose()
}

$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($out)
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$pngs.Count)

$dataOffset = 6 + $pngs.Count * 16
foreach ($p in $pngs) {
    $sz = $p.Size
    $widthByte  = if ($sz -ge 256) { [byte]0 } else { [byte]$sz }
    $heightByte = if ($sz -ge 256) { [byte]0 } else { [byte]$sz }
    $bw.Write($widthByte)
    $bw.Write($heightByte)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $bw.Write([UInt32]$p.Data.Length)
    $bw.Write([UInt32]$dataOffset)
    $dataOffset += $p.Data.Length
}
foreach ($p in $pngs) {
    $bw.Write($p.Data)
}
$bw.Flush()

$dir = Split-Path -Parent $OutPath
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
[System.IO.File]::WriteAllBytes($OutPath, $out.ToArray())
$bw.Dispose()
$out.Dispose()

$kb = [math]::Round((Get-Item $OutPath).Length / 1KB, 1)
Write-Host ("Wrote {0} ({1} KB, {2} sizes)" -f $OutPath, $kb, $pngs.Count) -ForegroundColor Green
