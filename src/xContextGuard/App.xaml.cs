using System.Diagnostics;
using System.Security.Principal;
using System.Windows;

namespace xContextGuard;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (!IsAdministrator())
        {
            try
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true,
                        Verb = "runas"
                    };

                    foreach (var arg in e.Args)
                    {
                        startInfo.ArgumentList.Add(arg);
                    }

                    Process.Start(startInfo);
                }
            }
            catch
            {
                // Elevation was cancelled by the user or failed
            }

            Shutdown();
            return;
        }

        base.OnStartup(e);
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
