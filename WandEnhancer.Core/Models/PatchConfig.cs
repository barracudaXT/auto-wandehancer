using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using WandEnhancer.Core.Extensions;

// EPatchType comes from the upstream 2.x engine; the fork's config carries the
// same selection plus the auto-patch bookkeeping 2.x does not have.
using EPatchType = WandEnhancer.Models.EPatchType;

namespace WandEnhancer.Core.Models
{
    public sealed class PatchConfig
    {
        private string _path;

        public HashSet<EPatchType> PatchTypes { get; set; } = new HashSet<EPatchType>
        {
            EPatchType.ActivatePro,
            EPatchType.RemoteWebPanelPreview
        };

        public List<string> CustomScriptPaths { get; set; } = new List<string>();

        public bool AutoApplyPatches { get; set; }

        // Whether the auto-patch watcher should run. Persisted so the tray toggle
        // survives a restart instead of silently re-enabling itself.
        public bool WatcherEnabled { get; set; } = true;

        public bool PatchingCompleted { get; set; }

        // The resolved payload folder (e.g. ...\app-12.44.0) and Wand version that
        // were last successfully patched. Used by auto-patch to detect updates.
        public string LastPatchedPayloadPath { get; set; }
        public string LastPatchedVersion { get; set; }

        [JsonIgnore]
        public WeModConfig AppProps { get; private set; }

        public string Path
        {
            get => _path;
            set
            {
                if (value != null && !PathExtensions.CheckWeModPath(value))
                {
                    var resolved = PathExtensions.ResolveWeModPayloadPath(value);
                    if (resolved == null)
                        throw new Exception("Invalid WeMod path");

                    // Store the root path (e.g. %LocalAppData%\WeMod) but expose the
                    // resolved payload path through AppProps.
                    _path = value;
                    var exeName = PathExtensions.GetWeModExecutableName(resolved) ?? "Wand.exe";
                    AppProps = new WeModConfig
                    {
                        BrandName = "Wand",
                        ExecutableName = exeName,
                        RootDirectory = resolved
                    };
                    return;
                }

                _path = value;
                if (_path == null)
                {
                    AppProps = null;
                    return;
                }

                var directExeName = PathExtensions.GetWeModExecutableName(_path) ?? "Wand.exe";
                AppProps = new WeModConfig
                {
                    BrandName = "Wand",
                    ExecutableName = directExeName,
                    RootDirectory = _path
                };
            }
        }
    }
}
