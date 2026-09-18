using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using WandEnhancer.Core.Extensions;
using WandEnhancer.Core.Models;

namespace WandEnhancer.Core.Services
{
    public class Patcher : IPatcher
    {
        private readonly Action<string, ELogType> _logger;

        public Patcher(Action<string, ELogType> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task PatchAsync(WeModInfo info, PatchConfig config)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            if (config == null) throw new ArgumentNullException(nameof(config));

            return Task.Run(() =>
            {
                _logger($"Starting patch for Wand at {info.BasePath}", ELogType.Info);

                // Upstream 2.x owns the engine and takes its own config types, so the
                // fork's bookkeeping config is mapped onto them here rather than being
                // merged into the engine.
                var rootDirectory = info.RootPath ?? info.BasePath;
                var exeName = PathExtensions.GetWeModExecutableName(rootDirectory)
                              ?? PathExtensions.GetWeModExecutableName(info.BasePath)
                              ?? "Wand.exe";

                var engineConfig = new WandEnhancer.Models.PatchConfig
                {
                    PatchTypes = config.PatchTypes == null
                        ? new HashSet<WandEnhancer.Models.EPatchType>()
                        : new HashSet<WandEnhancer.Models.EPatchType>(config.PatchTypes),
                    CustomScriptPaths = config.CustomScriptPaths ?? new List<string>()
                };

                var engineInstall = new WandEnhancer.Models.WeModConfig
                {
                    BrandName = "Wand",
                    ExecutableName = exeName,
                    RootDirectory = rootDirectory
                };

                // 2.x's engine: extracts, applies the structural patches, handles the
                // fuse in-process, and rolls back on failure.
                new WandEnhancer.Core.Enhancer(engineInstall, _logger, engineConfig).Patch();

                config.PatchingCompleted = true;
                _logger("Patch completed successfully.", ELogType.Info);
            });
        }
    }
}
