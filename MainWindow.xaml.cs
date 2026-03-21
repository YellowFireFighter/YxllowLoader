using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace YxllowLoader
{
    public sealed partial class MainWindow : Window
    {
        public static Frame RootFrame { get; private set; }

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
            appWindow.Title = "yxllow loader";

            // Center on screen
            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
            var x = (displayArea.WorkArea.Width - 960) / 2;
            var y = (displayArea.WorkArea.Height - 620) / 2;
            appWindow.Move(new PointInt32(x, y));

            RootFrame = ContentFrame;
            ContentFrame.Navigate(typeof(LoginPage));
        }
    }
}