@echo off
echo Building Buhly...
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /optimize+ /win32manifest:app.manifest /win32icon:Buhly.ico /out:Buhly.exe /resource:Template_TN2.xlsx,Buhly.Template_TN2.xlsx /resource:Template_TTN1.xlsx,Buhly.Template_TTN1.xlsx /r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll /r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll /r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll /r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll /r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll /r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll /r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.IO.Compression.dll /r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.IO.Compression.FileSystem.dll /r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Xml.dll /r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Xml.Linq.dll Program.cs SystemDiagnoser.cs GovServices.cs CompanyProfiles.cs OverviewTab.cs PortalsTab.cs RegistryTab.cs ToolsTab.cs WaybillEngine.cs WaybillTab.cs AnalyticsEngine.cs AnalyticsTab.cs
if %errorlevel% equ 0 (
    echo [OK] Buhly.exe created successfully!
) else (
    echo [ERROR] Build failed!
)
