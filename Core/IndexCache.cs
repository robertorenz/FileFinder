using System.IO;

namespace FileFinder.Core;

/// <summary>Resolves where the on-disk index cache lives (per-user LocalAppData).</summary>
public static class IndexCache
{
    public static string Directory
    {
        get
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FileFinder");
            System.IO.Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string IndexPath => Path.Combine(Directory, "index.ffix");
}
