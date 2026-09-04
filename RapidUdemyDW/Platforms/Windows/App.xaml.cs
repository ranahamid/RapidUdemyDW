using Microsoft.UI.Xaml;
using Serilog;

namespace RapidUdemyDW.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        public App()
        {
            this.InitializeComponent();

            // WinUI / Windows-specific: prevent the app from crashing on unhandled exceptions.
            this.UnhandledException += (s, e) =>
            {
                Log.Error(e.Exception, "WinUI.UnhandledException: {Message}", e.Message);
                e.Handled = true; // Swallow — keep the app running
            };
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
