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
# Build (Debug) — ~68 nullable warnings are normal (CS8618 is at zero), 0 errors expected
dotnet build dCompare.sln -c Debug

# Run tests — IMPORTANT: this project uses xunit v3 + Microsoft.Testing.Platform (MTP)
dotnet build dCompare.Tests -c Debug
dCompare.Tests\bin\Debug\net8.0-windows\dCompare.Tests.exe

# Tests + coverage (Cobertura). Current baseline: ~36.2% line coverage.
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
- There is a minimal `.editorconfig` (charset/CRLF/4 spaces/sorted usings only) — no analyser or style rules, so match the style of the file you edit.
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

- **The app is portable: config and logs live next to the EXE**, resolved through
  `Common/AppPaths.cs` (`BaseDirectory = AppContext.BaseDirectory`) — `dCompare.json`,
  `recentSessions.json`, and `<exe dir>/logs/dCompare.log`. Never resolve user files from
  `Directory.GetCurrentDirectory()`; that was a bug (settings vanished depending on how the exe was
  launched, and a planted `dCompare.json` in a writable cwd hijacked the tool config). Reads prefer
  the exe dir, then `%LOCALAPPDATA%\dCompare` (the write fallback used when the exe dir is not
  writable, e.g. Program Files) — and deliberately never read the cwd. Startup does a one-time
  copy-from-cwd migration for old installs (never overwrites, never deletes the original).
  `ConfigurationService(path)` still honours an explicitly passed path (tests depend on that).
  Serilog's file path comes from `Serilog.json` but is overridden to an absolute path by an
  in-memory config source added last in `App.xaml.cs`.
- A **corrupt `dCompare.json` throws** `InvalidDataException` out of `LoadConfigurationAsync`, and
  several callers are `async void` (unhandled → app crash). Config reads are also re-read from disk
  on almost every call site rather than cached.
  `ModifiedDateAndSize` (default, size + mtime within 1s), `xxHash64_Full`, `xxHash64_Partial`
  (hashes only `ComparisonPercentage` % of the file, default 30), `MD5`, `FullContents`
  (streamed in 8 KB chunks by `DirectoryComparer.StreamsEqual` — do not reintroduce
  `File.ReadAllBytes`, it OOM-killed the app on big files).
  Note `ComparisonAlgorithms.AlgorithmSettings` is a **static mutable** dictionary that the Options
  page edits in place.
- **Archive comparison uses entry metadata, not file reads.** `FileSystemNode` carries
  `IsArchiveEntry` + `Crc`; `DirectoryComparer.DetermineStatus` must consult them **before** any stream call,
  because an entry's `FullPath` (`C:\a.zip\dir\file.txt`) is fabricated and cannot be opened — reading it threw
  and every entry of two identical archives reported `Conflict`. Entries with no CRC (tar, solid 7z) fall back to
  size + timestamp. `ArchiveFileSystemTreeBuilderTests` locks all of this (identical archives are `Identical` under
  every algorithm; same-size/different-content pairs are `Modified`). Entry directory names are matched
  **case-insensitively** — keys are not normalised, and a case-sensitive match left `Docs/` and `docs/` as two
  nodes so the comparer showed only one side's files. Entry timestamps are
  `entry.LastModifiedTime ?? DateTime.MinValue` — never `DateTime.Now`, which made every identical
  archive pair report `Modified`. Archives are only ever READ, never extracted; do not add
  extraction without a zip-slip guard. `SharpCompress` 0.50.x uses `ArchiveFactory.OpenArchive`.
  `MftFileSystemTreeBuilder` and the Options "MFT" scan-method choice were deleted (they silently
  did nothing).
- **A folder that cannot be listed is not "identical" or "missing".** `DotnetFileSystemTreeBuilder` lists each
  side separately and reads each entry on its own, setting `FileSystemNode.ScanIncomplete`; a directory with that
  flag compares as `Conflict`. Never swallow a listing error around a whole loop again — the dropped children
  read as one-sided files and invite the user to delete good ones.
- **Rows read from inside an archive are read-only.** `TreeNode.IsArchiveEntry` gates copy/move/delete in
  `FileOperationHandler.GetItemsFor*`, and View/Diff in `ToolHandler`. Rename and "Open in Explorer" bypass those
  helpers (they call `RenameFileOrDirectory` / `OpenFileLocation` directly), so they are guarded in `CompareControl`
  itself — do not remove those two guards on the theory that `GetItemsFor*` covers everything: their path cannot be
  opened, so a delete was a silent no-op the transfer panel still reported as successful.
