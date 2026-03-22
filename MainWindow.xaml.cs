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
            // ── Build pixel elements — YX combined lettermark ────────────
            // The logo is defined on a 16×16 grid.  Each cell is 7 px (6 px pixel +
            // 1 px gap), so the canvas is still 112×112.
            // Y occupies cols 0-6, gap cols 7-8, X occupies cols 9-15.
            const int GridSize           = 16;
            const int CellSize           = 7;
            const int PixelSize          = 6;
            const int PixelStaggerDelayMs = 5;  // ms per-pixel stagger during assembly

            // 1 = filled pixel, 0 = empty
            var logo = new int[GridSize, GridSize]
            {
                //  0  1  2  3  4  5  6  7  8  9  A  B  C  D  E  F
                {   0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 0
                {   1, 1, 0, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 0, 1, 1 }, // 1
                {   1, 1, 0, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 0, 1, 1 }, // 2
                {   0, 1, 1, 0, 1, 1, 0, 0, 0, 0, 1, 1, 0, 1, 1, 0 }, // 3
                {   0, 1, 1, 0, 1, 1, 0, 0, 0, 0, 1, 1, 0, 1, 1, 0 }, // 4
                {   0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0 }, // 5
                {   0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0 }, // 6
                {   0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0 }, // 7
                {   0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0 }, // 8
                {   0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 1, 1, 0, 1, 1, 0 }, // 9
                {   0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 1, 1, 0, 1, 1, 0 }, // A
                {   0, 0, 0, 1, 1, 0, 0, 0, 0, 1, 1, 0, 0, 0, 1, 1 }, // B
                {   0, 0, 0, 1, 1, 0, 0, 0, 0, 1, 1, 0, 0, 0, 1, 1 }, // C
                {   0, 0, 0, 1, 1, 0, 0, 0, 0, 1, 1, 0, 0, 0, 1, 1 }, // D
                {   0, 0, 0, 1, 1, 0, 0, 0, 0, 1, 1, 0, 0, 0, 1, 1 }, // E
                {   0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // F
            };

            // Collect the (row, col) of every filled cell.
            var positions = new System.Collections.Generic.List<(int row, int col)>();
            for (int r = 0; r < GridSize; r++)
                for (int c = 0; c < GridSize; c++)
                    if (logo[r, c] == 1)
                        positions.Add((r, c));

            int Total = positions.Count;

            var pixelBrush = Application.Current.Resources["BrandYellowBrush"] as SolidColorBrush
                ?? new SolidColorBrush(ColorHelper.FromArgb(255, 255, 195, 0));

            var borders    = new Border[Total];
            var transforms = new TranslateTransform[Total];

            // Generate scatter positions via the golden-angle spiral so every pixel
            // flies in from a unique direction spread all over the screen.
            var scatterDx = new double[Total];
            var scatterDy = new double[Total];
            for (int i = 0; i < Total; i++)
            {
                double angle  = i * 137.508 * Math.PI / 180.0; // golden angle
                double dist   = 300.0 + (i % 23) * 18.0;       // 300..714 px from centre
                scatterDx[i]  = Math.Cos(angle) * dist;
                scatterDy[i]  = Math.Sin(angle) * dist;
            }

            for (int idx = 0; idx < Total; idx++)
            {
                double finalX = positions[idx].col * CellSize;
                double finalY = positions[idx].row * CellSize;

                var transform = new TranslateTransform { X = scatterDx[idx], Y = scatterDy[idx] };

                var pixel = new Border
                {
                    Width           = PixelSize,
                    Height          = PixelSize,
                    Background      = pixelBrush,
                    CornerRadius    = new CornerRadius(1),
                    Opacity         = 0,
                    RenderTransform = transform,
                };

                Canvas.SetLeft(pixel, finalX);
                Canvas.SetTop(pixel,  finalY);
                PixelCanvas.Children.Add(pixel);

                borders[idx]    = pixel;
                transforms[idx] = transform;
            }

            // ── Phase 1: Assembly — pixels fly from scatter to form the YX logo ──
            var assemblySb = new Storyboard();

            for (int i = 0; i < Total; i++)
            {
                int delayMs = i * PixelStaggerDelayMs;

                var animX = new DoubleAnimation
                {
                    From           = scatterDx[i],
                    To             = 0,
                    Duration       = new Duration(TimeSpan.FromMilliseconds(700)),
                    BeginTime      = TimeSpan.FromMilliseconds(delayMs),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                };
                Storyboard.SetTarget(animX, transforms[i]);
                Storyboard.SetTargetProperty(animX, "X");
                assemblySb.Children.Add(animX);

                var animY = new DoubleAnimation
                {
                    From           = scatterDy[i],
                    To             = 0,
                    Duration       = new Duration(TimeSpan.FromMilliseconds(700)),
                    BeginTime      = TimeSpan.FromMilliseconds(delayMs),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                };
                Storyboard.SetTarget(animY, transforms[i]);
                Storyboard.SetTargetProperty(animY, "Y");
                assemblySb.Children.Add(animY);

                var animOp = new DoubleAnimation
                {
                    From      = 0,
                    To        = 1,
                    Duration  = new Duration(TimeSpan.FromMilliseconds(320)),
                    BeginTime = TimeSpan.FromMilliseconds(delayMs),
                };
                Storyboard.SetTarget(animOp, borders[i]);
                Storyboard.SetTargetProperty(animOp, "Opacity");
                assemblySb.Children.Add(animOp);
            }

            var tcs1 = new TaskCompletionSource<bool>();
            assemblySb.Completed += (s, ea) => tcs1.TrySetResult(true);
            assemblySb.Begin();
            await tcs1.Task;

            // Brief hold so the completed YX mark is visible before fading out.
            await Task.Delay(400);

            // ── Phase 2: Fade out the splash overlay ─────────────────────
            var fadeSb = new Storyboard();
            var fadeAnim = new DoubleAnimation
            {
                From           = 1,
                To             = 0,
                Duration       = new Duration(TimeSpan.FromMilliseconds(450)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
            };
            Storyboard.SetTarget(fadeAnim, SplashOverlay);
            Storyboard.SetTargetProperty(fadeAnim, "Opacity");
            fadeSb.Children.Add(fadeAnim);

            var tcs2 = new TaskCompletionSource<bool>();
            fadeSb.Completed += (s, ea) => tcs2.TrySetResult(true);
            fadeSb.Begin();
            await tcs2.Task;
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