using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using WandEnhancer.Core.Services;

namespace WandEnhancer.Core.Tests
{
    internal static class ProcessManagerTests
    {
        public static void RunAll()
        {
            TerminateAllWandProcessesAsync_KillsDummyWandProcess().GetAwaiter().GetResult();
        }

        private static async Task TerminateAllWandProcessesAsync_KillsDummyWandProcess()
        {
            var logger = new MemoryLogger();
            var manager = new ProcessManager(logger);

            var cmdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "cmd.exe");
            var dummyExe = Path.Combine(Path.GetTempPath(), "Wand.exe");
            File.Copy(cmdPath, dummyExe, overwrite: true);
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = dummyExe,
                Arguments = "/c ping 127.0.0.1 -n 60 >nul",
                CreateNoWindow = true,
                UseShellExecute = false
            });

            if (process.HasExited)
                throw new Exception("Dummy Wand process exited immediately.");

            await manager.TerminateAllWandProcessesAsync(TimeSpan.FromSeconds(5));

            if (!process.HasExited)
                throw new Exception("Dummy Wand process was not terminated.");

            process.Dispose();
            try { File.Delete(dummyExe); } catch { }
        }
    }

    internal class MemoryLogger : ILogger
    {
        public void Info(string message) { }
        public void Error(string message) { }
    }
}
