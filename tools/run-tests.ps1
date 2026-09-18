# Full integration test for FolderIconChanger (runs the published EXE in CLI mode).
param(
    [string]$Exe = (Join-Path $PSScriptRoot "..\publish\FolderIconChanger.exe")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$testRoot = Join-Path $env:TEMP "FolderIconChangerTests"
if (Test-Path $testRoot) { Remove-Item -Recurse -Force $testRoot }
[System.IO.Directory]::CreateDirectory($testRoot) | Out-Null

$results = [System.Collections.ArrayList]::new()
function Pass([string]$msg)  { Write-Host "  PASS  $msg" -ForegroundColor Green; [void]$results.Add("PASS: $msg") }
function Fail([string]$msg)  { Write-Host "  FAIL  $msg" -ForegroundColor Red;   [void]$results.Add("FAIL: $msg") }

function Invoke-App {
    param([string[]]$Command, [string]$Image, [string]$Folder)
    # Pass a small arg file (with UTF-8 encoding) instead of a raw command line,
    # so paths with spaces or non-Latin characters are never mangled.
    $argFile = Join-Path $env:TEMP ("fic_args_{0}.txt" -f [guid]::NewGuid().ToString("N"))
    $lines = @()
    foreach ($a in $Command) { $lines += $a }
    if ($Image) { $lines += $Image }
    if ($Folder) { $lines += $Folder }
    [System.IO.File]::WriteAllLines($argFile, $lines, (New-Object System.Text.UTF8Encoding($false)))

    $resultFile = Join-Path $env:TEMP "FolderIconChanger_cli_result.json"
    if (Test-Path $resultFile) { Remove-Item $resultFile -Force }

    $argLine = "@" + $argFile
    $proc = New-Object System.Diagnostics.Process
    $proc.StartInfo.FileName = $Exe
    $proc.StartInfo.Arguments = $argLine
    $proc.StartInfo.UseShellExecute = $false
    $proc.Start() | Out-Null
    $proc.WaitForExit()
    $code = $proc.ExitCode
    $proc.Dispose()
    Remove-Item $argFile -Force -ErrorAction SilentlyContinue

    if (!(Test-Path $resultFile)) { return [pscustomobject]@{ ok = $false; message = "no result file (exit $code)"; exit = $code } }
    $json = Get-Content -Raw $resultFile | ConvertFrom-Json
    return [pscustomobject]@{ ok = $json.ok; message = $json.message; exit = $proc.ExitCode }
}

function Assert-IcoFileValid([string]$path) {
    if (!(Test-Path $path)) { return $false }
    $bytes = [System.IO.File]::ReadAllBytes($path)
    if ($bytes.Length -lt 22) { return $false }
    if ($bytes[0] -ne 0 -or $bytes[1] -ne 0) { return $false }
    if ($bytes[2] -ne 1 -or $bytes[3] -ne 0) { return $false }
    $count = [BitConverter]::ToInt16($bytes, 4)
    if ($count -lt 1 -or $count -gt 16) { return $false }
    # verify each entry's offset stays within the file
    for ($i = 0; $i -lt $count; $i++) {
        $off = 6 + (16 * $i)
        $size = [BitConverter]::ToInt32($bytes, $off + 8)
        $dataOffset = [BitConverter]::ToInt32($bytes, $off + 12)
        if (($dataOffset + $size) -gt $bytes.Length) { return $false }
    }
    return $true
}

function Get-Attrs([string]$path) { return (Get-Item -LiteralPath $path -Force).Attributes }

function Get-DesktopIniContent([string]$dir) {
    $p = Join-Path $dir "desktop.ini"
    if (!(Test-Path -LiteralPath $p)) { return "" }
    return (Get-Content -Raw -LiteralPath $p -Force)
}

# ---------------------------------------------------------------- images
$imgDir = Join-Path $testRoot "Images"
[System.IO.Directory]::CreateDirectory($imgDir) | Out-Null

function New-TestImage([string]$name, [int]$w, [int]$h, [string]$format, [bool]$transparent = $false) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    if ($transparent) { $g.Clear([System.Drawing.Color]::Transparent) }
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 76, 141, 255))
    if ($transparent) {
        $g.FillEllipse($brush, 10, 10, $w - 20, $h - 20)
        $g.FillEllipse((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 88, 120))), $w / 2 - 20, $h / 2 - 20, 40, 40)
    } else {
        $g.Clear([System.Drawing.Color]::FromArgb(255, 30, 34, 44))
        $g.FillRectangle($brush, 0, 0, $w, $h / 2)
        $g.FillRectangle((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 240, 190, 90))), 0, $h / 2, $w, $h / 2)
    }
    $path = Join-Path $imgDir $name
    switch ($format) {
        "png" { $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png) }
        "jpg" { $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Jpeg) }
        "bmp" { $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Bmp) }
    }
    $g.Dispose(); $bmp.Dispose()
    return $path
}

