# Builds FileFinderAsm.dll from search_asm.asm using MASM (ml64.exe) + link.exe.
# Locates the MSVC tools via vswhere, so it works on any machine with the
# "Desktop development with C++" workload (or VC Build Tools) installed.
#
#   pwsh native\build_asm.ps1 [-OutDir <dir>]
#
# The DLL is emitted next to this script by default, and also copied to -OutDir
# when supplied (used by the .csproj build target to drop it in bin\...).

param([string]$OutDir = "")

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) { Write-Error "vswhere.exe not found; install Visual Studio or VC Build Tools."; exit 1 }

$vsRoot = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vsRoot) { Write-Error "MSVC x64 tools (VC.Tools.x86.x64) not installed."; exit 1 }

$msvc = Get-ChildItem "$vsRoot\VC\Tools\MSVC" -Directory | Sort-Object Name | Select-Object -Last 1
$bin  = Join-Path $msvc.FullName "bin\Hostx64\x64"
$ml64 = Join-Path $bin "ml64.exe"
$link = Join-Path $bin "link.exe"
if (-not (Test-Path $ml64)) { Write-Error "ml64.exe not found at $ml64"; exit 1 }

$asm = Join-Path $here "search_asm.asm"
$obj = Join-Path $here "search_asm.obj"
$dll = Join-Path $here "FileFinderAsm.dll"

Write-Host "Assembling $asm"
& $ml64 /nologo /c /Fo$obj $asm
if ($LASTEXITCODE -ne 0) { Write-Error "ml64 failed"; exit 1 }

Write-Host "Linking $dll"
& $link /nologo /DLL /NOENTRY /MACHINE:X64 /OUT:$dll $obj /EXPORT:asm_search_range
if ($LASTEXITCODE -ne 0) { Write-Error "link failed"; exit 1 }

Remove-Item (Join-Path $here "search_asm.obj"),(Join-Path $here "FileFinderAsm.exp"),(Join-Path $here "FileFinderAsm.lib") -ErrorAction SilentlyContinue

if ($OutDir -and (Test-Path $OutDir)) {
    Copy-Item $dll (Join-Path $OutDir "FileFinderAsm.dll") -Force
    Write-Host "Copied DLL to $OutDir"
}
Write-Host "Done: $dll"
