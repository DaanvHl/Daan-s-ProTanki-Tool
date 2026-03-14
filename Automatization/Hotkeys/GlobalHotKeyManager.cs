using Automatization.Services;
using Automatization.Settings;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace Automatization.Hotkeys
{
    public static class GlobalHotKeyManager
    {
        private const int WmHotkey = 0x0312;
        private const int WH_MOUSE_LL = 14;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_XBUTTONDOWN = 0x020B;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public int ptX, ptY;
            public uint mouseData;
            public uint flags, time;
            public IntPtr dwExtraInfo;
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private static LowLevelMouseProc? _mouseProc;
        private static IntPtr _mouseHookId = IntPtr.Zero;

        private static AppSettings? _settings;
        private static bool _isInitialized = false;
        private static int _nextId;
        private static Dictionary<int, HotKey> IdToHotKeyMap = [];
        private static Dictionary<HotKey, int> HotKeyToIdMap = [];
        private static HashSet<HotKey> _mouseHotKeys = [];

        public static bool IsPaused { get; set; } = false;
        public static event Action<HotKey, Process?>? HotKeyPressed;

        public static void Initialize()
        {
            if (_isInitialized)
            {
                LogService.LogWarning("GlobalHotKeyManager already initialized.");
                return;
            }

            ComponentDispatcher.ThreadFilterMessage += OnThreadFilterMessage;
            _settings = AppSettings.Load();

            _mouseProc = MouseHookCallback;
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule? curModule = curProcess.MainModule)
            {
                if (curModule != null)
                    _mouseHookId = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(curModule.ModuleName), 0);
            }

            _isInitialized = true;
            LogService.LogInfo("Initialized GlobalHotKeyManager.");
        }

        public static bool Register(HotKey hotKey)
        {
            if (!_isInitialized || hotKey.IsEmpty)
            {
                LogService.LogWarning($"Skipping registration for hotkey {hotKey}. Manager not initialized or hotkey is empty.");
                return false;
            }

            if (hotKey.MouseButton.HasValue)
            {
                if (_mouseHotKeys.Contains(hotKey))
                {
                    LogService.LogWarning($"Mouse hotkey {hotKey} is already registered.");
                    return false;
                }
                _ = _mouseHotKeys.Add(hotKey);
                LogService.LogInfo($"Registered mouse hotkey: {hotKey}");
                return true;
            }

            if (HotKeyToIdMap.ContainsKey(hotKey))
            {
                LogService.LogWarning($"Keyboard hotkey {hotKey} is already registered.");
                return false;
            }

            int vk = KeyInterop.VirtualKeyFromKey(hotKey.Key);
            uint fsModifiers = (uint)hotKey.Modifiers;

            int id = _nextId++;
            bool success = RegisterHotKey(IntPtr.Zero, id, fsModifiers, (uint)vk);

            if (success)
            {
                IdToHotKeyMap[id] = hotKey;
                HotKeyToIdMap[hotKey] = id;
                LogService.LogInfo($"Registered hotkey: {hotKey} with ID {id}");
            }
            else
            {
                LogService.LogWarning($"Failed to register hotkey: {hotKey}. Error Code: {Marshal.GetLastWin32Error()}");
            }

            return success;
        }

        public static bool TryUnregister(HotKey hotKey)
        {
            if (!_isInitialized)
            {
                LogService.LogWarning($"Failed to unregister hotkey {hotKey}: Manager not initialized.");
                return false;
            }

            if (hotKey.MouseButton.HasValue)
            {
                bool removed = _mouseHotKeys.Remove(hotKey);
                LogService.LogInfo($"Unregistered mouse hotkey: {hotKey}");
                return removed;
            }

            if (!HotKeyToIdMap.TryGetValue(hotKey, out int id))
            {
                LogService.LogWarning($"Failed to unregister hotkey {hotKey}: Not found in map.");
                return false;
            }

            bool success = UnregisterHotKey(IntPtr.Zero, id);
            if (success)
                LogService.LogInfo($"Successfully unregistered hotkey from OS: {hotKey} with ID {id}");
            else
                LogService.LogWarning($"Failed to unregister hotkey from OS: {hotKey}. Error Code: {Marshal.GetLastWin32Error()}");

            _ = IdToHotKeyMap.Remove(id);
            _ = HotKeyToIdMap.Remove(hotKey);

            LogService.LogInfo($"Unregistered hotkey from manager: {hotKey}");
            return success;
        }

        public static void UnregisterAll()
        {
            if (!_isInitialized)
                return;

            LogService.LogInfo($"Unregistering all hotkeys. Currently {IdToHotKeyMap.Count} keyboard + {_mouseHotKeys.Count} mouse hotkeys registered.");

            foreach (KeyValuePair<int, HotKey> entry in IdToHotKeyMap.ToList())
            {
                bool success = UnregisterHotKey(IntPtr.Zero, entry.Key);
                if (success)
                    LogService.LogInfo($"Successfully unregistered hotkey from OS: {entry.Value} with ID {entry.Key}");
                else
                    LogService.LogWarning($"Failed to unregister hotkey from OS: {entry.Value}. Error Code: {Marshal.GetLastWin32Error()}");
            }

            IdToHotKeyMap.Clear();
            HotKeyToIdMap.Clear();
            _mouseHotKeys.Clear();

            LogService.LogInfo("All hotkeys unregistered.");
        }

        public static IEnumerable<HotKey> GetCurrentRegisteredHotkeys()
        {
            return HotKeyToIdMap.Keys.Concat(_mouseHotKeys).ToList();
        }

        private static void OnThreadFilterMessage(ref MSG msg, ref bool handled)
        {
            if (handled || IsPaused || msg.message != WmHotkey)
                return;

            Process? game = Process.GetProcessesByName(_settings?.GameProcessName ?? "ProTanki").FirstOrDefault();
            if (game == null || Utils.WindowUtils.IsGameWindowInForeground(game) == false)
            {
                handled = false;
                return;
            }

            int id = msg.wParam.ToInt32();
            if (IdToHotKeyMap.TryGetValue(id, out HotKey? hotKey))
            {
                HotKeyPressed?.Invoke(hotKey, game);
                handled = true;
                LogService.LogInfo($"Hotkey pressed: {hotKey}");
            }
        }

        private static IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && !IsPaused && _mouseHotKeys.Count > 0)
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
                    Process? game = Process.GetProcessesByName(_settings?.GameProcessName ?? "ProTanki").FirstOrDefault();
                    if (game != null && Utils.WindowUtils.IsGameWindowInForeground(game))
                    {
                        HotKey? matchingHotKey = _mouseHotKeys.FirstOrDefault(hk => hk.MouseButton == button);
                        if (matchingHotKey != null)
                        {
                            HotKeyPressed?.Invoke(matchingHotKey, game);
                            LogService.LogInfo($"Mouse hotkey pressed: {matchingHotKey}");
                        }
                    }
                }
            }

            return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
        }

        public static void Shutdown()
        {
            if (!_isInitialized)
                return;

            UnregisterAll();

            ComponentDispatcher.ThreadFilterMessage -= OnThreadFilterMessage;

            if (_mouseHookId != IntPtr.Zero)
            {
                _ = UnhookWindowsHookEx(_mouseHookId);
                _mouseHookId = IntPtr.Zero;
                _mouseProc = null;
            }

            _isInitialized = false;
            LogService.LogInfo("Shutting down GlobalHotKeyManager.");
        }
    }
}
