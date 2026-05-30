using System.Reflection;
using System.Text.RegularExpressions;
using Avalonia.Threading;

namespace TomodachiDrawer.UI.Avalonia;

internal static partial class CrashReporter
{
    private const string DSN =
        "https://d21cb41981a5038b6044d62c0dd4deb9@o82037.ingest.us.sentry.io/4511475971325952";

    private static bool _initialized;

    // As an extra layer of precaution, we manually scrub PII (the username) as well as using SendDefaultPii = false.
    // I do not want to be a nuisance to users by sending that stuff, plus it poses some legal concerns. Best to be safe.
    [GeneratedRegex(@"([A-Za-z]:\\Users\\|[/\\]home[/\\]|/Users/)[^\\/]+", RegexOptions.IgnoreCase)]
    private static partial Regex UserPathRegex();

    private static readonly string HomeDir = Environment.GetFolderPath(
        Environment.SpecialFolder.UserProfile
    );

    public static void Init()
    {
        if (_initialized)
            return;
        if (string.IsNullOrEmpty(DSN))
            return;

        var version =
            Assembly
                .GetEntryAssembly()
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
            ?? "unknown";

        SentrySdk.Init(o =>
        {
            o.Dsn = DSN;

            o.SendDefaultPii = false;
#if DEBUG
            o.ServerName = "a_developer";
#else
            o.ServerName = "a_user";
#endif

            o.IsGlobalModeEnabled = true;

            o.AutoSessionTracking = false;

            o.Release = version;
#if DEBUG
            o.Environment = "debug";
#else
            o.Environment = "release";
#endif

            o.SetBeforeSend(Scrub);
        });

        _initialized = true;
    }

    public static void Disable()
    {
        if (!_initialized)
            return;

        SentrySdk.Close();
        _initialized = false;
    }

    public static void Flush()
    {
        if (!_initialized)
            return;

        SentrySdk.Flush(TimeSpan.FromSeconds(2));
        SentrySdk.Close();
        _initialized = false;
    }

    public static void CaptureUIThreadExceptions() =>
        Dispatcher.UIThread.UnhandledException += (_, e) => SentrySdk.CaptureException(e.Exception);

    private static SentryEvent? Scrub(SentryEvent e, SentryHint hint)
    {
        e.ServerName = null;

        // scrub everything
        if (e.Message != null)
        {
            e.Message.Message = ScrubText(e.Message.Message);
            e.Message.Formatted = ScrubText(e.Message.Formatted);
        }
        // and anything
        foreach (var ex in e.SentryExceptions ?? [])
        {
            ex.Value = ScrubText(ex.Value);

            foreach (var frame in ex.Stacktrace?.Frames ?? [])
            {
                frame.FileName = ScrubText(frame.FileName);
                frame.AbsolutePath = ScrubText(frame.AbsolutePath);
            }
        }

        return e;
    }

    private static string? ScrubText(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        if (!string.IsNullOrEmpty(HomeDir))
            input = input.Replace(HomeDir, "~", StringComparison.OrdinalIgnoreCase);

        return UserPathRegex().Replace(input, "$1<user>");
    }
}
