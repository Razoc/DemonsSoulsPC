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
    [string]$Out = 'F:\DemonsSoulsPC\Release',
    # Génération d'images (0.2.2) : le runtime Streamline de NVIDIA (SDK 2.14.16, bin\x64) et le runtime FSR d'AMD
    # (FidelityFX SDK 1.1.4, PrebuiltSignedDLL\amd_fidelityfx_vk.dll), signés par leurs éditeurs (le moteur vérifie la
    # signature) ; leurs licences permettent de les livrer avec une application.
    [string]$Streamline = 'F:\AkiBot\sdk\streamline-2.14.16',
    [string]$FidelityFx = 'F:\AkiBot\sdk\fidelityfx-1.1.4'
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

# 1 bis. Génération d'images : Streamline dans Build\Engine\streamline (où le moteur le cherche de lui-même) pour
# DLSS Frame Generation (DLSS-G + Reflex) et DLSS super résolution (sl.dlss + nvngx_dlss, 0.2.2), et amd_fidelityfx_vk.dll à côté
# du moteur pour FSR 3.
$sl = Join-Path $dest 'streamline'
New-Item -ItemType Directory -Force $sl | Out-Null
foreach ($f in 'sl.interposer.dll', 'sl.common.dll', 'sl.dlss_g.dll', 'nvngx_dlssg.dll', 'sl.reflex.dll', 'sl.pcl.dll',
    'NvLowLatencyVk.dll', 'sl.dlss.dll', 'nvngx_dlss.dll', 'nvngx_dlss.license.txt', 'reflex.license.txt') {
    Copy-Item (Join-Path $Streamline "bin\x64\$f") $sl
}
Copy-Item (Join-Path $Streamline 'license.txt') (Join-Path $sl 'Streamline-license.txt')
Copy-Item (Join-Path $FidelityFx 'PrebuiltSignedDLL\amd_fidelityfx_vk.dll') $dest
Copy-Item (Join-Path $FidelityFx 'sdk\LICENSE.txt') (Join-Path $dest 'amd_fidelityfx-LICENSE.txt')

# 2. Source du moteur (GPL-2.0 : le code source correspondant est publié par son auteur).
@"
Moteur de Demon's Souls PC
==========================
KytyPS5, fork optimisé pour Demon's Souls par chenxiao07 : https://github.com/chenxiao07/KytyPS5
Version : release $EngineTag, commit $EngineCommit
Code source correspondant : https://github.com/chenxiao07/KytyPS5/tree/$EngineCommit
Licence : GNU GPL v2 (LICENSE), composants tiers dans LICENSES.
Le lanceur (DemonsSoulsPC.exe) démarre le moteur avec les réglages choisis. Deux modifications, faites par le lanceur
sur le PC du joueur (rien de modifié n'est distribué) :
- le mode « économie de mémoire » du moteur est coupé (28 octets à l'offset 0x00f2906c de kyty_emulator.exe :
  « usable = min(total, available + own) » devient « usable = max(total, 16 Gio + 1 Mio) », voir
  DemonsSoulsPC.App\Services\MemoryCheck.cs dans https://github.com/Razoc/DemonsSoulsPC) ;
- le titre de la fenêtre devient « Demon's Souls PC » : les chaînes de format de WindowContext::UpdateTitle
  (src/graphics/presentation/window/window.cpp) sont réécrites à longueur égale, voir
  DemonsSoulsPC.App\Services\EngineTitle.cs ; DSPC_TITLE_FPS=1 remet le titre d'origine.
Génération d'images : streamline\ = runtime NVIDIA Streamline 2.14.16 (licences dans ce dossier) ;
amd_fidelityfx_vk.dll = AMD FidelityFX SDK 1.1.4 (MIT, amd_fidelityfx-LICENSE.txt).
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
