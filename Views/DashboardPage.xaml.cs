using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using YxllowLoader.Models;

namespace YxllowLoader
{
    public sealed partial class DashboardPage : Page
    {
        private UserSession _session;
        private bool _isInjected = false;
        private Process _gameProcess = null;

        public DashboardPage()
        {
            this.InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            PageOpenAnim.Begin();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeGameProcess();
        }

        private void UnsubscribeGameProcess()
        {
            if (_gameProcess != null)
            {
                _gameProcess.Exited -= OnGameProcessExited;
                _gameProcess.Dispose();
                _gameProcess = null;
            }
        }

        private void OnGameProcessExited(object sender, EventArgs e)
        {
            UnsubscribeGameProcess();
            _isInjected = false;

            DispatcherQueue.TryEnqueue(() =>
            {
                StatusDot.Fill = Application.Current.Resources["BrandMutedBrush"] as Brush;
                StatusLabel.Text = "Not Injected";
                StatusLabel.Foreground = Application.Current.Resources["BrandMutedBrush"] as Brush;
            });
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is UserSession session)
            {
                _session = session;
                PopulateUI();
            }
        }

        private void PopulateUI()
        {
            // Nav bar
            NavUsername.Text = _session.Username;

            // Account view
            AvatarInitial.Text = _session.Username.Length > 0
                ? _session.Username[0].ToString().ToUpper()
                : "?";
            AccountUsername.Text = _session.Username;
            CardPlan.Text = _session.Plan;
            CardExpiry.Text = _session.ExpiryDisplay;
            CardDaysLeft.Text = _session.DaysLeft;
            CardHwid.Text = _session.HwidStatus;
            HwidStatusText.Text = _session.HwidStatus;

            if (_session.IsExpired)
            {
                CardExpiry.Foreground = Application.Current.Resources["BrandDangerBrush"] as Brush;
                CardDaysLeft.Foreground = Application.Current.Resources["BrandDangerBrush"] as Brush;
            }

            if (_session.HwidStatus != "Verified")
            {
                HwidDot.Fill = Application.Current.Resources["BrandDangerBrush"] as Brush;
                CardHwid.Foreground = Application.Current.Resources["BrandDangerBrush"] as Brush;
                HwidStatusText.Foreground = Application.Current.Resources["BrandDangerBrush"] as Brush;
                HwidPillBg.Color = Windows.UI.Color.FromArgb(0x1A, 0xEF, 0x44, 0x44);
            }
        }

        // ── Tab switching ───────────────────────────────────────────────

        private void TabInjectBtn_Click(object sender, RoutedEventArgs e)
        {
            InjectView.Visibility = Visibility.Visible;
            AccountView.Visibility = Visibility.Collapsed;

            // Active style: inject tab
            TabInjectBtn.Background = new SolidColorBrush(ColorHelper.FromArgb(0x1A, 0xFF, 0xC3, 0x00));
            SetTabTextColor(TabInjectText, isActive: true);
            TabAccountBtn.Background = new SolidColorBrush(ColorHelper.FromArgb(0, 0, 0, 0));
            SetTabTextColor(TabAccountText, isActive: false);
        }

        private void TabAccountBtn_Click(object sender, RoutedEventArgs e)
        {
            InjectView.Visibility = Visibility.Collapsed;
            AccountView.Visibility = Visibility.Visible;

            // Active style: account tab
            TabAccountBtn.Background = new SolidColorBrush(ColorHelper.FromArgb(0x1A, 0xFF, 0xC3, 0x00));
            SetTabTextColor(TabAccountText, isActive: true);
            TabInjectBtn.Background = new SolidColorBrush(ColorHelper.FromArgb(0, 0, 0, 0));
            SetTabTextColor(TabInjectText, isActive: false);
        }

        private void SetTabTextColor(TextBlock tb, bool isActive)
        {
            tb.Foreground = isActive
                ? Application.Current.Resources["BrandYellowBrush"] as Brush
                : Application.Current.Resources["BrandMutedBrush"] as Brush;
        }

        // ── Inject ─────────────────────────────────────────────────────

