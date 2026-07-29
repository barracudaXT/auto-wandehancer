using System;
using System.Threading.Tasks;
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
                var weModConfig = new WeModConfig
                {
                    BrandName = "Wand",
                    ExecutableName = "Wand.exe",
                    RootDirectory = info.BasePath
                };
                var enhancer = new Enhancer(weModConfig, _logger, config);
                enhancer.Patch();
                _logger("Patch completed successfully.", ELogType.Info);
            });
        }
    }
}
