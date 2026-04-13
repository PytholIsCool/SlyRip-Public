using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

internal sealed class VRCMonitor : IDisposable {
    private readonly string _cacheDir;
    private readonly string _outputDir;

    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _cts;

    private readonly object _lock = new();
    private readonly HashSet<string> _processedDataFiles = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Regex AvatarIdRegex =
        new(@"(avtr_[\w-]+)_", RegexOptions.Compiled);

    public Action<string>? OnLog;

    public VRCMonitor(string outputDir) {
        var basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"..\LocalLow\VRChat\VRChat\Cache-WindowsPlayer");

        _cacheDir = Path.GetFullPath(basePath);
        _outputDir = outputDir;
    }

    public void Start() {
        if (_cts != null)
            return;

        Directory.CreateDirectory(_outputDir);

        _cts = new CancellationTokenSource();
        _ = RunAsync(_cts.Token);
    }

    public void Dispose() {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        StopWatcher();
    }

    private void Log(string msg) {
        OnLog?.Invoke(msg);
    }

    private async Task RunAsync(CancellationToken token) {
        if (IsVrChatRunning())
            Log("Restart your VRChat.");

        while (!token.IsCancellationRequested) {
            await WaitForVrChatClosed(token);
            await WaitForCacheRelease(token);

            ClearCache();
            Log("Cache cleared. Waiting for VRChat...");

            await WaitForVrChatStart(token);

            Log("VRChat started. Watching cache...");

            lock (_lock)
                _processedDataFiles.Clear();

            StartWatcher();

            await WaitForVrChatExit(token);

            Log("VRChat closed.");

            StopWatcher();
        }
    }

    private void StartWatcher() {
        if (!Directory.Exists(_cacheDir))
            return;

        _watcher = new FileSystemWatcher(_cacheDir) {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
        };

        _watcher.Created += OnCreated;
    }

    private void StopWatcher() {
        if (_watcher == null)
            return;

        _watcher.EnableRaisingEvents = false;
        _watcher.Created -= OnCreated;
        _watcher.Dispose();
        _watcher = null;
    }

    private void OnCreated(object sender, FileSystemEventArgs e) {
        if (!Directory.Exists(e.FullPath))
            return;

        var folder = e.FullPath;

        _ = Task.Run(async () => {
            await Task.Delay(300);

            var dataFile = FindDataFile(folder);
            if (dataFile == null)
                return;

            lock (_lock) {
                if (_processedDataFiles.Contains(dataFile))
                    return;

                _processedDataFiles.Add(dataFile);
            }

            await ProcessDataFile(dataFile);
        });
    }

    private string? FindDataFile(string folder) {
        try {
            foreach (var file in Directory.EnumerateFiles(folder, "__data", SearchOption.AllDirectories))
                return file;
        } catch { }

        return null;
    }

    private async Task ProcessDataFile(string dataFile) {
        try {
            if (!File.Exists(dataFile))
                return;

            var info = new FileInfo(dataFile);

            if (info.Length < 500 * 1024) {
                long last = info.Length;

                for (int i = 0; i < 6; i++) {
                    await Task.Delay(300);
                    info.Refresh();

                    if (info.Length == last)
                        break;

                    last = info.Length;
                }
            }

            byte[] bytes;

            try {
                bytes = await File.ReadAllBytesAsync(dataFile);
            } catch {
                return;
            }

            var text = Encoding.ASCII.GetString(bytes);

            bool found = false;

            var worldId = ExtractWorldId(text);
            if (worldId != null) {
                SaveCacheFolder("World", worldId, dataFile);
                found = true;
            }

            var avatarMatch = AvatarIdRegex.Match(text);
            if (avatarMatch.Success) {
                SaveCacheFolder("Avatar", avatarMatch.Groups[1].Value, dataFile);
                found = true;
            }

            if (!found)
                Log("Found encrypted cache file");
        } catch { }
    }

    private static string? ExtractWorldId(string text) {
        const string prefix = "wrld_";

        int lastIndex = -1;
        int searchIndex = 0;

        while (true) {
            int idx = text.IndexOf(prefix, searchIndex, StringComparison.Ordinal);
            if (idx == -1)
                break;

            lastIndex = idx;
            searchIndex = idx + prefix.Length;
        }

        if (lastIndex == -1)
            return null;

        int end = lastIndex + prefix.Length;

        while (end < text.Length) {
            char c = text[end];

            bool valid =
                (c >= 'A' && c <= 'Z') ||
                (c >= 'a' && c <= 'z') ||
                (c >= '0' && c <= '9') ||
                c == '_' || c == '-';

            if (!valid)
                break;

            end++;
        }

        if (end <= lastIndex + prefix.Length)
            return null;

        return text.Substring(lastIndex, end - lastIndex);
    }

    private void SaveCacheFolder(string type, string id, string dataFile) {
        string root = type + "s";
        string ending = root == "Avatars" ? ".vrca" : ".vrcw";

        var targetDir = Path.Combine(_outputDir, root, id);
        var targetFile = Path.Combine(targetDir, $"{id}{ending}");

        if (Directory.Exists(targetDir))
            return;

        Directory.CreateDirectory(targetDir);

        try {
            File.Copy(dataFile, targetFile, false);
            Log($"{type} saved: {id}");
        } catch (Exception ex) {
            Log($"Failed to save {type} {id}: {ex.Message}");
        }
    }

    private static bool IsVrChatRunning() {
        return Process.GetProcessesByName("VRChat").Length > 0;
    }

    private async Task WaitForVrChatClosed(CancellationToken token) {
        while (!token.IsCancellationRequested) {
            if (!IsVrChatRunning())
                return;

            await Task.Delay(500, token);
        }
    }

    private async Task WaitForVrChatStart(CancellationToken token) {
        while (!token.IsCancellationRequested) {
            if (IsVrChatRunning()) {
                await Task.Delay(1500, token);
                return;
            }

            await Task.Delay(500, token);
        }
    }

    private async Task WaitForVrChatExit(CancellationToken token) {
        while (!token.IsCancellationRequested) {
            if (!IsVrChatRunning())
                return;

            await Task.Delay(500, token);
        }
    }

    private async Task WaitForCacheRelease(CancellationToken token) {
        for (int i = 0; i < 5; i++) {
            try {
                if (Directory.Exists(_cacheDir))
                    Directory.GetFileSystemEntries(_cacheDir);

                await Task.Delay(300, token);
            } catch {
                await Task.Delay(500, token);
                i--;
            }
        }
    }

    private void ClearCache() {
        if (!Directory.Exists(_cacheDir))
            return;

        foreach (var entry in Directory.EnumerateFileSystemEntries(_cacheDir)) {
            var name = Path.GetFileName(entry);

            if (name.Equals("__info", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("vrc-version", StringComparison.OrdinalIgnoreCase))
                continue;

            try {
                if (Directory.Exists(entry))
                    Directory.Delete(entry, true);
                else
                    File.Delete(entry);
            } catch { }
        }
    }
}