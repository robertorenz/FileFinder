using System.Windows;
using FileFinder.Core;

namespace FileFinder;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Hidden verification mode: `FileFinder.exe --selftest` builds a small
        // index in-memory, exercises the SIMD search + cache round-trip, prints
        // PASS/FAIL to the console, and exits without showing a window.
        if (e.Args.Length > 0 && e.Args[0] == "--selftest")
        {
            int code = SelfTest.Run();
            Shutdown(code);
            return;
        }
        if (e.Args.Length > 0 && e.Args[0] == "--benchindex")
        {
            int code = IndexBench.Run(e.Args.Length > 1 ? e.Args[1] : null);
            Shutdown(code);
            return;
        }
        base.OnStartup(e);
    }
}