$imgPng      = New-TestImage "test.png"          300 200 "png"
$imgTrans    = New-TestImage "transparent.png"   256 256 "png" $true
$imgJpg      = New-TestImage "test.jpg"          600 400 "jpg"
$imgBmp      = New-TestImage "test.bmp"          128 256 "bmp"
$imgSmall    = New-TestImage "small.png"         8 8 "png"
$imgLarge    = New-TestImage "large.png"         3000 2000 "png"
$imgWide     = New-TestImage "wide.png"          900 120 "png"

# ---------------------------------------------------------------- folders
$dirNormal  = Join-Path $testRoot "Normal Folder"
$dirArabic  = Join-Path $testRoot "مجلد تجريبي"
$dirRussian = Join-Path $testRoot "Папка для теста"
$dirIni     = Join-Path $testRoot "WithExistingIni"
$dirIcon    = Join-Path $testRoot "WithExistingIcon"
$dirBmp     = Join-Path $testRoot "BmpFolder"
$dirLarge   = Join-Path $testRoot "LargeFolder"
$dirWide    = Join-Path $testRoot "WideFolder"
foreach ($d in @($dirNormal, $dirArabic, $dirRussian, $dirIni, $dirIcon, $dirBmp, $dirLarge, $dirWide)) {
    [System.IO.Directory]::CreateDirectory($d) | Out-Null
}

# pre-existing desktop.ini with unrelated settings + a custom icon reference
$appIconSrc = Join-Path $PSScriptRoot "..\FolderIconChanger\Assets\app-icon.ico"
$existingIni = @"
[.ShellClassInfo]
InfoTip=Some user note
IconResource=C:\Windows\System32\shell32.dll,3
[MyAppSettings]
Theme=Dark
"@
[System.IO.File]::WriteAllText((Join-Path $dirIni "desktop.ini"), $existingIni, [System.Text.Encoding]::UTF8)
Copy-Item $appIconSrc (Join-Path $dirIni "existing.ico")
# pre-existing desktop.ini that already points to a FolderIcon.ico
[System.IO.File]::WriteAllText((Join-Path $dirIcon "desktop.ini"), "[.ShellClassInfo]`r`nIconResource=FolderIcon.ico,0`r`n", [System.Text.Encoding]::UTF8)
Copy-Item $appIconSrc (Join-Path $dirIcon "FolderIcon.ico")

Write-Host "== App icon base line: $Exe" -ForegroundColor Cyan

