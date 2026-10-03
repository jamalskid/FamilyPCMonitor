using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

namespace FamilyPCMonitor.Services;

public sealed class Settings
{
    public bool Enabled { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public int RetentionDays { get; set; } = 30;
    public string? ProtectedWebhook { get; set; }
    public Dictionary<string, bool> Notifications { get; set; } = new() { ["Startup"] = true, ["Login"] = true, ["Lock"] = true, ["Unlock"] = true, ["Logout"] = true, ["Shutdown"] = true, ["Restart"] = true };
}

public static class MonitorService
{
    public static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FamilyPCMonitor");
    public static string StorePath => Path.Combine(DirectoryPath, "events.db");
    private static readonly string SettingsPath = Path.Combine(DirectoryPath, "settings.json");
    public static readonly EventStore Store = new(StorePath);
    public static Settings Config { get; private set; } = new();
    public static string? LastError { get; private set; }
    public static void Initialize()
    {
        try { Directory.CreateDirectory(DirectoryPath); if (File.Exists(SettingsPath)) Config = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath)) ?? new(); Store.Initialize(); Store.Cleanup(Config.RetentionDays); if (Config.Enabled) { Store.Add("Startup"); _ = DiscordSender.FlushAsync(); } ApplyStartup(); }
        catch (Exception ex) { LastError = ex.Message; }
    }
    public static void Save() { Directory.CreateDirectory(DirectoryPath); File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true })); ApplyStartup(); }
    public static void ApplyStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (key is null) return;
        if (Config.StartWithWindows) key.SetValue("FamilyPCMonitor", $"\"{Environment.ProcessPath}\""); else key.DeleteValue("FamilyPCMonitor", false);
    }
    public static void Record(string type) { if (!Config.Enabled) return; try { Store.Add(type); _ = DiscordSender.FlushAsync(); } catch (Exception e) { LastError = e.Message; } }
}
