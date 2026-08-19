@echo off
:: Developer: heaplyn
:: Date: 2026-08-19
:: Summary: Optimized Jarvis Bootstrapper with binary auto-update logic.

cd /d "%~dp0"

echo 🔍 Checking for running instances...
taskkill /f /im JarvisLauncher.exe >nul 2>&1

echo 🧹 Cleaning previous build caches...
dotnet clean
echo.

echo ⚙️ Building High-Fidelity Jarvis Launcher...
dotnet build -c Debug
echo.

echo 🔄 Updating project root binary...
copy /y "bin\Debug\net8.0-windows\win-x64\JarvisLauncher.exe" "JarvisLauncher.exe" >nul
copy /y "bin\Debug\net8.0-windows\win-x64\JarvisLauncher.dll" "JarvisLauncher.dll" >nul

echo 🚀 Launching Jarvis HUD Environment...
dotnet run
exit