- **Destructive operations are guarded** — do not undo these:
  copies write `<dest>.dCompare.tmp` then `FileOperationRetry.MoveWithOverwrite` (never truncate the destination in
  place). That helper is the only legal replacement call: `MoveFileEx` refuses a read-only target (`ERROR_ACCESS_DENIED`,
  which is why "copy over a read-only file" failed outright), so it lifts `ReadOnly` for the move and puts the bit
  **back when the move did not happen** — a failed copy must not leave the user's file writable. `TryDelete` clears
  the staging file's attributes first, because `File.Delete` refuses a read-only file, `CopyFileEx` stamps the staging
  file with the source's attributes, and the staging name is fixed: one leftover poisoned every later copy into that
  folder with `ERROR_FILE_EXISTS`;
  deletes go to the **Recycle Bin** via `FileOperation/RecycleBin.cs` with a permanent-delete
  fallback, and skip reparse points; `FileOperationHandler` prompts per batch according to
  `Confirmation.Copy/Move/CopyOverwrite/MoveOverwrite/DeleteFile` (these settings are live — read
  them, and fall back to `new ConfirmationOptions()` when `config.Options` is null);
  `FileOperationRetry.Run` retries only Win32 32/33; `FileExtensions.IsReparsePoint` keeps the
  walkers from following junctions (a link loop = uncatchable `StackOverflowException`, and a link
  out of the compared root meant deletes destroying files elsewhere); transfers take a real
  `CancellationToken` and a cancelled item is reported `Cancelled`, not successful; the folder delete fallback
  recycles **each file** before deleting permanently (an unreadable subtree used to jump straight to permanent
  deletion); and `FileOperationHandler._transferGate` caps concurrent transfers at 4 — a selection of thousands
  must not start one task per file.
- **File copies use Win32 `CopyFileEx`** by default (`Kernel32FileCopyStrategy`); `FileStreamStrategy`
  exists and is tested but is not what DI registers by default.
- Comparison runs on a background thread inside `Task.Run`; call `DirectoryComparer.CompareAsync`
  directly (the old sync-over-async `Compare()` wrapper that deadlocked the UI thread was deleted).
  The parallel scan uses a linked CTS so a failure on one side cancels the other.
- **The progress denominator is published late — say nothing until it arrives.** `CountDirectories` is a second
  walk of both trees that runs *beside* the parallel scan, so `totalDirs` is still 0 for the first part of every
  compare. `DirectoryComparer.ProgressTenth` returns -1 there; clamping the denominator to the numerator instead
  (the obvious `Math.Max(total, processed)` guard) makes the first folder report 100% and the bar sit there for
  the rest of the scan. `DirectoryComparerProgressTests` locks this — the old assertion (`progress > 0`) passed
  straight over the bug.
  Reports are `DirectoryComparer.CompareProgress(double? Fraction, int DirectoriesFound)`. A **null fraction means
  "no honest percentage yet"**, and the ViewModel answers it with an indeterminate bar plus a live folder counter
  (`IsCountingDirectories` / `ComparisonProgressText`) — a determinate bar parked on 0% reads as a hang. Once a real
  fraction has arrived the bar never goes back to indeterminate, so a late count cannot demote it. The running count
  is a *separate* counter from `totalDirs` and is published in batches of `CountBatch` (64); each walk publishes only
  the directories it lists itself, because reporting a child in the parent **and** its subtree total from the
  recursion double-counts it (`CompareAsync_WhileTheTotalIsUnknown_ReportsTheRunningFolderCount` asserts the exact
  total, which is what caught that).
- **A cancelled comparison must throw, never return.** `CancellationToken` reaches hashing and
  `StreamsEqual`/`ReadFull` and is checked once per 8 KB chunk; returning `false` instead of throwing
  used to report a half-built tree as `Identical` (which invites the user to delete good files). Keep
  the check at chunk granularity — not per byte.
