@echo off
setlocal
title AMATORA OBS CONTROLLER — Setup Builder

echo ===================================================
echo  1. Compiling AMATORA OBS CONTROLLER (Native .NET EXE)
echo ===================================================
call "build.bat"
if %errorlevel% neq 0 (
    echo [ERROR] EXE build failed!
    exit /b %errorlevel%
)

echo.
echo ===================================================
echo  2. Building Inno Setup Installer (Setup.exe)
echo ===================================================

set "ISCC=C:\Users\NITRO 5\AppData\Local\Programs\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" (
    set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
)
if not exist "%ISCC%" (
    set "ISCC=C:\Program Files\Inno Setup 6\ISCC.exe"
)

if not exist "%ISCC%" (
    echo [ERROR] ISCC.exe not found! Please check Inno Setup installation.
    exit /b 1
)

"%ISCC%" "installer.iss"
if %errorlevel% neq 0 (
    echo [ERROR] Installer build failed!
    exit /b %errorlevel%
)

echo.
echo ===================================================
echo  [SUCCESS] AMATORA OBS Controller Setup.exe Created!
echo  Location: dist\AMATORA OBS Controller Setup.exe
echo ===================================================
endlocal
