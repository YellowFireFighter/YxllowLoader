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

        // Pixel grid positions (col, row) that form a hollow 4×4 square outline
        private static readonly (double col, double row)[] PixelGridPos =
        {
            (0,0),(1,0),(2,0),(3,0),   // top row
            (0,1),(3,1),               // middle rows
            (0,2),(3,2),
            (0,3),(1,3),(2,3),(3,3),   // bottom row
        };

        // Pre-defined scatter offsets so each pixel flies in from a unique direction
        private static readonly (double dx, double dy)[] ScatterOffsets =
        {
            (-80,-60), (-30,-90), (35,-85), (85,-55),
            (-105,  5), (110, 20),
            (-110, 45), (108, 55),
            (-75,  80), (-25,100), (40,  95), (92, 70),
        };

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
            // ── Build pixel elements ─────────────────────────────────────
            const int CellSize   = 16;  // pixels per grid cell (including gap)
            const int PixelSize  = 14;  // visual size of each pixel square
            const double CanvasW = 96;
            const double CanvasH = 96;

            double offsetX = (CanvasW - 4 * CellSize) / 2.0;  // = 16
            double offsetY = (CanvasH - 4 * CellSize) / 2.0;  // = 16

            var pixelBrush = Application.Current.Resources["BrandYellowBrush"] as SolidColorBrush
                ?? new SolidColorBrush(ColorHelper.FromArgb(255, 255, 195, 0));

            var borders    = new Border[PixelGridPos.Length];
            var transforms = new TranslateTransform[PixelGridPos.Length];

            for (int i = 0; i < PixelGridPos.Length; i++)
            {
                var (col, row) = PixelGridPos[i];
                double finalX = offsetX + col * CellSize;
                double finalY = offsetY + row * CellSize;
                var (dx, dy)  = ScatterOffsets[i];

                var transform = new TranslateTransform { X = dx, Y = dy };

                var pixel = new Border
                {
                    Width           = PixelSize,
                    Height          = PixelSize,
                    Background      = pixelBrush,
                    CornerRadius    = new CornerRadius(2),
                    Opacity         = 0,
                    RenderTransform = transform,
                };

                Canvas.SetLeft(pixel, finalX);
                Canvas.SetTop(pixel,  finalY);
                PixelCanvas.Children.Add(pixel);

                borders[i]    = pixel;
                transforms[i] = transform;
            }

            // ── Phase 1: Assembly — pixels fly from scatter to final position ──
            var assemblySb = new Storyboard();

            for (int i = 0; i < PixelGridPos.Length; i++)
            {
                int delayMs = 30 + i * 50;    // stagger each pixel by 50 ms
                var (dx, dy) = ScatterOffsets[i];

                var animX = new DoubleAnimation
                {
                    From           = dx,
                    To             = 0,
                    Duration       = new Duration(TimeSpan.FromMilliseconds(500)),
                    BeginTime      = TimeSpan.FromMilliseconds(delayMs),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                };
                Storyboard.SetTarget(animX, transforms[i]);
                Storyboard.SetTargetProperty(animX, "X");
                assemblySb.Children.Add(animX);

                var animY = new DoubleAnimation
                {
                    From           = dy,
                    To             = 0,
                    Duration       = new Duration(TimeSpan.FromMilliseconds(500)),
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
                    Duration  = new Duration(TimeSpan.FromMilliseconds(280)),
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

            // ── Phase 2: Rotation — assembled square spins two full turns ──
            var canvasRotate = new RotateTransform { Angle = 0, CenterX = 48, CenterY = 48 };
            PixelCanvas.RenderTransform = canvasRotate;

            var rotSb = new Storyboard();
            var rotAnim = new DoubleAnimation
            {
                From           = 0,
                To             = 720,
                Duration       = new Duration(TimeSpan.FromMilliseconds(900)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
            };
            Storyboard.SetTarget(rotAnim, canvasRotate);
            Storyboard.SetTargetProperty(rotAnim, "Angle");
            rotSb.Children.Add(rotAnim);

            var tcs2 = new TaskCompletionSource<bool>();
            rotSb.Completed += (s, ea) => tcs2.TrySetResult(true);
            rotSb.Begin();
            await tcs2.Task;

            // Brief pause before transitioning
            await Task.Delay(150);

            // ── Phase 3: Fade out the splash overlay ───────────────────────
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

            var tcs3 = new TaskCompletionSource<bool>();
            fadeSb.Completed += (s, ea) => tcs3.TrySetResult(true);
            fadeSb.Begin();
            await tcs3.Task;
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