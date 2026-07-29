using WandEnhancer.Core.Models;

namespace WandEnhancer.Core.Services
{
    public interface ISettingsStore
    {
        PatchConfig Load();
        void Save(PatchConfig config);
    }
}
