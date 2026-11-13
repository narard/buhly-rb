@echo off
echo Building Buhly v2...
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /optimize+ /win32manifest:app.manifest /win32icon:Buhly.ico /out:Buhly.exe /r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll /r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll /r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll /r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll /r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll /r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll /r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.IO.Compression.FileSystem.dll Program.cs
if %errorlevel% equ 0 (
    echo [OK] Buhly.exe v2 created successfully!
) else (
    echo [ERROR] Build failed!
)
