using System.Diagnostics;
using System.Security.Principal;

namespace WandEnhancer.Services
{
    public static class ElevationHelper
    {
        public static bool IsElevated
        {
            get
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
        }

        public static void RelaunchElevated(string arguments)
        {
            var currentProcess = Process.GetCurrentProcess();
            var startInfo = new ProcessStartInfo
            {
                FileName = currentProcess.MainModule.FileName,
                Arguments = arguments,
                Verb = "runas",
                UseShellExecute = true
            };

            Process.Start(startInfo);
        }
    }
}
