using Avalonia;

namespace TomodachiDrawer.UI.Avalonia
{
    public class Program
    {
        // The entry point for the actual application
        [STAThread]
        public static void Main(string[] args)
        {
            // Initialize crash reporting as early as possible so startup crashes are
            // caught, but ONLY IF THE USER HAS CONSENTED TO SUCH THINGS!
            // This is arguably not ideal since it means if a user cant launch the program at all, we wouldn't
            // be aware of it, but the alternative involves implicit consent which feels icky.
            if (AppSettings.TryLoad()?.EnableTelemetry == true)
                CrashReporter.Init();

            try
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            finally
            {
                // Deliver any queued crash events before the process dies.
                CrashReporter.Flush();
            }
        }

        // The entry point the Previewer looks for
        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
    }
}
