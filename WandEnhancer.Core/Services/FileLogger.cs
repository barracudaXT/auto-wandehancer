using System;
using System.IO;

namespace WandEnhancer.Core.Services
{
    public class FileLogger : ILogger
    {
        private readonly string _logDirectory;
        private readonly object _lock = new object();

        public FileLogger(string logDirectory)
        {
            _logDirectory = logDirectory ?? throw new ArgumentNullException(nameof(logDirectory));
            Directory.CreateDirectory(_logDirectory);
        }

        public void Info(string message) => Write("INFO", message);
        public void Error(string message) => Write("ERROR", message);

        private void Write(string level, string message)
        {
            var fileName = $"auto-patch-{DateTime.Now:yyyyMMdd}.log";
            var line = $"{DateTime.Now:O} [{level}] {message}{Environment.NewLine}";
            lock (_lock)
            {
                File.AppendAllText(Path.Combine(_logDirectory, fileName), line);
            }
        }
    }
}
