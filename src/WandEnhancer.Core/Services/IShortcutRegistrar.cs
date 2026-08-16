namespace WandEnhancer.Core.Services
{
    public interface IShortcutRegistrar
    {
        void Register(string wandPath, string autoPatchExePath);
        void Unregister();
        bool IsRegistered();
    }
}
