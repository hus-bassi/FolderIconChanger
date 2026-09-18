param(
    [string]$OutputFile = (Join-Path $PSScriptRoot "..\FolderIconChanger\Assets\app-icon.ico")
)

Add-Type -AssemblyName System.Drawing

function New-RoundedRectPath {
    param(
        [float]$X, [float]$Y, [float]$W, [float]$H,
        [float]$RadiusTL, [float]$RadiusTR, [float]$RadiusBR, [float]$RadiusBL
    )
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $RadiusTL * 2
    $p.AddArc($X, $Y, $d, $d, 180, 90)
    $d = $RadiusTR * 2
    $p.AddArc($X + $W - $d, $Y, $d, $d, 270, 90)
    $d = $RadiusBR * 2
    $p.AddArc($X + $W - $d, $Y + $H - $d, $d, $d, 0, 90)
    $d = $RadiusBL * 2
    $p.AddArc($X, $Y + $H - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-FolderPath {
    param([float]$U)
    # Folder silhouette at 256 units
    $tabL = 26; $tabR = 116; $tabTop = 56; $tabBot = 96
    $bodyL = 24; $bodyR = 232; $bodyTop = 96; $bodyBot = 228
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc($tabL, $tabTop, 28, 28, 180, 90)
    $p.AddLine($tabL + 14, $tabTop, $tabR - 20, $tabTop)
    $p.AddArc($tabR - 28, $tabTop, 28, 28, 270, 90)
    $p.AddLine($tabR, $tabTop + 20, $bodyL + 6, $bodyTop + 6)
    $p.AddArc($bodyL, $bodyTop, 24, 24, 180, 90)
    $p.AddLine($bodyL + 12, $bodyTop, $bodyR - 24, $bodyTop)
    $p.AddArc($bodyR - 24, $bodyTop, 24, 24, 270, 90)
    $p.AddLine($bodyR, $bodyTop + 40, $bodyR, $bodyBot - 20)
    $p.AddArc($bodyR - 24, $bodyBot - 24, 24, 24, 0, 90)
    $p.AddLine($bodyR - 12, $bodyBot, $bodyL + 20, $bodyBot)
    $p.AddArc($bodyL, $bodyBot - 24, 24, 24, 90, 90)
    $p.AddLine($bodyL, $bodyBot - 12, $bodyL, $bodyTop + 20)
    $p.CloseFigure()
    return $p
}

$size = 256
$bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.Clear([System.Drawing.Color]::Transparent)

# Drop shadow (subtle)
$shadowRect = New-Object System.Drawing.RectangleF(32, 40, 196, 192)
$shadowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(60, 0, 0, 0))
$g.FillEllipse($shadowBrush, 40, 232, 176, 20)
$g.Dispose()
$shadowBrush.Dispose()

# Folder body with vertical gradient
$rect = New-Object System.Drawing.RectangleF(0, 0, 256, 256)
$grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, [System.Drawing.Color]::FromArgb(255, 96, 158, 255), [System.Drawing.Color]::FromArgb(255, 58, 112, 224), 90)
$folder = New-FolderPath -U $size
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.FillPath($grad, $folder)

# Folder outline
$outline = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 42, 88, 178), 5)
$g.DrawPath($outline, $folder)

# White image badge (circle)
$badgeRect = New-Object System.Drawing.RectangleF(92, 132, 72, 72)
$badgeBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(250, 250, 251, 255))
$g.FillEllipse($badgeBrush, $badgeRect)

# "picture" motif inside badge (mountain + sun) in folder blue
$paint = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 58, 112, 224))
$sunRect = New-Object System.Drawing.RectangleF(146, 142, 10, 10)
$g.FillEllipse($paint, $sunRect)
$hills = [System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(104, 186)),
    (New-Object System.Drawing.PointF(120, 162)),
    (New-Object System.Drawing.PointF(134, 186))
)
$g.FillPolygon($paint, $hills)
$hills2 = [System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(126, 186)),
    (New-Object System.Drawing.PointF(142, 168)),
    (New-Object System.Drawing.PointF(156, 186))
)
$g.FillPolygon($paint, $hills2)

$g.Dispose(); $grad.Dispose(); $outline.Dispose(); $badgeBrush.Dispose(); $paint.Dispose(); $folder.Dispose()

# ---------- pack ICO with PNG entries ----------
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngStreams = @()
foreach ($s in $sizes) {
    $scaled = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $sg = [System.Drawing.Graphics]::FromImage($scaled)
    $sg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $sg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $sg.DrawImage($bmp, 0, 0, $s, $s)
    $ms = New-Object System.IO.MemoryStream
    $scaled.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngStreams += ,$ms
    $sg.Dispose(); $scaled.Dispose()
}

$header = New-Object System.IO.MemoryStream
$hw = New-Object System.IO.BinaryWriter($header)
$hw.Write([UInt16]0)
$hw.Write([UInt16]1)
$hw.Write([UInt16]$sizes.Count)

$offset = 6 + (16 * $sizes.Count)
$blobs = @()
foreach ($s in $sizes) {
    $data = $pngStreams[$i++].ToArray()
    $blobs += ,$data
}
$i = 0
foreach ($s in $sizes) {
    $hw.Write([byte]($(if ($s -eq 256) { 0 } else { $s })))
    $hw.Write([byte]($(if ($s -eq 256) { 0 } else { $s })))
    $hw.Write([byte]0)
    $hw.Write([byte]0)
    $hw.Write([UInt16]1)
    $hw.Write([UInt16]32)
    $hw.Write([UInt32]$blobs[$i].Length)
    $hw.Write([UInt32]$offset)
    $offset += $blobs[$i].Length
    $i++
}
foreach ($data in $blobs) { $hw.Write($data) }

$dir = Split-Path $OutputFile -Parent
if (!(Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
[System.IO.File]::WriteAllBytes($OutputFile, $header.ToArray())
$hw.Dispose(); $header.Dispose()
foreach ($ms in $pngStreams) { $ms.Dispose() }
$bmp.Dispose()

Write-Host "App icon written to $OutputFile"