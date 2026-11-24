using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DemonsSoulsPC.App.Views;

namespace DemonsSoulsPC.App.Services
{
    // The launcher with a controller only (0.2.3). Sixty times a second, while one of the launcher's windows is in front:
    //   D-pad / left stick   move between the settings (an open list: its choices)
    //   A / Cross            choose: a button, a switch, a page; opens a list, then takes the highlighted choice
    //   B / Circle           back: closes a list without changing it, cancels a dialog, else returns to the page list
    //   LB / RB (L1 / R1)    previous / next page
    //   Start / Options      Play
    //   View / Create        full screen <-> window
    //   X / Square           shows the ⓘ explanation of the setting
    // Windows' own dialogs (message boxes, the folder picker) get keys instead: arrows, Enter, Escape, Tab.
    // Nothing happens while another program is in front (the game itself reads the controller then).
    public static class GamepadNavigator
    {
        static DispatcherTimer? _timer;
        static GamepadInput.Buttons _held;
        static DateTime _repeatAt;
        static GamepadInput.Buttons _repeating;
        static ComboBox? _openList;
        static int _listIndex;
        static int _listStart = -1;
        static ToolTip? _shownTip;
        static FocusFrame? _frame;
        static UIElement? _frameTarget;
        static readonly int OwnPid = System.Diagnostics.Process.GetCurrentProcess().Id;

        // True since the controller was last used (the focus frame and the button hints show); the mouse turns it off.
        public static bool PadMode { get; private set; }
        public static event Action<bool>? PadModeChanged;

        public static void Start()
        {
            if (_timer != null) return;
            _timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
            _timer.Tick += (_, __) => Tick();
            _timer.Start();
            EventManager.RegisterClassHandler(typeof(UIElement), Keyboard.GotKeyboardFocusEvent,
                new KeyboardFocusChangedEventHandler((s, e) => { if (PadMode) ShowFrame(e.NewFocus as UIElement); }), true);
            EventManager.RegisterClassHandler(typeof(Window), UIElement.PreviewMouseMoveEvent,
                new MouseEventHandler((s, e) => SetPadMode(false)), true);
        }

        static void SetPadMode(bool on)
        {
            if (PadMode == on) return;
            PadMode = on;
            if (on) ShowFrame(Keyboard.FocusedElement as UIElement);
            else { HideFrame(); CloseTip(); }
            PadModeChanged?.Invoke(on);
        }

        static void Tick()
        {
            var hwnd = GetForegroundWindow();
            GetWindowThreadProcessId(hwnd, out int pid);
            bool ours = pid == OwnPid;
            GamepadInput.SetActive(ours);
            var state = ours ? GamepadInput.State() : GamepadInput.Buttons.None;
            var pressed = state & ~_held;
            _held = state;

            // D-pad held: again after 350 ms, then every 110 ms.
            const GamepadInput.Buttons dirs = GamepadInput.Buttons.Up | GamepadInput.Buttons.Down | GamepadInput.Buttons.Left | GamepadInput.Buttons.Right;
            if ((pressed & dirs) != 0) { _repeating = pressed & dirs; _repeatAt = DateTime.Now.AddMilliseconds(350); }
            else if ((state & _repeating) != 0 && DateTime.Now >= _repeatAt) { pressed |= state & _repeating; _repeatAt = DateTime.Now.AddMilliseconds(110); }
            else if ((state & _repeating) == 0) _repeating = GamepadInput.Buttons.None;
            if (pressed == GamepadInput.Buttons.None) return;

            // An open list draws its choices in a popup window of its own, which can be the one in front: the list's
            // window then (0.2.3: without this, the controller went to the popup as keys and no choice could be made).
            var root = HwndSource.FromHwnd(hwnd)?.RootVisual;
            var window = root as Window;
            if (window == null && _openList != null && _openList.IsDropDownOpen) window = Window.GetWindow(_openList);
            if (window == null && root != null) window = Window.GetWindow(root) ?? Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
            if (window == null) { Keys(pressed); return; }   // a Windows dialog of ours
            SetPadMode(true);
            try { Handle(window, pressed); } catch { }
        }

        static bool Has(GamepadInput.Buttons pressed, GamepadInput.Buttons b) => (pressed & b) != 0;

