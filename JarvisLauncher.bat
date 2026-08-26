@echo off
:: Developer: heaplyn
:: Date: 2026-08-19
:: Summary: Optimized Jarvis Bootstrapper with binary auto-update logic.

cd /d "%~dp0"

echo 🔍 Checking for running instances...
taskkill /f /im JarvisLauncher.exe >nul 2>&1

echo 🚀 Launching Jarvis HUD Environment...
dotnet build -t:Run -p:BuildInParallel=true
exit
