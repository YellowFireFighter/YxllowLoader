using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using YxllowLoader.Models;
using YxllowLoader.Views;

namespace YxllowLoader
{
    public sealed partial class DashboardPage : Page
    {
        private UserSession _session;
        private bool _isInjected = false;

        public DashboardPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is UserSession session)
            {
                _session = session;
                PopulateUI();
                Log("Session authenticated.", LogLevel.Info);
                Log($"Subscription: {session.Plan} — {session.DaysLeft}", LogLevel.Info);
                Log("Ready to inject.", LogLevel.Ok);
            }
        }

        private void PopulateUI()
        {
            // Sidebar
            AvatarInitial.Text = _session.Username.Length > 0 ? _session.Username[0].ToString().ToUpper() : "?";
            SidebarUsername.Text = _session.Username;
            SidebarPlan.Text = _session.Plan;

            // Header
            HeaderUsername.Text = _session.Username;

            // Cards
            CardPlan.Text = _session.Plan;
            CardExpiry.Text = _session.ExpiryDisplay;
            CardDaysLeft.Text = _session.DaysLeft;
            CardHwid.Text = _session.HwidStatus;

            if (_session.IsExpired)
            {
                CardExpiry.Foreground = Application.Current.Resources["BrandDangerBrush"] as Brush;
                CardDaysLeft.Foreground = Application.Current.Resources["BrandDangerBrush"] as Brush;
            }

            if (_session.HwidStatus != "Verified")
            {
                HwidDot.Fill = Application.Current.Resources["BrandDangerBrush"] as Brush;
                CardHwid.Foreground = Application.Current.Resources["BrandDangerBrush"] as Brush;
            }

            SubtitleText.Text = $"logged in as {_session.Username} · {DateTime.Now:HH:mm}";
        }

        // ── Inject ─────────────────────────────────────────────────────────

        private async void InjectBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isInjected)
            {
                Log("Already injected.", LogLevel.Warn);
                return;
            }

            var dllPath = DllPathBox.Text.Trim();
            if (string.IsNullOrEmpty(dllPath) || !File.Exists(dllPath))
            {
                Log("DLL not found — browse to select it first.", LogLevel.Error);
                return;
            }

            var processName = ProcessCombo.SelectedIndex == 0 ? "RocketLeague" : "RocketLeague_UE4";
            var procs = Process.GetProcessesByName(processName);
            if (procs.Length == 0)
            {
                Log($"Process '{processName}.exe' not found. Launch the game first.", LogLevel.Error);
                return;
            }

            SetInjectLoading(true);
            Log($"Found {processName}.exe (PID {procs[0].Id})", LogLevel.Info);
            Log("Injecting...", LogLevel.Info);

            bool success = await Task.Run(() => Inject(procs[0].Id, dllPath));

            SetInjectLoading(false);

            if (success)
            {
                _isInjected = true;
                SetInjectedState(true);
                Log("Injection successful.", LogLevel.Ok);
            }
            else
            {
                Log("Injection failed. Try running as administrator.", LogLevel.Error);
            }
        }

        private void SetInjectLoading(bool loading)
        {
            InjectBtnContent.Visibility = loading ? Visibility.Collapsed : Visibility.Visible;
            InjectSpinner.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
            InjectBtn.IsEnabled = !loading;
        }

        private void SetInjectedState(bool injected)
        {
            StatusDot.Fill = injected
                ? Application.Current.Resources["BrandSuccessBrush"] as Brush
                : Application.Current.Resources["BrandMutedBrush"] as Brush;
            StatusLabel.Text = injected ? "Injected" : "Not Injected";
            StatusLabel.Foreground = injected
                ? Application.Current.Resources["BrandSuccessBrush"] as Brush
                : Application.Current.Resources["BrandMutedBrush"] as Brush;
        }

        // ── DLL Injection (LoadLibrary method) ─────────────────────────────

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

        private static bool Inject(int pid, string dllPath)
        {
            const uint PROCESS_ALL_ACCESS = 0x1F0FFF;
            const uint MEM_COMMIT = 0x1000;
            const uint MEM_RESERVE = 0x2000;
            const uint PAGE_READWRITE = 0x04;

            var hProc = OpenProcess(PROCESS_ALL_ACCESS, false, pid);
            if (hProc == IntPtr.Zero) return false;

            var pathBytes = System.Text.Encoding.Default.GetBytes(dllPath + "\0");
            var alloc = VirtualAllocEx(hProc, IntPtr.Zero, (uint)pathBytes.Length, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            if (alloc == IntPtr.Zero) { CloseHandle(hProc); return false; }

            WriteProcessMemory(hProc, alloc, pathBytes, (uint)pathBytes.Length, out _);

            var loadLib = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");
            var thread = CreateRemoteThread(hProc, IntPtr.Zero, 0, loadLib, alloc, 0, out _);

            CloseHandle(hProc);
            return thread != IntPtr.Zero;
        }

        // ── Browse DLL ─────────────────────────────────────────────────────

        private async void BrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".dll");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                DllPathBox.Text = file.Path;
                Log($"DLL selected: {file.Name}", LogLevel.Info);
            }
        }

        // ── Logout ─────────────────────────────────────────────────────────

        private void LogoutBtn_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.RootFrame.Navigate(typeof(LoginPage));
        }

        // ── Console Log ────────────────────────────────────────────────────

        private enum LogLevel { Info, Ok, Warn, Error }

        private void Log(string message, LogLevel level = LogLevel.Info)
        {
            var (prefix, color) = level switch
            {
                LogLevel.Ok => ("[  OK  ]", "#22C55E"),
                LogLevel.Warn => ("[ WARN ]", "#F59E0B"),
                LogLevel.Error => ("[ ERR  ]", "#EF4444"),
                _ => ("[ INFO ]", "#666666"),
            };

            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

            row.Children.Add(new TextBlock
            {
                Text = prefix,
                Foreground = new SolidColorBrush(ColorHelper.FromArgb(255,
                    Convert.ToByte(color.Substring(1, 2), 16),
                    Convert.ToByte(color.Substring(3, 2), 16),
                    Convert.ToByte(color.Substring(5, 2), 16))),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
            });

            row.Children.Add(new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 0xCC, 0xCC, 0xCC)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
            });

            LogPanel.Children.Add(row);
            LogScroll.ScrollToVerticalOffset(LogScroll.ScrollableHeight + 9999);
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            LogPanel.Children.Clear();
        }
    }
}