using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Threading.Tasks;
using Windows.Graphics;

namespace YxllowLoader
{
    public sealed partial class MainWindow : Window
    {
        public static Frame RootFrame { get; private set; }

        private bool _isClosing = false;

        public MainWindow()
        {
            this.InitializeComponent();

            // Custom title bar
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(TitleBar);

            // Window size + title
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new SizeInt32(960, 620));
            appWindow.Title = "yxllow tech";

            // Center on screen
            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
            var x = (displayArea.WorkArea.Width - 960) / 2;
            var y = (displayArea.WorkArea.Height - 620) / 2;
            appWindow.Move(new PointInt32(x, y));

            // Closing animation
            appWindow.Closing += AppWindow_Closing;

            RootFrame = ContentFrame;
            // Navigation is deferred to after the splash animation (see RootGrid_Loaded)
        }

        private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await RunSplashAnimationAsync();
            }
            catch
            {
                // Intentionally swallowed — a failing splash animation must not prevent
                // the user from reaching the login page.
            }
            finally
            {
                ContentFrame.Navigate(typeof(LoginPage));
                SplashOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private async Task RunSplashAnimationAsync()
        {
            // ── Phase 1: Animate the loading bar from 0 → 100 ────────────
            const int StepMs = 16; // ~60 fps
            const int TotalMs = 1500;

            for (int elapsed = 0; elapsed <= TotalMs; elapsed += StepMs)
            {
                double t = Math.Min((double)elapsed / TotalMs, 1.0);
                // Ease in-out cubic
                t = t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
                SplashProgressBar.Value = t * 100.0;
                await Task.Delay(StepMs);
            }
            SplashProgressBar.Value = 100;

            await Task.Delay(200);

            // ── Phase 2: Fade out the splash overlay ─────────────────────
            var fadeSb = new Storyboard();
            var fadeAnim = new DoubleAnimation
            {
                From           = 1,
                To             = 0,
                Duration       = new Duration(TimeSpan.FromMilliseconds(350)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
            };
            Storyboard.SetTarget(fadeAnim, SplashOverlay);
            Storyboard.SetTargetProperty(fadeAnim, "Opacity");
            fadeSb.Children.Add(fadeAnim);

            var tcs = new TaskCompletionSource<bool>();
            fadeSb.Completed += (s, ea) => tcs.TrySetResult(true);
            fadeSb.Begin();
            await tcs.Task;
        }

        private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            if (_isClosing) return;
            args.Cancel = true;
            _isClosing = true;

            const int AnimDurationMs = 280;

            // Fade out animation
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(AnimDurationMs)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(fadeOut, ContentFrame);
            Storyboard.SetTargetProperty(fadeOut, "Opacity");

            var sb = new Storyboard();
            sb.Children.Add(fadeOut);
            sb.Begin();

            await Task.Delay(AnimDurationMs + 20);
            sender.Destroy();
        }
    }
}