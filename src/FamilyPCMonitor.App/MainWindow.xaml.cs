using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Forms = System.Windows.Forms;
using FamilyPCMonitor.Services;

namespace FamilyPCMonitor;

public partial class MainWindow : Window
{
    private Forms.NotifyIcon? tray;
    private HwndSource? source;
    private const int WmWtsSessionChange = 0x02B1;
    private const int NotifyForThisSession = 0;
    private const int SessionLogon=5, SessionLogoff=6, SessionLock=7, SessionUnlock=8;
    [DllImport("wtsapi32.dll", SetLastError=true)] private static extern bool WTSRegisterSessionNotification(IntPtr hwnd,int flags);
    [DllImport("wtsapi32.dll")] private static extern bool WTSUnRegisterSessionNotification(IntPtr hwnd);

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_,_) => { SetupTray(); LoadSettings(); Refresh(); };
        SourceInitialized += (_,_) => { source=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle); source?.AddHook(WindowProc); if(source is not null) WTSRegisterSessionNotification(source.Handle,NotifyForThisSession); };
        SystemEvents.SessionEnding += OnSessionEnding;
        StateChanged += (_,_) => { if(WindowState==WindowState.Minimized) Hide(); };
        Closing += (_,e) => { e.Cancel=true; Hide(); };
    }
    private void SetupTray()
    {
        tray=new Forms.NotifyIcon { Icon=System.Drawing.SystemIcons.Information, Text="Family PC Monitor — monitoring visible", Visible=true };
        var menu=new Forms.ContextMenuStrip(); menu.Items.Add("Open Family PC Monitor",null,(_,_)=>ShowWindow()); menu.Items.Add("Exit",null,(_,_)=>ExitApp()); tray.ContextMenuStrip=menu; tray.DoubleClick+=(_,_)=>ShowWindow();
    }
    private void ShowWindow() { Show(); WindowState=WindowState.Normal; Activate(); }
    private void ExitApp() { tray?.Dispose(); tray=null; SystemEvents.SessionEnding-=OnSessionEnding; if(source is not null) { WTSUnRegisterSessionNotification(source.Handle); source.RemoveHook(WindowProc); } Closing-=OnClosingExit; Closing+=OnClosingExit; Close(); }
    private void OnClosingExit(object? s,System.ComponentModel.CancelEventArgs e) { e.Cancel=false; }
    private IntPtr WindowProc(IntPtr hwnd,int msg,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        if(msg==WmWtsSessionChange && MonitorService.Config.Enabled) { var type=wParam.ToInt32() switch { SessionLogon=>"Login",SessionLogoff=>"Logout",SessionLock=>"Lock",SessionUnlock=>"Unlock",_=>null }; if(type is not null) { MonitorService.Record(type); Refresh(); } }
        return IntPtr.Zero;
    }
    private void OnSessionEnding(object? sender,SessionEndingEventArgs e) { if(MonitorService.Config.Enabled) MonitorService.Record("Shutdown"); }
    private void Refresh_Click(object sender,RoutedEventArgs e)=>Refresh();
    private void Refresh()
    {
        StatusText.Text=MonitorService.Config.Enabled?"● Monitoring enabled — visible tray icon":"○ Monitoring paused";
        DeviceText.Text=$"Computer: {Environment.MachineName}"; UserText.Text=$"Windows user: {Environment.UserName}";
        var last=MonitorService.Store.Latest(); LastEventText.Text=last is null?"Last event: None yet":$"Last event: {last.EventType} at {DateTime.Parse(last.LocalTimestamp).ToLocalTime():g}";
        DbText.Text=$"Local database: {MonitorService.StorePath}"; DiscordStatusText.Text=$"Discord webhook: {(MonitorService.Config.ProtectedWebhook is null?"Not configured":"Configured (encrypted locally)")}";
        WebhookStatus.Text=MonitorService.Config.ProtectedWebhook is null?"No webhook saved.":"A webhook is saved securely; the URL is not shown."; EventsGrid.ItemsSource=MonitorService.Store.Get();
    }
    private void LoadSettings()
    {
        MonitoringCheck.IsChecked=MonitorService.Config.Enabled; StartupCheck.IsChecked=MonitorService.Config.StartWithWindows;
        StartedCheck.IsChecked=Notify("Startup"); LoginCheck.IsChecked=Notify("Login"); LockCheck.IsChecked=Notify("Lock"); UnlockCheck.IsChecked=Notify("Unlock"); LogoutCheck.IsChecked=Notify("Logout"); ShutdownCheck.IsChecked=Notify("Shutdown") && Notify("Restart");
        RetentionBox.SelectedIndex=MonitorService.Config.RetentionDays switch {7=>0,90=>2,0=>3,_=>1};
    }
    private static bool Notify(string key)=>MonitorService.Config.Notifications.GetValueOrDefault(key,true);
    private void Search_Click(object sender,RoutedEventArgs e)=>EventsGrid.ItemsSource=MonitorService.Store.Get(SearchBox.Text.Trim());
    private void SaveWebhook_Click(object sender,RoutedEventArgs e) { try { DiscordSender.SaveWebhook(WebhookBox.Password); WebhookBox.Clear(); Refresh(); System.Windows.MessageBox.Show("Webhook saved encrypted for this Windows user."); } catch(Exception ex) { System.Windows.MessageBox.Show(ex.Message,"Could not save webhook",System.Windows.MessageBoxButton.OK,System.Windows.MessageBoxImage.Warning); } }
    private async void TestDiscord_Click(object sender,RoutedEventArgs e) { try { if(await DiscordSender.TestAsync()) System.Windows.MessageBox.Show("Test notification sent."); else System.Windows.MessageBox.Show("Save a webhook first."); } catch { System.Windows.MessageBox.Show("Could not send. Check the webhook and internet connection. Credentials are not written to logs."); } }
    private void SaveSettings_Click(object sender,RoutedEventArgs e)
    {
        MonitorService.Config.Notifications=new() { ["Startup"]=StartedCheck.IsChecked==true,["Login"]=LoginCheck.IsChecked==true,["Lock"]=LockCheck.IsChecked==true,["Unlock"]=UnlockCheck.IsChecked==true,["Logout"]=LogoutCheck.IsChecked==true,["Shutdown"]=ShutdownCheck.IsChecked==true,["Restart"]=ShutdownCheck.IsChecked==true };
        MonitorService.Save(); _=DiscordSender.FlushAsync(); System.Windows.MessageBox.Show("Notification preferences saved.");
    }
    private void SaveMonitoring_Click(object sender,RoutedEventArgs e) { MonitorService.Config.Enabled=MonitoringCheck.IsChecked==true; MonitorService.Config.StartWithWindows=StartupCheck.IsChecked==true; MonitorService.Save(); Refresh(); }
    private void SaveRetention_Click(object sender,RoutedEventArgs e) { if(RetentionBox.SelectedItem is System.Windows.Controls.ComboBoxItem item && int.TryParse(item.Tag?.ToString(),out var days)) { MonitorService.Config.RetentionDays=days; MonitorService.Save(); MonitorService.Store.Cleanup(days); System.Windows.MessageBox.Show("Retention setting saved."); } }
    private void DeleteLogs_Click(object sender,RoutedEventArgs e) { if(System.Windows.MessageBox.Show("Permanently delete all locally stored events?","Delete logs",System.Windows.MessageBoxButton.YesNo,System.Windows.MessageBoxImage.Warning)==System.Windows.MessageBoxResult.Yes) { MonitorService.Store.DeleteAll(); Refresh(); } }
    private void Export_Click(object sender,RoutedEventArgs e)
    {
        var dialog=new Microsoft.Win32.SaveFileDialog { Filter="CSV file (*.csv)|*.csv",FileName="FamilyPCMonitor-events.csv" }; if(dialog.ShowDialog()!=true)return;
        static string Q(string s)=>"\""+s.Replace("\"","\"\"")+"\"";
        var rows=new List<string>{"Date,Event,User,Computer"}; rows.AddRange(MonitorService.Store.Get(SearchBox.Text.Trim()).OrderBy(x=>x.TimestampUtc).Select(x=>string.Join(",",Q(DateTime.Parse(x.LocalTimestamp).ToLocalTime().ToString("O")),Q(x.EventType),Q(x.WindowsUser),Q(x.ComputerName)))); File.WriteAllLines(dialog.FileName,rows,Encoding.UTF8);
    }
}
