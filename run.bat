@echo off
:: Developer: heaplyn
:: Date: 2026-08-09
:: Summary: Cleans, builds, and launches the Jarvis Launcher WPF application on double-click.

:: Ensure the script runs in the directory where this file resides
cd /d "%~dp0"

echo 🧹 Cleaning previous build caches...
dotnet clean
echo.

echo ⚙️ Building Jarvis Launcher...
dotnet build -c Debug
echo.

echo 🚀 Launching Jarvis HUD background service...
dotnet run 
