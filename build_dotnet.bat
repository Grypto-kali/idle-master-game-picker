@echo off
title .NET Build Script
echo ============================
echo   .NET Build Automation
echo ============================
echo.

:: Check if dotnet is installed
where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    echo [ERROR] .NET SDK not found. Please install it from:
    echo https://dotnet.microsoft.com/download
    pause
    exit /b
)

:: Ask for project path
set /p projectPath=Enter project path (e.g. . or MyApp): 
if "%projectPath%"=="" set projectPath=.

if not exist "%projectPath%" (
    echo [ERROR] Project path "%projectPath%" not found.
    pause
    exit /b
)

:: Ask for configuration
set /p config=Enter configuration [Debug/Release] (default: Release): 
if "%config%"=="" set config=Release

:: Ask for runtime
set /p runtime=Enter runtime identifier (e.g. win-x64, linux-x64, osx-arm64) (default: win-x64): 
if "%runtime%"=="" set runtime=win-x64

:: Ask for self-contained
set /p selfContained=Self-contained? [true/false] (default: true): 
if "%selfContained%"=="" set selfContained=true

:: Ask for single-file
set /p singleFile=Publish single file? [true/false] (default: true): 
if "%singleFile%"=="" set singleFile=true

:: Ask for trimming
set /p trim=Enable trimming? [true/false] (default: false): 
if "%trim%"=="" set trim=false

:: Ask for clean
set /p clean=Clean project before build? [true/false] (default: false): 
if "%clean%"=="" set clean=false

:: Ask for output directory
set /p outputDir=Enter output directory (default: .\out\%runtime%): 
if "%outputDir%"=="" set outputDir=.\out\%runtime%

echo.
dotnet --version
echo.

if /I "%clean%"=="true" (
    echo ===== Cleaning project =====
    dotnet clean "%projectPath%" -c %config%
)

echo.
echo ===== Restoring packages =====
dotnet restore "%projectPath%"

echo.
echo ===== Publishing project =====
dotnet publish "%projectPath%" -c %config% -r %runtime% --self-contained %selfContained% -p:PublishSingleFile=%singleFile% -p:PublishTrimmed=%trim% -o "%outputDir%"

echo.
echo ===== DONE! =====
echo Output in: %outputDir%
echo Build completed at %date% %time%
pause
