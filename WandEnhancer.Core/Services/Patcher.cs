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
                // The engine resolves <root>\resources\app.asar, so it needs the payload
                // directory (app-<version>), not the WeMod root that contains it. These
                // two are named the other way round upstream, and the fork's WeModInfo
                // follows upstream: RootPath is the WeMod root, BasePath is the payload.
                // Passing RootPath made every patch fail with DirectoryNotFoundException
                // on <WeMod>\resources\.incomplete-patch, aborting --launch with it.
                var rootDirectory = info.BasePath ?? info.RootPath;
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
