namespace RapidUdemyDW
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // MAUI-level first-chance exception logger — useful for diagnostics.
            // The actual crash-prevention is handled by platform-specific handlers
            // (WinUI.UnhandledException, AndroidEnvironment.UnhandledExceptionRaiser)
            // and the AppDomain/TaskScheduler handlers in MauiProgram.
            AppDomain.CurrentDomain.FirstChanceException += (s, e) =>
            {
                // Only log — this fires for ALL exceptions (including caught ones),
                // so don't do anything heavy here.
                System.Diagnostics.Debug.WriteLine(
                    $"[FirstChanceException] {e.Exception.GetType().Name}: {e.Exception.Message}");
            };
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new MainPage()) { Title = "RapidUdemyDW" };
        }
    }
}
