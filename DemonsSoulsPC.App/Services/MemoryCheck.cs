using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace DemonsSoulsPC.App.Services
{
    // The engine's "memory savings of PCs short of RAM" removed. KytyPS5 (chenxiao07, src/local/local-platform.cpp,
    // LocalPlatform::EmulatorMemory) takes at start the PC's RAM less what other programs hold, and with 16 GiB or less
    // saves memory at the cost of smoothness: pipelines of the worlds left swept and made again (4.8 ms each on an RTX
    // 2080 Ti, issue #129), the engine's frees given back, smaller read-back rings, translated plans not kept. Windows
    // counts as available only what no working set holds: a 32 GB gaming PC with a browser, Discord and launchers open
    // has 15-16 GB of it (this PC: 15.6 GB), so most 32 GB PCs ran in that mode (a player's report: 15.5 GB left,
    // 12-18 FPS). No setting turns it off (KYTY_SIMULATE_RAM_MB only lowers the total), so the launcher patches the
    // engine's one computation of that value (the release v20261121, commit 65433d2e; 28 bytes at file offset 0xf2906c,
    // VA 0x140f29c6c): "usable = min(total, available + own), or total when one is unknown" becomes
    // "usable = max(total, 16 GiB + 1 MiB)", so the savings are never on, whatever the PC holds. The file's time is kept:
    // the shader translation cache's key holds the executable's size and time, and both stay. Another engine build (other
    // bytes there) is left alone. The engine's log then reads "PC memory: <total or 16385> MiB ... savings ... off".
    public static class MemoryCheck
    {
        private const long PatchOffset = 0xf2906c;

        // mov rdi,[rdi+8] ; add rdi,rbx ; cmp rdi,rsi ; cmovae rdi,rsi ; test rbx,rbx ; cmove rdi,rsi ; test rsi,rsi ; cmove rdi,rsi
        private static readonly byte[] Original = Hex("488b7f084801df4839f7480f43fe4885db480f44fe4885f6480f44fe");

        // mov rdi,rsi ; mov rax,0x400100000 ; cmp rdi,rax ; cmovb rdi,rax ; nop (8 bytes)
        private static readonly byte[] Patched = Hex("4889f748b800001000040000004839c7480f42f80f1f840000000000");

        private static byte[] Hex(string text) =>
            Enumerable.Range(0, text.Length / 2).Select(i => Convert.ToByte(text.Substring(i * 2, 2), 16)).ToArray();

        // Applies the patch to Build\Engine\kyty_emulator.exe when it holds the original bytes. Returns a line for the
        // logs; never throws (an engine in use or another build: left as it is).
        public static string RemoveMemorySavings(string engineDir)
        {
            var exe = Path.Combine(engineDir, GameSetup.GameExe);
            try
            {
                if (!File.Exists(exe)) return "économie de mémoire : moteur absent";
                var time = File.GetLastWriteTimeUtc(exe);
                var current = new byte[Original.Length];
                using (var file = new FileStream(exe, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (file.Length < PatchOffset + Original.Length) return "économie de mémoire : autre moteur, laissé tel quel";
                    file.Position = PatchOffset;
                    if (file.Read(current, 0, current.Length) != current.Length) return "économie de mémoire : lecture impossible";
                }
                if (current.SequenceEqual(Patched)) return "économie de mémoire : supprimée (moteur déjà modifié)";
                if (!current.SequenceEqual(Original)) return "économie de mémoire : autre moteur, laissé tel quel";
                using (var file = new FileStream(exe, FileMode.Open, FileAccess.Write, FileShare.Read))
                {
                    file.Position = PatchOffset;
                    file.Write(Patched, 0, Patched.Length);
                }
                File.SetLastWriteTimeUtc(exe, time);
                return "économie de mémoire : supprimée (moteur modifié)";
            }
            catch (Exception ex)
            {
                return "économie de mémoire : moteur non modifié (" + ex.Message + ")";
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatusEx
        {
            public uint Length, MemoryLoad;
            public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

        private static string Gb(ulong bytes) => (bytes / (double)(1UL << 30)).ToString("0.0");

        [DllImport("psapi.dll")]
        private static extern bool EmptyWorkingSet(IntPtr process);

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(uint access, bool inherit, int id);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        // Kept beside the patch (the user's choice): more free RAM for the game. The other programs' working sets emptied
        // right before the engine starts (what RAMMap's "Empty working sets" does: their pages go to the standby list,
        // which is available, and come back on use): this PC 15.6 -> 18.6 GB at once, 21.9 GB 20 s later. Only when less
        // than 17.5 GiB is free on a PC with more than 16.5 GiB; waits up to 10 s. Returns a line for the logs.
        public static string FreeUpForEngine()
        {
            const ulong GiB = 1UL << 30, Line = 16UL * GiB + GiB / 2;
            var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf(typeof(MemoryStatusEx)) };
            if (!GlobalMemoryStatusEx(ref status)) return "RAM disponible : inconnue";
            ulong total = status.TotalPhys, before = status.AvailPhys;
            if (total <= Line || before >= Line + GiB) return $"RAM disponible : {Gb(before)} Go sur {Gb(total)} Go";
            int trimmed = 0;
            var own = Process.GetCurrentProcess().Id;
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.Id == own || p.Id == 0 || p.Id == 4) continue;
                    var handle = OpenProcess(0x0100 | 0x0400, false, p.Id); // PROCESS_SET_QUOTA | PROCESS_QUERY_INFORMATION
                    if (handle == IntPtr.Zero) continue;
                    if (EmptyWorkingSet(handle)) trimmed++;
                    CloseHandle(handle);
                }
                catch { }
                finally { p.Dispose(); }
            }
            var after = before;
            for (int i = 0; i < 40; i++)
            {
                System.Threading.Thread.Sleep(250);
                if (GlobalMemoryStatusEx(ref status)) after = status.AvailPhys;
                if (after >= Line + GiB) break;
            }
            return $"RAM disponible : {Gb(before)} Go sur {Gb(total)} Go, {Gb(after)} Go après avoir vidé la mémoire de travail de {trimmed} programmes";
        }

        // The programs holding the most RAM (working set, summed by name), this launcher left out.
        private static List<(string Name, ulong Bytes)> TopPrograms(int count)
        {
            var own = Process.GetCurrentProcess().Id;
            var sums = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.Id == own || p.Id == 0 || p.Id == 4) continue;
                    sums[p.ProcessName] = (sums.TryGetValue(p.ProcessName, out var sum) ? sum : 0) + (ulong)p.WorkingSet64;
                }
                catch { }
                finally { p.Dispose(); }
            }
            return sums.OrderByDescending(kv => kv.Value).Take(count).Select(kv => (kv.Key, kv.Value)).ToList();
        }

        // One line for the session report: free RAM at launch and the programs holding the most.
        public static string Describe()
        {
            var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf(typeof(MemoryStatusEx)) };
            if (!GlobalMemoryStatusEx(ref status)) return "RAM libre au lancement : inconnue";
            var top = string.Join(", ", TopPrograms(6).Select(t => $"{t.Name} {Gb(t.Bytes)} Go"));
            return $"RAM libre au lancement : {Gb(status.AvailPhys)} Go sur {Gb(status.TotalPhys)} Go ; programmes les plus gourmands : {top}";
        }
    }
}
