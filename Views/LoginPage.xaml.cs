using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using YxllowLoader.Models;

namespace YxllowLoader
{
    public sealed partial class LoginPage : Page
    {
        public LoginPage()
        {
            this.InitializeComponent();
        }

        private async void LoginBtn_Click(object sender, RoutedEventArgs e)
        {
            var username = UsernameBox.Text.Trim();
            var password = PasswordBox.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowError("enter your username and password.");
                return;
            }

            SetLoading(true);

            // --- Replace with real API auth call ---
            var session = await AuthenticateAsync(username, password);
            // ---------------------------------------

            SetLoading(false);

            if (session is null)
            {
                ShowError("invalid credentials or subscription expired.");
                return;
            }

            // Navigate to dashboard, pass session
            MainWindow.RootFrame.Navigate(typeof(DashboardPage), session);
        }

        private void ShowError(string msg)
        {
            ErrorText.Text = msg;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void SetLoading(bool loading)
        {
            LoginBtnText.Visibility = loading ? Visibility.Collapsed : Visibility.Visible;
            LoginSpinner.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
            LoginBtn.IsEnabled = !loading;
            UsernameBox.IsEnabled = !loading;
            PasswordBox.IsEnabled = !loading;
            PasswordRevealBtn.IsEnabled = !loading;
            ErrorText.Visibility = Visibility.Collapsed;
        }

        private bool _isPasswordRevealed = false;

        private void PasswordRevealBtn_Click(object sender, RoutedEventArgs e)
        {
            _isPasswordRevealed = !_isPasswordRevealed;
            PasswordBox.PasswordRevealMode = _isPasswordRevealed
                ? PasswordRevealMode.Visible
                : PasswordRevealMode.Hidden;
            PasswordRevealIcon.Glyph = _isPasswordRevealed ? "\uED1A" : "\uE7B3";
        }

        // Stub — replace with your actual auth logic (HTTP call to your API, etc.)
        private async Task<UserSession> AuthenticateAsync(string username, string password)
        {
            await Task.Delay(1200); // Simulate network request

            // Mock: accept any non-empty creds for demo
            if (username.Length > 0 && password.Length >= 3)
            {
                return new UserSession
                {
                    Username = username,
                    Plan = "Pro",
                    ExpiresAt = DateTime.UtcNow.AddDays(28),
                    HwidStatus = "Verified"
                };
            }

            return null;
        }
    }
}