namespace WandEnhancer.Core.Services
{
    public class NullNotificationService : INotificationService
    {
        public void ShowInfo(string title, string message) { }
        public void ShowWarning(string title, string message) { }
        public void ShowError(string title, string message) { }
    }
}
