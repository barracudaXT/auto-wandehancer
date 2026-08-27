namespace WandEnhancer.Services
{
    public interface IScheduledTaskRegistrar
    {
        void Create(string wandPath, string autoPatchExePath);
        void Delete();
        bool Exists();
    }
}
