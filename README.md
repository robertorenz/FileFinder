# FileFinder — Fast Disk Search

A Windows desktop app (C# / WPF, .NET 9) that indexes selected drives and finds
files by name **instantly** as you type. The hot search loop is written with
AVX2 hardware intrinsics — the same vectorized machine instructions you'd
hand-write in assembly — so it scans millions of file names in a few
milliseconds.

![status](https://img.shields.io/badge/build-passing-16A34A) ![dotnet](https://img.shields.io/badge/.NET-9.0-2563EB)

## Download

Grab the latest build from the [**Releases**](https://github.com/robertorenz/FileFinder/releases/latest) page:

- **`FileFinder-Setup-1.0.12.exe`** — installer (Start Menu + optional desktop shortcut, uninstaller). Installs per-user without admin, or all-users with admin.
- **`FileFinder.exe`** — portable single file. No install, no .NET runtime required — just download and run.

## Highlights

- **Two indexing engines, picked automatically per drive**
  - **MFT read (fast):** reads the NTFS Master File Table directly — indexes a
    whole drive in *seconds*. Used automatically when running as Administrator
    on an NTFS volume.
  - **Directory walk (portable):** parallel recursive enumeration that needs no
    admin rights and works on any drive/filesystem (exFAT, USB, network).
  - The app detects elevation at runtime and falls back transparently. A
    **Restart as Administrator** button lets you opt into the fast path.
- **SIMD search.** File names are stored once, lower-cased, in a single
  contiguous UTF-8 blob. The matcher (`Core/SimdSearch.cs`) broadcasts the
  query's first byte across a 256-bit register, compares 32 bytes per
  instruction (`vpcmpeqb` / `vpmovmskb`), and only does a full compare on real
  candidates. Searches run in parallel across all CPU cores.
- **Cached index.** The index is saved to `%LocalAppData%\FileFinder\index.ffix`
  in a compact binary format and reloaded instantly on the next launch.
- **Statistics view** (*View → Index Statistics…*, or `Ctrl+I`) shows the file
  and folder counts, drives, RAM in use, cache file location and on-disk size,
  and a breakdown of the most common file types.
- **Case-insensitive & Unicode-aware** matching, with **wildcards**: plain text
  is a substring match, while `*` and `?` switch to whole-name glob matching
  (`*.gif`, `report*`, `IMG_????.jpg`).
- **Professional UI** — clean slate/blue theme, live result count and timing,
  double-click to open, right-click to reveal in Explorer. Modal dialogs (no
  system alert boxes).
- **Preferences** (*File → Preferences…*, or `Ctrl+,`) — pick the default search
  engine (MASM by default where available) and the UI language. Saved to
  `settings.json` and applied on every launch.
- **Multilingual** — English and Spanish (Español), switchable live from
  Preferences with no restart.
- **Configurable result columns** — show/hide Folder, Type, **Size**, **Date
  modified**, and **Attributes** from *Preferences → Result columns*. Size and
  dates are read from disk only for the rows you see, so they cost nothing when
  hidden.

## Two search engines (benchmark them yourself)

The substring search ships in **two interchangeable implementations** so you can
measure the difference on your own machine:

| Engine | Where | What it is |
|--------|-------|------------|
| **JIT** | `Core/SimdSearch.cs` | C# `System.Runtime.Intrinsics` (AVX2). The JIT lowers it to `vpcmpeqb`/`vpmovmskb`/`tzcnt`. |
| **MASM** | `native/search_asm.asm` → `FileFinderAsm.dll` | A hand-written x64 assembly routine, assembled with `ml64.exe` and P/Invoked. |

Pick the engine in **File → Preferences…** (`Ctrl+,`), or race them with
**View → Benchmark JIT vs MASM…** (`Ctrl+B`). The benchmark runs the current
search term through both engines 40× across all cores and reports the best time
for each. Both are verified to return identical results in the `--selftest`.

> Building the MASM DLL needs the MSVC C++ tools (the *Desktop development with
> C++* workload). If they're absent the app still builds and runs — it just uses
> the JIT engine and the MASM option is disabled. Build it manually with
> `pwsh native\build_asm.ps1`.

## Why it's fast

| Stage | Technique | Typical time |
|------|-----------|--------------|
| Index (admin/NTFS) | Raw MFT enumeration via `FSCTL_ENUM_USN_DATA` | seconds for millions of files |
| Index (no admin) | Parallel `Directory.EnumerateFiles` per top-level folder | minutes (first run only, then cached) |
| Search | AVX2 first-byte broadcast + parallel scan | **< 10 ms over millions of names** |

> The "assembly" lives in `Core/SimdSearch.cs`: `System.Runtime.Intrinsics.X86`
> calls (`Avx2`, `Sse2`) that the JIT lowers directly to SIMD opcodes — no
> separate native toolchain required.

## Architecture

```
Core/
  SimdSearch.cs    AVX2 substring matcher (the hot loop)
  FileIndex.cs     immutable flat index + parallel Search() + binary cache
  IndexBuilder.cs  growable blobs, thread-safe merge for the parallel walk
  IndexCache.cs    on-disk cache location
  SelfTest.cs      `--selftest` correctness + perf harness
Indexing/
  MftReader.cs     NTFS Master File Table reader (fast, needs admin)
  DirectoryWalker.cs  parallel portable walk (no admin)
  DriveIndexer.cs  orchestrator: MFT with walk fallback + elevation check
ViewModels/        MVVM (MainViewModel, RelayCommand, converters)
Dialogs/           themed modal dialog
MainWindow.xaml    UI (menu + search + results grid)
```

## Build & run

```powershell
dotnet build -c Release
.\bin\Release\net9.0-windows\FileFinder.exe
```

Verify the search engine:

```powershell
.\bin\Debug\net9.0-windows\FileFinder.exe --selftest
```

Measure where indexing time actually goes (disk/syscalls vs. the CPU
name-normalization kernel) for any folder:

```powershell
.\bin\Debug\net9.0-windows\FileFinder.exe --benchindex C:\Windows
```

On a typical drive ~97% of indexing is disk + `FindNextFile` syscalls and under
1% is the (assembly-accelerable) normalization kernel — which is why the
hand-written assembly lives in the **search** path, not the indexer.

## Usage

1. Open **File → Preferences…** (`Ctrl+,` or the *Preferences* button in the
   search bar). Tick the drives to index, then click **Build Index** (or
   **Restart as Administrator** first for the fast MFT path on NTFS drives).
   The drive selection, default engine, and language are all set here and
   remembered between launches.
2. Start typing in the search box — results and a live match count/timing appear
   instantly. The active engine (JIT/MASM) is shown in the result line.
3. Double-click a row to open the file, or right-click → *Open containing
   folder*. **Build Index** / **Clear Index** are also in the **File** menu.

## Requirements

- Windows 10/11, .NET 9 SDK/runtime
- A CPU with AVX2 (virtually all since ~2013); a scalar fallback runs otherwise
- Administrator rights are **optional** — only needed for the fast MFT path

---
Built with C# / WPF on .NET 9.
