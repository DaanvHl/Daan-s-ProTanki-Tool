using ProtankiTool.Hotkeys;
using ProtankiTool.Services;
using ProtankiTool.Settings;
using ProtankiTool.Types;
using ProtankiTool.UI;
using ProtankiTool.UI.Coordinate;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Point = System.Windows.Point;

namespace ProtankiTool
{
    public partial class SettingsWindow : Window
    {
        private AppSettings _settings;
        private bool _isGameProcessNameUnlocked = false;
        private UpdaterService _updaterService;
        private Version? _latestVersion;

        public SettingsWindow()
        {
            InitializeComponent();
            _settings = App.Settings ?? AppSettings.Load();
            _updaterService = new UpdaterService();
            LoadSettings();

            Loaded += SettingsWindow_Loaded;
        }

        private void LoadSettings()
        {
            ClickSpeedTextBox.Text = _settings.ClickSpeed.ToString(CultureInfo.InvariantCulture);

            GameProcessNameTextBox.Text = _settings.GameProcessName;
            GameProcessNameTextBox.IsReadOnly = !_isGameProcessNameUnlocked;
            UnlockGameProcessNameButton.Visibility = _isGameProcessNameUnlocked ? Visibility.Collapsed : Visibility.Visible;

            GlobalHotKeyBox.HotKey = _settings.GlobalHotKey;
            RedTeamHotKeyBox.HotKey = _settings.RedTeamHotKey;
            BlueTeamHotKeyBox.HotKey = _settings.BlueTeamHotKey;
            GoldBoxTimerHotKeyBox.HotKey = _settings.GoldBoxTimerHotKey;
            GamePathTextBox.Text = _settings.GameExecutablePath;

            RedTeamXTextBox.Text = _settings.RedTeamCoordinates.X.ToString(CultureInfo.InvariantCulture);
            RedTeamYTextBox.Text = _settings.RedTeamCoordinates.Y.ToString(CultureInfo.InvariantCulture);
            BlueTeamXTextBox.Text = _settings.BlueTeamCoordinates.X.ToString(CultureInfo.InvariantCulture);
            BlueTeamYTextBox.Text = _settings.BlueTeamCoordinates.Y.ToString(CultureInfo.InvariantCulture);

            if (_settings.Theme == ThemeType.Light)
            {
                LightRadio.IsChecked = true;
            }
            else
            {
                DarkRadio.IsChecked = true;
            }

            TransparentTimerWindowCheckBox.IsChecked = _settings.IsTimerWindowTransparent;

            RegisterSettingsHotkeys();
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            VersionTextBlock.Text = $"Current Version: {_updaterService.GetCurrentVersion()}";
        }

        private async void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            StatusTextBlock.Text = "Checking for updates...";

            try
            {
                (Version? latestVersion, string? releaseNotes) = await _updaterService.GetLatestVersionAsync();
                _latestVersion = latestVersion;
                LatestVersionTextBlock.Text = $"Latest Version: {latestVersion}";

                if (_latestVersion != null && _latestVersion > _updaterService.GetCurrentVersion())
                {
                    StatusTextBlock.Text = "Update available!";
                    UpdateNowButton.Visibility = Visibility.Visible;
                }
                else
                {
                    StatusTextBlock.Text = "You are up to date.";
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error checking for updates: {ex.Message}";
                LogService.LogError($"Error checking for updates: {ex}");
            }
        }