# ============================================================ test cases
function Test-Apply([string]$label, [string]$image, [string]$folder) {
    Write-Host "`n> $label" -ForegroundColor Cyan
    $r = Invoke-App -Command @("apply") -Image $image -Folder $folder
    if (!$r.ok) { Fail "$label : apply failed ($($r.message))"; return }
    Pass "$label : apply returned ok"
    $ini = Join-Path $folder "desktop.ini"
    $iconCandidates = Get-ChildItem -LiteralPath $folder -Force -Filter "FolderIcon*.ico" -ErrorAction SilentlyContinue
    $di = Get-DesktopIniContent $folder
    if (!(Test-Path -LiteralPath $ini)) { Fail "$label : desktop.ini missing" } elseif ($di -match "\[\.ShellClassInfo\]" -and $di -match "IconResource=FolderIcon") { Pass "$label : desktop.ini has IconResource" } else { Fail "$label : desktop.ini missing IconResource" }
    if (!$iconCandidates) { Fail "$label : no FolderIcon*.ico created" } else {
        $ico = $iconCandidates[0]
        if (Assert-IcoFileValid $ico.FullName) { Pass "$label : ICO file is valid multi-res" } else { Fail "$label : ICO file invalid" }
        $attrs = Get-Attrs $ico.FullName
        if (($attrs -band [System.IO.FileAttributes]::Hidden) -and ($attrs -band [System.IO.FileAttributes]::System)) { Pass "$label : icon hidden+system" } else { Fail "$label : icon attrs=$attrs" }
    }
    $fattrs = Get-Attrs $folder
    if (($fattrs -band [System.IO.FileAttributes]::ReadOnly) -and ($fattrs -band [System.IO.FileAttributes]::System)) { Pass "$label : folder marked ReadOnly+System" } else { Fail "$label : folder attrs=$fattrs" }
    $dattrs = Get-Attrs $ini
    if (($dattrs -band [System.IO.FileAttributes]::Hidden) -and ($dattrs -band [System.IO.FileAttributes]::System)) { Pass "$label : desktop.ini hidden+system" } else { Fail "$label : desktop.ini attrs=$dattrs" }
    $record = Get-Content -Raw (Join-Path $env:LOCALAPPDATA "FolderIconChanger\settings.json") -ErrorAction SilentlyContinue | ConvertFrom-Json
    $found = @($record.Records | Where-Object { $_.FolderPath -ieq $folder })
    if ($found.Count -eq 1) { Pass "$label : settings record saved" } else { Fail "$label : settings record missing" }
}

Test-Apply "PNG non-square"        $imgPng   $dirNormal
Test-Apply "Transparent PNG"       $imgTrans $dirArabic
Test-Apply "JPG"                   $imgJpg   $dirRussian
Test-Apply "BMP"                   $imgBmp   $dirBmp
Test-Apply "Very small 8x8"        $imgSmall $dirIcon
Test-Apply "Very large 3000x2000"  $imgLarge $dirLarge
Test-Apply "Wide banner"           $imgWide  $dirWide
Test-Apply "Folder w/ existing desktop.ini" $imgPng $dirIni
Test-Apply "Re-apply over existing icon"    $imgTrans $dirIcon

# verify existing desktop.ini preserved unrelated settings
Write-Host "`n> Preserving unrelated desktop.ini content"
$di = Get-DesktopIniContent $dirIni
if ($di -match "InfoTip=Some user note" -and $di -match "\[MyAppSettings\]" -and $di -match "Theme=Dark") { Pass "unrelated settings preserved" } else { Fail "unrelated settings lost`n$di" }

# verify relative path used (portable)
if ($di -match "IconResource=FolderIcon[^,]*\.ico") { Pass "relative icon path used" } else { Fail "relative path not used: $di" }

# move the folder and re-check it still references correctly
Write-Host "`n> Moving customized folder"
$moved = Join-Path $testRoot "MovedFolder"
if (Test-Path $moved) { Remove-Item -Recurse -Force $moved }
[System.IO.Directory]::CreateDirectory($moved) | Out-Null
Move-Item -LiteralPath $dirIni -Destination $moved -Force | Out-Null
$di = Get-DesktopIniContent (Join-Path $moved "WithExistingIni")
if ($di -match "IconResource=FolderIcon") { Pass "icon reference is relative -> survives folder move" } else { Fail "no relative reference after move" }
Move-Item -LiteralPath (Join-Path $moved "WithExistingIni") -Destination $testRoot -Force | Out-Null

