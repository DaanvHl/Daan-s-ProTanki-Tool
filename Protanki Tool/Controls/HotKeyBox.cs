using ProtankiTool.Hotkeys;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;

namespace ProtankiTool.Controls
{
    public class HotKeyBox : TextBox
    {
        #region Win32 Mouse Hook
        private const int WH_MOUSE_LL = 14;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_XBUTTONDOWN = 0x020B;

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public int ptX, ptY;
            public uint mouseData;
            public uint flags, time;
            public IntPtr dwExtraInfo;
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        private LowLevelMouseProc? _mouseProc;
        private IntPtr _mouseHookId = IntPtr.Zero;
        #endregion

        public static DependencyProperty HotKeyProperty =
            DependencyProperty.Register(nameof(HotKey), typeof(HotKey), typeof(HotKeyBox),
                new FrameworkPropertyMetadata(new HotKey(), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHotKeyChanged));

        public HotKey HotKey
        {
            get => (HotKey)GetValue(HotKeyProperty);
            set => SetValue(HotKeyProperty, value);
        }

        private static void OnHotKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HotKeyBox box)
            {
                HotKey newHotKey = e.NewValue as HotKey ?? new HotKey();
                box.Text = newHotKey.ToString();
            }
        }

        public HotKeyBox()
        {
            IsReadOnly = true;
            IsReadOnlyCaretVisible = false;
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            e.Handled = true;

            ModifierKeys modifiers = Keyboard.Modifiers;
            Key key = e.Key;

            if (key == Key.System)
                key = e.SystemKey;

            if (key is Key.LeftCtrl or Key.RightCtrl or
                Key.LeftShift or Key.RightShift or
                Key.LeftAlt or Key.RightAlt or
                Key.LWin or Key.RWin)
                return;

            if (key is Key.Back or Key.Delete)
            {
                HotKey = new HotKey();
                return;
            }

            if (modifiers != ModifierKeys.None || IsValidKey(key))
                HotKey = new HotKey(key, modifiers);

            _ = Keyboard.Focus(this);
        }

        private static bool IsValidKey(Key key)
        {
            return key is (>= Key.F1 and <= Key.F24) or
                   (>= Key.D0 and <= Key.D9) or
                   (>= Key.A and <= Key.Z) or
                   (>= Key.NumPad0 and <= Key.NumPad9) or
                   Key.Tab or Key.Enter or Key.Space or
                   Key.OemTilde or Key.OemMinus or Key.OemPlus or
                   Key.OemOpenBrackets or Key.OemCloseBrackets or
                   Key.OemPipe or Key.OemSemicolon or Key.OemQuotes or
                   Key.OemComma or Key.OemPeriod or Key.OemQuestion;
        }

        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnGotKeyboardFocus(e);
            GlobalHotKeyManager.IsPaused = true;

            SelectAll();
            Text = "Press a key or mouse button...";

            _mouseProc = MouseHookCallback;
            using Process curProcess = Process.GetCurrentProcess();
            using ProcessModule? curModule = curProcess.MainModule;
            if (curModule != null)
                _mouseHookId = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(curModule.ModuleName), 0);
        }

        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnLostKeyboardFocus(e);
            GlobalHotKeyManager.IsPaused = false;
            Text = HotKey.ToString();

            if (_mouseHookId != IntPtr.Zero)
            {
                _ = UnhookWindowsHookEx(_mouseHookId);
                _mouseHookId = IntPtr.Zero;
                _mouseProc = null;
            }
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && IsKeyboardFocused)
            {
                int msg = wParam.ToInt32();
                MouseButton? button = null;

                if (msg == WM_MBUTTONDOWN)
                {
                    button = MouseButton.Middle;
                }
                else if (msg == WM_XBUTTONDOWN)
                {
                    MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    int xBtn = (int)(hookStruct.mouseData >> 16) & 0xFFFF;
                    button = xBtn == 1 ? MouseButton.XButton1 : MouseButton.XButton2;
                }

                if (button.HasValue)
                {
                    MouseButton captured = button.Value;
                    _ = Dispatcher.BeginInvoke(() => HotKey = new HotKey(captured));
                }
            }

            return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
        }
    }
}
