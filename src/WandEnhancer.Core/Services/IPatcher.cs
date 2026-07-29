using System.Threading.Tasks;
using WandEnhancer.Core.Models;

namespace WandEnhancer.Core.Services
{
    public interface IPatcher
    {
        Task PatchAsync(WeModInfo info, PatchConfig config);
    }
}
