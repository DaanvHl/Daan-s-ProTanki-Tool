using Automatization.Hotkeys;
using Automatization.Listeners;
using Automatization.Services;
using Automatization.Settings;
using Automatization.Types;
using Automatization.UI;
using Automatization.Utils;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace Automatization
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _gameCheckTimer = null!;
        private PowerupUtils? _powerupUtils;
        private Process? _gameProcess;
        private string? _gameExecutablePath;
        private AppSettings _settings;

        private ClickerService? _clickerService;
        private Guid? _redClickerId;
        private Guid? _blueClickerId;

        private Dictionary<string, Action> _hotkeyActions = [];
        private NotifyIcon? _notifyIcon;
        private KeyboardListener? _keyboardListener;
        private TimerWindow? _timerWindow;
        private bool _arePowerupsPausedForChat = false;
        private List<PowerupType> _activePowerupsBeforeChat = [];
        private CancellationTokenSource? _chatResumeCts;

        private bool _isRailgunModeActive = false;
        private RailgunOverlayWindow? _railgunOverlay;
        private MouseListener? _mouseListener;

        #region Win32
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        private static IntPtr MakeLParam(int x, int y)
        {
            return (y << 16) | (x & 0xFFFF);
        }
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_RBUTTONDOWN = 0x0204;
        private const uint WM_RBUTTONUP = 0x0205;
        private const uint WM_MBUTTONDOWN = 0x0207;
        private const uint WM_MBUTTONUP = 0x0208;
        #endregion

        public MainWindow()
        {
            InitializeComponent();
            LogService.LogInfo("Initializing main window.");
            _settings = AppSettings.Load();
            _powerupUtils = new PowerupUtils(_settings);
            DataContext = _powerupUtils;

            ClickTypeComboBox.ItemsSource = Enum.GetValues(typeof(ClickType));
            ClickTypeComboBox.SelectedIndex = 0;

            CheckGameInstallation();
            InitializeGameCheckTimer();

            SourceInitialized += OnSourceInitialized;
            ToggleAllButton.Click += (_, _) => _powerupUtils?.ToggleAll();

            DisableAutomation();
            _ = GameCheckAsync();

            PauseHotkeysCheckBox.IsChecked = GlobalHotKeyManager.IsPaused;
            PauseHotkeysCheckBox.IsEnabled = true;

            InitializeNotifyIcon();
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            LogService.LogInfo("Main window source initialized.");

            InitializeTeamClickers();
            _powerupUtils?.Initialize();

            GlobalHotKeyManager.Initialize();
            GlobalHotKeyManager.HotKeyPressed += OnHotKeyPressed;
            RegisterHotkeysFromSettings();

            _keyboardListener = new KeyboardListener();
            _keyboardListener.KeyDown += KeyboardListener_OnKeyPressed;

            _hotkeyActions["ToggleAll"] = () => _powerupUtils?.ToggleAll();
            _hotkeyActions["RedTeam"] = () => RedTeamButton_Click(this, null);
            _hotkeyActions["BlueTeam"] = () => BlueTeamButton_Click(this, null);
            _hotkeyActions["StartTimer"] = StartGoldBoxTimer;
            _hotkeyActions["RailgunMode"] = ToggleRailgunMode;

            _railgunOverlay = new RailgunOverlayWindow();

            _mouseListener = new MouseListener();
            _mouseListener.LeftButtonDown += () =>
            {
                if (!_isRailgunModeActive) return;
                if (_gameProcess == null || !WindowUtils.IsGameWindowInForeground(_gameProcess)) return;
                if (_arePowerupsPausedForChat) return;
                _ = Dispatcher.BeginInvoke(ShowRailgunLine);
            };
        }

        private bool KeyboardListener_OnKeyPressed(Key e)
        {
            if (_gameProcess == null || !WindowUtils.IsGameWindowInForeground(_gameProcess))
                return false;

            if (e == Key.Enter)
            {
                if (!_arePowerupsPausedForChat)
                {
                    // Cancel any pending resume from a previous close
                    _chatResumeCts?.Cancel();

                    _activePowerupsBeforeChat = _powerupUtils?.GetActivePowerupTypes() ?? [];

                    if (_activePowerupsBeforeChat.Count > 0)
                    {
                        _powerupUtils?.StopAll();
                        LogService.LogInfo("Enter key pressed in-game, pausing all powerups for chat.");
                    }
                    else
                    {
                        LogService.LogInfo("Enter key pressed in-game, entering chat while no powerups are active.");
                    }

                    _arePowerupsPausedForChat = true;
                }
                else
                {
                    // Closing chat by sending (Enter)
                    CloseChatAndResume();
                }

                return false;
            }
            else if (e == Key.Escape && _arePowerupsPausedForChat)
            {
                // Closing chat by canceling (Escape)
                CloseChatAndResume();
                return false;
            }
            else if (e == Key.Space && _isRailgunModeActive && !_arePowerupsPausedForChat)
            {
                ShowRailgunLine();
                return false;
            }
            else if (_settings.PowerupKeys.ContainsValue(e))
            {
                PowerupType powerupType = _settings.PowerupKeys.FirstOrDefault(x => x.Value == e).Key;
                _powerupUtils?.UsePowerup(powerupType);
                LogService.LogInfo($"Powerup key '{e}' pressed in-game, using {powerupType}.");
                return true;
            }

            return false;
        }

        private void CloseChatAndResume()
        {
            _arePowerupsPausedForChat = false;

            if (_activePowerupsBeforeChat.Count == 0)
            {
                LogService.LogInfo("Chat closed, powerups stay inactive because they were not active before chat.");
                return;
            }

            // Cancel any previous pending resume before starting a new one
            _chatResumeCts?.Cancel();
            _chatResumeCts = new CancellationTokenSource();
            CancellationToken token = _chatResumeCts.Token;
            List<PowerupType> toRestore = _activePowerupsBeforeChat;

            _ = Dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    await Task.Delay(500, token);
                    _powerupUtils?.RestorePowerups(toRestore);
                    LogService.LogInfo("Resuming powerups after chat closed.");
                }
                catch (TaskCanceledException)
                {
                    LogService.LogInfo("Powerup resume cancelled (chat re-opened during delay).");
                }
            });
        }

        private void OnHotKeyPressed(HotKey hotKey, Process? gameProcess)
        {
            LogService.LogInfo($"Hotkey pressed: {hotKey}");

            if (_powerupUtils != null)
            {
                _powerupUtils.GameProcess = _gameProcess;
            }

            string? actionEntry = _settings.GetActionForHotKey(hotKey);

            if (actionEntry != null && _hotkeyActions.TryGetValue(actionEntry, out Action? action))
            {
                action.Invoke();
                return;
            }

            KeyValuePair<PowerupType, Key> powerupMapping = _settings.PowerupKeys.FirstOrDefault(kvp => kvp.Value == hotKey.Key);

            if (powerupMapping.Key != default)
            {
                _powerupUtils?.UsePowerup(powerupMapping.Key);

                LogService.LogInfo($"Used powerup {powerupMapping.Key} via hotkey.");
                return;
            }
        }

        public void RegisterHotkeysFromSettings()
        {
            GlobalHotKeyManager.UnregisterAll();

            _ = GlobalHotKeyManager.Register(_settings.GlobalHotKey);
            _ = GlobalHotKeyManager.Register(_settings.RedTeamHotKey);
            _ = GlobalHotKeyManager.Register(_settings.BlueTeamHotKey);
            _ = GlobalHotKeyManager.Register(_settings.GoldBoxTimerHotKey);
            _ = GlobalHotKeyManager.Register(_settings.RailgunModeHotKey);

        }

        private void ToggleRailgunMode()
        {
            _isRailgunModeActive = !_isRailgunModeActive;
            Status = _isRailgunModeActive ? "Railgun Mode: ACTIVE" : "Railgun Mode: OFF";
            LogService.LogInfo($"Railgun mode toggled: {_isRailgunModeActive}");
        }

        private void ShowRailgunLine()
        {
            _railgunOverlay?.ShowLine();
        }

        private void StartGoldBoxTimer()
        {
            if (_gameProcess == null || !WindowUtils.IsGameWindowInForeground(_gameProcess))
            {
                LogService.LogWarning("Gold Box Timer hotkey pressed, but game window is not in the foreground.");
                return;
            }

            if (_timerWindow != null && _timerWindow.IsLoaded)
            {
                // A timer is already running. Do nothing as requested.
                LogService.LogInfo("Gold Box Timer hotkey pressed, but a timer is already active. Ignoring.");
                return;
            }

            _timerWindow = new TimerWindow(startTimer: true);
            _timerWindow.Closed += (sender, args) =>
            {
                _timerWindow = null;
            };
            _timerWindow.Show();
        }

        #region Team Auto-Clickers

        private void InitializeTeamClickers()
        {
            _clickerService = new ClickerService(_settings);
        }

        private void ClickTeamButton(IntPtr windowHandle, int x, int y, ClickType clickType)
        {
            _ = Dispatcher.BeginInvoke(() =>
            {
                LogService.LogInfo($"Clicking team button at ({x}, {y}) with {clickType} click.");
            });

            IntPtr lParam = MakeLParam(x, y);

            switch (clickType)
            {
                case ClickType.Left:
                    _ = PostMessage(windowHandle, WM_LBUTTONDOWN, IntPtr.Zero, lParam);
                    _ = PostMessage(windowHandle, WM_LBUTTONUP, IntPtr.Zero, lParam);
                    break;
                case ClickType.Right:
                    _ = PostMessage(windowHandle, WM_RBUTTONDOWN, IntPtr.Zero, lParam);
                    _ = PostMessage(windowHandle, WM_RBUTTONUP, IntPtr.Zero, lParam);
                    break;
                case ClickType.Middle:
                    _ = PostMessage(windowHandle, WM_MBUTTONDOWN, IntPtr.Zero, lParam);
                    _ = PostMessage(windowHandle, WM_MBUTTONUP, IntPtr.Zero, lParam);
                    break;
                case ClickType.DoubleClick:
                    _ = PostMessage(windowHandle, WM_LBUTTONDOWN, IntPtr.Zero, lParam);
                    _ = PostMessage(windowHandle, WM_LBUTTONUP, IntPtr.Zero, lParam);
                    _ = PostMessage(windowHandle, WM_LBUTTONDOWN, IntPtr.Zero, lParam);
                    _ = PostMessage(windowHandle, WM_LBUTTONUP, IntPtr.Zero, lParam);
                    break;
            }
        }

        private void RedTeamButton_Click(object sender, RoutedEventArgs? e)
        {
            if (_redClickerId.HasValue)
            {
                _clickerService?.Unregister(_redClickerId.Value);
                _redClickerId = null;

                Dispatcher.Invoke(() =>
                {
                    RedTeamButton.Content = "Auto Red Team";
                    Status = "Red Team auto-clicker stopped";
                });

                LogService.LogInfo("Red Team auto-clicker stopped.");
            }
            else
            {
                ClickType clickType = (ClickType)ClickTypeComboBox.SelectedItem;

                _redClickerId = _clickerService?.Register(
                    (handle, ct) => ClickTeamButton(handle, (int)_settings.RedTeamCoordinates.X, (int)_settings.RedTeamCoordinates.Y, ct),
                    clickType,
                    _settings.GameProcessName
                );

                Dispatcher.Invoke(() =>
                {
                    RedTeamButton.Content = "Stop Red Team";
                    Status = "Red Team auto-clicker started";
                });

                LogService.LogInfo("Red Team auto-clicker started.");
            }
        }

        private void BlueTeamButton_Click(object sender, RoutedEventArgs? e)
        {
            if (_blueClickerId.HasValue)
            {
                _clickerService?.Unregister(_blueClickerId.Value);

                _blueClickerId = null;

                Dispatcher.Invoke(() =>
                {
                    BlueTeamButton.Content = "Auto Blue Team";
                    Status = "Blue Team auto-clicker stopped";
                });

                LogService.LogInfo("Blue Team auto-clicker stopped.");
            }
            else
            {
                ClickType clickType = (ClickType)ClickTypeComboBox.SelectedItem;

                _blueClickerId = _clickerService?.Register(
                    (handle, ct) => ClickTeamButton(handle, (int)_settings.BlueTeamCoordinates.X, (int)_settings.BlueTeamCoordinates.Y, ct),
                    clickType,
                    _settings.GameProcessName
                );

                Dispatcher.Invoke(() =>
                {
                    BlueTeamButton.Content = "Stop Blue Team";
                    Status = "Blue Team auto-clicker started";
                });

                LogService.LogInfo("Blue Team auto-clicker started.");
            }
        }

        #endregion

        private string Status
        {
            set => Dispatcher.Invoke(() => StatusText.Text = value);
        }

        #region Game Check Timer
        private void InitializeGameCheckTimer()
        {
            _gameCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _gameCheckTimer.Tick += async (_, _) => await GameCheckAsync();
            _gameCheckTimer.Start();
        }

        private async Task GameCheckAsync()
        {
            Process? game = await Task.Run(() => Process.GetProcessesByName(_settings.GameProcessName).FirstOrDefault());

            if (game != null)
            {
                if (_gameProcess == null || _gameProcess.Id != game.Id)
                {
                    _gameProcess = game;
                    if (_powerupUtils != null)
                    {
                        _powerupUtils.GameProcess = _gameProcess;
                    }

                    EnableAutomation();
                    LogService.LogInfo("Game process found.");
                }
            }
            else
            {
                if (_gameProcess != null)
                {
                    _gameProcess = null;
                    if (_powerupUtils != null)
                    {
                        _powerupUtils.GameProcess = null;
                    }

                    DisableAutomation();
                    LogService.LogInfo("Game process lost.");
                }
            }
        }
        #endregion

        #region Automation UI
        private void EnableAutomation()
        {
            PowerupsGroup.Visibility = Visibility.Visible;
            ToggleAllButton.Visibility = Visibility.Visible;
            LaunchButton.Visibility = Visibility.Collapsed;
            Status = "Game is running!";

            LogService.LogInfo("Automation enabled.");
        }

        private void DisableAutomation()
        {
            _powerupUtils?.StopAll();
            PowerupsGroup.Visibility = Visibility.Collapsed;
            ToggleAllButton.Visibility = Visibility.Collapsed;
            LaunchButton.Visibility = Visibility.Visible;
            Status = "Game not running. Click Launch to start.";

            LogService.LogInfo("Automation disabled.");
        }
        #endregion

        #region Game Launch / Installation
        private void CheckGameInstallation()
        {
            if (!string.IsNullOrEmpty(_settings.GameExecutablePath) && File.Exists(Path.Combine(_settings.GameExecutablePath, _settings.GameProcessName + ".exe")))
            {
                _gameExecutablePath = Path.Combine(_settings.GameExecutablePath, _settings.GameProcessName + ".exe");
            }
            else
            {
                string? found = FindExecutableFromUninstallRegistry() ?? SearchCommonInstallDirectories();

                if (found != null)
                {
                    _gameExecutablePath = found;
                    _settings.GameExecutablePath = Path.GetDirectoryName(found) ?? string.Empty;
                    _settings.Save();
                }
            }
        }

        private static string? FindExecutableFromUninstallRegistry()
        {
            string gameProcessName = AppSettings.Load().GameProcessName;
            string[] registryPaths =
            [
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            ];

            foreach (string path in registryPaths)
            {
                foreach (RegistryKey baseKey in new[] { Registry.LocalMachine, Registry.CurrentUser })
                {
                    using RegistryKey? key = baseKey.OpenSubKey(path);
                    if (key == null)
                    {
                        continue;
                    }

                    foreach (string subKeyName in key.GetSubKeyNames())
                    {
                        using RegistryKey? subKey = key.OpenSubKey(subKeyName);
                        if (subKey == null)
                        {
                            continue;
                        }

                        string? displayName = subKey.GetValue("DisplayName")?.ToString();
                        if (displayName != null && displayName.Contains(gameProcessName + " Online", StringComparison.OrdinalIgnoreCase))
                        {
                            string? installLocation = subKey.GetValue("InstallLocation")?.ToString();
                            if (!string.IsNullOrEmpty(installLocation))
                            {
                                string executablePath = Path.Combine(installLocation, gameProcessName + ".exe");
                                if (File.Exists(executablePath))
                                {
                                    return executablePath;
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }

        private static string? SearchCommonInstallDirectories()
        {
            string gameProcessName = AppSettings.Load().GameProcessName;
            List<string> commonPaths =
            [
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
            ];

            foreach (string basePath in commonPaths)
            {
                string directPath = Path.Combine(basePath, gameProcessName + ".exe");
                if (File.Exists(directPath))
                {
                    return directPath;
                }

                string subDirPath = Path.Combine(basePath, gameProcessName + " Online", gameProcessName + ".exe");
                if (File.Exists(subDirPath))
                {
                    return subDirPath;
                }

                subDirPath = Path.Combine(basePath, gameProcessName, gameProcessName + ".exe");
                if (File.Exists(subDirPath))
                {
                    return subDirPath;
                }
            }

            return null;
        }

        private void LaunchGame()
        {
            try
            {
                if (!File.Exists(_gameExecutablePath))
                {
                    Status = "Game executable not found. Please select it.";
                    LogService.LogWarning("Game executable not found.");

                    OpenFileDialog dlg = new()
                    {
                        Filter = "Executable files (*.exe)|*.exe",
                        Title = $"Select {_settings.GameProcessName} Online executable"
                    };

                    if (dlg.ShowDialog() == true)
                    {
                        _gameExecutablePath = dlg.FileName;
                        _settings.GameExecutablePath = Path.GetDirectoryName(dlg.FileName);
                        _settings.Save();

                        LogService.LogInfo($"Game executable path set to: {_gameExecutablePath}");
                    }
                    else
                    {
                        return;
                    }
                }

                LogService.LogInfo("Launching game.");

                _gameProcess = Process.Start(new ProcessStartInfo { FileName = _gameExecutablePath, UseShellExecute = true });

                if (_gameProcess != null)
                {
                    EnableAutomation();
                }
            }
            catch (Exception ex)
            {
                Status = "Error launching game.";
                LogService.LogError("Error launching game.", ex);
            }
        }
        #endregion

        #region UI Event Handlers
        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            LogService.LogInfo("Launch button clicked.");
            LaunchGame();
        }

        private void GameDataButton_Click(object sender, RoutedEventArgs e)
        {
            GameDataWindow wnd = new() { Owner = this };
            wnd.ShowDialog();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            LogService.LogInfo("Settings button clicked.");

            SettingsWindow wnd = new() { Owner = this };
            _ = wnd.ShowDialog();

            _settings = AppSettings.Load();
            if (_clickerService != null)
            {
                _clickerService.ClickSpeed = _settings.ClickSpeed;
            }

            if (_powerupUtils != null)
            {
                _powerupUtils.Settings = _settings;
                _powerupUtils.Initialize();
            }

            App.ApplyTheme(_settings.Theme);

            RegisterHotkeysFromSettings();
        }

        private void PauseHotkeysCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            GlobalHotKeyManager.IsPaused = true;
            LogService.LogInfo("Hotkeys paused by checkbox.");
        }

        private void PauseHotkeysCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            GlobalHotKeyManager.IsPaused = false;
            LogService.LogInfo("Hotkeys resumed by checkbox.");
        }
        #endregion

        #region Helpers

        protected override void OnClosed(EventArgs e)
        {
            LogService.LogInfo("Main window closing.");

            _gameCheckTimer?.Stop();
            _powerupUtils?.StopAll();
            _clickerService?.Dispose();
            GlobalHotKeyManager.Shutdown();
            _keyboardListener?.Dispose();
            _mouseListener?.Dispose();
            _notifyIcon?.Dispose();
            _timerWindow?.Close();
            _railgunOverlay?.Close();

            base.OnClosed(e);

        }
        #endregion

        #region Tray Icon

        private void InitializeNotifyIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Visible = false,
                Text = "Automatization"
            };

            try
            {
                _notifyIcon.Icon = new Icon("Settings/icon.ico");
            }
            catch (Exception ex)
            {
                LogService.LogWarning($"Failed to load tray icon: {ex.Message}");
            }

            _notifyIcon.DoubleClick += (s, args) => ShowWindow();

            ContextMenuStrip contextMenu = new();
            _ = contextMenu.Items.Add("Show", null, (s, e) => ShowWindow());
            _ = contextMenu.Items.Add("Toggle All", null, (s, e) => _powerupUtils?.ToggleAll());
            _ = contextMenu.Items.Add("Exit", null, (s, e) => Close());

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        protected override void OnStateChanged(EventArgs e)
        {
            //if (WindowState == WindowState.Minimized && _notifyIcon != null)
            //{
            //    Hide();
            //    _notifyIcon.Visible = true;

            //    LogService.LogInfo("Window minimized to tray.");
            //}

            base.OnStateChanged(e);
        }

        private void ShowWindow()
        {
            Show();
            WindowState = WindowState.Normal;

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
            }

            _ = Activate();

            LogService.LogInfo("Window restored from tray.");
        }
        #endregion
    }
}