# FileFinder — Documentation

A fast Windows file-name search tool (C# / WPF, .NET 9) with a SIMD-accelerated
search engine. This guide covers everything from indexing to the search syntax,
settings, and keyboard shortcuts.

- [Installing & running](#installing--running)
- [Indexing your drives](#indexing-your-drives)
- [Searching](#searching)
- [Search engines (JIT vs MASM)](#search-engines-jit-vs-masm)
- [Result columns](#result-columns)
- [Right-click actions](#right-click-actions)
- [Settings](#settings)
- [Tray & single instance](#tray--single-instance)
- [Statistics & the index cache](#statistics--the-index-cache)
- [Keyboard shortcuts](#keyboard-shortcuts)
- [FAQ](#faq)

---

## Installing & running

Two downloads on the [Releases](https://github.com/robertorenz/FileFinder/releases/latest) page:

- **`FileFinder-Setup-x.y.z.exe`** — installer. Installs per-user (no admin) or
  all-users (with admin), adds a Start-Menu entry and an optional desktop icon.
- **`FileFinder.exe`** — portable single file. No install and **no .NET runtime
  required** — just download and run.

On first launch with no index, FileFinder opens **Settings** so you can pick
drives and build one.

---

## Indexing your drives

FileFinder finds files in an in-memory index, so it indexes the drives you
choose first. It picks the method automatically per drive:

| Method | When | Speed |
|---|---|---|
| **NTFS MFT read** | Running **as Administrator** on an **NTFS** drive | Indexes a whole drive in seconds |
| **Directory walk** | Otherwise (no admin, or non-NTFS) | Slower first scan; no admin needed |

- Choose drives in **Settings → Drives to index**, then click **Build Index**.
- For the fast path, click **Restart as Administrator** in Settings first.
- The index is **cached to disk** and reloaded instantly on the next launch.
- **Clear Index** (Settings or the File menu) drops the index and deletes the
  cache; clearing from Settings keeps the dialog open so you can rebuild.

> The index is rebuilt on demand — it does not auto-update as files change.

---

## Searching

Type in the search box; results appear as you type. An **empty box lists all
indexed files** (up to the display limit).

**Plain text** is a case-insensitive substring match against the **file name and
extension** (not the full folder path):

```
report        → Quarterly Report 2024.xlsx, report.png, my-report.txt …
```

**Multiple words** must *all* appear, in any order, anywhere in the name:

```
icon headset png   → headset-icon.png, png_headset_icon_set.svg …
```

**Wildcards** — a word containing `*` or `?` is matched as a pattern against the
whole name:

| Pattern | Matches |
|---|---|
| `*.gif` | every `.gif` file |
| `report*` | names starting with "report" |
| `IMG_????.jpg` | `IMG_0001.jpg`, `IMG_2024.jpg`, … |

You can mix them: `icon *.png` requires the word "icon" **and** a `.png` name.

The result line shows the match count, time, and which engine ran (JIT/MASM).

---

## Search engines (JIT vs MASM)

The substring search ships in two interchangeable implementations:

- **JIT** — C# AVX2 hardware intrinsics, JIT-compiled to vectorized assembly.
- **MASM** — a hand-written x64 assembly routine in `FileFinderAsm.dll`,
  P/Invoked from C#.

Pick the default in **Settings → Default search engine** (MASM where available),
or race them with **View → Benchmark JIT vs MASM…** (`Ctrl+B`) — it runs your
current term through both engines 40× across all cores and reports the best time.

> Multi-word and wildcard queries always use the JIT path; single plain words can
> use MASM.

---

## Result columns

Toggle columns in **Settings → Result columns** (Name is always shown):

- **Folder**, **Type**, **Size**, **Date modified**, **Attributes**

Size / date / attributes are read from disk **only for the rows you see**, so
they cost nothing when hidden. Click a column header to **sort** — the active
column's header is **bold** with a ▲/▼ arrow. Size sorts numerically and Date
modified sorts chronologically.

---

## Right-click actions

Right-click any result:

- **Open**, **Open with…**, **Run as administrator**
- **Open containing folder**, **Open in Terminal here**
- **Copy ▸** File · Full path · Folder path · File name · Name without
  extension · Size · Date modified
- **Find other files of this type** (sets the search to `*.ext`)
- **Properties** (the native Windows dialog)

Double-clicking a row opens the file.

---

## Settings

**File → Settings…** (`Ctrl+,`) is the control center:

- Drives to index, Build / Clear Index, Restart as Administrator
- Default search engine + Benchmark
- Result columns
- Language (English / Español, switches live)

Everything is saved to `settings.json` (see below) and applied on every launch.

---

## Tray & single instance

- Closing the window **minimizes FileFinder to the system tray** so the in-memory
  index stays warm.
- Launching it again **brings the existing window back** (single instance).
- **Right-click the tray icon → Exit** (or **File → Exit**) to fully quit;
  double-click the tray icon to restore the window.

---

## Statistics & the index cache

**View → Index Statistics…** (`Ctrl+I`) shows file/folder counts, drives, RAM in
use, the cache location and size, and the top file types.

The index cache lives at:

```
%LocalAppData%\FileFinder\index.ffix
```

`.ffix` is FileFinder's own uncompressed binary index (a memory image of file
names/paths — no file contents). Settings live next to it in `settings.json`.
You can delete either safely; the app rebuilds/recreates them.

---

## Keyboard shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+,` | Settings |
| `Ctrl+I` | Index Statistics |
| `Ctrl+B` | Benchmark JIT vs MASM |
| `Enter` / double-click | Open selected file |

---

## FAQ

**Do I need administrator rights?** No — only for the fast MFT indexing path.
Everything else works as a standard user.

**Does it search file contents?** No, it searches file **names** (and extension).

**Why is the cache file large?** It stores names twice (display + lowercased for
search) plus folder paths. Clear it any time from Settings.

**Does it update as files change?** Not automatically — rebuild the index to pick
up changes.

**CPU without AVX2?** A scalar fallback runs automatically; search is still fast.
