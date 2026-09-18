using System.Threading.Tasks;
using WandEnhancer.Core.Models;

namespace WandEnhancer.Core.Services
{
    public interface IWeModLocator
    {
        Task<WeModInfo> LocateAsync(string configuredPath = null);
    }
}
