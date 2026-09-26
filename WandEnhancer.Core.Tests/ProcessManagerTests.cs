using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using WandEnhancer.Core.Services;

namespace WandEnhancer.Core.Tests
{
    [TestFixture]
    public class ProcessManagerTests
    {
        [Test]
        public async Task TerminateAllWandProcessesAsync_KillsDummyWandProcess()
        {
            var logger = new MemoryLogger();
            var manager = new ProcessManager(logger);

            // ProcessManager terminates EVERY process named Wand/WeMod, not just the
            // dummy started here, so running this suite with the real application open
            // closes and then force-kills the user's session. Refuse instead of
            // destroying it; the runner has no Wand session, so coverage is unaffected.
            var preexisting = Process.GetProcessesByName("Wand")
                .Concat(Process.GetProcessesByName("WeMod"))
                .ToArray();
            Assert.IsEmpty(preexisting,
                "A Wand/WeMod process was already running. This test terminates every process " +
                "with that name, so it would kill your live session. Close Wand and re-run.");

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

            Assert.IsFalse(process.HasExited, "Dummy Wand process exited immediately");

            await manager.TerminateAllWandProcessesAsync(TimeSpan.FromSeconds(5));

            Assert.IsTrue(process.HasExited, "Dummy Wand process was not terminated");

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
