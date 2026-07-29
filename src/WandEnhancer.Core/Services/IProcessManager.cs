using System;
using System.Threading.Tasks;

namespace WandEnhancer.Core.Services
{
    public interface IProcessManager
    {
        Task TerminateAllWandProcessesAsync(TimeSpan timeout);
    }
}
