using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace DemonsSoulsPC.App.Services
{
    // The launcher's controller input (0.2.3, the launcher used with a controller only): the buttons of every controller,
    // merged into one state. Xbox controllers and those that act like one (XInput: SCUF, 8BitDo...) through
    // xinput1_4.dll; Sony's DualSense and DualShock 4 (USB and Bluetooth) read directly from their HID reports, since
    // Windows gives them no XInput. Read only: nothing is ever sent to a controller (the game's own vibration and
    // haptics are untouched). The HID devices are opened shared and only while the launcher is in front
    // (SetActive), so nothing is held while the game plays.
    public static class GamepadInput
    {
        [Flags]
        public enum Buttons
        {
            None = 0,
            Up = 1 << 0, Down = 1 << 1, Left = 1 << 2, Right = 1 << 3,
            A = 1 << 4,          // Cross
            B = 1 << 5,          // Circle
            X = 1 << 6,          // Square
            Y = 1 << 7,          // Triangle
            LB = 1 << 8, RB = 1 << 9,
            Start = 1 << 10,     // Options / Menu
            Back = 1 << 11,      // Create / Share / View
            LT = 1 << 12, RT = 1 << 13,
        }

        // The buttons held now, the left stick counted as the D-pad (past half way).
        public static Buttons State()
        {
            var state = ReadXInput();
            lock (HidLock)
                foreach (var pad in Hid) state |= pad.State;
            return state;
        }

        // ---- XInput ----

        [StructLayout(LayoutKind.Sequential)]
        struct XInputGamepad
        {
            public ushort wButtons;
            public byte bLeftTrigger, bRightTrigger;
            public short sThumbLX, sThumbLY, sThumbRX, sThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct XInputState
        {
            public uint dwPacketNumber;
            public XInputGamepad Gamepad;
        }

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        static extern uint XInputGetState14(uint index, out XInputState state);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
        static extern uint XInputGetState91(uint index, out XInputState state);

        static bool _xinput14 = true, _xinputNone;
        static DateTime _xinputRetry = DateTime.MinValue;
        static readonly bool[] Connected = new bool[4];

        static Buttons ReadXInput()
        {
            var result = Buttons.None;
            if (_xinputNone) return result;
            // A slot with nothing in it is slow to ask (XInput looks for the device): empty slots once a second.
            bool retry = DateTime.Now >= _xinputRetry;
            if (retry) _xinputRetry = DateTime.Now.AddSeconds(1);
            for (uint i = 0; i < 4; i++)
            {
                if (!Connected[i] && !retry) continue;
                XInputState s;
                uint code;
                try { code = _xinput14 ? XInputGetState14(i, out s) : XInputGetState91(i, out s); }
                catch (DllNotFoundException)
                {
                    if (_xinput14) { _xinput14 = false; i--; continue; }
                    _xinputNone = true; return result;
                }
                catch (EntryPointNotFoundException) { _xinputNone = true; return result; }
                Connected[i] = code == 0;
                if (code != 0) continue;
                var g = s.Gamepad;
                ushort b = g.wButtons;
                if ((b & 0x0001) != 0) result |= Buttons.Up;
                if ((b & 0x0002) != 0) result |= Buttons.Down;
                if ((b & 0x0004) != 0) result |= Buttons.Left;
                if ((b & 0x0008) != 0) result |= Buttons.Right;
                if ((b & 0x0010) != 0) result |= Buttons.Start;
                if ((b & 0x0020) != 0) result |= Buttons.Back;
                if ((b & 0x0100) != 0) result |= Buttons.LB;
                if ((b & 0x0200) != 0) result |= Buttons.RB;
                if ((b & 0x1000) != 0) result |= Buttons.A;
                if ((b & 0x2000) != 0) result |= Buttons.B;
                if ((b & 0x4000) != 0) result |= Buttons.X;
                if ((b & 0x8000) != 0) result |= Buttons.Y;
                if (g.bLeftTrigger > 128) result |= Buttons.LT;
                if (g.bRightTrigger > 128) result |= Buttons.RT;
                const short dead = 16000;
                if (g.sThumbLY > dead) result |= Buttons.Up;
                if (g.sThumbLY < -dead) result |= Buttons.Down;
                if (g.sThumbLX < -dead) result |= Buttons.Left;
                if (g.sThumbLX > dead) result |= Buttons.Right;
            }
            return result;
        }

        // ---- Sony HID (DualSense 054C:0CE6, DualSense Edge 054C:0DF2, DualShock 4 054C:05C4 / 09CC, adapter 0BA0) ----

        class HidPad
        {
            public string Path = "";
            public bool DualSense;
            public SafeFileHandle? Handle;
            public FileStream? Stream;
            public Thread? Reader;
            public volatile Buttons State;
            public volatile bool Closed;
        }

        static readonly object HidLock = new object();
        static readonly List<HidPad> Hid = new List<HidPad>();
        static Timer? _scan;
        static bool _active;

        // While the launcher is in front: Sony controllers are looked for (every 2 s, for one plugged in later) and read;
        // in the background they are closed.
        public static void SetActive(bool active)
        {
            lock (HidLock)
            {
                if (active == _active) return;
                _active = active;
                if (active)
                {
                    _scan = new Timer(_ => ScanHid(), null, 0, 2000);
                }
                else
                {
                    _scan?.Dispose(); _scan = null;
                    foreach (var pad in Hid) Close(pad);
                    Hid.Clear();
                }
            }
        }

        static void Close(HidPad pad)
        {
            pad.Closed = true;
            pad.State = Buttons.None;
            try { pad.Stream?.Dispose(); } catch { }
            try { pad.Handle?.Dispose(); } catch { }
        }

        static void ScanHid()
        {
            List<(string Path, bool DualSense)> paths;
            try { paths = SonyPaths(); } catch { return; }
            lock (HidLock)
            {
                if (!_active) return;
                Hid.RemoveAll(p => p.Closed);
                foreach (var (path, dualSense) in paths)
                {
                    if (Hid.Exists(p => string.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase))) continue;
                    var handle = CreateFile(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
                    if (handle.IsInvalid) { handle.Dispose(); continue; }
                    var pad = new HidPad { Path = path, DualSense = dualSense, Handle = handle };
                    try { pad.Stream = new FileStream(handle, FileAccess.Read, 128, true); }
                    catch { handle.Dispose(); continue; }
                    pad.Reader = new Thread(() => ReadLoop(pad)) { IsBackground = true, Name = "pad " + path };
                    Hid.Add(pad);
                    pad.Reader.Start();
                }
            }
        }

        static void ReadLoop(HidPad pad)
        {
            var buffer = new byte[128];
            try
            {
                while (!pad.Closed)
                {
                    int n = pad.Stream!.ReadAsync(buffer, 0, buffer.Length).GetAwaiter().GetResult();
                    if (n <= 0) break;
                    pad.State = ParseSony(buffer, n, pad.DualSense);
                }
            }
            catch { }
            pad.Closed = true;
            pad.State = Buttons.None;
        }

        // The report's layout: USB report 0x01 (DualSense 64 bytes, DualShock 4 64 bytes), DualSense Bluetooth 0x31,
        // DualShock 4 Bluetooth 0x11, and the short Bluetooth report 0x01 both send before a game switches them over.
        static Buttons ParseSony(byte[] r, int n, bool dualSense)
        {
            int sticks, face, shoulders;
            if (r[0] == 0x31 && n >= 12) { sticks = 2; face = 9; shoulders = 10; }            // DualSense, Bluetooth
            else if (r[0] == 0x11 && n >= 10) { sticks = 3; face = 8; shoulders = 9; }       // DualShock 4, Bluetooth
            else if (r[0] == 0x01 && n >= 48) { sticks = 1; face = dualSense ? 8 : 5; shoulders = face + 1; } // USB (DualSense: counter at 7)
            else if (r[0] == 0x01 && n >= 10) { sticks = 1; face = 5; shoulders = 6; }       // short Bluetooth report
            else return Buttons.None;

            var result = Buttons.None;
            int dpad = r[face] & 0x0F;
            if (dpad == 7 || dpad == 0 || dpad == 1) result |= Buttons.Up;
            if (dpad == 1 || dpad == 2 || dpad == 3) result |= Buttons.Right;
            if (dpad == 3 || dpad == 4 || dpad == 5) result |= Buttons.Down;
            if (dpad == 5 || dpad == 6 || dpad == 7) result |= Buttons.Left;
            if ((r[face] & 0x10) != 0) result |= Buttons.X;   // square
            if ((r[face] & 0x20) != 0) result |= Buttons.A;   // cross
            if ((r[face] & 0x40) != 0) result |= Buttons.B;   // circle
            if ((r[face] & 0x80) != 0) result |= Buttons.Y;   // triangle
            byte s = r[shoulders];
            if ((s & 0x01) != 0) result |= Buttons.LB;
            if ((s & 0x02) != 0) result |= Buttons.RB;
            if ((s & 0x04) != 0) result |= Buttons.LT;
            if ((s & 0x08) != 0) result |= Buttons.RT;
            if ((s & 0x10) != 0) result |= Buttons.Back;      // create / share
            if ((s & 0x20) != 0) result |= Buttons.Start;     // options
            int lx = r[sticks], ly = r[sticks + 1];
            if (ly < 64) result |= Buttons.Up;
            if (ly > 192) result |= Buttons.Down;
            if (lx < 64) result |= Buttons.Left;
            if (lx > 192) result |= Buttons.Right;
            return result;
        }

        // The Sony controllers' HID paths, with whether each is a DualSense (its USB report has its buttons from byte 8,
        // a DualShock 4's from byte 5).
        static List<(string Path, bool DualSense)> SonyPaths()
        {
            var found = new List<(string Path, bool DualSense)>();
            HidD_GetHidGuid(out var guid);
            var set = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (set == new IntPtr(-1)) return found;
            try
            {
                var iface = new SP_DEVICE_INTERFACE_DATA { cbSize = Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>() };
                for (uint i = 0; SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i, ref iface); i++)
                {
                    SetupDiGetDeviceInterfaceDetail(set, ref iface, IntPtr.Zero, 0, out int size, IntPtr.Zero);
                    if (size <= 0) continue;
                    var detail = Marshal.AllocHGlobal(size);
                    try
                    {
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                        if (!SetupDiGetDeviceInterfaceDetail(set, ref iface, detail, size, out _, IntPtr.Zero)) continue;
                        var path = Marshal.PtrToStringUni(detail + 4) ?? "";
                        var lower = path.ToLowerInvariant();
                        if (!lower.Contains("vid_054c") && !lower.Contains("054c")) continue;
                        bool dualSense = lower.Contains("pid_0ce6") || lower.Contains("pid_0df2") || lower.Contains("0ce6") || lower.Contains("0df2");
                        bool ds4 = lower.Contains("pid_05c4") || lower.Contains("pid_09cc") || lower.Contains("pid_0ba0") || lower.Contains("05c4") || lower.Contains("09cc");
                        if (!dualSense && !ds4) continue;
                        found.Add((path, dualSense));
                    }
                    finally { Marshal.FreeHGlobal(detail); }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
            return found;
        }

        const uint GENERIC_READ = 0x80000000, FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2, OPEN_EXISTING = 3, FILE_FLAG_OVERLAPPED = 0x40000000;
        const int DIGCF_PRESENT = 0x2, DIGCF_DEVICEINTERFACE = 0x10;

        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [DllImport("hid.dll")] static extern void HidD_GetHidGuid(out Guid guid);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] static extern IntPtr SetupDiGetClassDevs(ref Guid guid, IntPtr enumerator, IntPtr parent, int flags);
        [DllImport("setupapi.dll")] static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr info, ref Guid guid, uint index, ref SP_DEVICE_INTERFACE_DATA data);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SP_DEVICE_INTERFACE_DATA data, IntPtr detail, int size, out int required, IntPtr info);
        [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    }
}
