<#
.SYNOPSIS
    Builds and packages JGF Jewel Gold Finance installers for Server (PC1) and Client (PC2).
.DESCRIPTION
    1. Validates build prerequisites (.NET 8 SDK, Inno Setup 6).
    2. Runs full automated test suite to ensure zero regressions.
    3. Publishes self-contained Release binaries targeting win-x64.
    4. Compiles JGF-Server-Setup-<version>.exe.
    5. Compiles JGF-Client-Setup-<version>.exe.
    6. Verifies outputs and outputs file sizes and SHA256 hashes.
#>

param(
    [string]$Configuration = "Release",
    [string]$Version = "",
    [switch]$SkipTests = $false
)

$ErrorActionPreference = "Stop"
$rootDir = $PSScriptRoot

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   JGF JEWEL GOLD FINANCE - PRODUCTION BUILD & PACKAGING   " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Determine Version
if ([string]::IsNullOrWhiteSpace($Version)) {
    $propsPath = Join-Path $rootDir "Directory.Build.props"
    if (Test-Path $propsPath) {
        $xml = [xml](Get-Content $propsPath)
        $Version = $xml.Project.PropertyGroup.Version
    }
    if ([string]::IsNullOrWhiteSpace($Version)) {
        $Version = "1.2.0"
    }
}
Write-Host "[+] Target Release Version: $Version" -ForegroundColor Green

# 2. Locate dotnet.exe
$dotnetPath = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnetPath -or -not (Test-Path $dotnetPath)) {
    $dotnetPath = "C:\Program Files\dotnet\dotnet.exe"
}
if (-not (Test-Path $dotnetPath)) {
    Write-Error ".NET SDK (dotnet.exe) could not be found. Please install .NET 8 SDK."
    exit 1
}
Write-Host "[+] Using .NET SDK: $dotnetPath" -ForegroundColor Green

# 3. Locate Inno Setup Compiler (ISCC.exe)
$isccCandidates = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)
$isccFromPath = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
if ($isccFromPath) { $isccCandidates += $isccFromPath }

$isccPath = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $isccPath) {
    Write-Error "Inno Setup Compiler (ISCC.exe) not found!`nTo install Inno Setup, run:`n  winget install JRSoftware.InnoSetup`nor download from https://jrsoftware.org/isdl.php"
    exit 1
}
Write-Host "[+] Using Inno Setup Compiler: $isccPath" -ForegroundColor Green

# 4. Run Test Suite
if (-not $SkipTests) {
    Write-Host "`n[*] Running automated test suite..." -ForegroundColor Yellow
    $testProj = Join-Path $rootDir "tests\JGF.Tests\JGF.Tests.csproj"
    & $dotnetPath test $testProj -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Test suite failed with exit code $LASTEXITCODE. Aborting build."
        exit $LASTEXITCODE
    }
    Write-Host "[+] All automated tests passed successfully!" -ForegroundColor Green
} else {
    Write-Host "`n[!] Skipping test suite (-SkipTests specified)." -ForegroundColor Yellow
}

# 5. Publish Self-Contained win-x64 Release Binaries
Write-Host "`n[*] Publishing self-contained win-x64 release output..." -ForegroundColor Yellow
$desktopProj = Join-Path $rootDir "src\JGF.Desktop\JGF.Desktop.csproj"
& $dotnetPath publish $desktopProj -c $Configuration /p:PublishProfile=win-x64 --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}
Write-Host "[+] Application binaries successfully published." -ForegroundColor Green

# 6. Ensure Releases directory exists
$releasesDir = Join-Path $rootDir "Releases"
if (-not (Test-Path $releasesDir)) {
    New-Item -ItemType Directory -Path $releasesDir -Force | Out-Null
}

$issFile = Join-Path $rootDir "packaging\installer.iss"

# 7. Build Server Setup (PC1)
Write-Host "`n[*] Compiling JGF Server Installer (PC1)..." -ForegroundColor Yellow
& $isccPath "/DMODE=Server" "/DAPP_VERSION=$Version" $issFile
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to compile Server installer."
    exit $LASTEXITCODE
}

# 8. Build Client Setup (PC2)
Write-Host "`n[*] Compiling JGF Client Installer (PC2)..." -ForegroundColor Yellow
& $isccPath "/DMODE=Client" "/DAPP_VERSION=$Version" $issFile
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to compile Client installer."
    exit $LASTEXITCODE
}

# 9. Verify and Output Summary
Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host "                BUILD ARTIFACTS SUMMARY                   " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$serverInstaller = Join-Path $releasesDir "JGF-Server-Setup-$Version.exe"
$clientInstaller = Join-Path $releasesDir "JGF-Client-Setup-$Version.exe"

$artifacts = @($serverInstaller, $clientInstaller) | Where-Object { Test-Path $_ } | ForEach-Object {
    $item = Get-Item $_
    $hash = (Get-FileHash -Path $_ -Algorithm SHA256).Hash
    [PSCustomObject]@{
        "Installer Package" = $item.Name
        "Size (MB)"         = [Math]::Round($item.Length / 1MB, 2)
        "Target Role"       = if ($item.Name -like "*Server*") { "PC1 (Server + Counter 1)" } else { "PC2 (Counter 2 Client)" }
        "SHA256 Checksum"   = $hash
    }
}

$artifacts | Format-Table -AutoSize
Write-Host "Output Directory: $releasesDir" -ForegroundColor Green
Write-Host "`nAll installers successfully built and ready for deployment!" -ForegroundColor Cyan
