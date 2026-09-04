using Serilog;

namespace RapidUdemyDW
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new MainPage()) { Title = AppConstants.AppName };

            window.Destroying += (s, e) =>
            {
                Log.Information("App shutting down");
                Log.CloseAndFlush();
            };

            return window;
        }
    }
}
