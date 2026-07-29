namespace WandEnhancer.AutoPatch
{
    public class AutoPatchArguments
    {
        public string Mode { get; set; }
        public string WeModPath { get; set; }

        public static AutoPatchArguments Parse(string[] args)
        {
            var result = new AutoPatchArguments();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "--patch":
                        result.Mode = "patch";
                        break;
                    case "--launch":
                        result.Mode = "launch";
                        break;
                    case "--watch":
                        result.Mode = "watch";
                        break;
                    default:
                        if (!args[i].StartsWith("--") && string.IsNullOrEmpty(result.WeModPath))
                        {
                            result.WeModPath = args[i];
                        }
                        break;
                }
            }
            return result;
        }
    }
}
