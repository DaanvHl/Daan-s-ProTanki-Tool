using Automatization.Settings;
using Automatization.Types;
using System.Diagnostics;

namespace Automatization.Services
{
    public class ClickerService : IDisposable
    {
        private Dictionary<Guid, System.Threading.Timer> _timers = [];
        private AppSettings _settings;

        public ClickerService(AppSettings settings)
        {
            _settings = settings;
            LogService.LogInfo("Clicker service created.");
        }

        public double ClickSpeed
        {
            get => _settings.ClickSpeed;
            set
            {
                _settings.ClickSpeed = value;
                UpdateTimersInterval();
            }
        }

        public Guid Register(Action<IntPtr, ClickType> clickAction, ClickType clickType, string gameProcessName)
        {
            Guid id = Guid.NewGuid();

            System.Threading.Timer timer = new(
                _ =>
                {
                    Process? gameProcess = Process.GetProcessesByName(gameProcessName).FirstOrDefault();
                    if (gameProcess != null && gameProcess.MainWindowHandle != IntPtr.Zero)
                    {
                        clickAction(gameProcess.MainWindowHandle, clickType);
                    }
                },
                null,
                0,
                (int)ClickSpeed);

            _timers[id] = timer;
            return id;
        }

        public void Unregister(Guid id)
        {
            if (_timers.TryGetValue(id, out System.Threading.Timer? timerInfo))
            {
                timerInfo.Dispose();
                _ = _timers.Remove(id);

                LogService.LogInfo($"Clicker unregistered with ID: {id}");
            }
        }

        public void Dispose()
        {
            foreach (System.Threading.Timer timer in _timers.Values)
            {
                timer.Dispose();
            }

            _timers.Clear();

            LogService.LogInfo("Clicker service disposed.");
        }

        private void UpdateTimersInterval()
        {
            foreach (System.Threading.Timer timer in _timers.Values)
            {
                _ = timer.Change(0, (int)ClickSpeed);
            }

            LogService.LogInfo($"Click speed updated to: {ClickSpeed}ms");
        }
    }
}