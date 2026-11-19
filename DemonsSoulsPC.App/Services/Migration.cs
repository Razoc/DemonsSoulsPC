using System;
using System.IO;

namespace DemonsSoulsPC.App.Services
{
    // Inherited from AkiBot (renames of an older upstream layout). Demon’s Souls PC has no older layout yet: nothing
    // to migrate, the method stays as the place for future renames.
    public static class Migration
    {
        private const string OldName = "astro" + "hybrid"; // nom du projet amont

        public static void Run(string root)
        {
            return;
#pragma warning disable CS0162
            try
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;
                var build = Path.Combine(root, "Build");
                var runHybrid = Path.Combine(build, "RunHybrid");

                MoveIfMissing(Path.Combine(build, OldName + "-settings.json"), Path.Combine(build, "dspc-settings.json"));
                MoveIfMissing(Path.Combine(build, OldName + "-settings.json.bak"), Path.Combine(build, "dspc-settings.json.bak"));
                MoveIfMissing(Path.Combine(runHybrid, OldName + "-game.json"), Path.Combine(runHybrid, GameFeatures.FileName));

                // Launch scripts: the launcher starts the game itself now.
                foreach (var pattern in new[] { "*.bat", "*.cmd", "*.sh" })
                {
                    foreach (var f in Directory.GetFiles(root, pattern, SearchOption.TopDirectoryOnly)) TryDelete(f);
                    if (Directory.Exists(runHybrid))
                        foreach (var f in Directory.GetFiles(runHybrid, pattern, SearchOption.TopDirectoryOnly)) TryDelete(f);
                }
                foreach (var f in Directory.GetFiles(root, OldName + ".exe*", SearchOption.TopDirectoryOnly)) TryDelete(f);

                // The old host module, once the Demon’s Souls PC runtime has put its own next to it.
                if (File.Exists(Path.Combine(runHybrid, RuntimeDefaults.HostModule)))
                {
                    TryDelete(Path.Combine(runHybrid, OldName + "_host.dll"));
                    var libs = Path.Combine(runHybrid, "libs");
                    if (Directory.Exists(libs))
                        foreach (var f in Directory.GetFiles(libs, "lib" + OldName + "host.prx", SearchOption.TopDirectoryOnly)) TryDelete(f);
                }
            }
            catch { }
        }

        private static void MoveIfMissing(string from, string to)
        {
            try
            {
                if (File.Exists(from) && !File.Exists(to)) File.Move(from, to);
                else if (File.Exists(from)) File.Delete(from);
            }
            catch { }
        }

        private static void TryDelete(string file)
        {
            try { if (File.Exists(file)) File.Delete(file); } catch { }
        }
    }
}
