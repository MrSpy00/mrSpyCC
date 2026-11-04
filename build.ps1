# ==============================================================================
# mrSpyCC - aegisSoft Build and Package Script
# Portable Single-File Executable Pipeline for Windows x64
# ==============================================================================

$ErrorActionPreference = "Stop"
$ScriptDir = $PSScriptRoot
$ProjectFile = Join-Path $ScriptDir "src\mrSpyCC\mrSpyCC.csproj"
$OutputDir = Join-Path $ScriptDir "build\dist"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " mrSpyCC - Advanced Control Center and ITE8291 RGB Engine" -ForegroundColor Green
Write-Host " aegisSoft | Developer: mrSpy" -ForegroundColor DarkGray
Write-Host "==========================================================" -ForegroundColor Cyan

Write-Host "`n[*] Proje derleniyor ve paketleniyor..." -ForegroundColor Yellow

if (Test-Path $OutputDir) {
    Get-ChildItem -Path $OutputDir -Recurse | Remove-Item -Force -Recurse -ErrorAction SilentlyContinue
}

dotnet publish $ProjectFile `
    -c Release `
    -r win-x64 `
    --self-contained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $OutputDir

if ($LASTEXITCODE -eq 0) {
    # Ensure resources folder with app.ico and logo.png exists next to portable exe
    $distResources = Join-Path $OutputDir "resources"
    if (-not (Test-Path $distResources)) {
        New-Item -ItemType Directory -Path $distResources -Force | Out-Null
    }
    Copy-Item (Join-Path $ScriptDir "src\mrSpyCC\resources\app.ico") $distResources -Force
    Copy-Item (Join-Path $ScriptDir "src\mrSpyCC\resources\logo.png") $distResources -Force

    $exePath = Join-Path $OutputDir "mrSpyCC.exe"
    if (Test-Path $exePath) {
        $sizeMb = [math]::Round((Get-Item $exePath).Length / 1MB, 2)
        Write-Host "`n[+] Derleme ve paketleme BASARILI!" -ForegroundColor Green
        Write-Host "    Konum : $exePath" -ForegroundColor White
        Write-Host "    Boyut : $sizeMb MB (Kurulumsuz, Tek Bagimsiz Executable)" -ForegroundColor White
    }
} else {
    Write-Host "`n[-] Derleme HATASI olustu! (Cikis Kodu: $LASTEXITCODE)" -ForegroundColor Red
}
