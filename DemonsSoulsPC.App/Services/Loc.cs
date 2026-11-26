using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Markup;

namespace DemonsSoulsPC.App.Services
{
    // Demon’s Souls PC translations (French and English). The language is chosen once at startup: from the user's choice
    // saved in %LOCALAPPDATA%\Demon’s Souls PC\akibot.json, else from the Windows display language. Changing it takes
    // effect on the next start.
    //   Code: Loc.T("key") or Loc.T("key", arg0, arg1)   XAML: Text="{svc:Tr key}"
    public static class Loc
    {
        public const string French = "fr";
        public const string English = "en";

        public static string Language { get; private set; } = English;

        public static void Init(string? language)
        {
            Language = language == French || language == English ? language! : DetectWindowsLanguage();
        }

        public static string DetectWindowsLanguage() =>
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr" ? French : English;

        public static string T(string key, params object[] args)
        {
            string text;
            if (Strings.TryGetValue(key, out var pair))
                text = Language == French ? pair.Fr : pair.En;
            else if (Language == French && (LocTables.French.TryGetValue(key, out var fr) || LocTables.CodeFrench.TryGetValue(key, out fr)))
                text = fr; // original English interface text used as the key
            else if (Language != French && (LocTables.EnglishOverrides.TryGetValue(key, out var en) || LocTables.CodeEnglishOverrides.TryGetValue(key, out en)))
                text = en;
            else
                text = key;
            return args.Length == 0 ? text : string.Format(text, args);
        }

