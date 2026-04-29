using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SlyRip.Utils;
using System;
using System.Collections.ObjectModel;
using System.IO;

namespace SlyRip;
public partial class MainWindow : Window {
    public static MainWindow instance = null!;

    private VRCMonitor _monitor;

    public MainWindow() {
        instance = this;

        InitializeComponent();

        _monitor = new VRCMonitor(path);

        // i was too lazy to mke a proper logging system, whatever
        // sue me.
        _monitor.OnLog += (msg) => Log(msg);

        _monitor.Start();
    }

    #region Header
    private void OnDrag(object? sender, PointerPressedEventArgs e) => BeginMoveDrag(e);

    static string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SlyRip", "Out");
    private void OnFolderOpen(object? sender, RoutedEventArgs e) {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        // yes, this is how i tested if the log fix worked and now im keeping it because i think its funny
        //for (int i = 0; i < 100; i++) {
        //    Log($"Balls {i}");
        //}

        FilePicker.OpenPath(path);
    }

    private void OnMinimise(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
    #endregion

    #region Console
    private const int MaxLines = 12;
    void Log(object msg) {
        var text = msg?.ToString() ?? string.Empty;

        Avalonia.Threading.Dispatcher.UIThread.Post(() => {
            var current = ConsoleOutput.Text;

            if (current.EndsWith(">_"))
                current = current[..^2];

            current += "> " + text + Environment.NewLine;

            var lines = current.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > MaxLines)
                lines = lines[^MaxLines..];

            ConsoleOutput.Text = string.Join('\n', lines) + Environment.NewLine + ">_";
        });
    }
    #endregion

    #region Controls
    static string cachePath = Path.Combine($"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}Low", "VRChat\\VRChat\\Cache-WindowsPlayer");
    private void OnOpenVRCCache(object? sender, RoutedEventArgs e) {
        if (!Directory.Exists(cachePath)) {
            Log("VRChat cache path does not exist.");
            return;
        }

        FilePicker.OpenPath(cachePath);
    }

    static string avisPath = Path.Combine(path, "Avatars");
    private void OnClearAvis(object? sender, RoutedEventArgs e) {
        if (!Directory.Exists(avisPath))
            return;

        foreach (var dir in Directory.EnumerateDirectories(avisPath))
            Directory.Delete(dir, true);
    }

    static string worldsPath = Path.Combine(path, "Worlds");
    private void OnClearWorlds(object? sender, RoutedEventArgs e) {
        if (!Directory.Exists(worldsPath))
            return;

        foreach (var dir in Directory.EnumerateDirectories(worldsPath))
            Directory.Delete(dir, true);
    }

    #endregion
}