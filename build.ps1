# Kompiluje AA.cs -> AA.exe w tym samym folderze, w ktorym lezy ten skrypt.
# Dziala niezaleznie od biezacego katalogu (uzywa $PSScriptRoot).

$ErrorActionPreference = 'Stop'
$dir = $PSScriptRoot

# Znajdz csc.exe (.NET Framework 4): najpierw 64-bit, potem 32-bit.
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) {
    Write-Host "Nie znaleziono csc.exe (.NET Framework 4)." -ForegroundColor Red
    exit 1
}

$src = Join-Path $dir 'AA.cs'
$out = Join-Path $dir 'AA.exe'

if (-not (Test-Path $src)) {
    Write-Host "Nie znaleziono pliku zrodlowego: $src" -ForegroundColor Red
    exit 1
}

Write-Host "csc : $csc"
Write-Host "src : $src"
Write-Host "out : $out"
Write-Host ""

& $csc /nologo /target:winexe /out:"$out" `
    /reference:System.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    "$src"

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "OK - zbudowano: $out" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "Blad kompilacji (kod $LASTEXITCODE)" -ForegroundColor Red
}