        private async void InjectBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isInjected)
            {
                StatusLabel.Text = "Already injected.";
                return;
            }

            ClearInjectionLog();

            AppendInjectionLog("Searching for Rocket League process...");
            var procs = Process.GetProcessesByName("RocketLeague");
            if (procs.Length == 0)
                procs = Process.GetProcessesByName("RocketLeague_UE4");

            SetInjectLoading(true, "Connecting to process...");
            await Task.Delay(300);

            if (procs.Length == 0)
            {
                SetInjectLoading(false);
                AppendInjectionLog("Rocket League not found. Launch the game first.", true);
                StatusLabel.Text = "Rocket League not found. Launch the game first.";
                StatusLabel.Foreground = Application.Current.Resources["BrandDangerBrush"] as Brush;
                StatusDot.Fill = Application.Current.Resources["BrandDangerBrush"] as Brush;
                return;
            }

            AppendInjectionLog("Found process: " + procs[0].ProcessName + " (PID " + procs[0].Id + ")");
            SetInjectLoading(true, "Injecting SDK...");

            // Capture a local Action so the background thread can post log lines to the UI.
            var logLines = new System.Collections.Concurrent.ConcurrentQueue<(string msg, bool err)>();
            bool success = await Task.Run(() => InjectInternal(procs[0].Id, (msg, err) => logLines.Enqueue((msg, err))));

            // Flush all queued log lines onto the UI thread.
            while (logLines.TryDequeue(out var entry))
                AppendInjectionLog(entry.msg, entry.err);

            SetInjectLoading(false);

            if (success)
            {
                AppendInjectionLog("SDK injected successfully!");
                _isInjected = true;
                _gameProcess = procs[0];
                _gameProcess.EnableRaisingEvents = true;
                _gameProcess.Exited += OnGameProcessExited;
                // Handle the edge case where the process exited between injection and subscribing.
                if (_gameProcess.HasExited)
                    OnGameProcessExited(_gameProcess, EventArgs.Empty);
                StatusDot.Fill = Application.Current.Resources["BrandSuccessBrush"] as Brush;
                StatusLabel.Text = "Injected";
                StatusLabel.Foreground = Application.Current.Resources["BrandSuccessBrush"] as Brush;
            }
            else
            {
                AppendInjectionLog("Injection failed. See log above.", true);
                StatusDot.Fill = Application.Current.Resources["BrandDangerBrush"] as Brush;
                StatusLabel.Text = "Injection failed. Run as administrator.";
                StatusLabel.Foreground = Application.Current.Resources["BrandDangerBrush"] as Brush;
            }
        }

        private void SetInjectLoading(bool loading, string statusText = "")
        {
            InjectBtnContent.Visibility = loading ? Visibility.Collapsed : Visibility.Visible;
            InjectSpinner.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
            InjectBtn.IsEnabled = !loading;
            InjectOverlay.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;

            if (loading && !string.IsNullOrEmpty(statusText))
                OverlayStatus.Text = statusText;
            else if (!loading)
                OverlayStatus.Text = "";
        }

        // ── DLL Injection (LoadLibrary method) ─────────────────────────

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint access, bool inheritHandle, int pid);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr VirtualAllocEx(IntPtr hProc, IntPtr addr, uint size, uint type, uint protect);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr hProc, IntPtr addr, byte[] buf, uint size, out int written);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateRemoteThread(IntPtr hProc, IntPtr attr, uint stackSize, IntPtr startAddr, IntPtr param, uint flags, out uint threadId);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetExitCodeThread(IntPtr hThread, out uint lpExitCode);

        private const string DllFileName = "sdk.dll";

        // ── Injection console log ──────────────────────────────────────

        private void ClearInjectionLog()
        {
            InjectLogText.Text = "";
            InjectLogText.Foreground = Application.Current.Resources["BrandMutedBrush"] as Brush;
            InjectLogPanel.Visibility = Visibility.Visible;
        }

        private void AppendInjectionLog(string message, bool isError = false)
        {
            string prefix = isError ? "[ERR] " : "[OK]  ";
            InjectLogText.Text += prefix + message + "\n";
            InjectLogText.Foreground = isError
                ? Application.Current.Resources["BrandDangerBrush"] as Brush
                : Application.Current.Resources["BrandMutedBrush"] as Brush;
            // Scroll to end.
            InjectLogScroll.ChangeView(null, InjectLogScroll.ScrollableHeight, null);
        }

        private static bool InjectInternal(int pid, Action<string, bool> log)
        {
            var assemblyDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (assemblyDir is null)
            {
                log("Cannot determine assembly directory.", true);
                return false;
            }

            var dllPath = System.IO.Path.Combine(assemblyDir, DllFileName);
            log("DLL path: " + dllPath, false);

            if (!System.IO.File.Exists(dllPath))
            {
                log(DllFileName + " not found next to the loader.", true);
                return false;
            }
            log("DLL found on disk.", false);

            const uint PROCESS_ALL_ACCESS = 0x1F0FFF;
            const uint MEM_COMMIT = 0x1000;
            const uint MEM_RESERVE = 0x2000;
            const uint PAGE_READWRITE = 0x04;

            log("Opening process handle (PID " + pid + ")...", false);
            var hProc = OpenProcess(PROCESS_ALL_ACCESS, false, pid);
            if (hProc == IntPtr.Zero)
            {
                log("OpenProcess failed – run as administrator.", true);
                return false;
            }
            log("Process handle acquired.", false);

            // Use Unicode (UTF-16 LE) path bytes and LoadLibraryW so the path
            // is correctly interpreted regardless of the system ANSI code page.
            var pathBytes = System.Text.Encoding.Unicode.GetBytes(dllPath + "\0");
            log("Allocating remote memory (" + pathBytes.Length + " bytes)...", false);
            var alloc = VirtualAllocEx(hProc, IntPtr.Zero, (uint)pathBytes.Length, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            if (alloc == IntPtr.Zero)
            {
                log("VirtualAllocEx failed.", true);
                CloseHandle(hProc);
                return false;
            }
            log("Remote memory allocated at 0x" + alloc.ToString("X"), false);

            log("Writing DLL path into remote process...", false);
            WriteProcessMemory(hProc, alloc, pathBytes, (uint)pathBytes.Length, out _);
            log("DLL path written.", false);

            log("Creating remote thread (LoadLibraryW)...", false);
            var loadLib = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryW");
            var thread = CreateRemoteThread(hProc, IntPtr.Zero, 0, loadLib, alloc, 0, out _);

            bool success = false;
            if (thread != IntPtr.Zero)
            {
                log("Remote thread created. Waiting for LoadLibraryW to return (max 5 s)...", false);
                // Wait up to 5 s for LoadLibraryW to return, then check its exit
                // code (the module handle) to confirm the DLL was actually loaded.
                const uint WAIT_TIMEOUT_MS = 5000;
                WaitForSingleObject(thread, WAIT_TIMEOUT_MS);
                GetExitCodeThread(thread, out uint exitCode);
                success = exitCode != 0;
                if (success)
                    log("LoadLibraryW returned 0x" + exitCode.ToString("X") + " – DLL loaded.", false);
                else
                    log("LoadLibraryW returned 0 – DLL failed to load.", true);
                CloseHandle(thread);
            }
            else
            {
                log("CreateRemoteThread failed.", true);
            }

            CloseHandle(hProc);
            return success;
        }

        // ── Logout ─────────────────────────────────────────────────────

        private void LogoutBtn_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.RootFrame.Navigate(typeof(LoginPage));
        }

        // ── Hover animations ────────────────────────────────────────────

        private void StatCard_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border card && card.RenderTransform is CompositeTransform tf)
                AnimateScale(tf, 1.0, 1.05, 160);
        }

        private void StatCard_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border card && card.RenderTransform is CompositeTransform tf)
                AnimateScale(tf, 1.05, 1.0, 200);
        }

        private void InjectBtn_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is UIElement el && el.RenderTransform is CompositeTransform tf)
                AnimateScale(tf, 1.0, 1.04, 130);
        }

        private void InjectBtn_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is UIElement el && el.RenderTransform is CompositeTransform tf)
                AnimateScale(tf, 1.04, 1.0, 180);
        }

        private void TabBtn_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is UIElement el && el.RenderTransform is CompositeTransform tf)
                AnimateScale(tf, 1.0, 1.06, 120);
        }

        private void TabBtn_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is UIElement el && el.RenderTransform is CompositeTransform tf)
                AnimateScale(tf, 1.06, 1.0, 160);
        }

        private void LogoutBtn_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is UIElement el && el.RenderTransform is CompositeTransform tf)
                AnimateScale(tf, 1.0, 1.07, 130);
        }

        private void LogoutBtn_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is UIElement el && el.RenderTransform is CompositeTransform tf)
                AnimateScale(tf, 1.07, 1.0, 180);
        }

        private static void AnimateScale(CompositeTransform tf, double from, double to, int durationMs)
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var dur  = new Duration(TimeSpan.FromMilliseconds(durationMs));

            var sx = new DoubleAnimation { From = from, To = to, Duration = dur, EasingFunction = ease };
            Storyboard.SetTarget(sx, tf);
            Storyboard.SetTargetProperty(sx, "ScaleX");

            var sy = new DoubleAnimation { From = from, To = to, Duration = dur, EasingFunction = ease };
            Storyboard.SetTarget(sy, tf);
            Storyboard.SetTargetProperty(sy, "ScaleY");

            var sb = new Storyboard();
            sb.Children.Add(sx);
            sb.Children.Add(sy);
            sb.Begin();
        }
    }
}
