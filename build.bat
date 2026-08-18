@echo off
setlocal
echo ===================================================
echo   Compiling AMATORA OBS CONTROLLER (Native .NET WPF EXE)
echo ===================================================

set CSC_PATH="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist %CSC_PATH% set CSC_PATH="C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"

set WPF_REF="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"
if not exist %WPF_REF% set WPF_REF="C:\Windows\Microsoft.NET\Framework\v4.0.30319\WPF"

set ICON_FLAG=
if exist "%~dp0app.ico" set ICON_FLAG=/win32icon:"%~dp0app.ico"

%CSC_PATH% /target:winexe /optimize+ %ICON_FLAG% ^
    /r:%WPF_REF%\PresentationCore.dll ^
    /r:%WPF_REF%\PresentationFramework.dll ^
    /r:%WPF_REF%\WindowsBase.dll ^
    /r:System.Xaml.dll ^
    /r:System.dll ^
    /r:System.Core.dll ^
    /r:System.Net.Http.dll ^
    /r:System.Windows.Forms.dll ^
    /r:System.Drawing.dll ^
    /r:System.Web.Extensions.dll ^
    /out:"%~dp0AMATORA.exe" ^
    "%~dp0src\Program.cs"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ===================================================
    echo  [SUCCESS] AMATORA.exe successfully created!
    echo ===================================================
) else (
    echo.
    echo [ERROR] Compilation failed with code %ERRORLEVEL%
)

endlocal
