using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using WandEnhancer.Core.Services;

namespace WandEnhancer.Core.Tests
{
    /// <summary>
    /// The updater runs its download elevated, so the bytes it verified must be the
    /// bytes it executes. These checks pin the sharing contract that makes that true.
    /// </summary>
    [TestFixture]
    public class UpdateHandoffTests
    {
        [Test]
        public void VerifiedHandle_PermitsRead_ButDeniesRename()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".exe");
            File.WriteAllBytes(path, Encoding.ASCII.GetBytes("verified-installer"));

            try
            {
                // What DownloadAndVerifyAsync hands back.
                using (var handle = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    // The elevated installer must still be able to read the file.
                    using (var childRead = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var buffer = new byte[4];
                        Assert.Greater(childRead.Read(buffer, 0, buffer.Length), 0,
                            "the installer process must be able to read the verified file");
                    }

                    // A substitution replaces the file by renaming a different one over it,
                    // which requires FILE_SHARE_DELETE. FileShare.Read withholds that.
                    Assert.Catch<IOException>(
                        () => File.Move(path, path + ".swapped"),
                        "a rename-based substitution must be blocked while the verified handle is held");
                }

                // Proof the block was due to sharing and not a broken test: once released,
                // the same rename succeeds.
                File.Move(path, path + ".swapped");
                Assert.IsTrue(File.Exists(path + ".swapped"));
            }
            finally
            {
                TryDelete(path);
                TryDelete(path + ".swapped");
            }
        }

        [Test]
        public void VerifiedHandle_BlocksOverwriteInPlace()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".exe");
            File.WriteAllBytes(path, Encoding.ASCII.GetBytes("verified-installer"));

            try
            {
                using (var handle = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    // Writing over the verified bytes must also fail.
                    Assert.Catch<IOException>(
                        () => File.WriteAllBytes(path, Encoding.ASCII.GetBytes("tampered")));
                }
            }
            finally
            {
                TryDelete(path);
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
