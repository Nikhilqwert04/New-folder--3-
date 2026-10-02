@echo off
setlocal enabledelayedexpansion

REM ═══════════════════════════════════════════════════════════════
REM  Intel Vibrance — Build Script
REM  Requirements: .NET Framework 4.8 (built-in on Windows 10/11)
REM                Internet access (first run, downloads Roslyn compiler)
REM ═══════════════════════════════════════════════════════════════

set OUTDIR=bin
set EXE=%OUTDIR%\IntelVibrance.exe
set ROSLYN_DIR=%OUTDIR%\compilers\Microsoft.Net.Compilers.3.11.0\tools
set CSC=%ROSLYN_DIR%\csc.exe
set NUGET=%OUTDIR%\nuget.exe

echo.
echo  Intel Vibrance Build Script
echo  ════════════════════════════
echo.

REM ── Create output directory ─────────────────────────────────────
if not exist "%OUTDIR%" mkdir "%OUTDIR%"

REM ── Download Roslyn compiler if not present ─────────────────────
if not exist "%CSC%" (
    echo [1/3] Downloading Roslyn C# compiler...

    if not exist "%NUGET%" (
        powershell -Command "Invoke-WebRequest -Uri 'https://dist.nuget.org/win-x86-commandline/latest/nuget.exe' -OutFile '%NUGET%'"
        if not exist "%NUGET%" (
            echo [ERROR] Failed to download nuget.exe
            echo         Check internet connection and try again.
            exit /b 1
        )
    )

    "%NUGET%" install Microsoft.Net.Compilers -Version 3.11.0 -OutputDirectory "%OUTDIR%\compilers" -NonInteractive
    if not exist "%CSC%" (
        echo [ERROR] Failed to install Roslyn compiler
        exit /b 1
    )
    echo [1/3] Roslyn compiler ready.
) else (
    echo [1/3] Roslyn compiler already present. Skipping download.
)

echo.
echo [2/3] Compiling Intel Vibrance...

"%CSC%" ^
    /target:winexe ^
    /optimize+ ^
    /platform:x64 ^
    /out:"%EXE%" ^
    /nologo ^
    /warn:3 ^
    /langversion:8.0 ^
    /reference:System.dll ^
    /reference:System.Core.dll ^
    /reference:System.Drawing.dll ^
    /reference:System.Windows.Forms.dll ^
    /reference:System.Management.dll ^
    /reference:System.Runtime.InteropServices.dll ^
    Program.cs ^
    Core\Logger.cs ^
    Core\DisplayManager.cs ^
    Core\ColorTransform.cs ^
    Core\VibranceController.cs ^
    Core\StateManager.cs ^
    Windows\DisplayAPI.cs ^
    UI\Controls.cs ^
    UI\MainWindow.cs

if %ERRORLEVEL% == 0 (
    echo.
    echo [3/3] Build successful!
    echo.
    echo  Output: %EXE%
    echo.
    echo  Run with: %EXE%
    echo.
    start "" "%EXE%"
) else (
    echo.
    echo [ERROR] Build failed with error %ERRORLEVEL%
    echo         See above for details.
    exit /b %ERRORLEVEL%
)

endlocal
