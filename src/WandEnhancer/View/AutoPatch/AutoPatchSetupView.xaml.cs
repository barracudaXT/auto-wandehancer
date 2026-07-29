using System.IO;
using WandEnhancer.Core.Services;
using WandEnhancer.View.MainWindow;

namespace WandEnhancer.View.AutoPatch
{
    public partial class AutoPatchSetupView
    {
        public AutoPatchSetupView()
        {
            InitializeComponent();

            var settingsPath = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "WandEnhancer",
                Constants.AppSettingsFileName);

            DataContext = new AutoPatchSetupVm(new SettingsStore(settingsPath));
        }
    }
}
