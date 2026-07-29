using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using WandEnhancer.Core.Extensions;

namespace WandEnhancer.Core.Models
{
    public enum EPatchType
    {
        ActivatePro = 1,
        DisableUpdates = 2,
        DisableTelemetry = 4,
        DevToolsOnF12 = 8,
        RemoteWebPanelPreview = 16
    }

    public sealed class PatchConfig
    {
        private string _path;

        public HashSet<EPatchType> PatchTypes { get; set; } = new HashSet<EPatchType>();

        public List<string> CustomScriptPaths { get; set; } = new List<string>();

        public bool AutoApplyPatches { get; set; }

        [JsonIgnore]
        public WeModConfig AppProps { get; private set; }

        public string Path
        {
            get => _path;
            set
            {
                if (value != null && !PathExtensions.CheckWeModPath(value))
                    throw new Exception("Invalid WeMod path");

                _path = value;
                if (_path == null)
                {
                    AppProps = null;
                    return;
                }

                AppProps = new WeModConfig
                {
                    BrandName = "Wand",
                    ExecutableName = "Wand.exe",
                    RootDirectory = _path
                };
            }
        }
    }
}
