using System;

namespace WandEnhancer.Core.Services
{
    public static class PatchDecision
    {
        /// <summary>
        /// Decides whether auto-patch should skip patching because the exact same
        /// install (payload folder + version) was already successfully patched.
        /// A version or folder change must NEVER skip — that's the update-detection fix.
        /// </summary>
        public static bool ShouldSkipPatch(
            string lastPatchedPath,
            string lastPatchedVersion,
            string currentPath,
            string currentVersion,
            bool alreadyPatchedOnDisk)
        {
            if (!alreadyPatchedOnDisk) return false;

            return string.Equals(lastPatchedPath, currentPath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(lastPatchedVersion, currentVersion, StringComparison.OrdinalIgnoreCase);
        }
    }
}
