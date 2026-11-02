# AGENTS.md

Guidance for AI coding agents (and humans) working in this repository.

## What this is

**dCompare** — a Windows desktop directory/file comparison tool. .NET 8, **WPF** (`net8.0-windows`),
x64/AnyCPU, Windows-only. Compare two folders or two archive files, then copy/move/delete/rename
from the comparison view. Built-in text diff (DiffPlex + AvalonEdit) and image diff (ImageSharp),
plus external viewer/diff tools launched from config.

`README.md` is user-facing and long; it also contains research notes (compression library and
file-copy-method comparisons) that are **reference material, not spec**.

## Commands

Everything is verified to work from the repo root.

```powershell
# Build (Debug) — ~54 nullable warnings are normal, 0 errors expected
dotnet build dCompare.sln -c Debug

# Run tests — IMPORTANT: this project uses xunit v3 + Microsoft.Testing.Platform (MTP)
dotnet build dCompare.Tests -c Debug
dCompare.Tests\bin\Debug\net8.0-windows\dCompare.Tests.exe

# Tests + coverage (Cobertura). Current baseline: ~18.3% line coverage.
dCompare.Tests\bin\Debug\net8.0-windows\dCompare.Tests.exe --coverage --coverage-output-format cobertura
# report lands in dCompare.Tests\bin\Debug\net8.0-windows\TestResults\*.cobertura.xml
```

### Build/test gotchas

- **Do not use `dotnet test ... --collect:"XPlat Code Coverage"`, and do not use
  `dotnet test ... -- --coverage` yet.** `coverlet.collector` was removed (VSTest-only, dead under
  MTP), and MTP 2.5.1 + MSBuild node-mode has an upstream bug that hangs on the `test-app-info`
  pipe. Run the test executable directly as shown above.
- **Release build fails without a Six Labors license key** (ImageSharp 4.1.3):
  `error: No Six Labors license found. Set $(SixLaborsLicenseKey)`. Debug builds fine.
  Unblock with `-p:SixLaborsLicenseKey=<KEY>` or a `sixlabors.lic` in `dCompare/` (gitignored).
  The key is assembly-scoped: it must be issued for `AssemblyName` = **`dCompare`**.
- There is no lint/format config (no `.editorconfig`). Match the style of the file you edit.
- `.vs/`, `bin/`, `obj/` are gitignored but present locally — never commit them, never edit files
  under them.

## Project layout

```
dCompare.sln
dCompare/                     WPF app (WinExe, net8.0-windows)
  App.xaml.cs                 entry point: config + Serilog + Generic Host DI wiring
  MainWindow.xaml(.cs)        tabbed shell; tabs host CompareControl
  Compare/                    the heart: DirectoryComparer, TreeNodeManager, ImageComparer,
                              CompareControl(+ViewModel), ToolHandler, FileOperations
  DirTreeSystem/              tree builders behind IFileSystemTreeBuilder + factory
                              (Dotnet…, Archive…(SharpCompress), Mft… = unimplemented stub)
  FileOperation/              copy/move/delete: FileManager, FileOperationHandler,
                              Strategies/ (Kernel32FileCopyStrategy = default, FileStreamStrategy)
  Session/                    session save/load, tab state (SessionManager, SessionFileSystem)
  Configuration/              AppConfiguration POCO + ConfigurationService (JSON), tool config
  Options/                    settings window + one ViewModel per settings page
  Tools/                      viewers/diffs: Viewer/TextViewer, Viewer/ImageViewer,
                              Diff/TextDiff, Diff/TextDiffPlex; ToolFactory picks the tool
  Common/                     Constant (filenames, formats), enums, FileExtensions, helpers
  Infrastructure/Converters.cs  all WPF value converters (consolidated — put new ones here)
  Properties/Resources*.resx  localized strings (en default, zh-CN, zh-TW)
dCompare.Tests/               xunit v3, Moq; mirrors source folders (Compare/, FileOperation/,
                              Configuration/, Options/)
docs/                         README screenshots only
```

## Conventions

- **MVVM with CommunityToolkit.Mvvm 8.4**: prefer `[ObservableProperty]`, `[RelayCommand]`,
  partial classes, and `WeakReferenceMessenger` over code-behind. Migration is **in progress** —
  `CompareControl.xaml.cs` (~1000 lines), `MainWindow.xaml.cs`, and `TextViewer.xaml.cs` still hold
  logic in code-behind. When touching them, move logic out; do not add new logic to code-behind.
  ViewModels must not reference `TabItem`, `TabControl`, `MessageBox`, or other WPF types —
  that is the known debt, not the pattern to copy.
