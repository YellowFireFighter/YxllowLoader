using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
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
            ContentFrame.Navigate(typeof(LoginPage));
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

            await System.Threading.Tasks.Task.Delay(AnimDurationMs + 20);
            sender.Destroy();
        }
    }
}