using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DemonsSoulsPC.App.Services
{
    // The game window's title (0.2.2): "Demon's Souls PC" instead of the engine's
    // "[Source build 65433d2 | Release] Demon's Souls, PPSA01341, 01.007.000 [GPU] [CPU], frame: N, fps: N".
    // The engine (src/graphics/presentation/window/window.cpp, WindowContext::UpdateTitle) builds it twice a second with
    // fmt::format from a format string kept in the exe; the launcher rewrites that string in place, as it does for the
    // memory savings (MemoryCheck): same length (the size is built into the code), padded with spaces, which fmt prints
    // as they are, the arguments left unused. The " (DLSS-G: 120)" suffix of frame generation is blanked the same way.
    // The file's date is kept (the shader translation cache is keyed on it). DSPC_TITLE_FPS=1 puts the engine's own
    // title back (for measurements: its frame count and fps). Before its first frame the engine's window is "Game"
    // (KYTY_SDL_WINDOW_CAPTION, too short to rewrite): WatchWindow renames it once it is there.
    public static class EngineTitle
    {
        public const string Title = "Demon's Souls PC";

        static readonly byte[] TitleFormat = Encoding.ASCII.GetBytes("[{} | {}] {}{}{}{}{}{}[{}] [{}], frame: {}, fps: {:.0f}");
        static readonly byte[] GeneratorFormat = Encoding.ASCII.GetBytes(" ({}: {:.0f})");

        static byte[] Padded(string text, int length)
        {
            var bytes = Encoding.ASCII.GetBytes(text.PadRight(length));
            return bytes;
        }

        public static string Apply(string engineDir)
        {
            var exe = Path.Combine(engineDir, GameSetup.GameExe);
            try
            {
                if (!File.Exists(exe)) return "titre : moteur absent";
                bool engineOwn = Environment.GetEnvironmentVariable("DSPC_TITLE_FPS") == "1";
                var titleOurs = Padded(Title, TitleFormat.Length);
                var generatorOurs = Padded("", GeneratorFormat.Length);
                var data = File.ReadAllBytes(exe);
                // The title's format string (the engine's or ours, both unique), the generator's right after it (its NUL
                // between them, as the compiler laid them out); anything else is another engine, left as it is.
                int title = Find(data, TitleFormat);
                bool titleIsOurs = false;
                if (title < 0) { title = Find(data, titleOurs); titleIsOurs = title >= 0; }
                int generator = title + TitleFormat.Length + 1;
                if (title < 0 || generator + GeneratorFormat.Length >= data.Length || data[generator - 1] != 0)
                    return "titre : autre moteur, laissé tel quel";
                bool generatorIsOurs = Same(data, generator, generatorOurs);
                if (!generatorIsOurs && !Same(data, generator, GeneratorFormat)) return "titre : autre moteur, laissé tel quel";
                var writes = new System.Collections.Generic.List<(int Offset, byte[] Bytes)>();
                if (titleIsOurs == engineOwn) writes.Add((title, engineOwn ? TitleFormat : titleOurs));
                if (generatorIsOurs == engineOwn) writes.Add((generator, engineOwn ? GeneratorFormat : generatorOurs));
                if (writes.Count == 0) return engineOwn ? "titre : celui du moteur (DSPC_TITLE_FPS=1)" : "titre : « " + Title + " » (moteur déjà modifié)";
                var time = File.GetLastWriteTimeUtc(exe);
                using (var file = new FileStream(exe, FileMode.Open, FileAccess.Write, FileShare.Read))
                {
                    foreach (var (offset, bytes) in writes)
                    {
                        file.Position = offset;
                        file.Write(bytes, 0, bytes.Length);
                    }
                }
                File.SetLastWriteTimeUtc(exe, time);
                return engineOwn ? "titre : celui du moteur remis (DSPC_TITLE_FPS=1)" : "titre : « " + Title + " » (moteur modifié)";
            }
            catch (Exception ex)
            {
                return "titre : moteur non modifié (" + ex.Message + ")";
            }
        }

        static bool Same(byte[] data, int at, byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; i++) if (data[at + i] != bytes[i]) return false;
            return data[at + bytes.Length] == 0;
        }

        static int Find(byte[] data, byte[] pattern)
        {
            for (int i = 0; i <= data.Length - pattern.Length; i++)
            {
                if (data[i] != pattern[0]) continue;
                int j = 1;
                while (j < pattern.Length && data[i + j] == pattern[j]) j++;
                if (j == pattern.Length) return i;
            }
            return -1;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool SetWindowText(IntPtr hwnd, string text);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);

        delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr param);

        [DllImport("user32.dll")]
        static extern bool EnumWindows(EnumWindowsProc callback, IntPtr param);

        [DllImport("user32.dll")]
        static extern int GetWindowThreadProcessId(IntPtr hwnd, out int processId);

        // The engine's window before its first frame ("Game"): renamed as soon as it is there (up to 5 minutes:
        // the shader preparation may run first).
        public static void WatchWindow(Process script)
        {
            if (Environment.GetEnvironmentVariable("DSPC_TITLE_FPS") == "1") return;
            Task.Run(() =>
            {
                var until = DateTime.Now.AddMinutes(5);
                while (DateTime.Now < until && !script.HasExited)
                {
                    foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(GameSetup.GameExe)))
                    {
                        try
                        {
                            // Every top-level window of the engine (its "Loading the pipeline cache..." window comes
                            // first and stays the main one for a moment: left as it is).
                            bool done = false;
                            int pid = p.Id;
                            EnumWindows((hwnd, _) =>
                            {
                                GetWindowThreadProcessId(hwnd, out int owner);
                                if (owner != pid) return true;
                                var text = new StringBuilder(256);
                                GetWindowText(hwnd, text, text.Capacity);
                                if (text.ToString() == "Game") { SetWindowText(hwnd, Title); done = true; }
                                else if (text.ToString().StartsWith(Title)) done = true;
                                return true;
                            }, IntPtr.Zero);
                            if (done) return;
                        }
                        catch { }
                    }
                    System.Threading.Thread.Sleep(250);
                }
            });
        }
    }
}
