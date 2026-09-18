# Renders the README hero banner (assets/hero.png).
Add-Type -AssemblyName System.Drawing

$iconPath = "D:\FolderICO\FolderIconChanger\Assets\app-icon.ico"
$outPath  = "D:\FolderICO\assets\hero.png"

# Extract the largest (PNG-encoded) frame straight from the .ico so GDI+ can decode it.
$ico = [System.IO.File]::ReadAllBytes($iconPath)
$count = [BitConverter]::ToInt16($ico, 4)
$bestOff = 0; $bestSize = 0; $bestW = 0; $bestH = 0
for ($i = 0; $i -lt $count; $i++) {
    $off = 6 + (16 * $i)
    $w = $ico[$off]; $h = $ico[$off + 1]
    if ($w -eq 0) { $w = 256 }; if ($h -eq 0) { $h = 256 }
    $size = [BitConverter]::ToInt32($ico, $off + 8)
    $data = [BitConverter]::ToInt32($ico, $off + 12)
    if ($size -gt $bestSize) { $bestSize = $size; $bestOff = $data; $bestW = $w; $bestH = $h }
}
$ms = New-Object System.IO.MemoryStream(,$ico[$bestOff..($bestOff + $bestSize - 1)])
$iconBmp = [System.Drawing.Image]::FromStream($ms)

$w = 1600; $h = 900
$flags = [System.Drawing.Imaging.ImageFormat]::Png
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$g.Clear([System.Drawing.Color]::FromArgb(255, 16, 19, 26))

$rect = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    $rect,
    [System.Drawing.Color]::FromArgb(255, 24, 34, 52),
    [System.Drawing.Color]::FromArgb(255, 12, 14, 22),
    45.0)
$g.FillRectangle($brush, $rect)

# accent glow disc behind the icon
$glow = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(45, 76, 141, 255))
$g.FillEllipse($glow, 120, 220, 560, 560)
$glow.Dispose()

# app icon, high quality
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.DrawImage($iconBmp, 210, 280, 340, 340)

$titleFont = New-Object System.Drawing.Font("Segoe UI", 66, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$tBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$g.DrawString("FolderIconChanger", $titleFont, $tBrush, 620, 280)

$subFont = New-Object System.Drawing.Font("Segoe UI", 28, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$sBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 150, 170, 200))
$g.DrawString("Change any folder icon on Windows in seconds", $subFont, $sBrush, 622, 395)

$arFont = New-Object System.Drawing.Font("Segoe UI", 30, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$aBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 76, 141, 255))
$g.DrawString("غيّر أيقونة أي مجلد في ثواني على ويندوز", $arFont, $aBrush, 60, 760)

$hintFont = New-Object System.Drawing.Font("Segoe UI", 22, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$hBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 120, 135, 160))
$g.DrawString("Windows 10/11   ·   .NET 8   ·   Free   ·   Open Source", $hintFont, $hBrush, 622, 690)

$g.Dispose()
$bmp.Save($outPath, $flags)
$bmp.Dispose(); $iconBmp.Dispose(); $ms.Dispose()
"Banner: $outPath ($((Get-Item $outPath).Length) bytes)"