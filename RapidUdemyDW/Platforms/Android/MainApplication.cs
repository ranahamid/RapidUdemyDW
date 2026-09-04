using Android.App;
using Android.Runtime;

namespace RapidUdemyDW
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        public override void OnCreate()
        {
            base.OnCreate();

            // Android-specific: catch unhandled exceptions from the Android runtime
            // so the app doesn't force-close.
            AndroidEnvironment.UnhandledExceptionRaiser += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[AndroidEnvironment.UnhandledException] {e.Exception}");
                e.Handled = true; // Swallow — keep the app alive
            };
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
