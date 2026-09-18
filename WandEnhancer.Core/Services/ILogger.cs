namespace WandEnhancer.Core.Services
{
    public interface ILogger
    {
        void Info(string message);
        void Error(string message);
    }
}
