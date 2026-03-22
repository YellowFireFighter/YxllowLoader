using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using YxllowLoader.Models;

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

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            PageOpenAnim.Begin();
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

            var procs = Process.GetProcessesByName("RocketLeague");
            if (procs.Length == 0)
                procs = Process.GetProcessesByName("RocketLeague_UE4");

            // Always show animation so it can be tested without Rocket League open.
            // TODO: Remove the simulation path once testing is complete.
            SetInjectLoading(true, "Connecting to process...");
            await Task.Delay(800);

            if (procs.Length == 0)
            {
                SetInjectLoading(true, "Simulating injection...");
                // Wait for the full code animation to complete all lines
                if (_codeAnimTask != null)
                {
                    // Await the animation task; OperationCanceledException is expected if
                    // SetInjectLoading(false) is called before the animation finishes.
                    try { await _codeAnimTask; } catch (OperationCanceledException) { }
                }
                SetInjectLoading(false, "");

                StatusLabel.Text = "Rocket League not found. Launch the game first.";
                StatusLabel.Foreground = Application.Current.Resources["BrandDangerBrush"] as Brush;
                StatusDot.Fill = Application.Current.Resources["BrandDangerBrush"] as Brush;
                return;
            }

            SetInjectLoading(true, "Injecting SDK...");
            bool success = await Task.Run(() => InjectInternal(procs[0].Id));

            SetInjectLoading(false, "");

            if (success)
            {
                _isInjected = true;
                StatusDot.Fill = Application.Current.Resources["BrandSuccessBrush"] as Brush;
                StatusLabel.Text = "Injected";
                StatusLabel.Foreground = Application.Current.Resources["BrandSuccessBrush"] as Brush;
            }
            else
            {
                StatusDot.Fill = Application.Current.Resources["BrandDangerBrush"] as Brush;
                StatusLabel.Text = "Injection failed. Run as administrator.";
                StatusLabel.Foreground = Application.Current.Resources["BrandDangerBrush"] as Brush;
            }
        }

        private CancellationTokenSource _codeAnimCts;
        private Task _codeAnimTask;

        private void SetInjectLoading(bool loading, string statusText = "")
        {
            InjectBtnContent.Visibility = loading ? Visibility.Collapsed : Visibility.Visible;
            InjectSpinner.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
            InjectBtn.IsEnabled = !loading;

            if (loading)
            {
                bool wasHidden = InjectOverlay.Visibility == Visibility.Collapsed;
                InjectOverlay.Visibility = Visibility.Visible;
                if (!string.IsNullOrEmpty(statusText))
                    OverlayStatus.Text = statusText;
                if (wasHidden)
                {
                    ResetCodeLines();
                    CursorBlinkAnim.Begin();
                    CarPulseAnim.Begin();
                    ArrowsAnim.Begin();
                    _codeAnimCts = new CancellationTokenSource();
                    _codeAnimTask = RunCodeAnimationAsync(_codeAnimCts.Token);
                }
            }
            else
            {
                _codeAnimCts?.Cancel();
                _codeAnimCts = null;
                InjectOverlay.Visibility = Visibility.Collapsed;
                CursorBlinkAnim.Stop();
                CarPulseAnim.Stop();
                ArrowsAnim.Stop();
                ResetCodeLines();
            }
        }

        private async Task RunCodeAnimationAsync(CancellationToken ct)
        {
            const int TypingDelayMs       = 22;   // ms between each typed character
            const int LineCompletionPause = 120;  // ms pause after a line finishes typing

            // Upload log messages — themed around beaming code into the car
            var lines = new[]
            {
                "> Locating Rocket League process...",
                "> Attaching to boost controller...",
                "> Allocating memory in car ECU...",
                "> Uploading SDK payload...",
                "> Writing boost modules to car...",
                "> Verifying data transfer...",
                "> Loading SDK libraries...",
                "> Initializing boost hooks...",
                "> Syncing with car telemetry...",
                "> Upload complete. Boost activated!",
            };

            // Short code snippets shown in the car upload visualizer
            var snippets = new[]
            {
                new[] { "LoadLibraryA()", "sdk.init()", "0xDEADBEEF" },
                new[] { "boost.attach()", "rl_hook()", "0x4F50454E" },
                new[] { "VAlloc(0x100)", "WriteProc()", "RemoteThread" },
                new[] { "sdk.inject()", "boost.exe", "car.patch()" },
                new[] { "modules.load()", "hooks.apply()", "mem.write()" },
                new[] { "verify.crc()", "checksum ok", "transfer done" },
                new[] { "lib.load()", "symbols.map()", "entry.find()" },
                new[] { "hook.init()", "boost_fn()", "rl.attach()" },
                new[] { "telemetry()", "car.sync()", "stats.link()" },
                new[] { "✓ SDK ready", "✓ boost live", "✓ car online" },
            };

            var slots = new[] { CodeLine1, CodeLine2, CodeLine3, CodeLine4, CodeLine5 };

            // Type the "current" prompt line character by character, then
            // move it into one of the history slots and advance.
            int historyIdx = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                // Remove leading "> " for the prompt — CodePrompt already shows "> "
                string content = line.StartsWith("> ") ? line[2..] : line;

                // Update the flying code snippets in the car visualizer
                var snip = snippets[i];
                FlyCode1.Text = snip[0];
                FlyCode2.Text = snip[1];
                FlyCode3.Text = snip[2];

                // Show boost flame on the final step
                if (i == lines.Length - 1)
                    BoostFlame.Text = "🔥";

                // Type out the current line
                CodeCurrent.Text = "";
                for (int ch = 0; ch < content.Length; ch++)
                {
                    if (ct.IsCancellationRequested) return;
                    CodeCurrent.Text = content[..(ch + 1)];
                    try { await Task.Delay(TypingDelayMs, ct); }
                    catch (OperationCanceledException) { return; }
                }

                // Brief pause after finishing the line
                try { await Task.Delay(LineCompletionPause, ct); }
                catch (OperationCanceledException) { return; }

                // Commit the completed line into the next history slot
                slots[historyIdx % slots.Length].Text = line;
                historyIdx++;
                CodeCurrent.Text = "";

                // Update boost meter
                InjectProgressBar.Value = (i + 1) * 100.0 / lines.Length;
            }
        }

        private void ResetCodeLines()
        {
            CodeLine1.Text = "";
            CodeLine2.Text = "";
            CodeLine3.Text = "";
            CodeLine4.Text = "";
            CodeLine5.Text = "";
            CodeCurrent.Text = "";
            FlyCode1.Text = "";
            FlyCode2.Text = "";
            FlyCode3.Text = "";
            BoostFlame.Text = "";
            InjectProgressBar.Value = 0;
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

        private const string DllFileName = "yxllow.dll";

        private static bool InjectInternal(int pid)
        {
            // Placeholder — in production supply a real DLL path from config/license.
            var assemblyDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (assemblyDir is null)
                return false;

            var dllPath = System.IO.Path.Combine(assemblyDir, DllFileName);

            if (!System.IO.File.Exists(dllPath))
                return false;

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
