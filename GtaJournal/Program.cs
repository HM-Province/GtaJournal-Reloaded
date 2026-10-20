using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace GtaJournal;

/// <summary>Точка входа: одна копия приложения; повторный запуск просто показывает окно.</summary>
public static class Program
{
    private const string InstanceKey = "GtaJournal.Main";

    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        var current = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (!current.IsCurrent)
        {
            var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            Task.Run(() => current.RedirectActivationToAsync(activation).AsTask()).Wait();
            return 0;
        }

        current.Activated += (_, _) => App.OnRedirectedActivation();

        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
        return 0;
    }
}
