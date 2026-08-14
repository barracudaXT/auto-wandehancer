using System;
using WandEnhancer.Core.Services;

namespace WandEnhancer.Core.Tests
{
    internal static class PatchDecisionTests
    {
        public static void RunAll()
        {
            SamePathAndVersion_AndBackupExists_ReturnsTrue();
            SamePath_DifferentVersion_AndBackupExists_ReturnsFalse();
            DifferentPath_SameVersion_AndBackupExists_ReturnsFalse();
            BackupMissing_ReturnsFalse();
        }

        private static void SamePathAndVersion_AndBackupExists_ReturnsTrue()
        {
            if (!PatchDecision.ShouldSkipPatch(
                @"C:\WeMod\app-12.43.1", "12.43.1",
                @"C:\WeMod\app-12.43.1", "12.43.1",
                alreadyPatchedOnDisk: true))
                throw new Exception("Expected skip=true when path and version match");
        }

        private static void SamePath_DifferentVersion_AndBackupExists_ReturnsFalse()
        {
            if (PatchDecision.ShouldSkipPatch(
                @"C:\WeMod\app-12.43.1", "12.43.1",
                @"C:\WeMod\app-12.43.1", "12.44.0",
                alreadyPatchedOnDisk: true))
                throw new Exception("Expected skip=false when version differs — this is the update-detection regression");
        }

        private static void DifferentPath_SameVersion_AndBackupExists_ReturnsFalse()
        {
            if (PatchDecision.ShouldSkipPatch(
                @"C:\WeMod\app-12.43.1", "12.43.1",
                @"C:\WeMod\app-12.44.0", "12.43.1",
                alreadyPatchedOnDisk: true))
                throw new Exception("Expected skip=false when payload path differs (new app-* folder)");
        }

        private static void BackupMissing_ReturnsFalse()
        {
            if (PatchDecision.ShouldSkipPatch(
                @"C:\WeMod\app-12.43.1", "12.43.1",
                @"C:\WeMod\app-12.43.1", "12.43.1",
                alreadyPatchedOnDisk: false))
                throw new Exception("Expected skip=false when backup is missing on disk");
        }
    }
}
