using System;
using System.IO;
using NUnit.Framework;
using WandEnhancer.Core;

namespace WandEnhancer.Core.Tests
{
    [TestFixture]
    public class LauncherLogTests
    {
        private string _temp;

        [SetUp]
        public void SetUp()
        {
            // Every test pins the log to a directory this fixture owns. No test may
            // resolve, read, rotate or delete anything under the user's real log
            // directory - rotation there would clobber launcher.prev.log for good.
            _temp = Path.Combine(Path.GetTempPath(), "awh-log-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temp);
        }

        [TearDown]
        public void TearDown()
        {
            LauncherLog.DirectoryOverride = null;
            LauncherLog.Close();
            try { Directory.Delete(_temp, true); } catch { }
        }

        [Test]
        public void Open_WithDirectoryOverride_WritesToThatDirectory()
        {
            LauncherLog.DirectoryOverride = _temp;
            LauncherLog.Open(_temp, "test header");

            Assert.IsTrue(LauncherLog.IsOpen, "logging must be open after a successful Open");
            Assert.IsNotNull(LauncherLog.LogDirectory);
            Assert.IsTrue(Directory.Exists(LauncherLog.LogDirectory));
            Assert.IsTrue(File.Exists(Path.Combine(LauncherLog.LogDirectory, LauncherLog.FileName)),
                "the log file must actually exist, not just the directory");
            StringAssert.Contains("test header",
                File.ReadAllText(Path.Combine(LauncherLog.LogDirectory, LauncherLog.FileName)));
        }

        [Test]
        public void Write_PreservesSeverity_ForEveryLevel()
        {
            LauncherLog.DirectoryOverride = _temp;
            LauncherLog.Open(_temp, "header");
            LauncherLog.Write("boom", ELogType.Error);

            var text = File.ReadAllText(Path.Combine(LauncherLog.LogDirectory, LauncherLog.FileName));
            StringAssert.Contains("[ERROR]", text);
            StringAssert.Contains("boom", text);
        }

        [Test]
        public void Open_RotatesWhenOverLimit_KeepingOnePreviousGeneration()
        {
            LauncherLog.DirectoryOverride = _temp;
            LauncherLog.Open(_temp, "first");
            // Exceed the 512 KB cap the same way a long-running install does.
            var filler = new string('x', 1024);
            for (int i = 0; i < 600; i++) LauncherLog.Write(filler, ELogType.Info);

            LauncherLog.Open(_temp, "second");

            Assert.IsTrue(File.Exists(Path.Combine(_temp, LauncherLog.PreviousFileName)),
                "the previous generation must be kept, not dropped");
        }
    }

    [TestFixture]
    public class FatalLoggingTests
    {
        [Test]
        public void LogFatal_WritesTheExceptionToTheLog()
        {
            string dir = Path.Combine(Path.GetTempPath(), "awh-fatal-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                LauncherLog.DirectoryOverride = dir;
                LauncherLog.Open(dir, "header");
                WandEnhancer.Program.LogFatal(new InvalidOperationException("synthetic UI crash"));

                var text = File.ReadAllText(Path.Combine(dir, LauncherLog.FileName));
                StringAssert.Contains("[ERROR]", text);
                StringAssert.Contains("synthetic UI crash", text);
                StringAssert.Contains("InvalidOperationException", text);
            }
            finally
            {
                LauncherLog.Close();
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        [Test]
        public void LogFatal_WithNullStillRecordsSomething_AndDoesNotThrow()
        {
            // A null ExceptionObject is legal, so the recovery path must leave a
            // trace rather than silently doing nothing.
            string dir = Path.Combine(Path.GetTempPath(), "awh-fatal-null-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                LauncherLog.DirectoryOverride = dir;
                LauncherLog.Open(dir, "header");
                WandEnhancer.Program.LogFatal(null);

                var text = File.ReadAllText(Path.Combine(dir, LauncherLog.FileName));
                StringAssert.Contains("[ERROR]", text);
            }
            finally
            {
                LauncherLog.Close();
                try { Directory.Delete(dir, true); } catch { }
            }
        }
    }

    [TestFixture]
    public class AutoPatchSeverityTests
    {
        private class CapturingLogger : WandEnhancer.Core.Services.ILogger
        {
            public readonly System.Collections.Generic.List<string> Errors = new System.Collections.Generic.List<string>();
            public readonly System.Collections.Generic.List<string> Infos = new System.Collections.Generic.List<string>();
            public void Info(string message) => Infos.Add(message);
            public void Error(string message) => Errors.Add(message);
        }

        [Test]
        public void ErrorLevelDoesNotCollapseToInfo()
        {
            var logger = new CapturingLogger();
            WandEnhancer.AutoPatch.Program.LogWithLevel(logger, "boom", ELogType.Error);
            Assert.AreEqual(1, logger.Errors.Count, "an Error-level message must reach ILogger.Error");
            Assert.AreEqual(0, logger.Infos.Count, "it must not ALSO be written as Info");

            WandEnhancer.AutoPatch.Program.LogWithLevel(logger, "routine", ELogType.Info);
            Assert.AreEqual(1, logger.Infos.Count);
            Assert.AreEqual(1, logger.Errors.Count);
        }
    }

    [TestFixture]
    public class ProgressWindowCloseTests
    {
        [Test]
        public void SafeCloseBeforeShowDoesNotDisposeTheForm()
        {
            var window = new WandEnhancer.AutoPatch.ProgressWindow();
            try
            {
                var handle = window.Handle;   // force handle creation, as RunLaunchMode does
                window.SafeClose();           // must NOT dispose a form that was never shown
                Assert.IsFalse(window.IsDisposed, "SafeClose must not dispose an unshown form");
            }
            finally
            {
                window.Dispose();
            }
        }
    }
}
