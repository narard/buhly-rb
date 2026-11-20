<#
.SYNOPSIS
  Упаковка Buhly.exe в MSIX-пакет (для Microsoft Store или корпоративного развёртывания).
.DESCRIPTION
  Требует Windows 10/11 SDK (makeappx.exe).
  Для публикации в Microsoft Store замени Identity.Name и Identity.Publisher в Package.appxmanifest
  на значения из Partner Center (Microsoft Store сам подписывает пакет при публикации — SmartScreen не появляется).
  Для локального теста/развёртывания передайте -PfxPath и -PfxPassword, чтобы подписать .msix через signtool.exe.
#>
param(
    [string]$Version = "3.2.0.0",
    [string]$PfxPath = "",
    [string]$PfxPassword = ""
)

$ErrorActionPreference = "Stop"
$msixDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $msixDir
$releaseDir = Join-Path $rootDir "Release"
$stageDir = Join-Path $env:TEMP "Buhly_MSIX_Stage"

# 1. Собираем актуальный Buhly.exe через build.bat
Push-Location $rootDir
try {
    & cmd /c "build.bat"
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $rootDir "Buhly.exe"))) {
        throw "Ошибка сборки Buhly.exe через build.bat"
    }
}
finally {
    Pop-Location
}

# 2. Ищем makeappx.exe в Windows SDK
$kitsRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
$makeAppx = Get-ChildItem -Path $kitsRoot -Filter "makeappx.exe" -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match "\\x64\\" } |
    Sort-Object FullName -Descending |
    Select-Object -First 1

if (-not $makeAppx) {
    Write-Warning "makeappx.exe (Windows 10/11 SDK) не найден в $kitsRoot."
    Write-Host "Установите Windows SDK (Desktop/UWP App Certification Kit) или соберите пакет в GitHub Actions / Microsoft Store."
    exit 1
}

# 3. Готовим staging-папку
if (Test-Path $stageDir) { Remove-Item $stageDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stageDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $stageDir "Assets") | Out-Null

Copy-Item (Join-Path $rootDir "Buhly.exe") -Destination $stageDir -Force
Copy-Item (Join-Path $msixDir "Package.appxmanifest") -Destination $stageDir -Force
Copy-Item (Join-Path $msixDir "Assets\*.png") -Destination (Join-Path $stageDir "Assets") -Force

# 4. Упаковываем .msix
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
$outMsix = Join-Path $releaseDir ("Buhly_" + $Version + "_x64.msix")
if (Test-Path $outMsix) { Remove-Item $outMsix -Force }

& $makeAppx.FullName pack /d $stageDir /p $outMsix /o
if ($LASTEXITCODE -ne 0) {
    throw "makeappx.exe завершился с ошибкой ($LASTEXITCODE)"
}

Write-Host "[OK] MSIX-пакет создан: $outMsix"

# 5. Опциональная локальная подпись .pfx
if (-not [string]::IsNullOrWhiteSpace($PfxPath) -and (Test-Path $PfxPath)) {
    $signTool = Get-ChildItem -Path $kitsRoot -Filter "signtool.exe" -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "\\x64\\" } |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if ($signTool) {
        $argsList = @("sign", "/fd", "SHA256", "/f", $PfxPath)
        if (-not [string]::IsNullOrWhiteSpace($PfxPassword)) {
            $argsList += @("/p", $PfxPassword)
        }
        $argsList += $outMsix
        & $signTool.FullName @argsList
        if ($LASTEXITCODE -ne 0) { throw "signtool.exe завершился с ошибкой" }
        Write-Host "[OK] MSIX-пакет подписан."
    }
    else {
        Write-Warning "signtool.exe не найден, пакет остался неподписанным."
    }
}
