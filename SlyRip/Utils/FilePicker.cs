using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SlyRip.Utils;

// if you're looking at this and wondering why theres linux shit here too,
// its because i stole this from a separate project that was meant to be cross platform
internal static class FilePicker {
    internal static async Task<string?> GetPath() {
        try {
            var lifetime = Application.Current?.ApplicationLifetime;

            if (lifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                var mainWindow = desktop.MainWindow;

                if (mainWindow?.StorageProvider is { CanPickFolder: true } storage) {
                    var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions {
                        AllowMultiple = false,
                        Title = "Select your Yandere Simulator game path."
                    });

                    return folders.Count > 0 ? folders[0].Path.LocalPath : null;
                }
            }
        } catch { }

        return null;
    }

    internal static void OpenPath(string path, bool selectFile = false) {
        if (!Directory.Exists(path))
            return;

        // The code below is mostly untested but I got it from some random ass stack overflow forum so I'm gonna assume it works
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            string args = selectFile ? $"/select,\"{path}\"" : $"\"{path}\"";
            Process.Start(new ProcessStartInfo {
                FileName = "explorer",
                Arguments = args,
                UseShellExecute = true
            });

        } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            string args = selectFile ? $"-R \"{path}\"" : $"\"{path}\"";
            Process.Start("open", args);

        } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            string[] linuxFileManagers = { "xdg-open", "nautilus", "dolphin", "nemo", "thunar" };
            foreach (var fm in linuxFileManagers) {
                try {
                    Process.Start(fm, $"\"{path}\"");
                    break;
                } catch { }
            }
        } else
            throw new PlatformNotSupportedException("Unsupported OS for opening file explorer.");
    }

    internal static string GetLocalFolder() {
        if (OperatingSystem.IsWindows())
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (OperatingSystem.IsLinux())
            return Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        if (OperatingSystem.IsMacOS())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "Application Support");

        throw new PlatformNotSupportedException();
    }

    internal static async Task<string?> GetFile(string extension, string? title = null) {
        try {
            var lifetime = Application.Current?.ApplicationLifetime;

            if (lifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                var mainWindow = desktop.MainWindow;

                if (mainWindow?.StorageProvider is { CanOpen: true } storage) {
                    var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions {
                        AllowMultiple = false,
                        Title = title ?? "Open File",
                        FileTypeFilter = [
                        new FilePickerFileType("Supported File") {
                            Patterns = [$"*{extension}"]
                        }
                    ]
                    });

                    return files.Count > 0 ? files[0].Path.LocalPath : null;
                }
            }
        } catch (Exception ex) {
            Trace.WriteLine($"[FilePicker] Failed to get file: {ex.Message}");
        }

        return null;
    }

    internal static async Task<string?> SaveFile(string defaultName = "file.json", string? title = null) {
        try {
            var lifetime = Application.Current?.ApplicationLifetime;

            if (lifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                var mainWindow = desktop.MainWindow;

                if (mainWindow?.StorageProvider is { CanSave: true } storage) {
                    var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions {
                        Title = title ?? "Save File",
                        SuggestedFileName = defaultName,
                        ShowOverwritePrompt = true,
                        FileTypeChoices = new[] {
                        new FilePickerFileType("JSON File") {
                            Patterns = new[] { "*.json" }
                        }
                    }
                    });

                    return file?.Path.LocalPath;
                }
            }
        } catch {
            // Silently ignore exceptions to avoid crashes
        }

        return null;
    }

}