        static void Handle(Window window, GamepadInput.Buttons pressed)
        {
            var main = window as MainWindow;
            if (_openList != null && !_openList.IsDropDownOpen) _openList = null;

            // An open list: its choices.
            if (_openList != null)
            {
                int count = _openList.Items.Count;
                if (Has(pressed, GamepadInput.Buttons.Up)) MoveInList(-1);
                if (Has(pressed, GamepadInput.Buttons.Down)) MoveInList(+1);
                if (Has(pressed, GamepadInput.Buttons.A))
                {
                    var list = _openList; _openList = null;
                    if (_listIndex >= 0 && _listIndex < count) list.SelectedIndex = _listIndex;
                    list.IsDropDownOpen = false; list.Focus();
                }
                else if (Has(pressed, GamepadInput.Buttons.B))
                {
                    // Back to the choice it had when it opened.
                    var list = _openList; _openList = null;
                    if (_listStart >= 0 && _listStart < count) list.SelectedIndex = _listStart;
                    list.IsDropDownOpen = false; list.Focus();
                }
                return;
            }

            var focused = Keyboard.FocusedElement as DependencyObject;
            bool inWindow = focused != null && Window.GetWindow(focused) == window;

            if (Has(pressed, GamepadInput.Buttons.Back) && main != null) { main.ToggleFullScreen(); return; }
            if (Has(pressed, GamepadInput.Buttons.Start) && main != null) { main.Play(); return; }
            if ((Has(pressed, GamepadInput.Buttons.LB) || Has(pressed, GamepadInput.Buttons.RB)) && main != null)
            {
                CloseTip();
                main.NextPage(Has(pressed, GamepadInput.Buttons.RB) ? +1 : -1);
                window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => main.FocusFirstSetting()));
                return;
            }

            if (!inWindow)
            {
                // Nothing chosen yet: the first setting (main window) or the window's first control.
                if (main != null) main.FocusFirstSetting();
                else window.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
                ShowFrame(Keyboard.FocusedElement as UIElement);
                return;
            }

