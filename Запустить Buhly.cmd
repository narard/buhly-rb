@echo off
chcp 65001 >nul
setlocal
set "APP_DIR=%~dp0"
set "EXE_PATH=%APP_DIR%Buhly.exe"
set "ICO_PATH=%APP_DIR%Buhly.ico"

if not exist "%EXE_PATH%" (
    echo [Error] Buhly.exe not found in %APP_DIR%
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command "Unblock-File -Path '%EXE_PATH%' -ErrorAction SilentlyContinue; $desk = [Environment]::GetFolderPath('Desktop'); $lnkPath = Join-Path $desk 'Buhly - ГосПорталы РБ.lnk'; if (-not (Test-Path $lnkPath)) { $ws = New-Object -ComObject WScript.Shell; $sc = $ws.CreateShortcut($lnkPath); $sc.TargetPath = '%EXE_PATH%'; $sc.WorkingDirectory = '%APP_DIR%'; if (Test-Path '%ICO_PATH%') { $sc.IconLocation = '%ICO_PATH%' }; $sc.Description = 'Buhly — Вход на госпорталы РБ'; $sc.Save() }"

start "" "%EXE_PATH%"
endlocal
