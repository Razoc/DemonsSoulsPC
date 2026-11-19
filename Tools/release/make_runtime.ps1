# Fabrique le paquet runtime de Demon's Souls PC (ce que le lanceur télécharge à l'installation) à partir du
# paquet runtime d'AkiBot 0.3.0 (mêmes bibliothèques PS5, relinker, polices, fichiers NVIDIA/AMD), en remplaçant
# ce que Demon's Souls PC compile lui-même (branches demonssouls) :
#   - kyty_gpu.dll (Kyty, pont de 302 fonctions : Agc, AgcDriver, VideoOut, mémoire, clavier virtuel) ;
#   - les relais libSceAgc / libSceAgcDriver / libSceVideoOut / libSceImeDialog vers kyty_gpu.dll (make_shims.py) ;
#   - libkernel.prx d'AnyPS5 (mémoire directe relayée vers Kyty).
# Aucun script de lancement, aucun fichier du jeu.
# Usage : powershell -NoProfile -ExecutionPolicy Bypass -File Tools\release\make_runtime.ps1 -Version 0.1.0
param(
    [string]$Version = '0.1.0',
    [string]$Base = 'F:\AkiBot\Release\AkiBot-runtime-0.3.0.zip',
    [string]$KytyDll = 'F:\DemonsSoulsPC\kyty-gpu\kyty_gpu.dll',
    [string]$KytyNids = 'F:\DemonsSoulsPC\kyty\src\hybrid\kytyGpuNids.inc',
    [string]$LibKernel = 'F:\DemonsSoulsPC\anyps5-build\core\libs\libs\libkernel.prx',
    [string]$MakeShims = 'F:\DemonsSoulsPC\outils\make_shims.py',
    [string]$Python = 'python',
    [string]$Llvm = 'C:\Program Files\LLVM\bin',
    [string]$Out = 'F:\DemonsSoulsPC\Release'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$name = "DemonsSoulsPC-runtime-$Version"
$stage = [IO.Path]::GetFullPath((Join-Path $Out $name))
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force $stage | Out-Null

# 1. Le paquet d'AkiBot, sans son README ni les fichiers refaits plus bas.
$zip = [IO.Compression.ZipFile]::OpenRead($Base)
try {
    foreach ($e in $zip.Entries) {
        if (-not $e.Name) { continue }  # dossier
        $path = $e.FullName -replace '/', '\'
        if ($path -eq 'README.txt') { continue }
        if ($path -eq 'Build\RunHybrid\kyty_gpu.dll') { continue }
        if ($path -match '^Build\\RunHybrid\\libs\\(libSceAgc|libSceAgcDriver|libSceVideoOut|libSceImeDialog|libkernel)\.prx$') { continue }
        $dest = Join-Path $stage $path
        New-Item -ItemType Directory -Force (Split-Path $dest -Parent) | Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($e, $dest, $true)
    }
} finally { $zip.Dispose() }

# 2. Ce que Demon's Souls PC compile.
$run = Join-Path $stage 'Build\RunHybrid'
$libs = Join-Path $run 'libs'
Copy-Item $KytyDll $run
Copy-Item $LibKernel $libs
& $Python -I $MakeShims $KytyNids $libs $Llvm
if ($LASTEXITCODE -ne 0) { throw 'make_shims.py a échoué' }
foreach ($f in 'libSceAgc.prx', 'libSceAgcDriver.prx', 'libSceVideoOut.prx', 'libSceImeDialog.prx', 'libkernel.prx') {
    if (-not (Test-Path (Join-Path $libs $f))) { throw "$f manquant" }
}

@"
Demon's Souls PC runtime $Version
=================================
Fichiers que le lanceur Demon's Souls PC installe à côté du jeu : moteur graphique (kyty_gpu.dll), bibliothèques
système (Build\RunHybrid\libs), module hôte (akibot_host.dll), convertisseur (tools\relinker.exe), fichiers NVIDIA
et AMD, polices Noto. Aucun fichier du jeu : il faut son propre dump PS5 de Demon's Souls.
Lancer le jeu : DemonsSoulsPC.exe (aucun script). Licences : dossier licenses.
"@ | Set-Content -Encoding UTF8 (Join-Path $stage 'README.txt')

# 3. Contrôles : ni script, ni fichier du jeu.
$bad = Get-ChildItem $stage -Recurse -File | Where-Object { $_.Extension -in '.bat', '.cmd', '.sh' }
if ($bad) { $bad.FullName; throw 'scripts trouvés dans le paquet' }
$game = Get-ChildItem $stage -Recurse -File | Where-Object { $_.Name -match '^DemonsSouls.*\.exe$|^AstroBot.*\.exe$|\.elf$|eboot|param\.(json|sfo)$|renderconfig' }
if ($game) { $game.FullName; throw 'fichiers du jeu trouvés' }

$zipOut = Join-Path $Out "$name.zip"
if (Test-Path $zipOut) { Remove-Item $zipOut }
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $zipOut, [IO.Compression.CompressionLevel]::Optimal, $false)
$hash = (Get-FileHash $zipOut -Algorithm SHA256).Hash.ToLowerInvariant()
"{0} : {1:N0} fichiers, {2} octets, SHA-256 {3}" -f $zipOut, (Get-ChildItem $stage -Recurse -File).Count, (Get-Item $zipOut).Length, $hash
