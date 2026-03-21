using Microsoft.UI.Xaml;

namespace YxllowLoader
{
    public partial class App : Application
    {
        private Window _window;

        public static Window MainWindow { get; private set; }

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            MainWindow = _window;
            _window.Activate();
        }
    }
}