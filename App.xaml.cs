using CefSharp;
using CefSharp.Wpf;
using System.Windows;

namespace FlowBrowser;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        CefSharpSettings.ShutdownOnExit = false;
        var settings = new CefSettings
        {
            CachePath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FlowBrowser", "CEF")
        };
        Cef.Initialize(settings);
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Cef.Shutdown();
        base.OnExit(e);
    }
}
