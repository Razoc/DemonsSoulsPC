using System.Collections.Generic;

namespace DemonsSoulsPC.App.Services
{
    // Texts of the original (English) interface: the English text is the key. French translations, plus
    // English overrides where the original text names Demon’s Souls PC (Demon’s Souls PC's own branding).
    public static partial class LocTables
    {
        public static readonly Dictionary<string, string> EnglishOverrides = new Dictionary<string, string>
        {
            ["Demon’s Souls PC"] = "Demon’s Souls PC",
            ["Demon’s Souls PC Setup"] = "Demon’s Souls PC setup",
            ["Demon’s Souls PC Port"] = "Demon’s Souls on PC",
            ["Native PC Port • Demon’s Souls PC"] = "Native PC port • Demon’s Souls PC",
            ["NEW AKIBOT UPDATE AVAILABLE"] = "NEW AKIBOT UPDATE AVAILABLE",
            ["Installing Demon’s Souls PC"] = "Installing Demon’s Souls PC",
            ["Uninstall Demon’s Souls PC"] = "Uninstall Demon’s Souls PC",
            ["Welcome to Demon’s Souls PC! This setup wizard will help you prepare Demon’s Souls for Windows using your own legally obtained PS5 game dump."] =
                "Welcome to Demon’s Souls PC! This wizard prepares Demon’s Souls for Windows from your own legally obtained PS5 game dump.",
            ["Demon’s Souls PC has been successfully installed. You can now configure your graphics and controller settings and launch the game."] =
                "Demon’s Souls PC is ready. You can now adjust the graphics and controller settings and launch the game.",
        };

