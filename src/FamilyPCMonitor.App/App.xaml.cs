using System.Windows;

namespace FamilyPCMonitor;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Services.MonitorService.Initialize();
    }
}
