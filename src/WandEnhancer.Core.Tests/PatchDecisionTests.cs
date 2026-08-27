using NUnit.Framework;
using WandEnhancer.Core.Services;

namespace WandEnhancer.Core.Tests
{
    [TestFixture]
    public class PatchDecisionTests
    {
        [Test]
        public void SamePathAndVersion_AndBackupExists_ReturnsTrue()
        {
            Assert.IsTrue(PatchDecision.ShouldSkipPatch(
                @"C:\WeMod\app-12.43.1", "12.43.1",
                @"C:\WeMod\app-12.43.1", "12.43.1",
                alreadyPatchedOnDisk: true),
                "Expected skip=true when path and version match");
        }

        [Test]
        public void SamePath_DifferentVersion_AndBackupExists_ReturnsFalse()
        {
            Assert.IsFalse(PatchDecision.ShouldSkipPatch(
                @"C:\WeMod\app-12.43.1", "12.43.1",
                @"C:\WeMod\app-12.43.1", "12.44.0",
                alreadyPatchedOnDisk: true),
                "Expected skip=false when version differs");
        }

        [Test]
        public void DifferentPath_SameVersion_AndBackupExists_ReturnsFalse()
        {
            Assert.IsFalse(PatchDecision.ShouldSkipPatch(
                @"C:\WeMod\app-12.43.1", "12.43.1",
                @"C:\WeMod\app-12.44.0", "12.43.1",
                alreadyPatchedOnDisk: true),
                "Expected skip=false when payload path differs (new app-* folder)");
        }

        [Test]
        public void BackupMissing_ReturnsFalse()
        {
            Assert.IsFalse(PatchDecision.ShouldSkipPatch(
                @"C:\WeMod\app-12.43.1", "12.43.1",
                @"C:\WeMod\app-12.43.1", "12.43.1",
                alreadyPatchedOnDisk: false),
                "Expected skip=false when backup is missing on disk");
        }
    }
}
