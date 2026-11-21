# Fabrique le paquet runtime de Demon's Souls PC (ce que le lanceur télécharge à l'installation) à partir d'une
# release Windows du moteur KytyPS5 optimisé pour Demon's Souls (fork chenxiao07/KytyPS5, licence GPL-2.0) :
#   Build\Engine\ = kyty_emulator.exe, kyty_shader_precompile.exe, leurs DLL, launch.json, run-windows.ps1 et
#   precompile-windows.ps1 (appelés en interne et sans fenêtre par DemonsSoulsPC.exe), tools\local\static-precompile,
#   licences. Retirés : le lanceur du fork (KytyLauncher.exe, launcher.ps1) et tous les .cmd.
# Aucun fichier du jeu : le moteur lit le dump de l'utilisateur sur place.
# Usage : powershell -NoProfile -ExecutionPolicy Bypass -File Tools\release\make_runtime.ps1 -Version 0.2.0
param(
    [string]$Version = '0.2.0',
    [string]$Engine = 'F:\DemonsSoulsPC\KytyDS\v20261121\KytyPS5-v20261121.zip',
    [string]$EngineTag = 'v20261121',
    [string]$EngineCommit = '65433d2e',
    [string]$Out = 'F:\DemonsSoulsPC\Release'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$name = "DemonsSoulsPC-runtime-$Version"
$stage = [IO.Path]::GetFullPath((Join-Path $Out $name))
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
$dest = Join-Path $stage 'Build\Engine'
New-Item -ItemType Directory -Force $dest | Out-Null

# 1. La release du moteur (un dossier racine KytyPS5-<tag>\ dans le zip), sans son lanceur ni ses .cmd.
$zip = [IO.Compression.ZipFile]::OpenRead($Engine)
try {
    foreach ($e in $zip.Entries) {
        if (-not $e.Name) { continue }
        $path = ($e.FullName -replace '/', '\') -replace '^KytyPS5-[^\\]+\\', ''
        if ($path -in 'KytyLauncher.exe', 'launcher.ps1', 'README.md') { continue }
        if ($path -match '\.(cmd|bat|sh)$') { continue }
        $file = Join-Path $dest $path
        New-Item -ItemType Directory -Force (Split-Path $file -Parent) | Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($e, $file, $true)
    }
} finally { $zip.Dispose() }
foreach ($f in 'kyty_emulator.exe', 'kyty_shader_precompile.exe', 'launch.json', 'run-windows.ps1', 'precompile-windows.ps1',
    'tools\local\static-precompile\pass-states.json', 'LICENSE') {
    if (-not (Test-Path (Join-Path $dest $f))) { throw "$f manquant dans la release du moteur" }
}

# 2. Source du moteur (GPL-2.0 : le code source correspondant est publié par son auteur).
@"
Moteur de Demon's Souls PC
==========================
KytyPS5, fork optimisé pour Demon's Souls par chenxiao07 : https://github.com/chenxiao07/KytyPS5
Version : release $EngineTag, commit $EngineCommit
Code source correspondant : https://github.com/chenxiao07/KytyPS5/tree/$EngineCommit
Licence : GNU GPL v2 (LICENSE), composants tiers dans LICENSES.
Demon's Souls PC ne modifie pas ces fichiers : son lanceur (DemonsSoulsPC.exe) les démarre avec les réglages choisis.
"@ | Set-Content -Encoding UTF8 (Join-Path $dest 'SOURCE.txt')
"$EngineTag $EngineCommit" | Set-Content -Encoding ASCII (Join-Path $dest 'engine-version.txt')

@"
Demon's Souls PC runtime $Version
=================================
Moteur KytyPS5 optimisé pour Demon's Souls ($EngineTag, voir Build\Engine\SOURCE.txt), installé par le lanceur
Demon's Souls PC. Aucun fichier du jeu : il faut son propre dump PS5 de Demon's Souls (PPSA01341, v1.07 ou v1.05).
Lancer le jeu : DemonsSoulsPC.exe (aucun script à ouvrir).
"@ | Set-Content -Encoding UTF8 (Join-Path $stage 'README.txt')

# 3. Contrôles : ni .bat/.cmd, ni fichier du jeu.
$bad = Get-ChildItem $stage -Recurse -File | Where-Object { $_.Extension -in '.bat', '.cmd', '.sh' }
if ($bad) { $bad.FullName; throw 'scripts .bat/.cmd/.sh trouvés dans le paquet' }
$game = Get-ChildItem $stage -Recurse -File | Where-Object { $_.Name -match '\.elf$|^eboot|param\.(json|sfo)$|renderconfig|\.prx$' }
if ($game) { $game.FullName; throw 'fichiers du jeu trouvés' }

$zipOut = Join-Path $Out "$name.zip"
if (Test-Path $zipOut) { Remove-Item $zipOut }
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $zipOut, [IO.Compression.CompressionLevel]::Optimal, $false)
$hash = (Get-FileHash $zipOut -Algorithm SHA256).Hash.ToLowerInvariant()
"{0} : {1:N0} fichiers, {2} octets, SHA-256 {3}" -f $zipOut, (Get-ChildItem $stage -Recurse -File).Count, (Get-Item $zipOut).Length, $hash
