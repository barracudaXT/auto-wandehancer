using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WandEnhancer.Core.Models;

namespace WandEnhancer.Core
{
    /// <summary>
    /// Testable patch engine that applies EnhancerConfig patches to JS files in a directory.
    /// This is the same logic as Enhancer.PatchAsar but operates on an arbitrary directory
    /// and uses a simple logger callback instead of the Enhancer's instance fields.
    /// </summary>
    public static class PatchEngine
    {
        public class PatchResult
        {
            public bool AllPatchesApplied;
            public HashSet<EPatchType> RemainingPatches;
            public Dictionary<string, string> PatchedFiles; // fileName -> patched content
        }

        public static PatchResult ApplyPatches(string directory, HashSet<EPatchType> patchTypes, Action<string, ELogType> logger = null)
        {
            if (logger == null) logger = (msg, type) => { };

            var items = Directory.EnumerateFiles(directory, "*.js", SearchOption.TopDirectoryOnly)
                .Where(IsCandidateBundleFile)
                .ToList();

            if (!items.Any())
                throw new Exception("[ENHANCER] No app bundle found");

            var remainingPatches = new HashSet<EPatchType>(patchTypes);
            var enhancerConfig = EnhancerConfig.GetInstance();
            var patchedFiles = new Dictionary<string, string>();

            foreach (var item in items)
            {
                if (remainingPatches.Count == 0) break;
                if (!CouldFileContainRemainingPatch(item, remainingPatches, enhancerConfig)) continue;

                string data = File.ReadAllText(item);
                bool fileChanged = false;

                foreach (var entry in remainingPatches.ToList())
                {
                    var entries = enhancerConfig[entry];
                    foreach (var patchEntry in entries)
                    {
                        bool patchApplied;
                        data = ApplyJsPatch(item, data, patchEntry, entry, logger, out patchApplied);
                        fileChanged = fileChanged || patchApplied;
                    }

                    if (entries.All(x => x.Applied))
                        remainingPatches.Remove(entry);
                }

                if (fileChanged)
                {
                    patchedFiles[Path.GetFileName(item)] = data;
                }
            }

            return new PatchResult
            {
                AllPatchesApplied = remainingPatches.Count == 0,
                RemainingPatches = remainingPatches,
                PatchedFiles = patchedFiles
            };
        }

        // --- These are exact copies of Enhancer.cs's private methods, made public for testability ---

        public static bool IsCandidateBundleFile(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            return fileName.Equals("index.js", StringComparison.OrdinalIgnoreCase)
                || (fileName.StartsWith("app-", StringComparison.OrdinalIgnoreCase)
                    && fileName.EndsWith(".bundle.js", StringComparison.OrdinalIgnoreCase));
        }

        public static bool CouldFileContainRemainingPatch(string filePath, IEnumerable<EPatchType> remainingPatches, Dictionary<EPatchType, EnhancerConfig.PatchEntry[]> enhancerConfig)
        {
            foreach (var patchType in remainingPatches)
            {
                foreach (var patchEntry in enhancerConfig[patchType])
                {
                    if (patchEntry.Applied) continue;
                    if (CanSearchPatchInFile(filePath, patchEntry)) return true;
                }
            }
            return false;
        }

        public static bool CanSearchPatchInFile(string filePath, EnhancerConfig.PatchEntry patch)
        {
            if (patch.CandidateFileNames == null || patch.CandidateFileNames.Length == 0) return true;
            string fileName = Path.GetFileName(filePath);
            return patch.CandidateFileNames.Any(candidate => fileName.Equals(candidate, StringComparison.OrdinalIgnoreCase));
        }

        public static bool ContainsSearchHint(string source, string[] searchHints)
        {
            if (searchHints == null || searchHints.Length == 0) return true;
            return searchHints.Any(searchHint => source.IndexOf(searchHint, StringComparison.Ordinal) >= 0);
        }

        public static string ApplyJsPatch(string fileName, string js, EnhancerConfig.PatchEntry patch, EPatchType patchType, Action<string, ELogType> logger, out bool patchApplied)
        {
            patchApplied = false;

            if (patch.Applied) return js;
            if (!CanSearchPatchInFile(fileName, patch) || !ContainsSearchHint(js, patch.SearchHints)) return js;

            var match = patch.Target.Match(js);
            if (!match.Success) return js;

            var prefix = $"[ENHANCER] [{patchType} -> {patch.Name}]";

            if (patch.SingleMatch && match.NextMatch().Success)
                throw new Exception($"{prefix} Patch failed. Multiple target functions found. Looks like the version is not supported");

            string patchSource = patch.PatchFactory != null ? patch.PatchFactory(match) : patch.Patch;

            if (patch.Resolver != null)
            {
                string resolvedField = patch.Resolver.Handler(match.Value);
                if (string.IsNullOrEmpty(resolvedField))
                    throw new Exception($"{prefix} Resolver failed to find field name");
                patchSource = patchSource.Replace(patch.Resolver.Placeholder, resolvedField);
            }

            logger($"{prefix} Found target function in: " + Path.GetFileName(fileName), ELogType.Info);

            string newJs;
            if (patch.PatchFactory != null)
            {
                newJs = patch.SingleMatch
                    ? patch.Target.Replace(js, _ => patchSource, 1)
                    : patch.Target.Replace(js, _ => patchSource);
            }
            else
            {
                newJs = patch.SingleMatch
                    ? patch.Target.Replace(js, patchSource, 1)
                    : patch.Target.Replace(js, patchSource);
            }

            logger($"{prefix} Patch applied", ELogType.Success);
            patch.Applied = true;
            patchApplied = true;

            return newJs;
        }
    }
}
