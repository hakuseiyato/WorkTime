# WorkTime app icon generator
# Produces src/WorkTime/Resources/WorkTime.ico (multi-size: 16/32/48/64/128/256)
#
# Design: dark rounded plate + coral filled circle + hands knocked out in the
# plate colour. Reads as a recording dot at 16px and as a clock at 32px+,
# which matches what WorkTime does (計測と録画).
# Palette follows DESIGN.md: Ink #07080a / Border #363739 / Coral #ff6363.
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

    $ink    = [System.Drawing.Color]::FromArgb(255, 0x07, 0x08, 0x0A)
    $border = [System.Drawing.Color]::FromArgb(255, 0x36, 0x37, 0x39)
    $coral  = [System.Drawing.Color]::FromArgb(255, 0xFF, 0x63, 0x63)

    # 角丸のプレート
    $r = [float]([Math]::Max(2.0, $size * 0.22))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $r * 2, $r * 2, 180, 90)
    $path.AddArc($size - $r * 2, 0, $r * 2, $r * 2, 270, 90)
    $path.AddArc($size - $r * 2, $size - $r * 2, $r * 2, $r * 2, 0, 90)
    $path.AddArc(0, $size - $r * 2, $r * 2, $r * 2, 90, 90)
    $path.CloseFigure()
    $bg = New-Object System.Drawing.SolidBrush($ink)
    $g.FillPath($bg, $path)

    # ヘアラインの境界 (小さいサイズでは潰れるので出さない)
    if ($size -ge 32) {
        $pen = New-Object System.Drawing.Pen($border, [float][Math]::Max(1.0, $size / 64.0))
        $g.DrawPath($pen, $path)
        $pen.Dispose()
    }

    # コーラルの文字盤
    $d = [float]($size * 0.56)
    $o = [float](($size - $d) / 2.0)
    $face = New-Object System.Drawing.SolidBrush($coral)
    $g.FillEllipse($face, $o, $o, $d, $d)

    # 針はプレート色で切り抜く (別色を足さず、面の抜きだけで表現する)
    $hand = New-Object System.Drawing.Pen($ink, [float][Math]::Max(1.5, $size * 0.075))
    $hand.StartCap = [System.Drawing.Drawing2D.LineCap]::Flat
    $hand.EndCap   = [System.Drawing.Drawing2D.LineCap]::Flat
    $cx = [float]($size / 2.0)
    $g.DrawLine($hand, $cx, $cx, $cx, [float]($cx - $d * 0.34))
    $g.DrawLine($hand, $cx, $cx, [float]($cx + $d * 0.26), $cx)

    $hand.Dispose()
    $face.Dispose()
    $bg.Dispose()
    $path.Dispose()
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