            var element = (UIElement)focused!;
            if (Has(pressed, GamepadInput.Buttons.Up)) Move(element, FocusNavigationDirection.Up, FocusNavigationDirection.Previous);
            else if (Has(pressed, GamepadInput.Buttons.Down)) Move(element, FocusNavigationDirection.Down, FocusNavigationDirection.Next);
            else if (Has(pressed, GamepadInput.Buttons.Left)) Move(element, FocusNavigationDirection.Left, null);
            else if (Has(pressed, GamepadInput.Buttons.Right)) Move(element, FocusNavigationDirection.Right, null);
            else if (Has(pressed, GamepadInput.Buttons.A)) Activate(element);
            else if (Has(pressed, GamepadInput.Buttons.X)) ToggleTip(element);
            else if (Has(pressed, GamepadInput.Buttons.B))
            {
                if (CloseTip()) return;
                if (main != null) main.FocusPageList();
                else
                {
                    // A dialog: its cancel button, else close it.
                    var cancel = FindAll<Button>(window).FirstOrDefault(b => b.IsCancel && b.IsVisible && b.IsEnabled);
                    if (cancel != null) Click(cancel); else window.Close();
                }
            }
        }

        static void Move(UIElement element, FocusNavigationDirection direction, FocusNavigationDirection? fallback)
        {
            CloseTip();
            var from = element;
            if (!from.MoveFocus(new TraversalRequest(direction)) && fallback.HasValue)
                from.MoveFocus(new TraversalRequest(fallback.Value));
        }

        static void Activate(UIElement element)
        {
            CloseTip();
            switch (element)
            {
                case ComboBox list when list.IsEnabled:
                    _openList = list;
                    _listStart = list.SelectedIndex;
                    _listIndex = Math.Max(0, list.SelectedIndex);
                    list.IsDropDownOpen = true;
                    list.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => MoveInList(0)));
                    break;
                case ComboBoxItem item when ItemsControl.ItemsControlFromItemContainer(item) is ComboBox owner:
                    // The focus already in an open list (opened by the mouse): take that choice.
                    owner.SelectedItem = owner.ItemContainerGenerator.ItemFromContainer(item);
                    owner.IsDropDownOpen = false; owner.Focus();
                    break;
                case ButtonBase button when button.IsEnabled:
                    Click(button);
                    break;
                case TextBox box:
                    box.SelectAll();
                    break;
            }
        }

        static void MoveInList(int step)
        {
            if (_openList == null) return;
            int count = _openList.Items.Count;
            if (count == 0) return;
            _listIndex = Math.Max(0, Math.Min(count - 1, _listIndex + step));
            // The choice is shown chosen as you move (the list itself and the box), the focus stays on the list's box:
            // B puts the first one back. Moving the keyboard focus into the popup lost the controller.
            _openList.SelectedIndex = _listIndex;
            if (_openList.ItemContainerGenerator.ContainerFromIndex(_listIndex) is ComboBoxItem item)
                item.BringIntoView();
        }

        // A click as the mouse makes it: Click event, command, a switch toggled, a page chosen.
        static readonly MethodInfo? OnClick = typeof(ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);
        static void Click(ButtonBase button) => OnClick?.Invoke(button, null);

        // The setting's ⓘ explanation: the tooltip of the element or of its row.
        static void ToggleTip(UIElement element)
        {
            if (CloseTip()) return;
            for (DependencyObject? d = element; d != null; d = VisualTreeHelper.GetParent(d))
            {
                if (d is FrameworkElement fe && fe.ToolTip != null)
                {
                    var tip = fe.ToolTip as ToolTip ?? new ToolTip { Content = fe.ToolTip };
                    if (!(fe.ToolTip is ToolTip)) fe.ToolTip = tip;
                    tip.PlacementTarget = fe;
                    tip.Placement = PlacementMode.Bottom;
                    tip.IsOpen = true;
                    _shownTip = tip;
                    return;
                }
                if (d is Window) break;
            }
        }

        static bool CloseTip()
        {
            if (_shownTip == null) return false;
            _shownTip.IsOpen = false;
            _shownTip = null;
            return true;
        }

        // ---- The focus frame: an accent border around the focused setting (its whole row when it has one). ----

        static void ShowFrame(UIElement? element)
        {
            HideFrame();
            if (element == null || element is ComboBoxItem) return;
            var target = RowOf(element) ?? element;
            var layer = AdornerLayer.GetAdornerLayer(target);
            if (layer == null) return;
            _frame = new FocusFrame(target);
            _frameTarget = target;
            layer.Add(_frame);
        }

        static void HideFrame()
        {
            if (_frame != null && _frameTarget != null)
                AdornerLayer.GetAdornerLayer(_frameTarget)?.Remove(_frame);
            _frame = null;
            _frameTarget = null;
        }

        static UIElement? RowOf(UIElement element)
        {
            for (DependencyObject? d = VisualTreeHelper.GetParent(element); d != null; d = VisualTreeHelper.GetParent(d))
            {
                if (d is Border b && b.Style != null && Equals(b.Style, Application.Current.TryFindResource("SettingRowStyle"))) return b;
                if (d is Window) break;
            }
            return null;
        }

        class FocusFrame : Adorner
        {
            static readonly Pen Pen = MakePen();
            static Pen MakePen()
            {
                var brush = Application.Current.TryFindResource("PrimaryLightBrush") as Brush ?? Brushes.Gold;
                var pen = new Pen(brush, 3);
                pen.Freeze();
                return pen;
            }
            public FocusFrame(UIElement target) : base(target) { IsHitTestVisible = false; }
            protected override void OnRender(DrawingContext dc)
            {
                var size = AdornedElement.RenderSize;
                dc.DrawRoundedRectangle(null, Pen, new Rect(-2, -2, size.Width + 4, size.Height + 4), 11, 11);
            }
        }

        static System.Collections.Generic.IEnumerable<T> FindAll<T>(DependencyObject root) where T : DependencyObject
        {
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T t) yield return t;
                foreach (var sub in FindAll<T>(child)) yield return sub;
            }
        }

        // ---- Windows' own dialogs: keys. ----

        static void Keys(GamepadInput.Buttons pressed)
        {
            if (Has(pressed, GamepadInput.Buttons.Up)) Key(0x26);
            if (Has(pressed, GamepadInput.Buttons.Down)) Key(0x28);
            if (Has(pressed, GamepadInput.Buttons.Left)) Key(0x25);
            if (Has(pressed, GamepadInput.Buttons.Right)) Key(0x27);
            if (Has(pressed, GamepadInput.Buttons.A)) Key(0x0D);
            if (Has(pressed, GamepadInput.Buttons.B)) Key(0x1B);
            if (Has(pressed, GamepadInput.Buttons.RB)) Key(0x09);
            if (Has(pressed, GamepadInput.Buttons.LB)) Key(0x09, shift: true);
        }

        static void Key(ushort vk, bool shift = false)
        {
            var inputs = new System.Collections.Generic.List<INPUT>();
            if (shift) inputs.Add(KeyInput(0x10, false));
            bool extended = vk >= 0x25 && vk <= 0x28;
            inputs.Add(KeyInput(vk, false, extended));
            inputs.Add(KeyInput(vk, true, extended));
            if (shift) inputs.Add(KeyInput(0x10, true));
            SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
        }

        static INPUT KeyInput(ushort vk, bool up, bool extended = false) => new INPUT
        {
            type = 1,
            ki = new KEYBDINPUT { wVk = vk, dwFlags = (up ? 2u : 0u) | (extended ? 1u : 0u) }
        };

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Explicit)]
        struct INPUT
        {
            [FieldOffset(0)] public uint type;
            [FieldOffset(8)] public KEYBDINPUT ki;
            [FieldOffset(8)] MOUSEINPUT mi; // (the union's size)
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

        [DllImport("user32.dll")] static extern uint SendInput(uint count, INPUT[] inputs, int size);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out int pid);
    }

}
