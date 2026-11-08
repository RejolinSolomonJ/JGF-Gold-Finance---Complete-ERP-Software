@echo off
:: ===================================================================
:: One-Click PostgreSQL Password Reset for JGF Jewel Gold Finance
:: ===================================================================
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [!] Administrator privileges required.
    echo Please right-click this file and select 'Run as administrator'.
    pause
    exit /b 1
)

echo [*] Resetting PostgreSQL master password to 'postgres'...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$pgHba = (Get-ChildItem 'C:\Program Files\PostgreSQL' -Filter 'pg_hba.conf' -Recurse | Select-Object -First 1).FullName; " ^
    "$orig = Get-Content $pgHba; " ^
    "$tmp = $orig -replace 'host\s+all\s+all\s+127\.0\.0\.1/32\s+scram-sha-256', 'host    all             all             127.0.0.1/32            trust'; " ^
    "$tmp = $tmp -replace 'host\s+all\s+all\s+::1/128\s+scram-sha-256', 'host    all             all             ::1/128                 trust'; " ^
    "Set-Content $pgHba $tmp; " ^
    "Restart-Service *postgres*; " ^
    "$psql = (Get-ChildItem 'C:\Program Files\PostgreSQL' -Filter 'psql.exe' -Recurse | Select-Object -First 1).FullName; " ^
    "& $psql -h localhost -p 5432 -U postgres -d postgres -c 'ALTER USER postgres WITH PASSWORD ''postgres'';'; " ^
    "& $psql -h localhost -p 5432 -U postgres -d postgres -c 'CREATE DATABASE jgf_db;'; " ^
    "& $psql -h localhost -p 5432 -U postgres -d postgres -c 'CREATE USER jgf_app WITH ENCRYPTED PASSWORD ''JgfSecure_AppPassword2026!'';'; " ^
    "& $psql -h localhost -p 5432 -U postgres -d postgres -c 'ALTER USER jgf_app WITH SUPERUSER CREATEDB;'; " ^
    "& $psql -h localhost -p 5432 -U postgres -d postgres -c 'GRANT ALL PRIVILEGES ON DATABASE jgf_db TO jgf_app;'; " ^
    "Set-Content $pgHba $orig; " ^
    "Restart-Service *postgres*; " ^
    "Write-Host '`n[+] SUCCESS: PostgreSQL password successfully reset to ''postgres''!' -ForegroundColor Green; " ^
    "Write-Host '[+] jgf_app user and jgf_db database created and ready!' -ForegroundColor Green;"

echo.
pause

