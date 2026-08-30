using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using AsarSharp;
using WandEnhancer.Core.Models;

namespace WandEnhancer.Core
{
    public class Enhancer
    {
        private const string ResourcesDirectoryName = "resources";
        private const string AppAsarFileName = "app.asar";
        private const string AppAsarUnpackedDirectoryName = "app.asar.unpacked";
        private const string AppAsarBackupFileName = "app.asar.backup";
        private const string AppAsarUnpackedBackupDirectoryName = "app.asar.unpacked.backup";
        private const string WebPanelDirectoryName = "web-panel";
        private const string WebPanelDistDirectoryName = "dist";
        private const string LocalCustomScriptsDirectoryName = "renderer-scripts";
        private const string RemotePanelDirectoryName = "remote-panel";
        private const string RemoteBridgeTargetFileName = "bridge.cjs";
        private const string RemoteRendererScriptsDirectoryName = "renderer-scripts";
        private const string EmbeddedRemotePanelDistPrefix = "remote-panel/dist/";
        private const string JavaScriptFileExtension = ".js";
        private const string JavaScriptFileSearchPattern = "*.js";
        private const string DuplicateScriptSuffix = ".custom";
        private const int FirstDuplicateScriptIndex = 1;

        private readonly WeModConfig _weModConfig;
        private readonly Action<string, ELogType> _logger;
        private readonly PatchConfig _config;
        private readonly string _asarPath;
        private readonly string _backupPath;
        private readonly string _unpackedPath;
        private readonly string _unpackedBackupPath;

        public Enhancer(WeModConfig weModConfig, Action<string, ELogType> logger, PatchConfig config)
        {
            _weModConfig = weModConfig;
            _logger = logger;
            _config = config;

            _asarPath = Path.Combine(weModConfig.RootDirectory, ResourcesDirectoryName, AppAsarFileName);
            _unpackedPath = Path.Combine(weModConfig.RootDirectory, ResourcesDirectoryName, AppAsarUnpackedDirectoryName);
            _backupPath = Path.Combine(weModConfig.RootDirectory, ResourcesDirectoryName, AppAsarBackupFileName);
            _unpackedBackupPath = Path.Combine(weModConfig.RootDirectory, ResourcesDirectoryName, AppAsarUnpackedBackupDirectoryName);
        }
        
        private void PatchAsar()
        {
            var result = PatchEngine.ApplyPatches(
                _unpackedPath,
                new HashSet<EPatchType>(_config.PatchTypes),
                _logger,
                writeResults: true);

            if (!result.AllPatchesApplied)
            {
                var failedPatches = string.Join(", ", result.RemainingPatches.Select(p => p.ToString()));
                throw new Exception($"[ENHANCER] Failed to apply patches: {failedPatches}. The version may not be supported.");
            }
        }

        private static string FindWorkspacePath(params string[] segments)
        {
            string current = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            while (!string.IsNullOrEmpty(current))
            {
                string candidate = Path.Combine(new[] { current }.Concat(segments).ToArray());
                if (Directory.Exists(candidate) || File.Exists(candidate))
                {
                    return candidate;
                }

                current = Directory.GetParent(current)?.FullName;
            }

            throw new FileNotFoundException($"Required workspace artifact not found: {Path.Combine(segments)}");
        }

        public static void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);

            foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = directory.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(destinationDir, relativePath));
            }

            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = file.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var destinationPath = Path.Combine(destinationDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? destinationDir);
                File.Copy(file, destinationPath, true);
            }
        }

        private static int CopyJavaScriptFiles(string sourceDir, string destinationDir)
        {
            if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
            {
                return 0;
            }

            Directory.CreateDirectory(destinationDir);

            int copied = 0;
            foreach (var file in Directory.GetFiles(sourceDir, JavaScriptFileSearchPattern, SearchOption.TopDirectoryOnly))
            {
                File.Copy(file, GetAvailableScriptPath(destinationDir, Path.GetFileName(file)));
                copied++;
            }

            return copied;
        }

        private static string GetAvailableScriptPath(string destinationDir, string fileName)
        {
            string destinationPath = Path.Combine(destinationDir, fileName);
            if (!File.Exists(destinationPath))
            {
                return destinationPath;
            }

            string name = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            for (int index = FirstDuplicateScriptIndex; ; index++)
            {
                destinationPath = Path.Combine(destinationDir, $"{name}{DuplicateScriptSuffix}{index}{extension}");
                if (!File.Exists(destinationPath))
                {
                    return destinationPath;
                }
            }
        }

        private static int CopyEmbeddedDirectory(string resourcePrefix, string destinationDir)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceNames = assembly.GetManifestResourceNames()
                .Where(name => name.StartsWith(resourcePrefix, StringComparison.Ordinal))
                .ToList();

            if (resourceNames.Count == 0)
            {
                return 0;
            }

            Directory.CreateDirectory(destinationDir);

            foreach (var resourceName in resourceNames)
            {
                var relativePath = resourceName.Substring(resourcePrefix.Length)
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar);
                var destinationPath = Path.Combine(destinationDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? destinationDir);

                using (var resource = assembly.GetManifestResourceStream(resourceName))
                {
                    if (resource == null)
                    {
                        throw new FileNotFoundException($"Embedded resource not found: {resourceName}");
                    }

                    using (var output = File.Create(destinationPath))
                    {
                        resource.CopyTo(output);
                    }
                }
            }

            return resourceNames.Count;
        }

        private static string FindLocalCustomScriptsPath()
        {
            string executableDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(executableDirectory))
            {
                return null;
            }

            string localScripts = Path.Combine(executableDirectory, LocalCustomScriptsDirectoryName);
            return Directory.Exists(localScripts) ? localScripts : null;
        }

        private static int CopySelectedJavaScriptFiles(IEnumerable<string> files, string destinationDir)
        {
            if (files == null)
            {
                return 0;
            }

            Directory.CreateDirectory(destinationDir);

            int copied = 0;
            foreach (var file in files.Where(IsJavaScriptFile).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                File.Copy(file, GetAvailableScriptPath(destinationDir, Path.GetFileName(file)));
                copied++;
            }

            return copied;
        }

        private static bool IsJavaScriptFile(string file)
        {
            return File.Exists(file) && string.Equals(Path.GetExtension(file), JavaScriptFileExtension, StringComparison.OrdinalIgnoreCase);
        }

        private void InjectRemotePanelFiles()
        {
            if (!_config.PatchTypes.Contains(EPatchType.RemoteWebPanelPreview))
            {
                return;
            }

            string localCustomScriptsRoot = FindLocalCustomScriptsPath();
            string targetRoot = Path.Combine(_unpackedPath, RemotePanelDirectoryName);
            string targetScriptsRoot = Path.Combine(targetRoot, RemoteRendererScriptsDirectoryName);
            string targetBridgePath = Path.Combine(targetRoot, RemoteBridgeTargetFileName);

            if (Directory.Exists(targetRoot))
            {
                Directory.Delete(targetRoot, true);
            }

            if (CopyEmbeddedDirectory(EmbeddedRemotePanelDistPrefix, targetRoot) == 0)
            {
                CopyDirectory(FindWorkspacePath(WebPanelDirectoryName, WebPanelDistDirectoryName), targetRoot);
            }

            if (!File.Exists(targetBridgePath))
            {
                throw new FileNotFoundException("[ENHANCER] Remote bridge artifact is missing. Run `cd web-panel && pnpm run build` before patching.", targetBridgePath);
            }

            int defaultScriptCount = Directory.Exists(targetScriptsRoot)
                ? Directory.GetFiles(targetScriptsRoot, JavaScriptFileSearchPattern, SearchOption.TopDirectoryOnly).Length
                : 0;
            if (defaultScriptCount == 0)
            {
                throw new FileNotFoundException("[ENHANCER] Remote renderer script artifacts are missing. Run `cd web-panel && pnpm run build` before patching.", targetScriptsRoot);
            }

            int selectedScriptCount = CopySelectedJavaScriptFiles(_config.CustomScriptPaths, targetScriptsRoot);
            int localScriptCount = CopyJavaScriptFiles(localCustomScriptsRoot, targetScriptsRoot);

            _logger($"[ENHANCER] Injected remote panel assets and renderer scripts into app.asar (default: {defaultScriptCount}, selected: {selectedScriptCount}, local: {localScriptCount})", ELogType.Info);
        }

        private static readonly byte[] FuseSentinel =
            Encoding.ASCII.GetBytes("dL7pKGdnNz796PbbjQWNKmHXBZaB9tsX");

        private const int FuseAsarIntegrityIndex = 4;
        private const byte FuseStateRemoved = (byte)'r';

        private void PatchElectronFuse()
        {
            var exePath = _weModConfig.ExecutablePath;
            if (!File.Exists(exePath))
            {
                _logger("[ENHANCER] Electron executable not found, skipping fuse patch", ELogType.Warn);
                return;
            }

            try
            {
                using (var fs = new FileStream(exePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                {
                    long sentinelPos = FindSentinel(fs, FuseSentinel);
                    if (sentinelPos < 0)
                    {
                        _logger("[ENHANCER] Fuse sentinel not found in executable, skipping fuse patch", ELogType.Warn);
                        return;
                    }

                    long headerPos = sentinelPos + FuseSentinel.Length;
                    fs.Seek(headerPos, SeekOrigin.Begin);
                    int version = fs.ReadByte();
                    int wireLength = fs.ReadByte();

                    if (version != 1)
                    {
                        _logger($"[ENHANCER] Unsupported fuse version {version}, skipping fuse patch", ELogType.Warn);
                        return;
                    }

                    if (wireLength < FuseAsarIntegrityIndex + 1)
                    {
                        _logger($"[ENHANCER] Fuse wire too short ({wireLength}), skipping fuse patch", ELogType.Warn);
                        return;
                    }

                    long fusePos = headerPos + 2 + FuseAsarIntegrityIndex;
                    fs.Seek(fusePos, SeekOrigin.Begin);
                    int currentValue = fs.ReadByte();

                    if (currentValue == FuseStateRemoved)
                    {
                        _logger("[ENHANCER] Electron fuse already patched", ELogType.Info);
                        return;
                    }

                    fs.Seek(fusePos, SeekOrigin.Begin);
                    fs.WriteByte(FuseStateRemoved);
                    _logger("[ENHANCER] Electron fuse patched (asar integrity check disabled)", ELogType.Info);
                }
            }
            catch (Exception ex)
            {
                _logger($"[ENHANCER] Failed to patch Electron fuse: {ex.Message}", ELogType.Warn);
            }
        }

        private static long FindSentinel(FileStream fs, byte[] sentinel)
        {
            const int chunkSize = 64 * 1024;
            var buffer = new byte[chunkSize + sentinel.Length - 1];
            long filePos = 0;

            while (filePos < fs.Length - sentinel.Length)
            {
                fs.Seek(filePos, SeekOrigin.Begin);
                int bytesRead = fs.Read(buffer, 0, buffer.Length);
                if (bytesRead < sentinel.Length)
                    break;

                int searchLen = bytesRead - sentinel.Length + 1;
                for (int i = 0; i < searchLen; i++)
                {
                    if (buffer[i] != sentinel[0])
                        continue;

                    bool match = true;
                    for (int j = 1; j < sentinel.Length; j++)
                    {
                        if (buffer[i + j] != sentinel[j])
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                        return filePos + i;
                }

                filePos += bytesRead - sentinel.Length + 1;
            }

            return -1;
        }

        private void AttachProxyDll()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var dll = assembly.GetManifestResourceStream(Constants.ProxyDllResourceName);
            if (dll == null)
            {
                throw new Exception("[ENHANCER] Proxy DLL resource not found");
            }
            var destPath = Path.Combine(_weModConfig.RootDirectory, "version.dll");
            using (var fileStream = File.Create(destPath))
            {
                dll.CopyTo(fileStream);
            }
            _logger("[ENHANCER] Proxy DLL attached", ELogType.Info);
        }

        public void Patch()
        {
            if (!File.Exists(_backupPath))
            {
                _logger("[ENHANCER] Creating backup...", ELogType.Info);
                File.Copy(_asarPath, _backupPath);
            }
            else
            {
                _logger("[ENHANCER] Backup found, restoring pristine app.asar before patching...", ELogType.Info);
                File.Copy(_backupPath, _asarPath, true);
            }

            if (!Directory.Exists(_unpackedBackupPath) && Directory.Exists(_unpackedPath))
            {
                _logger("[ENHANCER] Creating backup of app.asar.unpacked...", ELogType.Info);
                CopyDirectory(_unpackedPath, _unpackedBackupPath);
            }
            else if (Directory.Exists(_unpackedBackupPath))
            {
                _logger("[ENHANCER] Restoring pristine app.asar.unpacked before patching...", ELogType.Info);
                if (Directory.Exists(_unpackedPath))
                {
                    Directory.Delete(_unpackedPath, true);
                }

                CopyDirectory(_unpackedBackupPath, _unpackedPath);
            }
            else if (!Directory.Exists(_unpackedPath))
            {
                throw new Exception("[ENHANCER] app.asar.unpacked is missing and no backup exists. Restore the original Wand installation files or reinstall Wand, then patch again.");
            }

            if(!File.Exists(_asarPath))
            {
                throw new Exception("app.asar not found");
            }

            try
            {
                _logger("[ENHANCER] Extracting app.asar...", ELogType.Info);
                AsarExtractor.ExtractAll(_asarPath, _unpackedPath);
            }
            catch (Exception e)
            {
                throw new Exception($"[ENHANCER] Failed to unpack app.asar: {e.Message}");
            }
            
            PatchAsar();
            InjectRemotePanelFiles();

            try
            {
                new AsarCreator(_unpackedPath, _asarPath, new CreateOptions
                {
                    Unpack = new Regex(@"^static\\unpacked.*$")
                }).CreatePackageWithOptions();
            }
            catch (Exception e)
            {
                throw new Exception($"[ENHANCER] Failed to pack app.asar: {e.Message}");
            }
            
            PatchElectronFuse();
            AttachProxyDll();

            _logger("[ENHANCER] Done!", ELogType.Success);
        }
    }
}