        public static readonly Dictionary<string, string> French = new Dictionary<string, string>
        {
            // Main window
            ["Demon’s Souls PC"] = "Demon’s Souls PC",
            ["Demon’s Souls PC Port"] = "Demon’s Souls sur PC",
            ["Home"] = "Accueil",
            ["Graphics"] = "Graphismes",
            ["Controller"] = "Manette",
            ["Advanced"] = "Avancé",
            ["Installation"] = "Installation",
            ["STATUS"] = "ÉTAT",
            ["NEW AKIBOT UPDATE AVAILABLE"] = "NOUVELLE MISE À JOUR D'AKIBOT DISPONIBLE",
            ["Update Now"] = "Mettre à jour",
            ["Downloading..."] = "Téléchargement…",
            ["Ignore for Now"] = "Plus tard",
            ["Cancel"] = "Annuler",
            ["Best Settings for This PC"] = "Meilleurs réglages pour ce PC",
            ["Update"] = "Mettre à jour",
            ["Play"] = "Jouer",

            // Home
            ["ASTRO BOT"] = "ASTRO BOT",
            ["Native PC Port • Demon’s Souls PC"] = "Portage PC natif • Demon’s Souls PC",
            ["INSTALLED"] = "INSTALLÉE",
            ["AVAILABLE"] = "DISPONIBLE",
            ["LATEST"] = "DERNIÈRE",
            ["UP TO DATE"] = "À JOUR",
            ["GAME VERSION"] = "VERSION DU JEU",
            ["Title ID: "] = "Title ID : ",
            ["CPU"] = "CPU",
            ["GPU"] = "GPU",
            ["RAM / DISPLAY"] = "RAM / ÉCRAN",
            ["RAM"] = "RAM",
            ["CURRENT GRAPHICS CONFIGURATION"] = "CONFIGURATION GRAPHIQUE ACTUELLE",
            ["Graphics Settings"] = "Réglages graphiques",
            ["WHAT'S NEW IN"] = "NOUVEAUTÉS DE LA",
            ["View Full Release Notes"] = "Voir toutes les notes de version",
            ["QUICK ACTIONS"] = "ACTIONS RAPIDES",
            ["Click to toggle Ray Tracing between OFF (Non-RT faster lighting) and ON (Ray Tracing)"] =
                "Clique pour basculer le ray tracing entre OFF (éclairage rapide sans RT) et ON (ray tracing)",
            ["Controller Settings"] = "Réglages manette",

            // Graphics
            ["Display"] = "Affichage",
            ["Resolution"] = "Résolution",
            ["Start in fullscreen"] = "Démarrer en plein écran",
            ["Ultrawide 21:9 rendering"] = "Rendu ultra-large 21:9",
            ["Graphics card"] = "Carte graphique",
            ["VSync (no tearing; may add a little input delay)"] = "VSync (pas de déchirement ; peut ajouter un peu de latence)",
            ["Frame Rate"] = "Images par seconde",
            ["Limit"] = "Limite",
            ["NVIDIA RTX Features"] = "Fonctions NVIDIA RTX",
            ["Ray Tracing (RTX)"] = "Ray tracing (RTX)",
            ["Upscaling"] = "Mise à l'échelle",
            ["Frame Generation"] = "Génération d'images",
            ["Target FPS"] = "FPS visés",
            ["NVIDIA Low Latency (Reflex)"] = "Faible latence NVIDIA (Reflex)",
            ["Performance Overlay"] = "Overlay de performances",
            ["Show Stats"] = "Afficher les stats",
            ["Hardware Summary"] = "Résumé du matériel",
            ["In game: F2 shows/hides overlay, Ctrl+F2 changes frame-rate limit, F11 or Alt+Enter toggles fullscreen."] =
                "En jeu : F2 affiche/masque l'overlay, Ctrl+F2 change la limite d'images, F11 ou Alt+Entrée bascule le plein écran.",

            // Controller
            ["Preferred Controller"] = "Manette préférée",
            ["Vibration"] = "Vibration",
            ["Strength"] = "Intensité",
            ["0 to 200 percent; choose Custom in the list to change it"] = "0 à 200 % ; choisis « Personnalisé » dans la liste pour la modifier",
            ["Stronger vibration on Switch-style pads (third-party ones)"] = "Vibration plus forte sur les manettes type Switch (tierces)",
            ["Motion Controls"] = "Détection de mouvement",
            ["DualSense Features (Triggers, Speaker & Haptics)"] = "Fonctions DualSense (gâchettes, haut-parleur et haptique)",
            ["DualSense Wireless Speaker over Bluetooth"] = "Haut-parleur de la DualSense en Bluetooth",
            ["DualSense Wireless HD Haptics over Bluetooth"] = "Retour haptique HD de la DualSense en Bluetooth",
            ["Connected Controllers"] = "Manettes connectées",
            ["Refresh"] = "Actualiser",
            ["✓ Connected"] = "✓ Connectée",
            ["✗ Disconnected"] = "✗ Déconnectée",
            ["🎮 Haptics"] = "🎮 Haptique",
            ["🎯 Motion"] = "🎯 Mouvement",
            ["⚡ Triggers"] = "⚡ Gâchettes",
            ["Auto detects the controller you press. PlayStation and Switch Pro controllers support motion and haptics. Xbox controllers use View/Back button for tilt."] =
                "Auto détecte la manette sur laquelle tu appuies. Les manettes PlayStation et Switch Pro gèrent le mouvement et l'haptique. Sur Xbox, le bouton Affichage/Back sert à incliner.",
            ["DualSense and Switch Pro controllers have built-in motion sensors. Xbox and generic pads: press View/Back to enable tilt with right stick, press again to disable."] =
                "Les manettes DualSense et Switch Pro ont des capteurs de mouvement intégrés. Xbox et manettes génériques : appuie sur Affichage/Back pour incliner avec le stick droit, rappuie pour arrêter.",

            // Advanced
            ["Renderer"] = "Moteur de rendu",
            ["Lighting"] = "Éclairage",
            ["Game Speed"] = "Vitesse du jeu",
            ["Speed"] = "Vitesse",
            ["Language"] = "Langue",
            ["Game language"] = "Langue du jeu",
            ["Runtime Options"] = "Options du runtime",
            ["Hang Watchdog (record diagnostics on freeze)"] = "Surveillance des blocages (diagnostic en cas de gel)",
            ["Persistent Shader Cache (eliminates compile hitches)"] = "Cache de shaders persistant (supprime les saccades de compilation)",
            ["Draw Prep Tuning (reduces CP spin with background apps)"] = "Réglage de la préparation des draws (moins d'attente CPU avec des applis en fond)",
            ["Motion Controls (accelerometer/gyroscope)"] = "Détection de mouvement (accéléromètre/gyroscope)",
            ["Trigger State (DualSense adaptive triggers)"] = "État des gâchettes (gâchettes adaptatives DualSense)",
            ["DualSense Wireless Speaker (Bluetooth HID audio)"] = "Haut-parleur DualSense sans fil (audio HID Bluetooth)",
            ["DualSense Wireless Haptics (Bluetooth HID haptics)"] = "Haptique DualSense sans fil (haptique HID Bluetooth)",
            ["Skip Intro Videos (-skipIntro)"] = "Passer les vidéos d'intro (-skipIntro)",
            ["Process Priority"] = "Priorité du processus",
            ["Command Processor Priority"] = "Priorité du command processor",
            ["NVIDIA Frame Generation"] = "Génération d'images NVIDIA",
            ["Experimental Features"] = "Fonctions expérimentales",
            ["Additional experimental options will be added here as they are validated."] = "D'autres options expérimentales arriveront ici une fois validées.",

            // Installation page
            ["Installation Status"] = "État de l'installation",
            ["Status:"] = "État :",
            ["Install Path:"] = "Dossier d'installation :",
            ["Game Dump:"] = "Dump du jeu :",
            ["Game Version:"] = "Version du jeu :",
            ["Title ID:"] = "Title ID :",
            ["Installed:"] = "Installé le :",
            ["Installation Size"] = "Taille de l'installation",
            ["Tool Versions"] = "Versions des outils",
            ["AnyPS5:"] = "AnyPS5 :",
            ["Kyty GPU:"] = "Kyty GPU :",
            ["Actions"] = "Actions",
            ["Repair Installation"] = "Réparer l'installation",
            ["Rebuild Installation"] = "Reconstruire l'installation",
            ["Change Location"] = "Changer d'emplacement",
            ["Open Install Folder"] = "Ouvrir le dossier d'installation",
            ["Open Log Folder"] = "Ouvrir le dossier des logs",
            ["⚠ Danger Zone"] = "⚠ Zone dangereuse",
            ["Run Setup Again"] = "Relancer la configuration",
            ["Uninstall Demon’s Souls PC"] = "Désinstaller Demon’s Souls PC",
            ["These actions are irreversible. Your original game dump will NOT be deleted."] = "Ces actions sont irréversibles. Ton dump d'origine ne sera PAS supprimé.",

            // Setup wizard
            ["Demon’s Souls PC Setup"] = "Configuration de Demon’s Souls PC",
            ["Demon’s Souls Native PC Port Setup"] = "Préparation de Demon’s Souls pour le PC",
            ["Welcome to Demon’s Souls PC! This setup wizard will help you prepare Demon’s Souls for Windows using your own legally obtained PS5 game dump."] =
                "Bienvenue dans Demon’s Souls PC ! Cet assistant prépare Demon’s Souls pour Windows à partir de ton propre dump PS5, obtenu légalement.",
            ["You will need:"] = "Il te faut :",
            ["• A PS5 Demon’s Souls game dump (folder containing eboot.bin)"] = "• Un dump PS5 de Demon’s Souls (le dossier qui contient eboot.bin)",
            ["• Approximately 2-3 GB of free disk space"] = "• Environ 2 à 3 Go d'espace libre",
            ["• NVIDIA RTX GPU recommended for DLSS/Frame Generation"] = "• Une carte NVIDIA RTX, conseillée pour le DLSS et la génération d'images",
            ["The original game dump will never be modified. All files are created in a separate installation directory."] =
                "Ton dump d'origine ne sera jamais modifié. Tous les fichiers sont créés dans le dossier d'installation.",
            ["Select Game Dump"] = "Choisir le dump du jeu",
            ["Browse to your Demon’s Souls PS5 game dump folder (the folder containing eboot.bin):"] = "Indique le dossier de ton dump PS5 de Demon’s Souls (celui qui contient eboot.bin) :",
            ["Type or paste the dump folder, or use Browse"] = "Tape ou colle le chemin du dump, ou utilise Parcourir",
            ["Browse..."] = "Parcourir…",
            ["Detected Game Information"] = "Jeu détecté",
            ["Version: "] = "Version : ",
            ["Eboot Size: "] = "Taille de l'eboot : ",
            ["Status: "] = "État : ",
            ["Valid ✓"] = "Valide ✓",
            ["Installing Demon’s Souls PC"] = "Préparation du jeu",
            ["% complete"] = "% terminé",
            ["✓ Setup Complete"] = "✓ Configuration terminée",
            ["Demon’s Souls PC has been successfully installed. You can now configure your graphics and controller settings and launch the game."] =
                "Demon’s Souls PC est prêt. Tu peux régler les graphismes et la manette, puis lancer le jeu.",
            ["Check folder"] = "Vérifier le dossier",
            ["Install"] = "Installer",
            ["Finish"] = "Terminer",
        };
    }
}
