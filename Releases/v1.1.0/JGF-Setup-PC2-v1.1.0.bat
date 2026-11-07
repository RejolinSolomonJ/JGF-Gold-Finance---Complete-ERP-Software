@echo off
echo ====================================================
echo  JGF Jewel Gold Finance - Setup PC2 Client (v1.1.0)
echo ====================================================
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\..\packaging\pc2-client-installer\Install-PC2-Client.ps1"
pause

