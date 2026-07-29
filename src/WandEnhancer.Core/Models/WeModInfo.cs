namespace WandEnhancer.Core.Models
{
    public class WeModInfo
    {
        // The folder that actually contains Wand.exe/resources/app.asar.
        public string BasePath { get; set; }

        // For WeMod-style installs this is the parent folder that contains
        // a launcher stub plus versioned app-* payload subfolders.
        public string RootPath { get; set; }

        public string ExecutablePath { get; set; }
        public string Version { get; set; }
    }
}
