# Kopiuje projekt AA do stalego folderu i publikuje go na GitHubie.
# Wymaga: git oraz gh (GitHub CLI) zalogowany (gh auth login).
#
# Przyklad:
#   powershell -ExecutionPolicy Bypass -File .\publish-github.ps1
#   powershell -ExecutionPolicy Bypass -File .\publish-github.ps1 -Dest "D:\projekty\always-awake" -Visibility private

param(
    [string]$Dest = "$env:USERPROFILE\source\always-awake",
    [string]$RepoName = "always-awake",
    [ValidateSet('public','private')]
    [string]$Visibility = "public"
)

$ErrorActionPreference = 'Stop'
$src = $PSScriptRoot

# --- sprawdzenie narzedzi ---
foreach ($tool in 'git','gh') {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        Write-Host "Brak narzedzia: $tool. Zainstaluj je i sprobuj ponownie." -ForegroundColor Red
        exit 1
    }
}

# --- kopiowanie plikow projektu do stalego folderu ---
New-Item -ItemType Directory -Force -Path $Dest | Out-Null
$files = 'AA.cs','build.ps1','README.md','LICENSE','.gitignore'
foreach ($f in $files) {
    $p = Join-Path $src $f
    if (Test-Path $p) { Copy-Item $p -Destination $Dest -Force }
    else { Write-Host "Uwaga: brak $f w $src" -ForegroundColor Yellow }
}

Set-Location $Dest
Write-Host "Projekt w: $Dest"

# --- git ---
if (-not (Test-Path (Join-Path $Dest '.git'))) {
    git init -b main | Out-Null
}
git add .
git commit -m "AA - Always Awake: keep-awake tray utility" -m "Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Nic do zacommitowania albo blad commita." -ForegroundColor Yellow
}

# --- GitHub: utworz repo i wypchnij ---
$visFlag = "--$Visibility"
gh repo create $RepoName $visFlag --source . --remote origin --push

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Opublikowano repo '$RepoName' ($Visibility)." -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "Nie udalo sie utworzyc/wypchnac repo. Sprawdz 'gh auth status'." -ForegroundColor Red
}
