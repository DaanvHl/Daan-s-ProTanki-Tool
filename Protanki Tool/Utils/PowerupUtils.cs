using ProtankiTool.Services;
using ProtankiTool.Settings;
using ProtankiTool.Types;
using ProtankiTool.ViewModels;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Linq;

namespace ProtankiTool.Utils
{
    public class PowerupUtils
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        public AppSettings Settings { get; set; }
        public Process? GameProcess { get; set; }

        private ObservableCollection<PowerupViewModel> _powerups = [];
        public ReadOnlyObservableCollection<PowerupViewModel> Powerups { get; }

        public bool AreAnyPowerupsActive => _powerups.Any(vm => vm.IsActive);

        public PowerupUtils(AppSettings settings)
        {
            Settings = settings;
            Powerups = new ReadOnlyObservableCollection<PowerupViewModel>(_powerups);
        }

        public void Initialize()
        {
            LogService.LogInfo("Initializing PowerupUtils.");

            _powerups.Clear();

            bool settingsChanged = false;

            foreach (PowerupType powerup in Enum.GetValues<PowerupType>())
            {
                if (!Settings.PowerupDelays.TryGetValue(powerup, out double value))
                {
                    Settings.PowerupDelays[powerup] = 1000;
                    settingsChanged = true;
                    LogService.LogInfo($"Default delay set for {powerup}: 1000ms.");
                }

                PowerupViewModel viewModel = new(powerup, Settings.PowerupDelays[powerup], Settings, UsePowerup, SaveDelay);
                _powerups.Add(viewModel);
            }

            if (settingsChanged)
            {
                Settings.Save();
                LogService.LogInfo("Powerup settings saved due to changes.");
            }

            LogService.LogInfo("PowerupUtils initialized.");
        }

        public void UpdateSettings(AppSettings newSettings)
        {
            LogService.LogInfo("Updating PowerupUtils settings.");

            Settings = newSettings;

            foreach (PowerupViewModel viewModel in _powerups)
            {
                if (Settings.PowerupDelays.TryGetValue(viewModel.PowerupType, out double newDelay))
                {
                    viewModel.Delay = newDelay;
                }
            }

            LogService.LogInfo("PowerupUtils settings updated.");
        }

        private void SaveDelay(PowerupType powerup, double delay)
        {
            Settings.PowerupDelays[powerup] = delay;
            Settings.Save();

            LogService.LogInfo($"Powerup {powerup} delay saved to {delay}ms.");
        }

        public bool ToggleAll()
        {
            LogService.LogInfo("Toggling global-toggle powerups.");

            bool anyActive = _powerups
                .Where(vm => Settings.GlobalToggleEnabledPowerups.Contains(vm.PowerupType))
                .Any(vm => vm.IsActive);
            bool newState = !anyActive;

            foreach (PowerupViewModel viewModel in _powerups)
            {
                if (Settings.GlobalToggleEnabledPowerups.Contains(viewModel.PowerupType))
                {
                    viewModel.IsActive = newState;
                }
            }

            LogService.LogInfo($"Global-toggle powerups set to state: {newState}.");
            return newState;
        }

        public void StopAll()
        {
            LogService.LogInfo("Stopping all powerups.");

            foreach (PowerupViewModel viewModel in _powerups)
            {
                viewModel.IsActive = false;
            }

            LogService.LogInfo("All powerups stopped.");
        }

        public void StartAll()
        {
            LogService.LogInfo("Starting all powerups.");

            foreach (PowerupViewModel viewModel in _powerups)
            {
                viewModel.IsActive = true;
            }

            LogService.LogInfo("All powerups started.");
        }

        public List<PowerupType> GetActivePowerupTypes()
        {
            return _powerups.Where(vm => vm.IsActive).Select(vm => vm.PowerupType).ToList();
        }

        public void RestorePowerups(List<PowerupType> powerupTypes)
        {
            LogService.LogInfo($"Restoring {powerupTypes.Count} powerups.");

            foreach (PowerupViewModel viewModel in _powerups)
            {
                if (powerupTypes.Contains(viewModel.PowerupType))
                    viewModel.IsActive = true;
            }
        }

        public void UsePowerup(PowerupType powerup)
        {
            if (GameProcess == null)
            {
                LogService.LogWarning("Game process not found. Cannot send powerup key press.");
                return;
            }

            try
            {
                if (!Settings.PowerupKeys.TryGetValue(powerup, out Key key))
                {
                    LogService.LogWarning($"Attempted to use powerup {powerup}, but no key is assigned.");
                    return;
                }

                SendKey(key);

                LogService.LogInfo($"Sent powerup {powerup} with key {key}.");
            }
            catch (Exception ex)
            {
                LogService.LogError($"Error using powerup {powerup}.", ex);
            }
        }

        private void SendKey(Key key)
        {
            ushort virtualKey = (ushort)KeyInterop.VirtualKeyFromKey(key);
            ushort scanCode = (ushort)NativeMethods.MapVirtualKey(virtualKey, 0);

            INPUT[] inputs = new INPUT[2];

            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = virtualKey;
            inputs[0].U.ki.wScan = scanCode;
            inputs[0].U.ki.dwFlags = 0;
            inputs[0].U.ki.time = 0;
            inputs[0].U.ki.dwExtraInfo = IntPtr.Zero;

            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = virtualKey;
            inputs[1].U.ki.wScan = scanCode;
            inputs[1].U.ki.dwFlags = KEYEVENTF_KEYUP;
            inputs[1].U.ki.time = 0;
            inputs[1].U.ki.dwExtraInfo = IntPtr.Zero;

            _ = SendInput(2, inputs, Marshal.SizeOf<INPUT>());
        }
    }
}
