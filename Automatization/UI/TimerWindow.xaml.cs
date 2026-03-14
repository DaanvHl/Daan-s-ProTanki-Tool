using Automatization.Services;
using Automatization.Settings;
using Automatization.Utils;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Automatization.UI
{
    public partial class TimerWindow : Window
    {
        private DispatcherTimer _timer;
        private DispatcherTimer _gameCheckTimer;
        private DispatcherTimer _hideTimer;
        private int _seconds;
        private Process? _gameProcess;
        private string _gameProcessName;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int HWND_TOPMOST = -1;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        public TimerWindow(bool startTimer = false)
        {
            InitializeComponent();

            _seconds = 40;
            TimerLabel.Text = _seconds.ToString();

            AppSettings settings = AppSettings.Load();
            _gameProcessName = settings.GameProcessName;

            if (settings.IsTimerWindowTransparent)
            {
                BackgroundBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0));
            }

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000)
            };
            _timer.Tick += Timer_Tick;

            _gameCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _gameCheckTimer.Tick += GameCheckTimer_Tick;
            _gameCheckTimer.Start();

            _hideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _hideTimer.Tick += HideTimer_Tick;

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = 100;
            Top = 100;

            Topmost = true;

            if (startTimer)
            {
                Start();
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            WindowInteropHelper helper = new(this);
            IntPtr currentStyle = GetWindowLongPtr(helper.Handle, GWL_EXSTYLE);

            _ = SetWindowLongPtr(helper.Handle, GWL_EXSTYLE, new IntPtr(currentStyle.ToInt64() | WS_EX_NOACTIVATE));
        }

        public void Start()
        {
            _timer.Start();
        }

        public void ResetTimer()
        {
            _timer.Stop();
            _seconds = 40;
            TimerLabel.Text = _seconds.ToString();
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _seconds--;
            TimerLabel.Text = _seconds.ToString();

            if (_seconds <= 0)
            {
                _timer.Stop();
                Close();
            }
        }

        private void GameCheckTimer_Tick(object? sender, EventArgs e)
        {
            Process? currentGame = Process.GetProcessesByName(_gameProcessName).FirstOrDefault();

            if (currentGame == null)
            {
                HandleGameProcessLost();
                return;
            }

            HandleGameProcessFound(currentGame);
        }

        private void HandleGameProcessFound(Process game)
        {
            if (_gameProcess == null || _gameProcess.Id != game.Id)
            {
                _gameProcess = game;
                LogService.LogInfo("Game process found for TimerWindow.");
            }

            if (WindowUtils.IsGameWindowInForeground(_gameProcess))
            {
                ShowTimerWindow();
            }
            else
            {
                DebounceHideTimerWindow();
            }
        }

        private void HandleGameProcessLost()
        {
            if (_gameProcess != null)
            {
                _gameProcess = null;
                LogService.LogInfo("Game process lost for TimerWindow.");
            }
            HideTimerWindow();
        }

        private void ShowTimerWindow()
        {
            _hideTimer.Stop();
            if (Visibility == Visibility.Visible)
            {
                return;
            }

            Visibility = Visibility.Visible;
            LogService.LogInfo("TimerWindow shown because game is in foreground.");
        }

        private void HideTimer_Tick(object? sender, EventArgs e)
        {
            _hideTimer.Stop();
            HideTimerWindow(isDebounced: true);
        }

        private void DebounceHideTimerWindow()
        {
            if (Visibility == Visibility.Hidden || _hideTimer.IsEnabled)
            {
                return;
            }

            _hideTimer.Start();
            LogService.LogInfo("TimerWindow hide debounced.");
        }

        private void HideTimerWindow(bool isDebounced = false)
        {
            if (Visibility == Visibility.Hidden)
            {
                return;
            }

            Visibility = Visibility.Hidden;
            LogService.LogInfo(isDebounced
                ? "TimerWindow hidden after debounce."
                : "TimerWindow hidden because game process is not running.");
        }

        protected override void OnClosed(EventArgs e)
        {
            _gameCheckTimer.Stop();
            _timer.Stop();
            _hideTimer.Stop();
            base.OnClosed(e);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }
    }
}
