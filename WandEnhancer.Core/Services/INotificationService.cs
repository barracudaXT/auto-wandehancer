namespace WandEnhancer.Core.Services
{
    public interface INotificationService
    {
        void ShowInfo(string title, string message);
        void ShowWarning(string title, string message);
        void ShowError(string title, string message);
    }
}
