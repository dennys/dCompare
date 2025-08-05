![GitHub release (latest by date)](https://img.shields.io/github/v/release/dennys/dCompare)
![GitHub all releases](https://img.shields.io/github/downloads/dennys/dCompare/total)

# dCompare - A Directory/File Comparison Tool

<a href="docs/screenshot-main.webp">
  <img src="docs/screenshot-main.webp" align="center" alt="MainWindow" width="500">
</a>

**Disclosure**: Approximately 80% of this project's code was generated with the assistance of AI tools.

## Features

- **Tabbed Interface**: Supports multiple comparison sessions in a a tabbed layout, with drag-and-drop tab reordering, close buttons, and keyboard shortcuts (e.g., Ctrl+N for new tab, Ctrl+W to close tab).

- **Directory Comparison**:
  - Compare folder structures side-by-side to identify differences in files and subdirectories.  
  - The comparison view displays file names, sizes, last modified dates, and status indicators.  
  - Provides filters to focus on specific differences (e.g., identical, different, only in left/right).  
  - Comparison Methods (Algorithms)
    - **Modified time and file size** (✅ *Recommended for speed*)
    - **xxHash64** (⚡ *Recommended for fast content comparison*)
    - **MD5**
    - **Byte-by-byte**
    > 🟢 For most use cases, **"Modified time + size" is the fastest and most efficient** method.  
    > 🔍 If you need to **compare actual file contents**, we recommend **`xxHash64`** for its excellent speed–accuracy balance (e.g., comparing a 2.6 GB file took ~5 seconds with xxHash64 vs. ~20 seconds with MD5).

- **File Operations**:
  - Perform common file operations — copy, move, delete, and rename — directly within the application.
  - A dedicated file operation window displays queued, successful, and failed tasks, with detailed progress, speed, and filtering capabilities.
  - Real-time progress feedback is provided via a cancelable overlay showing percentage completion and a progress bar.

- **File Comparison**:  Compare file contents side-by-side with intuitive visual feedback.
  - **Text File Comparison**: Compare text-based files (e.g., .txt, .cs, .json, .xml) side-by-side with color-coded highlights
    - 🟩 insertions in green
    - 🟥 deletions in red
    - ⬜ unchanged content
    - Scrollable view for easy navigation.
  - **Image Comparison**: Display two images side-by-side for visual comparison. Images are automatically scaled to fit the window while preserving their aspect ratio. Large images can be viewed using a ScrollViewer.
  - **Archive File Support**: Supports comparison of archive files, including: .zip, .rar, .7z, .tar, .gz, .tar.gz, .bz2, .tar.bz2, .xz, .tar.xz, .lzma, .tar.lzma, .z, .iso, .deb, .ar, .cab (This feature is powered by [SharpCompress](https://github.com/adamhathcock/sharpcompress). The tool supports any archive formats that SharpCompress can read.)

- **Context Menus**: Right-click context menus on comparison results provide actions like comparing contents, copying/moving files, deleting, renaming, opening file locations, copying paths/filenames, and viewing properties.
- **Session Management**: Save and load comparison sessions, with recent sessions accessible via the File menu, and options to auto-load default sessions on startup.
- **Customizable Settings**: Configure via a settings window with sections for:
  - UI (theme, language)
  - Comparison algorithm and double-click action
  - Text and image diff viewers with customizable file extensions
  - Session settings (default session file, auto-load/compare)
  - Hotkeys for various commands

## Planned Features

Partial implemented

- **Custom Diff Viewer Selection**: Enable selection of diff viewers based on file extensions (partially implemented in settings).

Not implemented

- **Backup**
  - Automatically backup file instead of overwrite when copying or moveing files
- **File Preview**:
  - Feature: Add a preview pane for text and image files when selected in the CompareControl ListView. This could show a quick view of text file contents or image thumbnails.
  - Implementation: Add a DockPanel or ContentControl in CompareControl.xamlx that displays content based on the selected file’s extension, using the TextDiffOptions or ImageDiffOptions from AppConfiguration.cs.
- **Cloud Storage Support**:
  - Feature: Allow comparisons and operations with cloud storage services (e.g., Google Drive, Dropbox, OneDrive).
- **JSON-Specific Comparison**: Implement specialized comparison for JSON files to highlight structural differences.
- **File Explorer Integration**: Associate dCompare with Windows File Explorer for quick access to comparisons.

- **Scheduled Comparisons**:
  - Feature: Allow users to schedule recurring directory comparisons (e.g., nightly backups).

## Known Issues

- **Text Selection**: Partial text selection with the mouse is not supported in the text comparison view.
- **Image Comparison**: No pixel-level difference highlighting in the image comparison view.

## Development

### Requirements

- **.NET 8** is required for features like `OpenFolderDialog` used for folder selection. See [Microsoft Documentation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.openfolderdialog?view=windowsdesktop-8.0).

### Comparison Algorithm

- xxHash64: Use Microsoft's implementation: <https://learn.microsoft.com/zh-tw/dotnet/api/system.io.hashing.xxhash64.hash>

### 3rd party tools to view or compare files

| Type    | View          | Diff          |
| ------- | ------------- | --------      |
| Text    | Avalon.Edit   | DiffPlex      |
| Image   | Build-in      | Build-in      |
| Archive | N/A           | SharpCompress |

### 3dr party libraries

- **[CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)**:  See <https://learn.microsoft.com/zh-tw/dotnet/communitytoolkit/mvvm/>
- **[AvalonEdit](https://github.com/icsharpcode/AvalonEdit)**: Used for text file viewer/editor.
- **[DiffPlex](https://github.com/mmanela/diffplex)**: Used for text file comparison.
- **[Serilog](https://github.com/serilog/serilog)**: Used for logging application events and debugging.

Not used yet

- **[Dragablz](https://github.com/ButchersBoy/Dragablz)**
- **[MetadataExtractor](https://github.com/drewnoakes/metadata-extractor-dotnet)**:
- **[Google APIs client Library for .NET](https://github.com/googleapis/google-api-dotnet-client)**:
- **[jsondiffpatch.net](https://github.com/wbish/jsondiffpatch.net)**:

### File Copy Method Comparison (by ChatGPT)

Below is a table comparing three methods for file and directory copying in .NET 8—`CopyFileEx`, `.NET File.Copy`, and `FileStream`—based on requirements: Windows 10 or above, directory copying, progress callbacks, metadata preservation (e.g., timestamps), and no copy window.

| **Feature**                      | **CopyFileEx**                                                                                                                                                                                                                                                                                                                                                  | **.NET File.Copy**                                                                                                                                                                                                                                          | **FileStream**                                                                                                                                                                                                                                                                                                                                                                                                                  |
| -------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Description**                  | Windows API function (`kernel32.dll`), low-level file copy, mimics `explorer.exe` behavior.                                                                                                                                                                                                                                                                     | .NET 8 high-level method, encapsulates underlying API, simple to use.                                                                                                                                                                                       | .NET low-level stream I/O class, flexible control over file read/write.                                                                                                                                                                                                                                                                                                                                                         |
| **Platform Support**             | Windows only (meets Windows 10+ requirement).                                                                                                                                                                                                                                                                                                                   | Cross-platform (Windows, Linux, macOS).                                                                                                                                                                                                                     | Cross-platform (Windows, Linux, macOS).                                                                                                                                                                                                                                                                                                                                                                                         |
| **Directory Copy**               | Requires manual recursive traversal of files and subdirectories.                                                                                                                                                                                                                                                                                                | Requires manual recursive traversal of files and subdirectories.                                                                                                                                                                                            | Requires manual recursive traversal of files and subdirectories.                                                                                                                                                                                                                                                                                                                                                                |
| **Progress Callback**            | **Supported**: Via `lpProgressRoutine`, provides per-file copy progress (bytes copied), requires manual directory progress calculation.                                                                                                                                                                                                                         | **Not Supported**: No progress callbacks, requires `FileStream` for manual calculation.                                                                                                                                                                     | **Supported**: Via tracking bytes read/written, requires manual directory progress accumulation.                                                                                                                                                                                                                                                                                                                                |
| **Cancel Functionality**         | **Supported**: Via `pbCancel` boolean, allows mid-operation termination, similar to `explorer.exe` cancel button. Requires cleanup of incomplete files post-cancel.                                                                                                                                                                                             | **Not Supported**: Cannot terminate mid-operation unless via exception or process termination.                                                                                                                                                              | **Supported**: Via `CancellationToken` in async read/write, requires cleanup of incomplete files post-cancel.                                                                                                                                                                                                                                                                                                                   |
| **Metadata Preservation**        | **Automatic**: Preserves creation, modification, access times, and attributes (e.g., read-only, hidden) by default, no extra code needed.                                                                                                                                                                                                                       | **Not Preserved**: Sets target file timestamps to current time, requires manual setting via `FileInfo` (3-4 lines/file).                                                                                                                                    | **Not Preserved**: Requires manual setting of metadata via `FileInfo`, similar to `File.Copy`.                                                                                                                                                                                                                                                                                                                                  |
| **Performance**                  | **Slightly Better**: Direct Windows API call, no .NET runtime overhead, fastest (<1-2% difference).                                                                                                                                                                                                                                                             | **Slightly Weaker**: Encapsulates underlying API, minor .NET overhead.                                                                                                                                                                                      | **Slightly Weaker**: .NET stream management and runtime overhead, slightly slower than `CopyFileEx`, optimizable via buffer size.                                                                                                                                                                                                                                                                                               |
| **Memory Usage**                 | Streamed copy, buffer size system-controlled (typically 4KB-64KB), suitable for large files.                                                                                                                                                                                                                                                                    | Streamed copy, buffer managed by .NET, similar memory usage.                                                                                                                                                                                                | Streamed copy, customizable buffer size (e.g., 8KB), similar memory usage.                                                                                                                                                                                                                                                                                                                                                      |
| **Async Support**                | Synchronous API, requires `Task` wrapping for async.                                                                                                                                                                                                                                                                                                            | Synchronous method, no native async, requires `FileStream` for async.                                                                                                                                                                                       | **Native Support**: `ReadAsync`/`WriteAsync` provide async, suitable for modern apps.                                                                                                                                                                                                                                                                                                                                           |
| **Ease of Use**                  | **More Complex**: Requires P/Invoke, progress/cancel implementation needs callback and error code handling.                                                                                                                                                                                                                                                     | **Simplest**: Direct call, .NET-style exception handling, but metadata requires extra code.                                                                                                                                                                 | **Complex**: Requires manual buffer, progress, cancel, and metadata management, more code.                                                                                                                                                                                                                                                                                                                                      |
| **Error Handling**               | Uses Windows error codes (`GetLastWin32Error`), requires documentation lookup, debugging tedious.                                                                                                                                                                                                                                                               | .NET exceptions (`IOException`, etc.), clear and readable, debug-friendly.                                                                                                                                                                                  | .NET exceptions, same as `File.Copy`, debug-friendly.                                                                                                                                                                                                                                                                                                                                                                           |
| **Mimic `explorer.exe`**         | **Closest**: Auto-preserves metadata, supports cancel/progress, behaves like `explorer.exe`.                                                                                                                                                                                                                                                                    | **Weaker**: Requires manual metadata setting, no progress/cancel, poor mimicry.                                                                                                                                                                             | **Moderate**: Supports progress/cancel, but metadata requires manual setting, less mimicry than `CopyFileEx`.                                                                                                                                                                                                                                                                                                                   |
| **No Copy Window**               | **Meets**: Pure API, no GUI.                                                                                                                                                                                                                                                                                                                                    | **Meets**: No GUI.                                                                                                                                                                                                                                          | **Meets**: No GUI.                                                                                                                                                                                                                                                                                                                                                                                                              |
| **Code Example (Single File)**   | `csharp<br>[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]<br>private static extern bool CopyFileEx(string lpExistingFileName, string lpNewFileName, IntPtr lpProgressRoutine, IntPtr lpData, ref bool pbCancel, uint dwCopyFlags);<br>bool cancel = false;<br>CopyFileEx(source, dest, IntPtr.Zero, IntPtr.Zero, ref cancel, 0);<br>` | `csharp<br>File.Copy(source, dest, true);<br>FileInfo destInfo = new FileInfo(dest);<br>destInfo.CreationTime = sourceInfo.CreationTime;<br>destInfo.LastWriteTime = sourceInfo.LastWriteTime;<br>destInfo.LastAccessTime = sourceInfo.LastAccessTime;<br>` | `csharp<br>using FileStream sourceStream = File.OpenRead(source);<br>using FileStream destStream = File.Create(dest);<br>byte[] buffer = new byte[8192];<br>while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0) {<br>    await destStream.WriteAsync(buffer, 0, bytesRead, token);<br>}<br>FileInfo destInfo = new FileInfo(dest);<br>destInfo.CreationTime = sourceInfo.CreationTime;<br>` |
| **Suitability for Requirements** | **Best**: Meets all requirements (progress, cancel, directories, metadata, no GUI), slightly better performance, moderate code.                                                                                                                                                                                                                                 | **Unsuitable**: Lacks progress/cancel, requires manual metadata, fails requirements.                                                                                                                                                                        | **Viable but Suboptimal**: Supports progress/cancelation, but requires manual metadata, more complex and slightly slower.                                                                                                                                                                                                                                                                                                       |

## Recommendations for Requirements (Windows 10+, Directory Copy, Progress Callback, Metadata, No GUI)

- **Primary Choice: `CopyFileEx`**:
  - Automatically preserves metadata, no extra code needed.
  - Native support for progress callbacks (`lpProgressRoutine`) and cancellation (`pbCancel`), ideal for directory operations and UI integration.
  - Slightly better performance, closest behavior to `explorer.exe`.
  - Reference: `DirectoryCopier` class implementation (includes recursion, progress, cancellation).
- **Secondary Choice: `FileStream`**:
  - Supports progress and cancellation, suitable for .NET-native or future cross-platform apps.
  - Requires manual metadata handling, more complex code, slightly lower performance.
  - Reference: `FileStream` directory copy example above.
- **Avoid: `File.Copy`**:
  - Lacks progress/cancellation support, requires manual metadata setting, does not meet requirements.
  - Terminating via Task Manager is possible but not recommended due to potential incomplete files or data inconsistency without cleanup logic.

## Notes

- **Post-Cancellation Cleanup**: `CopyFileEx` and `FileStream` require deleting incomplete files/directories after cancellation to avoid residual data.
- **Progress Callback Frequency**: Test with large directories or network paths to ensure callback frequency isn’t too high; limit update frequency for performance.
- **Permissions**: Ensure read permissions for source files and write permissions for destination paths.
- **UI Integration**: Connect progress callbacks to UI updates (e.g., WPF progress bars in `FileOperationWindow.xaml`) for real-time feedback.
