namespace WandEnhancer.Core.Models
{
    // The fork's view of an install. Deliberately separate from the engine's
    // WandEnhancer.Core.Models.WeModConfig: this one carries the resolved payload
    // folder plus the Squirrel root, which the auto-patch layer needs.
    public class WeModConfig
    {
        public string BrandName { get; set; }
        public string ExecutableName { get; set; }
        public string RootDirectory { get; set; }
    }
}
