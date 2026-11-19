# Demon's Souls PC

Portage PC natif de **Demon's Souls** (remake PS5, Bluepoint 2020), à partir de **ta propre copie** du jeu.
Même méthode qu'[AkiBot](https://github.com/Razoc/AkiBot) : le code du jeu est converti en programme Windows
(relinker d'AnyPS5) et son rendu passe par le moteur graphique Kyty.

> Aucun fichier du jeu n'est fourni. Il te faut un dump PS5 déchiffré de Demon's Souls que tu as obtenu légalement.

## État (version 0.1.0)

- **Jouable** : logos, intro, écran titre, mode hors ligne, création de personnage (nom au clavier virtuel),
  cinématique d'ouverture, tutoriel en jeu (déplacements, combat, caméra).
- **Performances** : ~57-60 FPS dans les menus et la création de personnage, **16-20 FPS en jeu** pour l'instant
  (testé sur Ryzen 7 9800X3D + RTX 5070). L'optimisation est la prochaine étape.
- Version testée : **PPSA01341 (Europe) 01.007.000**. Les autres régions du même jeu sont acceptées, sans garantie.
- Pas encore : DLSS / FSR / génération d'images (masqués dans le lanceur), mode en ligne, contenus DLC.

## Jouer

### Avec le lanceur seul

1. Télécharge `DemonsSoulsPC.exe` depuis la dernière release et lance-le.
2. Choisis le dossier d'installation (par défaut `C:\DemonsSoulsPC`, hors OneDrive) : le lanceur télécharge son
   runtime (~200 Mo, vérifié par SHA-256).
3. Indique le dossier de ton dump (celui qui contient `eboot.bin`, `sce_module` et `sce_sys`). Il n'est jamais
   modifié : le lanceur crée des liens vers lui.
4. Règle l'affichage et la manette, puis **Jouer**.

Windows SmartScreen peut avertir au premier lancement (exe non signé) : « Informations complémentaires » puis
« Exécuter quand même ».

### Avec RecompLauncher

Le dépôt contient un `recomplauncher.json` : RecompLauncher installe le zip `DemonsSoulsPC-v<version>-Windows-x64.zip`,
lance `DemonsSoulsPC.exe --prepare "<dump>"` (runtime + conversion, lignes `progress`), puis
`DemonsSoulsPC.exe --play`. Les réglages sont dans `Build\dspc-settings.json`, les sauvegardes dans
`Build\RunHybrid\_sd`.

## Réglages

| Réglage | Effet |
|---|---|
| Qualité de rendu | **Performance** (réglages du mode Performance PS5, sans rendu 4K) ou **Qualité** (réglages d'origine, 4K, bien plus lourd) |
| Limite d'images/s | 60 par défaut (le jeu vise 60 en mode Performance) |
| Résolution, plein écran, VSync, compteur de FPS | comme d'habitude |
| Manette | DualSense, Xbox, Switch… vibrations réglables |

## Lignes de commande

| Commande | Rôle |
|---|---|
| `DemonsSoulsPC.exe --prepare "<dump>"` | installe ou met à jour le runtime à côté de l'exe, puis convertit le jeu (sans fenêtre) |
| `DemonsSoulsPC.exe --play` | lance le jeu avec les réglages enregistrés (sans fenêtre) |
| `DemonsSoulsPC.exe --run [args]` | lance le jeu avec les seuls réglages par défaut et l'environnement de l'appelant |
| `DemonsSoulsPC.exe --remove-game` | supprime ce que la conversion a créé (le dump n'est pas touché) |

Journaux : dossier `Logs` (`setup.log`, `dspc-launch.log` avec toutes les variables passées au jeu,
`hybrid-run.out.log` / `.err.log`).

## Construire

- Lanceur : `MSBuild DemonsSoulsPC.App\DemonsSoulsPC.App.csproj -restore -p:Configuration=Release`
  (.NET Framework 4.8, un seul exe : les DLL sont embarquées).
- Runtime : `Tools\release\make_runtime.ps1 -Version 0.1.0` (runtime d'AkiBot 0.3.0 + `kyty_gpu.dll` et
  `libkernel.prx` des branches `demonssouls` de Kyty et AnyPS5, relais régénérés).
- Zip RecompLauncher : `Tools\release\make_launcher_zip.ps1 -Version 0.1.0`.
- Icône (originale, aucune image du jeu) : `Tools\release\make_icon.ps1`.

## Crédits

- **AnyPS5** (boykopovar) : relinker (code x86-64 du jeu → exécutable Windows) et bibliothèques système PS5.
- **[KytyPS5](https://github.com/Jetsku/KytyPS5) / Kyty** : le moteur graphique traduit vers Vulkan (`kyty_gpu.dll`).
- **[AkiBot](https://github.com/Razoc/AkiBot)** et son amont **[pcportastrobot](https://github.com/imnotsharingmyname/pcportastrobot)** :
  le lanceur et le pont Kyty dont ce projet est dérivé.
- AMD FidelityFX, NVIDIA Streamline / DLSS / Reflex : voir le dossier `licenses` du runtime.
- Demon's Souls appartient à Sony Interactive Entertainment. Projet non officiel, sans lien avec Sony ni Bluepoint.
