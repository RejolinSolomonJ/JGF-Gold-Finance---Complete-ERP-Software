@echo off
echo ====================================================
echo  JGF Jewel Gold Finance - Setup PC1 Server (v1.1.0)
echo ====================================================
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\..\packaging\pc1-server-installer\Install-PC1-Server.ps1"
pause
