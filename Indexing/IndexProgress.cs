namespace FileFinder.Indexing;

public enum IndexMethod
{
    None,
    Mft,          // fast NTFS Master File Table read (needs admin)
    DirectoryWalk // portable recursive enumeration (no admin)
}

public readonly record struct IndexProgress(
    string Drive,
    IndexMethod Method,
    long FilesSoFar,
    string CurrentPath,
    bool Done);