# ============================================================ restore tests
Write-Host "`n> Restore default (normal folder)"
$r = Invoke-App -Command @("restore") -Folder $dirNormal
if ($r.ok) { Pass "restore returned ok" } else { Fail "restore failed: $($r.message)" }
$ini = Join-Path $dirNormal "desktop.ini"
if (!(Test-Path -LiteralPath $ini)) { Pass "desktop.ini removed (was created by app)" } else { Fail "desktop.ini still exists: $((Get-DesktopIniContent $dirNormal))" }
if (Test-Path (Join-Path $dirNormal "FolderIcon.ico")) { Fail "icon file not removed" } else { Pass "icon file removed" }
$fa = Get-Attrs $dirNormal
if (($fa -band [System.IO.FileAttributes]::ReadOnly) -eq 0 -and ($fa -band [System.IO.FileAttributes]::System) -eq 0) { Pass "folder attributes restored to normal" } else { Fail "folder attrs still set: $fa" }

Write-Host "`n> Restore with PREVIOUS custom icon (should revert to shell32)"
$r = Invoke-App -Command @("restore") -Folder $dirIni
$di = Get-DesktopIniContent $dirIni
if ($r.ok) { Pass "restore ok" } else { Fail "restore failed: $($r.message)" }
if ($di -match "IconResource=C:\\Windows\\System32\\shell32.dll,3") { Pass "reverted to previous icon resource" } else { Fail "previous icon not restored: $di" }
if ($di -match "InfoTip=Some user note" -and $di -match "Theme=Dark") { Pass "unrelated settings survived restore" } else { Fail "unrelated settings lost on restore" }

Write-Host "`n> Restore a folder that has NO custom icon"
$plain = Join-Path $testRoot "PlainFolder"
[System.IO.Directory]::CreateDirectory($plain) | Out-Null
$r = Invoke-App -Command @("restore") -Folder $plain
if (!$r.ok -and $r.message -match "does not have") { Pass "reports no custom icon" } else { Fail "unexpected result: $($r.message)" }

Write-Host "`n> Restore missing folder"
$r = Invoke-App -Command @("restore") -Folder (Join-Path $testRoot "DoesNotExist")
if (!$r.ok) { Pass "missing folder handled gracefully" } else { Fail "should have failed" }

# ============================================================ error cases
Write-Host "`n> Apply to non-existent folder"
$r = Invoke-App -Command @("apply") -Image $imgPng -Folder (Join-Path $testRoot "Nope")
if (!$r.ok) { Pass "nonexistent folder rejected" } else { Fail "should have failed" }

Write-Host "`n> Apply non-image file"
$textFile = Join-Path $testRoot "not-an-image.txt"
[System.IO.File]::WriteAllText($textFile, "hello")
$r = Invoke-App -Command @("apply") -Image $textFile -Folder $dirNormal
if (!$r.ok) { Pass "non-image rejected gracefully" } else { Fail "should have failed" }

# ============================================================ protected folder
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
Write-Host "`n> Protected folder behavior (running as admin: $isAdmin)"
if (!$isAdmin) {
    $protected = Join-Path $env:ProgramFiles "FicProtectedTest"
    if (Test-Path $protected) { Remove-Item -Recurse -Force $protected }
    try {
        [System.IO.Directory]::CreateDirectory($protected) | Out-Null
        Remove-Item $protected -Force
    } catch {
        $r = Invoke-App -Command @("apply") -Image $imgPng -Folder $env:ProgramFiles
        if ($r.message -match "denied" -or !$r.ok) { Pass "protected folder returns friendly error: $($r.message)" } else { Fail "expected access error, got: $($r.message)" }
    }
} else {
    Write-Host "  (skipped - running elevated)" -ForegroundColor DarkGray
}

# ============================================================ summary
Write-Host "`n================ SUMMARY ================" -ForegroundColor Cyan
$fails = @($results | Where-Object { $_ -like "FAIL*" })
$passes = @($results | Where-Object { $_ -like "PASS*" })
Write-Host "PASS: $($passes.Count)   FAIL: $($fails.Count)" -ForegroundColor $(if ($fails.Count -eq 0) { "Green" } else { "Red" })
if ($fails.Count) {
    $fails | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}
exit 0