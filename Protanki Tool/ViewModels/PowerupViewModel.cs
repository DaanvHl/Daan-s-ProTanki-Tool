using ProtankiTool.Settings;
using ProtankiTool.Types;
using ProtankiTool.Utils;
using System.ComponentModel;
using System.Windows.Input;

namespace ProtankiTool.ViewModels
{
    public class PowerupViewModel : INotifyPropertyChanged, IDisposable
    {
        private AppSettings _settings;
        private Action<PowerupType> _usePowerupAction;
        private Action<PowerupType, double> _saveDelayAction;
        private System.Threading.Timer? _timer;

        private PowerupType _powerupType;
        public PowerupType PowerupType
        {
            get => _powerupType;
            set
            {
                if (_powerupType != value)
                {
                    _powerupType = value;
                    OnPropertyChanged(nameof(PowerupType));
                    OnPropertyChanged(nameof(ButtonContent));
                }
            }
        }

        private double _delay;
        public double Delay
        {
            get => _delay;
            set
            {
                if (_delay != value)
                {
                    _delay = value;
                    OnPropertyChanged(nameof(Delay));
                    DelayText = $"{_delay:0}ms";
                    if (_isActive) _timer?.Change((int)_delay, (int)_delay);
                    _saveDelayAction?.Invoke(PowerupType, _delay);
                }
            }
        }

        private string _delayText;
        public string DelayText
        {
            get => _delayText;
            set
            {
                if (_delayText != value)
                {
                    _delayText = value;
                    OnPropertyChanged(nameof(DelayText));
                }
            }
        }

        public bool IsGlobalToggleEnabled
        {
            get => _settings.GlobalToggleEnabledPowerups.Contains(_powerupType);
            set
            {
                if (value)
                    _settings.GlobalToggleEnabledPowerups.Add(_powerupType);
                else
                    _settings.GlobalToggleEnabledPowerups.Remove(_powerupType);

                _settings.Save();
                OnPropertyChanged(nameof(IsGlobalToggleEnabled));
            }
        }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged(nameof(IsActive));

                    if (_isActive)
                    {
                        _timer?.Change(0, (int)_delay);
                    }
                    else
                    {
                        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
                    }

                    OnPropertyChanged(nameof(ButtonContent));
                }
            }
        }

        public string ButtonContent => IsActive ? $"Stop {PowerupType}" : $"Start {PowerupType}";

        public ICommand TogglePowerupCommand { get; }

        public PowerupViewModel(
            PowerupType powerupType,
            double initialDelay,
            AppSettings settings,
            Action<PowerupType> usePowerupAction,
            Action<PowerupType, double> saveDelayAction)
        {
            _powerupType = powerupType;
            _delay = initialDelay;
            _delayText = $"{initialDelay:0}ms";
            _settings = settings;
            _usePowerupAction = usePowerupAction;
            _saveDelayAction = saveDelayAction;

            TogglePowerupCommand = new RelayCommand(TogglePowerup);

            _timer = new System.Threading.Timer(_ => _usePowerupAction?.Invoke(PowerupType), null, Timeout.Infinite, Timeout.Infinite);
        }

        private void TogglePowerup(object? parameter)
        {
            IsActive = !IsActive;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
