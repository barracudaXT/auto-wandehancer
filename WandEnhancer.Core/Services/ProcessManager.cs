using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace WandEnhancer.Core.Services
{
    public class ProcessManager : IProcessManager
    {
        private readonly string[] _processNames = { "Wand", "WeMod" };
        private readonly ILogger _logger;

        public ProcessManager(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task TerminateAllWandProcessesAsync(TimeSpan timeout)
        {
            var processes = _processNames
                .SelectMany(name => Process.GetProcessesByName(name))
                .Distinct()
                .ToList();

            if (!processes.Any()) return;

            _logger.Info($"Terminating {processes.Count} Wand process(es).");

            foreach (var process in processes)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.CloseMainWindow();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to close main window of process {process.Id}: {ex.Message}");
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(2));

            var deadline = DateTime.UtcNow + timeout;
            foreach (var process in processes.ToList())
            {
                try
                {
                    if (!process.HasExited)
                    {
                        if (!process.WaitForExit((int)Math.Max(0, (deadline - DateTime.UtcNow).TotalMilliseconds)))
                        {
                            _logger.Info($"Force killing process {process.Id}.");
                            process.Kill();
                            process.WaitForExit(2000);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to terminate process {process.Id}: {ex.Message}");
                    throw;
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
    }
}
