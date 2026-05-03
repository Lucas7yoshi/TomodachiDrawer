using Avalonia;
using TomodachiDrawer.UI.Avalonia;

public class Program
{
    // The entry point for the actual application
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // The entry point the Previewer looks for
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}