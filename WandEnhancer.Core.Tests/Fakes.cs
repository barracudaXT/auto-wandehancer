using System;
using System.IO;
using System.Threading.Tasks;
using WandEnhancer.Core.Models;
using WandEnhancer.Core.Services;

namespace WandEnhancer.Core.Tests
{
    internal static class TestHelpers
    {
        public static string CreateFakeWandDir()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "Wand.exe"), "fake");
            Directory.CreateDirectory(Path.Combine(path, "resources"));
            File.WriteAllText(Path.Combine(path, "resources", "app.asar"), "fake");
            return path;
        }
    }

    internal class FakeWeModLocator : IWeModLocator
    {
        private readonly WeModInfo _info;
        public FakeWeModLocator(WeModInfo info) { _info = info; }
        public Task<WeModInfo> LocateAsync(string configuredPath = null) => Task.FromResult(_info);
    }

    internal class FakePatcher : IPatcher
    {
        public int CallCount;
        public Task PatchAsync(WeModInfo info, PatchConfig config)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }

    internal class FakeSettingsStore : ISettingsStore
    {
        private PatchConfig _config;
        public FakeSettingsStore(PatchConfig config) { _config = config; }
        public PatchConfig Load() => _config;
        public void Save(PatchConfig config) { _config = config; }
    }

    internal class FakeProcessManager : IProcessManager
    {
        public Task TerminateAllWandProcessesAsync(TimeSpan timeout) => Task.CompletedTask;
    }

    internal class FakeLogger : ILogger
    {
        public void Info(string message) { }
        public void Error(string message) { }
    }

    internal class FakeNotificationService : INotificationService
    {
        public void ShowInfo(string title, string message) { }
        public void ShowWarning(string title, string message) { }
        public void ShowError(string title, string message) { }
    }
}