- **DI**: `Ioc.Default` (CommunityToolkit) wraps the `Host` service provider; registrations live in
  `App.xaml.cs` (`ConfigureServices`). Add new services there and take them as constructor
  parameters. Existing `Ioc.Default.GetRequiredService<T>()` service-locator calls
  (`ToolHandler`, `ToolFactory`, `MainWindow.xaml.cs`) are legacy — inject instead.
  Anything resolved through `ToolFactory` must be registered, or `GetService` silently yields null.
- **Naming**: interfaces `I*`, implementations bare; one public type per file; models under
  `Models/`, message types under `Messages/`.
- **Localization**: never hardcode user-visible text. Add to `Properties/Resources.resx` and use
  `Properties.Resources.<Key>`; satellite languages are limited by
  `<SatelliteResourceLanguages>en;zh-TW;zh-CN</SatelliteResourceLanguages>`. Add the key to
  `Resources.resx`, `Resources.zh-CN.resx`, and `Resources.zh-TW.resx`.
- **Logging**: Serilog via `Log.*` (static) or `ILogger<T>`; config in `dCompare/Serilog.json`
  (file sink, daily rolling). No `Console.WriteLine`.
- Tests: xunit v3 `[Fact]`/`[Theory]`, Moq for interfaces, no UI/threading in tests. Name test
  files after the type under test.

## Runtime facts agents must know

- **Config location is the process working directory**, not the install folder:
  `dCompare.json` is read from `Directory.GetCurrentDirectory()` (see `App.xaml.cs` and
  `ConfigurationService`), and logs go to `./logs/dCompare.log`. Same for `recentSessions.json`
  (`Constant` in `Common/Common.cs`). Behaviour differs depending on how the exe is launched —
  assume nothing about cwd in new code.
- A **corrupt `dCompare.json` throws** `InvalidDataException` out of `LoadConfigurationAsync`, and
  several callers are `async void` (unhandled → app crash). Config reads are also re-read from disk
  on almost every call site rather than cached.
- **Comparison algorithm enum** (`Configuration/ComparisonAlgorithms.cs`):
  `ModifiedDateAndSize` (default, size + mtime within 1s), `xxHash64_Full`, `MD5`, `FullContents`
  (byte-by-byte — currently `File.ReadAllBytes` on both files, i.e. loads both into RAM).
- **Archive comparison** goes through SharpCompress (`ArchiveFactory.OpenArchive` — the 0.50.x
  name; `ArchiveFactory.Open` was the old 0.40.x name). `MftFileSystemTreeBuilder` is an empty
  stub and is never returned by `FileSystemTreeBuilderFactory`.
- **File copies use Win32 `CopyFileEx`** by default (`Kernel32FileCopyStrategy`); `FileStreamStrategy`
  exists and is tested but is not what DI registers by default.
- Comparison runs on a background thread: callers wrap `DirectoryComparer.Compare(...)` in
  `Task.Run`. `Compare()` is a **sync-over-async wrapper** (`.GetAwaiter().GetResult()`) — calling it
  from the UI thread deadlocks. Prefer `CompareAsync`.
- External tools are launched from `Options:Tools` config (exe path + arguments, `{file}`
  placeholder) — see `ToolFactory` / `ToolService` / `ToolHandler`.

## CI

`.github/workflows/dotnet-desktop.yml` (windows-latest, matrix `x64`/`x86`, Release only):
restore → test with coverage → ReportGenerator → Codecov + Codacy → publish framework-dependent and
self-contained single-file → auto-tag next patch → GitHub Release. It still uses the removed
`--collect:"XPlat Code Coverage"` VSTest flag and does not pass `SixLaborsLicenseKey`, so **the
Release pipeline is currently broken**; see `TODO.md`.

## Before you finish

1. `dotnet build dCompare.sln -c Debug` → 0 errors, and no *new* warnings.
2. Run the test exe (above) → all green; current count 76.
3. For new non-trivial logic (comparison, path handling, transfer, parsing) add a test in
   `dCompare.Tests/` mirroring the source path.
4. User-visible strings go through resources, all three languages.
