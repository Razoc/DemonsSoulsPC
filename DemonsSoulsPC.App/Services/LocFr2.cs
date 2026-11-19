using System.Collections.Generic;

namespace DemonsSoulsPC.App.Services
{
    // Texts produced by the code (statuses, messages, option lists). English text = key.
    public static partial class LocTables
    {
        public static readonly Dictionary<string, string> CodeEnglishOverrides = new Dictionary<string, string>
        {
            ["Welcome to Demon’s Souls PC Setup"] = "Welcome to Demon’s Souls PC setup",
            ["This will remove all Demon’s Souls PC generated files. Your original game dump will NOT be deleted. Continue?"] =
                "This removes every file Demon’s Souls PC generated. Your original game dump will NOT be deleted. Continue?",
            ["To move the game, close the app, move the whole Demon’s Souls PC folder, then use Repair once."] =
                "To move the game, close Demon’s Souls PC, move the whole install folder, then use Repair once.",
        };

        public static readonly Dictionary<string, string> CodeFrench = new Dictionary<string, string>
        {
            // Statuses
            ["Ready"] = "Prêt",
            ["Not Ready"] = "Pas prêt",
            ["Unknown"] = "Inconnu",
            ["Installed"] = "Installé",
            ["Not Installed"] = "Non installé",
            ["Not installed"] = "Non installé",
            ["Installed and Ready"] = "Installé et prêt",
            ["Not configured"] = "Non configuré",
            ["Never"] = "Jamais",
            ["Loading..."] = "Chargement…",
            ["Setup required"] = "Configuration requise",
            ["Launching Demon’s Souls..."] = "Lancement de Demon’s Souls…",
            ["Game exited normally"] = "Le jeu s'est fermé normalement",
            ["Game exited with code {0}"] = "Le jeu s'est fermé avec le code {0}",
            ["Applied best settings for this PC"] = "Meilleurs réglages appliqués pour ce PC",
            ["Please complete the setup first."] = "Termine d'abord la configuration.",
            ["Converting the game for this processor..."] = "Conversion du jeu pour ce processeur…",
            ["Select your Demon’s Souls dump folder (the one with eboot.bin)"] = "Choisis le dossier de ton dump de Demon’s Souls (celui avec eboot.bin)",
            ["That folder is not an Demon’s Souls dump."] = "Ce dossier n'est pas un dump de Demon’s Souls.",
            ["This will rebuild the installation from your game dump. Continue?"] = "L'installation va être reconstruite depuis ton dump. Continuer ?",
            ["Repair Installation"] = "Réparer l'installation",
            ["Repairing installation..."] = "Réparation de l'installation…",
            ["Repair complete"] = "Réparation terminée",
            ["Repair failed"] = "La réparation a échoué",
            ["This will completely rebuild the installation. Continue?"] = "L'installation va être entièrement reconstruite. Continuer ?",
            ["Rebuild Installation"] = "Reconstruire l'installation",
            ["Rebuilding installation..."] = "Reconstruction de l'installation…",
            ["Rebuild complete"] = "Reconstruction terminée",
            ["Rebuild failed"] = "La reconstruction a échoué",
            ["Select new installation location"] = "Choisis le nouvel emplacement de l'installation",
            ["Moving installation..."] = "Déplacement de l'installation…",
            ["Installation moved successfully"] = "Installation déplacée",
            ["This will remove all Demon’s Souls PC generated files. Your original game dump will NOT be deleted. Continue?"] =
                "Tous les fichiers générés par Demon’s Souls PC vont être supprimés. Ton dump d'origine ne sera PAS supprimé. Continuer ?",
            ["Uninstall Demon’s Souls PC"] = "Désinstaller Demon’s Souls PC",
            ["Are you sure? This cannot be undone."] = "Tu es sûr ? C'est irréversible.",
            ["Confirm Uninstall"] = "Confirmer la désinstallation",
            ["Uninstalling..."] = "Désinstallation…",
            ["Uninstall complete. Run setup to reinstall."] = "Désinstallation terminée. Relance la configuration pour réinstaller.",
            ["Installation not found or incomplete."] = "Installation introuvable ou incomplète.",
            ["Invalid game dump"] = "Dump du jeu invalide",
            ["To move the game, close the app, move the whole Demon’s Souls PC folder, then use Repair once."] =
                "Pour déplacer le jeu, ferme Demon’s Souls PC, déplace tout le dossier d'installation, puis utilise « Réparer » une fois.",
            ["Setup is still converting the game (about a minute). Please wait until it finishes."] =
                "La configuration convertit encore le jeu (environ une minute). Attends qu'elle se termine.",

            // Setup wizard
            ["Welcome to Demon’s Souls PC Setup"] = "Bienvenue dans la configuration de Demon’s Souls PC",
            ["Select your Demon’s Souls PS5 game dump folder (containing eboot.bin)"] = "Choisis le dossier de ton dump PS5 de Demon’s Souls (qui contient eboot.bin)",
            ["Validating game dump..."] = "Vérification du dump…",
            ["Game dump validated successfully!"] = "Dump vérifié !",
            ["Validation failed"] = "La vérification a échoué",
            ["Unknown error"] = "Erreur inconnue",
            ["Validation error"] = "Erreur de vérification",
            ["Installation failed"] = "L'installation a échoué",
            ["Installation error"] = "Erreur d'installation",
            ["Selected folder does not exist."] = "Le dossier choisi n'existe pas.",
            ["eboot.bin not found in the selected folder. Please select the game dump root folder (containing eboot.bin)."] =
                "eboot.bin introuvable dans ce dossier. Choisis le dossier racine du dump (celui qui contient eboot.bin).",
            ["Neither a 'data' directory nor 'app0.zar' was found in the game dump."] = "Ni dossier « data » ni « app0.zar » dans ce dump.",
            [" (not tested; features this version does not support turn themselves off)"] = " (non testée ; les fonctions non gérées par cette version se désactivent toutes seules)",

            // Game setup progress
            ["The runtime files are missing (tools\\relinker.exe or Build\\RunHybrid\\kyty_gpu.dll). Extract the whole download again."] =
                "Des fichiers du runtime manquent (tools\\relinker.exe ou Build\\RunHybrid\\kyty_gpu.dll). Réinstalle Demon’s Souls PC.",
            ["Linking the game folder (nothing is copied into or changed in your dump)..."] = "Liaison du dossier du jeu (rien n'est copié ni modifié dans ton dump)…",
            ["Unwrapping the game code (eboot.bin)..."] = "Extraction du code du jeu (eboot.bin)…",
            ["Converting the game code to a Windows program (about a minute)..."] = "Conversion du code du jeu en programme Windows (environ une minute)…",
            ["Applying the faster-lighting patch..."] = "Application du patch d'éclairage rapide…",
            ["Done."] = "Terminé.",

            // Hardware summary
            ["DLSS + Multi Frame Gen (up to 4x) + Reflex"] = "DLSS + Multi Frame Gen (jusqu'à x4) + Reflex",
            ["DLSS + Frame Gen (2x) + Reflex"] = "DLSS + Frame Gen (x2) + Reflex",
            ["DLSS + Reflex (no Frame Gen)"] = "DLSS + Reflex (sans Frame Gen)",
            ["Game's own upscaling (no NVIDIA features)"] = "Mise à l'échelle du jeu (sans fonctions NVIDIA)",
            ["No controller detected"] = "Aucune manette détectée",
            ["None detected"] = "Aucune détectée",
            ["No graphics card detected."] = "Aucune carte graphique détectée.",
            ["Detected: "] = "Détectée : ",
            [" Automatic uses the discrete card: the renderer prefers one over integrated graphics."] = " Automatique utilise la carte dédiée : le rendu la préfère à la puce intégrée.",
            [" The choice is passed to the game as ANYPS5_GPU; the renderer also prefers a discrete card by itself."] = " Le choix est transmis au jeu (ANYPS5_GPU) ; le rendu préfère de toute façon une carte dédiée.",
            [" DLSS, Reflex and frame generation use this card."] = " DLSS, Reflex et la génération d'images utilisent cette carte.",
            [" The NVIDIA options need the RTX card: pick it, or Automatic, to use them."] = " Les options NVIDIA ont besoin de la carte RTX : choisis-la, ou Automatique, pour en profiter.",

            // Home
            ["21:9 Ultrawide"] = "Ultra-large 21:9",
            ["Fullscreen"] = "Plein écran",
            ["Windowed"] = "Fenêtré",
            ["RTX OFF (Non-RT)"] = "RTX OFF (sans RT)",
            ["Reflex"] = "Reflex",

            // Graphics page
            ["Automatic - best graphics card"] = "Automatique - meilleure carte graphique",
            ["Uses the 21:9 game patch and 21:9 window dimensions (2560x1080 / 3440x1440) when enabled; 16:9 remains unchanged when disabled."] =
                "Activé : patch 21:9 du jeu et fenêtre en 21:9 (2560x1080 / 3440x1440). Désactivé : le 16:9 reste inchangé.",
            ["Ray Tracing ON: runs DemonsSouls.exe with full GI probes and ray-traced lighting shaders (higher GPU load)."] =
                "Ray tracing ON : lance DemonsSouls.exe avec toutes les sondes GI et l'éclairage en ray tracing (GPU plus chargé).",
            ["Ray Tracing OFF: runs AstroBot-NonRT-SoftAssert.exe for faster performance and steadier frame times."] =
                "Ray tracing OFF : lance AstroBot-NonRT-SoftAssert.exe, plus rapide et plus régulier.",
            ["Locks presentation to your screen's refresh (no tearing)."] = "Cale l'affichage sur la fréquence de ton écran (pas de déchirement).",
            ["The game may run below the selected limit in demanding scenes."] = "Le jeu peut passer sous la limite choisie dans les scènes lourdes.",
            ["30 FPS - steady (recommended)"] = "30 FPS - stable (conseillé)",
            ["No limit"] = "Sans limite",
            ["Game's own (default)"] = "Celle du jeu (par défaut)",
            ["NVIDIA DLSS - sharper image"] = "NVIDIA DLSS - image plus nette",
            ["Off (default)"] = "Désactivée (par défaut)",
            ["Auto - reach target (recommended)"] = "Auto - atteindre la cible (conseillé)",
            ["Fixed 2x"] = "Fixe x2",
            ["Fixed 3x"] = "Fixe x3",
            ["Fixed 4x"] = "Fixe x4",
            ["No lock - game as fast as it can"] = "Sans limite - le jeu va aussi vite qu'il peut",
            ["50 FPS on screen"] = "50 FPS à l'écran",
            ["60 FPS on screen (recommended)"] = "60 FPS à l'écran (conseillé)",
            ["90 FPS on screen"] = "90 FPS à l'écran",
            ["120 FPS on screen"] = "120 FPS à l'écran",
            ["144 FPS on screen"] = "144 FPS à l'écran",
            ["165 FPS on screen"] = "165 FPS à l'écran",
            ["Off (F2 shows it)"] = "Désactivé (F2 l'affiche)",
            ["Everything (all stats)"] = "Tout (toutes les stats)",

            // Controller page
            ["Auto - the controller you press (recommended)"] = "Auto - la manette sur laquelle tu appuies (conseillé)",
            ["PlayStation (DualSense, DualShock 4)"] = "PlayStation (DualSense, DualShock 4)",
            ["Switch Pro (and compatible pads)"] = "Switch Pro (et manettes compatibles)",
            ["Other / generic"] = "Autre / générique",
            ["Off"] = "Désactivée",
            ["Almost off (5%)"] = "Presque rien (5 %)",
            ["Minimal (10%)"] = "Minimale (10 %)",
            ["Very light (20%)"] = "Très légère (20 %)",
            ["Light (40%)"] = "Légère (40 %)",
            ["Medium (70%, recommended)"] = "Moyenne (70 %, conseillé)",
            ["Full (100%)"] = "Pleine (100 %)",
            ["Strong (150%)"] = "Forte (150 %)",
            ["Custom..."] = "Personnalisé…",
            ["DualSense adaptive triggers are supported. Trigger effect state is sent to the game so L2/R2 work correctly."] =
                "Les gâchettes adaptatives DualSense sont gérées : leur état est transmis au jeu pour que L2/R2 marchent.",

            // Advanced page
            ["OFF - Faster lighting (Non-RT, recommended)"] = "OFF - Éclairage rapide (sans RT, conseillé)",
            ["ON - Ray Tracing (Original lighting)"] = "ON - Ray tracing (éclairage d'origine)",
            ["The language the game shows its text in. Auto follows your Windows language when the game has it."] =
                "La langue des textes du jeu. Auto suit la langue de Windows quand le jeu la propose.",
            ["Auto (Windows language)"] = "Auto (langue de Windows)",
            ["Japanese"] = "Japonais",
            ["English (United States)"] = "Anglais (États-Unis)",
            ["French"] = "Français",
            ["Spanish"] = "Espagnol",
            ["German"] = "Allemand",
            ["Italian"] = "Italien",
            ["Dutch"] = "Néerlandais",
            ["Portuguese (Portugal)"] = "Portugais (Portugal)",
            ["Russian"] = "Russe",
            ["Korean"] = "Coréen",
            ["Chinese (Traditional)"] = "Chinois (traditionnel)",
            ["Chinese (Simplified)"] = "Chinois (simplifié)",
            ["Finnish"] = "Finnois",
            ["Swedish"] = "Suédois",
            ["Danish"] = "Danois",
            ["Norwegian"] = "Norvégien",
            ["Polish"] = "Polonais",
            ["Portuguese (Brazil)"] = "Portugais (Brésil)",
            ["English (United Kingdom)"] = "Anglais (Royaume-Uni)",
            ["Turkish"] = "Turc",
            ["Spanish (Latin America)"] = "Espagnol (Amérique latine)",
            ["Arabic"] = "Arabe",
            ["French (Canada)"] = "Français (Canada)",
            ["Czech"] = "Tchèque",
            ["Hungarian"] = "Hongrois",
            ["Greek"] = "Grec",
            ["Romanian"] = "Roumain",
            ["Thai"] = "Thaï",
            ["Vietnamese"] = "Vietnamien",
            ["Indonesian"] = "Indonésien",
            ["Ukrainian"] = "Ukrainien",
            ["100% - real time"] = "100 % - temps réel",
            ["75% (recommended)"] = "75 % (conseillé)",
            ["The game runs in real-time regardless of FPS. Lower values slow down the game simulation."] =
                "Le jeu tourne en temps réel quel que soit le FPS. Une valeur plus basse ralentit la simulation.",
            ["Enables DualSense trigger effect state reporting. The game ignores L2/R2 without it."] =
                "Transmet l'état des gâchettes DualSense. Sans ça, le jeu ignore L2/R2.",
            ["Skips the opening PlayStation Studios and Team Asobi intro videos (-skipIntro)."] =
                "Passe les vidéos d'intro PlayStation Studios et Team Asobi (-skipIntro).",
        };
    }
}