        private static readonly Dictionary<string, (string Fr, string En)> Strings = new Dictionary<string, (string, string)>
        {
            // Common
            ["App.Name"] = ("Demon’s Souls PC", "Demon’s Souls PC"),
            ["App.Tagline"] = ("Demon’s Souls en natif sur PC", "Demon’s Souls running natively on PC"),
            // Demon’s Souls PC
            ["UI.GroupRender"] = ("RENDU", "RENDERING"),
            ["UI.RenderQualityLabel"] = ("Qualité de rendu", "Rendering quality"),
            ["UI.RenderPerf"] = ("Standard (comme sur PS5, conseillé)", "Standard (as on PS5, recommended)"),
            ["UI.RenderQuality"] = ("Bas (plus rapide)", "Low (faster)"),
            ["UI.RenderLowest"] = ("Très bas (le plus rapide)", "Lowest (fastest)"),
            ["UI.RenderPerfTip"] = ("Le jeu tel que sur PS5.",
                "The game as on the PS5."),
            ["UI.RenderQualityTip"] = ("Ombres et détails lointains plus courts, textures plus petites : pour un processeur ou une carte graphique modestes.",
                "Shorter shadows and distant detail, smaller textures: for a modest processor or graphics card."),
            ["UI.Fps60Recommended"] = ("60 FPS (conseillé)", "60 FPS (recommended)"),
            ["UI.PrecompileButton"] = ("Précompiler les shaders", "Precompile the shaders"),
            ["Prep.Title"] = ("Préparation des shaders", "Preparing the shaders"),
            ["Prep.Heading"] = ("Préparation des shaders", "Preparing the shaders"),
            ["Prep.Intro"] = ("Avant la première partie, tous les shaders et pipelines du jeu (dans tous les mondes) sont compilés pour ta carte graphique : plus aucun gel ni chute de FPS la première fois qu'une zone ou un effet apparaît. C'est obligatoire et à faire une fois (et après une mise à jour du pilote graphique) : de 30 min à plus d'une heure selon le processeur, qui tourne à fond pendant ce temps. Le jeu se lance tout seul à la fin.",
                "Before the first game, every shader and pipeline of the game (in every world) is compiled for your graphics card: no more freezes or frame drops the first time an area or an effect appears. It is required and done once (and again after a graphics driver update): from 30 minutes to over an hour depending on the processor, which is fully loaded meanwhile. The game starts by itself at the end."),
            ["Prep.RebuildIntro"] = ("Tous les shaders et pipelines du jeu (dans tous les mondes) sont compilés pour ta carte graphique : plus aucun gel ni chute de FPS la première fois qu'une zone ou un effet apparaît. Ce qui est déjà fait est gardé, seul le reste est compilé : de quelques minutes à plus d'une heure selon le processeur, qui tourne à fond pendant ce temps. La fenêtre se ferme toute seule à la fin.",
                "Every shader and pipeline of the game (in every world) is compiled for your graphics card: no more freezes or frame drops the first time an area or an effect appears. What is already done is kept, only the rest is compiled: from a few minutes to over an hour depending on the processor, which is fully loaded meanwhile. The window closes by itself at the end."),
            ["Prep.AlreadyRunning"] = ("Une préparation des shaders tourne déjà pour ce jeu (dans une autre fenêtre de Demon’s Souls PC). Laisse-la finir : celle-ci se ferme.",
                "A shader preparation is already running for this game (in another Demon’s Souls PC window). Let it finish: this one closes."),
            ["Prep.DriverHeading"] = ("Nouveau pilote graphique détecté", "New graphics driver detected"),
            ["Prep.DriverIntro"] = ("Ton pilote graphique est passé de la version {0} à la {1}. Les shaders compilés par l'ancien pilote ne servent plus au nouveau (c'est pareil dans tous les jeux PC) : il faut les recompiler une fois, sinon le jeu gèlerait à chaque nouvelle zone ou effet. Comme la première fois, cela prend de 30 min à plus d'une heure selon le processeur, qui tourne à fond. Le jeu se lance tout seul à la fin. Astuce : évite d'installer un nouveau pilote juste avant de vouloir jouer.",
                "Your graphics driver went from version {0} to {1}. The shaders the old driver compiled are no use to the new one (the same in every PC game): they must be compiled again once, or the game would freeze at each new area or effect. As the first time, it takes from 30 minutes to over an hour depending on the processor, which is fully loaded. The game starts by itself at the end. Tip: avoid installing a new driver just before you want to play."),
            ["Prep.Starting"] = ("Liste des shaders du jeu...", "Listing the game's shaders..."),
            ["Prep.CloseHint"] = ("Fermer cette fenêtre arrête la préparation sans lancer le jeu : ce qui est fait est gardé, la suite reprendra au prochain lancement.",
                "Closing this window stops the preparation without starting the game: what is done is kept, the rest continues at the next launch."),
            ["Prep.CommonReady"] = ("Shaders les plus courants prêts : compilation du reste...", "The most common shaders are ready: compiling the rest..."),
            ["Prep.Planning"] = ("Analyse des shaders du jeu (une à quelques minutes)...", "Looking through the game's shaders (a minute or a few)..."),
            ["Prep.Translating"] =("Traduction des shaders pour ton PC...", "Translating the shaders for your PC..."),
            ["Prep.TranslatingCount"] = ("Traduction des shaders pour ton PC : {0} sur {1}", "Translating the shaders for your PC: {0} of {1}"),
            ["Prep.Merging"] =("Assemblage du cache de shaders...", "Putting the shader cache together..."),
            ["Prep.LeftMinutes"] = ("Environ {0} min restantes", "About {0} min left"),
            ["Prep.LeftAlmost"] = ("Presque fini", "Almost done"),
            ["Prep.FailedRetry"] = ("Le jeu ne se lance qu'une fois la préparation finie. Réessayer maintenant ? (Elle reprend là où elle s'est arrêtée.)",
                "The game starts only once the preparation is finished. Try again now? (It resumes where it stopped.)"),
            ["Common.Browse"] = ("Parcourir…", "Browse…"),
            ["Common.Cancel"] = ("Annuler", "Cancel"),
            ["Common.Close"] = ("Fermer", "Close"),
            ["Common.Next"] = ("Suivant", "Next"),
            ["Common.Back"] = ("Retour", "Back"),
            ["Common.Error"] = ("Erreur", "Error"),
            ["Common.Yes"] = ("Oui", "Yes"),
            ["Common.No"] = ("Non", "No"),
            ["Common.UnexpectedError"] = ("Une erreur inattendue s'est produite :\n{0}", "An unexpected error occurred:\n{0}"),
            ["Common.StartupError"] = ("Erreur au démarrage :\n{0}", "Startup error:\n{0}"),

            // Main window (Demon’s Souls PC 0.2, 1280x720)
            ["UI.Home"] = ("Accueil", "Home"),
            ["UI.Graphics"] = ("Graphismes", "Graphics"),
            ["UI.Controller"] = ("Manette", "Controller"),
            ["UI.Advanced"] = ("Avancé", "Advanced"),
            ["UI.Installation"] = ("Installation", "Installation"),
            ["UI.Play"] = ("Jouer", "Play"),
            ["UI.Quit"] = ("Quitter", "Quit"),
            ["UI.LeaveFullScreen"] = ("Quitter le plein écran (F11)", "Leave full screen (F11)"),
            ["UI.FullScreen"] = ("Plein écran (F11)", "Full screen (F11)"),
            ["UI.Edit"] = ("Modifier", "Edit"),
            ["UI.UpdateAvailable"] = ("Une mise à jour de Demon’s Souls PC est disponible", "An Demon’s Souls PC update is available"),
            ["UI.UpdateNow"] = ("Mettre à jour", "Update now"),
            ["UI.Later"] = ("Plus tard", "Later"),
            ["UI.Downloading"] = ("Téléchargement…", "Downloading…"),
            ["UI.BestSettings"] = ("Réglages conseillés", "Recommended settings"),
            ["UI.GroupDisplay"] = ("AFFICHAGE", "DISPLAY"),
            ["UI.Fullscreen"] = ("Plein écran", "Fullscreen"),
            ["UI.Ultrawide"] = ("Ultra-large 21:9", "Ultrawide 21:9"),
            ["UI.UltrawideTip"] = ("Rendu en 21:9 (2560×1080 / 3440×1440). Le 16:9 reste inchangé.", "21:9 rendering (2560x1080 / 3440x1440). 16:9 stays unchanged."),
            ["UI.FpsLimit"] = ("Limite d'images/s", "Frame rate limit"),
            ["UI.FgTarget"] = ("Objectif à l'écran", "On-screen target"),
            ["UI.Reflex"] = ("NVIDIA Reflex", "NVIDIA Reflex"),
            ["UI.Overlay"] = ("Statistiques détaillées", "Detailed stats"),
            ["UI.OverlayTip"] = ("En jeu : F2 affiche/masque, Ctrl+F2 change la limite d'images/s, F11 ou Alt+Entrée bascule le plein écran.",
                                 "In game: F2 shows/hides, Ctrl+F2 changes the frame rate limit, F11 or Alt+Enter toggles fullscreen."),
            ["UI.FpsCounter"] = ("Compteur de FPS", "FPS counter"),
            ["UI.FpsCounterTip"] = ("Affiche les FPS en haut à droite du jeu. En jeu, F2 l'affiche ou le masque.",
                                    "Shows the FPS in the top-right corner of the game. In game, F2 shows or hides it."),
            ["UI.OverlayOff"] = ("Désactivé (F2 l'affiche)", "Off (F2 shows it)"),
            ["UI.OverlayFps"] = ("FPS seulement", "FPS only"),
            ["UI.OverlayCompact"] = ("FPS + processeur, carte graphique, mémoire", "FPS + CPU, GPU, VRAM, RAM"),
            ["UI.OverlayFull"] = ("Tout (toutes les statistiques)", "Everything (all stats)"),
            ["UI.UpscaleGame"] = ("Celui du jeu", "Game's own"),
            ["UI.DlssQuality"] = ("DLSS Qualité", "DLSS Quality"),
            ["UI.DlssBalanced"] = ("DLSS Équilibré", "DLSS Balanced"),
            ["UI.DlssPerformance"] = ("DLSS Performance", "DLSS Performance"),
            ["UI.DlssUltra"] = ("DLSS Ultra Performance", "DLSS Ultra Performance"),
            ["UI.Upscaling"] = ("Mise à l'échelle", "Upscaling"),
            ["UI.UpscaleGameNote"] = ("L'anti-crénelage et la mise à l'échelle du jeu, comme sur PS5.", "The game's own anti-aliasing and upscaling, as on PS5."),
            ["UI.UpscaleDlssNote"] = ("NVIDIA DLSS (cartes RTX) : la 3D est calculée à {0} de la hauteur de la fenêtre, puis DLSS en fait l'image finale, plus nette que celle du jeu. Plus le mode est bas, plus il y a de FPS. La génération d'images marche avec : elle s'arrête seulement pendant les écrans de chargement.", "NVIDIA DLSS (RTX cards): the 3D is rendered at {0} of the window's height, then DLSS makes the final image, sharper than the game's. The lower the mode, the more FPS. Frame generation works with it: it only stops during loading screens."),
            ["UI.UpscaleDlssNotHere"] = ("Ta carte n'a pas DLSS : AMD FSR 3 sera utilisé à la place.", "Your card has no DLSS: AMD FSR 3 is used instead."),
            ["UI.UpscaleFsrNote"] = ("AMD FSR 3 (toutes cartes) : la 3D est calculée plus bas, puis FSR en fait l'image à la taille de la fenêtre. Plus de FPS quand la carte graphique limite.", "AMD FSR 3 (any card): the 3D is rendered lower, then FSR makes the image at the window's size. More FPS when the graphics card is the limit."),
            ["UI.FsrFg"] = ("AMD FSR 3 ×2 (toutes cartes)", "AMD FSR 3 2x (any card)"),
            ["UI.FrameGen"] = ("Génération d'images", "Frame generation"),
            ["UI.DlssFg2"] = ("NVIDIA DLSS ×2 (RTX 40/50)", "NVIDIA DLSS 2x (RTX 40/50)"),
            ["UI.DlssFg3"] = ("NVIDIA DLSS ×3 (RTX 50)", "NVIDIA DLSS 3x (RTX 50)"),
            ["UI.DlssFg4"] = ("NVIDIA DLSS ×4 (RTX 50)", "NVIDIA DLSS 4x (RTX 50)"),
            ["UI.FgOffDs"] = ("Le jeu affiche ses propres images (60 par seconde).", "The game shows its own frames (60 per second)."),
            ["UI.FgDlssNote"] = ("NVIDIA DLSS : {0} images affichées par image du jeu, Reflex inclus. Un peu plus de latence ; les artefacts possibles touchent surtout l'interface. VSync indisponible : utilise G-Sync.", "NVIDIA DLSS: {0} frames shown per game frame, Reflex included. A little more latency; possible artifacts mostly on the HUD. No VSync: use G-Sync."),
            ["UI.PadHints"] = ("Ⓐ/✕ Choisir   Ⓑ/○ Retour   Ⓧ/□ Info   LB/RB Pages   Menu/Options Jouer   Ⓨ/△ Quitter   Vue/Create Plein écran", "Ⓐ/✕ Select   Ⓑ/○ Back   Ⓧ/□ Info   LB/RB Pages   Menu/Options Play   Ⓨ/△ Quit   View/Create Full screen"),
            ["UI.Goal"] = ("Objectif", "Goal"),
            ["Goal.Custom"] = ("Personnalisé", "Custom"),
            ["Goal.Smooth"] = ("Fluide (le plus de FPS)", "Smooth (most FPS)"),
            ["Goal.Balanced"] = ("Équilibré (60 FPS, plus belle image)", "Balanced (60 FPS, best picture)"),
            ["Goal.Quiet"] = ("Silencieux (60 FPS, carte au repos)", "Quiet (60 FPS, card at rest)"),
            ["Goal.CustomNote"] = ("Tu as changé un réglage à la main. Choisis un objectif pour que le lanceur règle tout pour ce PC.", "You changed a setting by hand. Pick a goal and the launcher sets everything for this PC."),
            ["Goal.Applied"] = ("Objectif « {0} » appliqué.", "Goal \"{0}\" applied."),
            ["Goal.CpuLimit"] = ("Graphismes comme sur PS5 : sur ce jeu c'est surtout le processeur qui fixe la limite (~70 FPS même sur un Ryzen 7 9800X3D), les baisser ne donnerait presque rien.", "Graphics as on PS5: in this game the processor mostly sets the limit (~70 FPS even on a Ryzen 7 9800X3D); lowering them would give almost nothing."),
            ["Goal.SmoothLow"] = ("Graphismes « bas » : ombres et détails lointains plus courts, ce qui soulage aussi ton processeur.", "\"Low\" graphics: shorter shadows and distant detail, which also helps your processor."),
            ["Goal.FgDlss"] = ("Génération d'images NVIDIA DLSS ×2 : jeu jusqu'à {0} FPS → {1} à l'écran ({2} Hz). Le ×2 seulement : elle marche le mieux à partir d'environ 60 FPS de base.", "NVIDIA DLSS frame generation 2x: game up to {0} FPS → {1} on screen ({2} Hz). 2x only: it works best from about 60 base FPS."),
            ["Goal.FgFsr"] = ("Génération d'images AMD FSR 3 ×2 : jeu jusqu'à {0} FPS → {1} à l'écran ({2} Hz).", "AMD FSR 3 frame generation 2x: game up to {0} FPS → {1} on screen ({2} Hz)."),
            ["Goal.FgNoUpscale"] = ("Mise à l'échelle du jeu : c'est surtout le processeur qui limite, DLSS n'ajouterait presque pas de FPS.", "The game's own upscaling: the processor is what holds the game back, DLSS would add almost no FPS."),
            ["Goal.Screen60"] = ("Ton écran ({0} Hz) n'affiche pas plus de 60 images : 60 FPS stables avec VSync.", "Your screen ({0} Hz) shows no more than 60 frames: a steady 60 FPS with VSync."),
            ["Goal.Reflex"] = ("Faible latence (Reflex) activée.", "Low latency (Reflex) on."),
            ["Goal.Steady60"] = ("60 FPS stables avec VSync, la cadence d'origine du jeu.", "A steady 60 FPS with VSync, the game's own rate."),
            ["Goal.Dlaa"] = ("NVIDIA DLAA : à 60 FPS ta carte a de la marge, donc l'image la plus nette (anti-crénelage DLSS en pleine résolution).", "NVIDIA DLAA: at 60 FPS your card has room, so the sharpest picture (DLSS anti-aliasing at full resolution)."),
            ["Goal.DlssQuality"] = ("NVIDIA DLSS Qualité : plus net que l'anti-crénelage du jeu, sans coûter de FPS.", "NVIDIA DLSS Quality: sharper than the game's anti-aliasing, without costing FPS."),
            ["Goal.GameAa"] = ("Anti-crénelage du jeu, comme sur PS5.", "The game's own anti-aliasing, as on PS5."),
            ["Goal.NoFgBalanced"] = ("Pas de génération d'images : aucune latence en plus, VSync possible.", "No frame generation: no added latency, VSync available."),
            ["Goal.QuietRest"] = ("60 FPS avec VSync : la carte se repose entre les images (moins de bruit et de chaleur).", "60 FPS with VSync: the card rests between frames (less noise and heat)."),
            ["Goal.QuietDlss"] = ("NVIDIA DLSS Qualité : la 3D calculée aux 2/3 de la hauteur, moins de travail pour la carte.", "NVIDIA DLSS Quality: the 3D at 2/3 of the height, less work for the card."),
            ["Goal.QuietLow"] = ("Graphismes « bas » : un cran en dessous, moins de travail pour la carte et le processeur.", "\"Low\" graphics: one step down, less work for the card and the processor."),
            ["UI.FgPace"] = ("Réglé sur ton écran : jeu jusqu'à {0} FPS → {1} FPS à l'écran ({2} Hz).", "Set for your screen: game up to {0} FPS → {1} FPS on screen ({2} Hz)."),
            ["UI.FgDlssNotHere"] = ("Ta carte ne le permet pas : le jeu tournera sans.", "Your card does not support it: the game runs without it."),
            ["UI.FgFsrNote"] = ("AMD FSR 3 : 2 images affichées par image du jeu, sur toutes les cartes récentes. Un peu plus de latence. VSync indisponible : utilise FreeSync/G-Sync.", "AMD FSR 3: 2 frames shown per game frame, on any recent card. A little more latency. No VSync: use FreeSync/G-Sync."),
            ["UI.GroupController"] = ("MANETTE", "CONTROLLER"),
            ["UI.PreferredController"] = ("Manette utilisée", "Controller"),
            ["UI.VibrationTip"] = ("Règle la vibration des manettes Xbox, Switch et autres. Une DualSense garde ses propres retours haptiques.",
                                   "Sets vibration for Xbox, Switch and other pads. A DualSense keeps its own haptics."),
            ["UI.SwitchBoost"] = ("Vibration renforcée (manettes type Switch)", "Stronger vibration (Switch-style pads)"),
            ["UI.Motion"] = ("Détecteurs de mouvement", "Motion sensors"),
            ["UI.MotionTip"] = ("Secouer et incliner la manette (DualSense, Switch Pro). Manette sans gyroscope : View/Back puis stick droit.",
                                "Shake and tilt the controller (DualSense, Switch Pro). Without a gyroscope: View/Back then right stick."),
            ["UI.Triggers"] = ("Gâchettes adaptatives", "Adaptive triggers"),
            ["UI.TriggersTip"] = ("Envoie l'état des gâchettes au jeu : sans ça, L2/R2 sont ignorées.", "Sends trigger state to the game: without it, L2/R2 are ignored."),
            ["UI.BtSpeaker"] = ("Haut-parleur en Bluetooth", "Speaker over Bluetooth"),
            ["UI.BtHaptics"] = ("Vibrations HD en Bluetooth", "HD haptics over Bluetooth"),
            ["UI.BluetoothTip"] = ("Pour une DualSense en Bluetooth. Branchée en USB, elle utilise déjà le son et les vibrations USB.",
                                   "For a DualSense over Bluetooth. Over USB it already uses USB audio and haptics."),
            ["UI.GroupConnected"] = ("MANETTES BRANCHÉES", "CONNECTED CONTROLLERS"),
            ["UI.NoGyroTip"] = ("Manette sans gyroscope : appuie sur View/Back, puis incline avec le stick droit.",
                                "Controller without a gyroscope: press View/Back, then tilt with the right stick."),
            ["UI.NoPad"] = ("Aucune manette détectée. Branche-la puis clique sur Actualiser.", "No controller detected. Plug it in, then click Refresh."),
            ["UI.GroupGame"] = ("JEU", "GAME"),
            ["UI.LightingTip"] = ("L'éclairage rapide (sans ray tracing) n'existe pas sur toutes les versions du jeu.",
                                  "Faster lighting (no ray tracing) is not available on every game version."),
            ["UI.SkipIntro"] = ("Passer les vidéos d'intro", "Skip intro videos"),
            ["UI.GroupPerf"] = ("PERFORMANCES", "PERFORMANCE"),
            ["UI.Priority"] = ("Priorité du jeu", "Game priority"),
            ["UI.CpPriority"] = ("Priorité du rendu", "Renderer priority"),
            ["UI.PriorityTip"] = ("« abovenormal » et 2 sont conseillés : les autres programmes laissent le cœur du rendu tranquille.",
                                  "\"abovenormal\" and 2 are recommended: other programs stay off the renderer's core."),
            ["UI.ShaderCache"] = ("Cache de shaders", "Shader cache"),
            ["UI.ShaderCacheTip"] = ("Garde les shaders compilés entre deux parties : supprime les saccades de compilation.",
                                     "Keeps compiled shaders between sessions: removes compile stutters."),
            ["UI.DrawPrep"] = ("Préparation du rendu prioritaire", "Prioritised draw preparation"),
            ["UI.DrawPrepTip"] = ("Gagne environ 1 ms par image quand d'autres programmes tournent.", "Saves about 1 ms per frame when other programs run."),
            ["UI.Watchdog"] = ("Diagnostic des blocages", "Freeze diagnostics"),
            ["UI.WatchdogTip"] = ("Enregistre un diagnostic si le jeu se bloque. Coûte ~1,8 ms par image : à activer seulement pour signaler un problème.",
                                  "Records diagnostics if the game freezes. Costs ~1.8 ms per frame: turn on only to report a problem."),
            ["UI.Status"] = ("État", "Status"),
            ["UI.InstallFolder"] = ("Dossier d'installation", "Install folder"),
            ["UI.GameDump"] = ("Copie du jeu", "Game dump"),
            ["UI.GameVersion"] = ("Version du jeu", "Game version"),
            ["UI.Size"] = ("Taille", "Size"),
            ["UI.GroupActions"] = ("ACTIONS", "ACTIONS"),
            ["UI.GroupDanger"] = ("ZONE SENSIBLE", "DANGER ZONE"),
            ["UI.VSyncNoFg"] = ("Indisponible avec la génération d'images (Vulkan). Pour éviter les déchirures : écran G-Sync/FreeSync, ou coupe la génération d'images.",
                                "Not available with frame generation (Vulkan). For no tearing: a G-Sync/FreeSync screen, or turn frame generation off."),
            ["UI.FpsLimitFg"] = ("Génération d'images active : la limite vient de l'objectif à l'écran.", "Frame generation is on: the limit comes from the on-screen target."),
            ["UI.FgOff"] = ("Multiplie les images affichées (30 × 2 = 60 à l'écran) et active Reflex. Seulement quand la fenêtre du jeu est au premier plan.",
                            "Multiplies the frames shown (30 x 2 = 60 on screen) and turns on Reflex. Only while the game window is focused."),
            ["UI.FgAuto"] = ("Jeu jusqu'à {0} images/s ; ×2 à ×4 choisi en jeu pour rester à {1}+ à l'écran.", "Game up to {0} FPS; x2 to x4 picked in game to stay at {1}+ on screen."),
            ["UI.FgUnlocked"] = ("Jeu sans limite, ×{0} à l'écran (ex. 30 → {1}).", "Game unlocked, x{0} on screen (e.g. 30 -> {1})."),
            ["UI.FgFixed"] = ("Jeu jusqu'à {0} images/s × {1} = {2} à l'écran.", "Game up to {0} FPS x{1} = {2} on screen."),

            // Install window (first run)
            ["Install.Title"] = ("Installation de Demon’s Souls PC", "Demon’s Souls PC setup"),
            ["Install.Welcome"] = ("Bienvenue !", "Welcome!"),
            ["Install.Intro"] = (
                "Demon’s Souls PC fait tourner ta propre copie PS5 de Demon’s Souls nativement sous Windows. Aucun fichier du jeu n'est fourni : il te faudra ton propre dump à l'étape suivante.",
                "Demon’s Souls PC runs your own PS5 copy of Demon’s Souls natively on Windows. No game files are included: you will need your own dump in the next step."),
            ["Install.Language"] = ("Langue", "Language"),
            ["Install.Folder"] = ("Dossier d'installation", "Install folder"),
            ["Install.FolderHint"] = (
                "Choisis un dossier simple, hors OneDrive, par exemple C:\\Demon’s Souls PC. Prévois environ {0} libres.",
                "Pick a plain folder outside OneDrive, for example C:\\Demon’s Souls PC. Allow about {0} of free space."),
            ["Install.FreeSpace"] = ("Espace libre sur ce disque : {0}", "Free space on this drive: {0}"),
            ["Install.Shortcuts"] = ("Créer un raccourci sur le Bureau et dans le menu Démarrer", "Create Desktop and Start menu shortcuts"),
            ["Install.Install"] = ("Installer", "Install"),
            ["Install.PickFolder"] = ("Choisis où installer Demon’s Souls PC", "Choose where to install Demon’s Souls PC"),
            ["Install.Downloading"] = ("Téléchargement du runtime ({0} / {1})…", "Downloading the runtime ({0} / {1})…"),
            ["Install.Verifying"] = ("Vérification de l'intégrité (SHA-256)…", "Checking integrity (SHA-256)…"),
            ["Install.Extracting"] = ("Extraction des fichiers…", "Extracting files…"),
            ["Install.Finishing"] = ("Finalisation…", "Finishing…"),
            ["Install.Done"] = ("Installation terminée. Étape suivante : ton dump du jeu.", "Install complete. Next step: your game dump."),
            ["Install.Failed"] = ("L'installation a échoué : {0}", "Install failed: {0}"),
            ["Install.BadFolder"] = ("Ce dossier n'est pas utilisable : {0}", "This folder cannot be used: {0}"),
            ["Install.OneDrive"] = ("Évite un dossier synchronisé par OneDrive : les gros fichiers du jeu y posent problème.", "Avoid a OneDrive-synced folder: the game's large files cause problems there."),
            ["Install.NotEmpty"] = ("Ce dossier contient déjà des fichiers. Installer Demon’s Souls PC dedans quand même ?", "This folder already has files in it. Install Demon’s Souls PC there anyway?"),
            ["Install.HashMismatch"] = ("Le fichier téléchargé est corrompu (SHA-256 différent). Réessaie plus tard.", "The downloaded file is corrupt (SHA-256 mismatch). Try again later."),
            ["Install.LanguageRestart"] = ("La langue sera appliquée au prochain démarrage.", "The language applies on the next start."),

            // Runtime update
            ["Runtime.UpdateAvailable"] = (
                "Une nouvelle version du runtime ({0}) est disponible (installée : {1}). L'installer maintenant ? Le jeu sera reconstruit depuis ton dump (environ 2 minutes).",
                "A new runtime version ({0}) is available (installed: {1}). Install it now? The game will be rebuilt from your dump (about 2 minutes)."),
            ["Runtime.Rebuilding"] = ("Reconstruction du jeu depuis ton dump…", "Rebuilding the game from your dump…"),

            // Self-update
            ["Update.Available"] = ("Demon’s Souls PC {0} est disponible (tu as la {1}).", "Demon’s Souls PC {0} is available (you have {1})."),
            ["Update.AskInstall"] = ("Demon’s Souls PC {0} est disponible (tu as la {1}). Mettre à jour maintenant ? Demon’s Souls PC va redémarrer.", "Demon’s Souls PC {0} is available (you have {1}). Update now? Demon’s Souls PC will restart."),
            ["Update.UpToDate"] = ("Demon’s Souls PC est à jour ({0}).", "Demon’s Souls PC is up to date ({0})."),
            ["Update.Checking"] = ("Recherche de mises à jour…", "Checking for updates…"),
            ["Update.Downloading"] = ("Téléchargement de la mise à jour…", "Downloading the update…"),
            ["Update.Failed"] = ("La mise à jour a échoué : {0}", "Update failed: {0}"),
            ["Update.NoDigest"] = ("La release ne publie pas d'empreinte SHA-256 : mise à jour refusée par sécurité.", "The release has no SHA-256 digest: update refused for safety."),
        };
    }

    // XAML: Text="{svc:Tr Install.Title}" with xmlns:svc="clr-namespace:DemonsSoulsPC.App.Services"
    [MarkupExtensionReturnType(typeof(string))]
    public class TrExtension : MarkupExtension
    {
        public TrExtension() { }
        public TrExtension(string key) { Key = key; }
        [ConstructorArgument("key")]
        public string Key { get; set; } = "";
        public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
    }
}
