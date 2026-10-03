using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FamilyPCMonitor.Services;

public static class DiscordSender
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static bool flushing;
    public static void SaveWebhook(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "discord.com" || !uri.AbsolutePath.StartsWith("/api/webhooks/"))) throw new ArgumentException("Enter a Discord HTTPS webhook URL.");
        MonitorService.Config.ProtectedWebhook = string.IsNullOrWhiteSpace(value) ? null : Convert.ToBase64String(Protect(Encoding.UTF8.GetBytes(value)));
        MonitorService.Save();
    }
    private static byte[] Protect(byte[] bytes) { var input = new Blob(bytes); if (!CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out var output)) throw new CryptographicException(Marshal.GetLastWin32Error()); var result = new byte[output.Length]; Marshal.Copy(output.Data, result, 0, result.Length); LocalFree(output.Data); return result; }
    private static byte[] Unprotect(byte[] bytes) { var input = new Blob(bytes); if (!CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out var output)) throw new CryptographicException(Marshal.GetLastWin32Error()); var result = new byte[output.Length]; Marshal.Copy(output.Data, result, 0, result.Length); LocalFree(output.Data); return result; }
    [StructLayout(LayoutKind.Sequential)] private struct Blob { public int Length; public IntPtr Data; public Blob(byte[] b) { Length=b.Length; Data=Marshal.AllocHGlobal(b.Length); Marshal.Copy(b,0,Data,b.Length); } }
    [DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)] private static extern bool CryptProtectData(ref Blob input,string? description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
    [DllImport("crypt32.dll",SetLastError=true)] private static extern bool CryptUnprotectData(ref Blob input,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr p);
    public static async Task<bool> TestAsync() { var url=GetUrl(); if(url is null) return false; using var response=await Client.PostAsync(url,new StringContent(JsonSerializer.Serialize(new { content=$"✅ Family PC Monitor test — {Environment.MachineName} — {DateTime.Now:g}" }),Encoding.UTF8,"application/json")); response.EnsureSuccessStatusCode(); return true; }
    private static string? GetUrl() => MonitorService.Config.ProtectedWebhook is null ? null : Encoding.UTF8.GetString(Unprotect(Convert.FromBase64String(MonitorService.Config.ProtectedWebhook)));
    public static async Task FlushAsync()
    {
        if(flushing)return; flushing=true;
        try { var url=GetUrl(); if(url is null)return; var delay=1000; for(var attempt=0;attempt<5;attempt++) { foreach(var ev in MonitorService.Store.Pending()) { if(!MonitorService.Config.Notifications.GetValueOrDefault(ev.Type,true)) { MonitorService.Store.MarkSent(ev.Id); continue; } try { using var response=await Client.PostAsync(url,new StringContent(JsonSerializer.Serialize(new { content=$"{Icon(ev.Type)} **{Label(ev.Type)}**\nDevice: {ev.Computer}\nUser: {ev.User}\nTime: {DateTime.Parse(ev.Local).ToLocalTime():MMMM d, yyyy h:mm tt}" }),Encoding.UTF8,"application/json")); response.EnsureSuccessStatusCode(); MonitorService.Store.MarkSent(ev.Id); } catch { await Task.Delay(delay); delay=Math.Min(delay*2,30000); break; } } if(MonitorService.Store.Pending().Count==0)break; } } catch { } finally { flushing=false; }
    }
    private static string Label(string e)=>e switch {"Startup"=>"Computer Started","Login"=>"User Logged In","Lock"=>"Computer Locked","Unlock"=>"Computer Unlocked","Logout"=>"User Logged Out","Restart"=>"Computer Restarting",_=>"Computer Shut Down"};
    private static string Icon(string e)=>e switch {"Startup"=>"🟢","Lock"=>"🔒","Shutdown"=>"🔴","Restart"=>"🔄",_=>"ℹ️"};
}