        private async void UpdateNowButton_Click(object sender, RoutedEventArgs e)
        {
            LogService.LogInfo("UpdateNowButton_Click started.");
            UpdateNowButton.IsEnabled = false;
            CheckForUpdatesButton.IsEnabled = false;
            StatusTextBlock.Text = "Downloading update...";
            DownloadProgressBar.Visibility = Visibility.Visible;

            try
            {
                LogService.LogInfo("Calling DownloadAndInstallUpdateAsync.");

                (bool uninstalled, string? installerPath) = await _updaterService.DownloadAndInstallUpdateAsync((bytesReceived, totalBytes) =>
                {
                    _ = Dispatcher.BeginInvoke(() =>
                    {
                        DownloadProgressBar.Value = (double)bytesReceived / totalBytes * 100;
                    });
                });

                LogService.LogInfo($"DownloadAndInstallUpdateAsync finished. Uninstalled: {uninstalled}");

                if (uninstalled)
                {
                    StatusTextBlock.Text = "Update installed. Relaunching...";
                    LogService.LogInfo("Relaunching application.");
                    _updaterService.RelaunchApplication();
                }
                else
                {
                    StatusTextBlock.Text = $"New version downloaded to {installerPath}. Please run it manually.";
                    LogService.LogInfo("Update downloaded, but not installed. Manual installation required.");
                    CheckForUpdatesButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error during update: {ex.Message}";
                LogService.LogError($"Error during update: {ex}");
                UpdateNowButton.IsEnabled = true;
                CheckForUpdatesButton.IsEnabled = true;
                DownloadProgressBar.Visibility = Visibility.Collapsed;
            }

            LogService.LogInfo("UpdateNowButton_Click finished.");
        }

        private void ThemeRadio_Checked(object? sender, RoutedEventArgs e)
        {
            if (DarkRadio.IsChecked == true)
            {
                App.ApplyTheme(ThemeType.Dark);
            }
            else if (LightRadio.IsChecked == true)
            {
                App.ApplyTheme(ThemeType.Light);
            }
        }

        private void BrowseGamePathButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new()
            {
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                Title = "Select Game Executable"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                GamePathTextBox.Text = openFileDialog.FileName;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            FocusManager.SetFocusedElement(this, SaveButton);

            if (double.TryParse(ClickSpeedTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double clickSpeed) && clickSpeed >= 1)
            {
                _settings.ClickSpeed = clickSpeed;
            }
            else
            {
                LogService.LogError("Click speed must be a number greater than or equal to 1.");
                return;
            }
            _settings.GameProcessName = !string.IsNullOrWhiteSpace(GameProcessNameTextBox.Text) ? GameProcessNameTextBox.Text.Trim() : "ProTanki";

            _settings.GameExecutablePath = GamePathTextBox.Text;

            _settings.GlobalHotKey = GlobalHotKeyBox.HotKey;
            _settings.RedTeamHotKey = RedTeamHotKeyBox.HotKey;
            _settings.BlueTeamHotKey = BlueTeamHotKeyBox.HotKey;
            _settings.GoldBoxTimerHotKey = GoldBoxTimerHotKeyBox.HotKey;

            if (double.TryParse(RedTeamXTextBox.Text, out double rX) && double.TryParse(RedTeamYTextBox.Text, out double rY))
            {
                _settings.RedTeamCoordinates = new Point(rX, rY);
            }
            else
            {
                LogService.LogError("Invalid Red Team coordinates.");
                return;
            }

            if (double.TryParse(BlueTeamXTextBox.Text, out double bX) && double.TryParse(BlueTeamYTextBox.Text, out double bY))
            {
                _settings.BlueTeamCoordinates = new Point(bX, bY);
            }
            else
            {
                LogService.LogError("Invalid Blue Team coordinates.");
                return;
            }

            _settings.Theme = DarkRadio.IsChecked == true ? ThemeType.Dark : ThemeType.Light;

            _settings.IsTimerWindowTransparent = TransparentTimerWindowCheckBox.IsChecked ?? false;

            _settings.Save();

            Close();
        }
        private void LogsButton_Click(object sender, RoutedEventArgs e)
        {
            LogViewerWindow logViewer = new() { Owner = this };
            _ = logViewer.ShowDialog();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            FocusManager.SetFocusedElement(this, CloseButton);

            _settings = AppSettings.Load();
            LoadSettings();
            Close();
        }

        private void PickRedTeamButton_Click(object sender, RoutedEventArgs e)
        {
            CoordinatePickerWindow picker = new();
            if (picker.ShowDialog() == true)
            {
                RedTeamXTextBox.Text = picker.SelectedPoint.X.ToString(CultureInfo.InvariantCulture);
                RedTeamYTextBox.Text = picker.SelectedPoint.Y.ToString(CultureInfo.InvariantCulture);
            }
        }

        private void PickBlueTeamButton_Click(object sender, RoutedEventArgs e)
        {
            CoordinatePickerWindow picker = new();
            if (picker.ShowDialog() == true)
            {
                BlueTeamXTextBox.Text = picker.SelectedPoint.X.ToString(CultureInfo.InvariantCulture);
                BlueTeamYTextBox.Text = picker.SelectedPoint.Y.ToString(CultureInfo.InvariantCulture);
            }
        }

        private void UnlockGameProcessNameButton_Click(object sender, RoutedEventArgs e)
        {
            InputDialog inputDialog = new()
            {
                Owner = this
            };

            if (inputDialog.ShowDialog() == true)
            {
                _isGameProcessNameUnlocked = true;
                GameProcessNameTextBox.IsReadOnly = false;
                _ = GameProcessNameTextBox.Focus();
                UnlockGameProcessNameButton.Visibility = Visibility.Collapsed;
                LogService.LogInfo("Game Process Name unlocked for editing.");
            }
            else
            {
                LogService.LogInfo("Game Process Name unlock cancelled or incorrect password entered.");
            }
        }

        private void RegisterSettingsHotkeys()
        {
            GlobalHotKeyManager.UnregisterAll();

            _ = GlobalHotKeyManager.Register(_settings.GlobalHotKey);
            _ = GlobalHotKeyManager.Register(_settings.RedTeamHotKey);
            _ = GlobalHotKeyManager.Register(_settings.BlueTeamHotKey);
            _ = GlobalHotKeyManager.Register(_settings.GoldBoxTimerHotKey);
        }
    }
}