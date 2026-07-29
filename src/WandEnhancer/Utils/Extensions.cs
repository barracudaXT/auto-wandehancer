using System;
using System.Text;
using System.Threading.Tasks;
using WandEnhancer.Core.Extensions;
using WandEnhancer.Core.Models;
using WandEnhancer.Core.Services;

namespace WandEnhancer.Utils
{
    public static class Extensions
    {
        public static bool CheckWeModPath(string path) =>
            PathExtensions.CheckWeModPath(path);

        public static WeModConfig FindWeMod()
        {
            var locator = new WeModLocator(PathExtensions.CheckWeModPath, allowManualFallback: true);
            var info = locator.LocateAsync().GetAwaiter().GetResult();
            if (info == null) return null;

            return new WeModConfig
            {
                BrandName = "Wand",
                ExecutableName = "Wand.exe",
                RootDirectory = info.BasePath
            };
        }

        public static string Base64Decode(string base64EncodedData) 
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }
        
        public static string Base64Encode(string plainText) 
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
    }
}
