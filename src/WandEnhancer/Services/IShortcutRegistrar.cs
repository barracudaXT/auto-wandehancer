namespace WandEnhancer.Services
{
    public interface IShortcutRegistrar
    {
        void Register(string wandPath, string autoPatchExePath);
        void Unregister();
    }
}
