using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Ejar.Maui.WinUI;

/// <summary>
/// نقطة دخول Windows لـ unpackaged MAUI app (WindowsPackageType=None).
/// عند MSIX يُنشِئها MAUI تلقائياً، لكن في وضع unpackaged يجب وجود
/// <c>Main</c> صريح يُهيِّئ COM ويبدأ WinUI Application.
/// </summary>
public static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(p =>
        {
            var ctx = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(ctx);
            _ = new App();
        });
        return 0;
    }
}
