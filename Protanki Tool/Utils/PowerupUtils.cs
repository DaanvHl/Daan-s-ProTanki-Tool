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
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;

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
            if (GameProcess?.MainWindowHandle == null || GameProcess.MainWindowHandle == IntPtr.Zero)
            {
                return;
            }

            ushort virtualKey = (ushort)KeyInterop.VirtualKeyFromKey(key);
            uint scanCode = NativeMethods.MapVirtualKey(virtualKey, 0);

            IntPtr lParamDown = (IntPtr)((scanCode << 16) | 1);
            IntPtr lParamUp = (IntPtr)((scanCode << 16) | 0xC0000001);

            _ = PostMessage(GameProcess.MainWindowHandle, WM_KEYDOWN, virtualKey, lParamDown);
            _ = PostMessage(GameProcess.MainWindowHandle, WM_KEYUP, virtualKey, lParamUp);
        }
    }
}
