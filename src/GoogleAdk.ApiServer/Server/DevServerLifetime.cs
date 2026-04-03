using Microsoft.AspNetCore.Builder;
using System.Runtime.Loader;

namespace GoogleAdk.ApiServer;

internal static class DevServerLifetime
{
    internal static CancellationToken Register(WebApplication app)
    {
        var cts = new CancellationTokenSource();

        void RequestStop()
        {
            if (cts.IsCancellationRequested) return;
            cts.Cancel();
            try
            {
                app.Lifetime.StopApplication();
            }
            catch
            {
                // Best-effort shutdown only.
            }
        }

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            RequestStop();
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) => RequestStop();
        AssemblyLoadContext.Default.Unloading += _ => RequestStop();
        app.Lifetime.ApplicationStopping.Register(() => RequestStop());

        return cts.Token;
    }
}