- **Session loading rejects UNC paths before anything touches them** — `Common/SessionPathGuard.cs`
  (`IsAllowedComparePath`, `IsAllowedDefaultSessionFile`). Auto-compare after session load would
  SMB-authenticate against `\\attacker\share` (relayable Net-NTLM2) and hash files at an
  attacker-chosen location. A rejected entry creates no tab and starts no compare; `LoadSession`
  returns false when nothing loaded, so the UI cannot claim success. Do not route session paths to
  the comparer without this guard. The test is deliberately **not** `Path.IsPathRooted`, which calls
  `C:evil` rooted while Windows resolves it off that drive's working directory (`C:..\..\..\Windows` then
  reaches anywhere on the drive): `IsRootedLocalPath` requires a real `X:\` root.
- **The compare-tab algorithm list comes from `dCompare.json`, not from code** —
  `CompareControlViewModel.LoadAlgorithms` builds it (and picks the default) from
  `Options.Compare.Algorithms`; `ComparisonAlgorithms.EnabledAlgorithms` was deleted for exactly that reason.
  Binding the ComboBox back to the static `AlgorithmSettings` seed makes the Options | Compare enable/disable
  table lose its effect on the next start. `CompareOptionsViewModel` also **clones** each `ComparisonAlgorithmInfo`
  instead of adopting it, because the seeded dictionary shares its value objects with that static seed — adopting
  them meant ticking a box changed the live settings even when the user pressed Cancel.
  `CompareControl.InitializeAsync` reads that config **once from the constructor** and reloads on
  `OptionsSavedMessage` (sent by Settings | Save). It used to sit in the control's `Loaded` handler, but the
  TabControl hosts only the selected tab (`ContentPresenter ContentSource="SelectedContent"`), so `Loaded` fired on
  every tab switch — re-reading the file and resetting the algorithm the user had just picked. Do not move it back,
  and do not "simplify" it to a flag alone: the message is what keeps an open tab in step with the Options page.
  A refresh **keeps the user's current choice** when the new list still offers it (only a switched-off or never-made
  choice falls back), and the same config now also builds the `DirectoryComparer` itself — its
  `ScanMethod.ParallelOptions` and per-algorithm `ComparisonPercentage` were silently ignored while `_comparer` was
  `new DirectoryComparer()`.
- **Config `Tools:*.Handler` is attacker-influenced text.** `ToolFactory` refuses any type that is not
  an `ITool` in the dCompare assembly, *before* invoking a constructor. `ToolFactorySecurityTests`
  locks this (including “the target file is not truncated”, which proves the constructor never ran).
  The four real tools are `TextViewer`, `ImageViewer`, `TextDiff`, `TextDiffPlex`. `CompareControl` used to
  advertise itself as an `ITool` with `LoadFile => throw new NotImplementedException()`; it is the compare tab
  itself and is no longer an `ITool`.
- **A first run gets a working tool set from code, not from a config file.** `Configuration/DefaultConfiguration.cs`
  seeds `Options.Tools` and `Options.FileTypeMapping` (only the four built-in `ITool` types, assembly-qualified
  handler strings); `AppConfiguration` defaults to it, and `DefaultConfigurationTests` asserts every handler
  resolves to a real built-in `ITool` and every mapping names a defined tool. Empty dictionaries here meant View and
  Diff both dead-ended at "no default tool", and the only working set lived in one developer's untracked
  `dCompare.json`. Delete `<exe dir>\dCompare.json` to get the seeded set back.
- **Column sorting is keyed on the column index, never the header text** — headers are localised
  (`CompareControl.ColumnHeader_Click` + `ColumnSortProperties`). Switching on `"Left Name"` silently made every
  header click sort by left name in zh-CN/zh-TW. `GridViewColumn` has **no** `SortMemberPath` (that is DataGrid).
- **`dCompare.json` is read by `ConfigurationService.LoadConfigurationAsync`** (System.Text.Json) →
  `AppConfiguration`. `App.xaml.cs` reads the same file a second time, for nothing but the startup culture
  (`Options:UI:Language`), and must resolve it through `AppPaths.ResolveExisting` like every other read: the
  Options page *saves* to `%LOCALAPPDATA%\dCompare` when the exe folder is read-only, so an exe-dir-only read
  brings up the wrong language on a Program Files install. Nothing feeds it into the Host (`IConfiguration` is
  injected nowhere). The old `IConfigurationRoot`/`GetSection("Tools")` query methods were dead duplicates of
  `ToolConfigService` and let the context menus disagree with the default tool; do not add them back.
- External tools are launched from `Options:Tools` config (exe path + arguments, `{file}`
  placeholder) — see `ToolFactory` / `ToolHandler`. Launch rules (do not weaken them):
  `UseShellExecute = false`; every substituted placeholder path is quoted (without double-quoting a
  template that already quotes it); a bare tool name is resolved against the app dir, System32,
  Program Files(x86) and PATH; `.bat`/`.cmd` run via `cmd.exe /c "<script>"`; a missing tool fails
  with a logged error instead of a raw Win32 exception. `ToolFactory` only accepts handler types
  that implement `ITool` **in the dCompare assembly** — it instantiates before casting otherwise,
  which was an arbitrary-type-execution hole from a tampered config. (`ToolService`/`IToolService`
  and `ParentPathConverter` were dead and have been deleted.)

## CI

`.github/workflows/dotnet-desktop.yml` (windows-latest, matrix `x64`/`x86`, Release only):
restore → test with coverage → ReportGenerator → Codecov + Codacy → publish framework-dependent and
self-contained single-file → auto-tag next patch → GitHub Release. It still uses the removed
`--collect:"XPlat Code Coverage"` VSTest flag and does not pass `SixLaborsLicenseKey`, so **the
Release pipeline is currently broken**; see `TODO.md`.

## Before you finish

1. `dotnet build dCompare.sln -c Debug` → 0 errors, and no *new* warnings.
2. Run the test exe (above) → all green; current count 230.
3. For new non-trivial logic (comparison, path handling, transfer, parsing) add a test in
   `dCompare.Tests/` mirroring the source path.
4. User-visible strings go through resources, all three languages.
