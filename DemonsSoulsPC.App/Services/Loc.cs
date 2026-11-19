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
            ["UI.RenderPerf"] = ("Performance (conseillé)", "Performance (recommended)"),
            ["UI.RenderQuality"] = ("Qualité (4K, plus lourd)", "Quality (4K, heavier)"),
            ["UI.RenderPerfTip"] = ("Réglages du mode Performance de la PS5 : rendu sans 4K, plus fluide.",
                "The PS5 Performance mode settings: no 4K rendering, smoother."),
            ["UI.RenderQualityTip"] = ("Réglages d'origine du jeu (mode Fidélité, rendu 4K) : bien plus lourd pour la carte graphique.",
                "The game's own settings (Fidelity mode, 4K rendering): much heavier on the graphics card."),
            ["UI.Fps60Recommended"] = ("60 FPS (conseillé)", "60 FPS (recommended)"),
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
            ["UI.FsrFg"] = ("AMD FSR 3 (×2, toutes cartes)", "AMD FSR 3 (2x, any card)"),
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
