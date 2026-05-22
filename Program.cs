using System.Runtime.Versioning;

namespace DesktopIdle;

internal static class Program
{
    [STAThread]
    [SupportedOSPlatform("windows10.0.19041.0")]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new DesktopIdleContext());
    }
}
