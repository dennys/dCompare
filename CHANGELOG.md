# Changelog

> 2026-11-02 → 03 兩日審查/重構 session 的完成項目紀錄（todo.md 移轉至此以省 token）。
> 所有變更維持 `net8.0-windows`。最終狀態：build 0 error、測試 78 → 119 全綠、覆蓋率 18.3% → 23.9%。

## Bug 修復

### 比對引擎（`Compare/DirectoryComparer.cs`）
- 右側 archive 判斷誤用 `leftPath` → 總目錄數/進度百分比算錯。
- 進度節流 `lastReportedProgress` 雙執行緒 race → `Interlocked` + `Volatile`。
- **取消時 `CompareNodes` 回傳 false** → 半完成的樹被報成 `Identical`（使用者可能據此刪好檔）→ 改 `ThrowIfCancellationRequested()`。
- `FullContents` 用 `File.ReadAllBytes`（大檔 OOM）→ 8KB 串流比對（`StreamsEqual`/`ReadFull`）。
- 移除 sync-over-async 的 `Compare()`（UI 執行緒呼叫即死鎖）→ 呼叫端改 `CompareAsync` + `Dispatcher.InvokeAsync`。
- **`xxHash64_Partial` 從未 hash**（掉進 `default:`，實際是 size+date；`ComparisonPercentage` 是死設定）→ 補 case 並接回設定（預設 30%）。
- **單側資料夾狀態標錯**（`LastOrDefault(i => i.Left == left || ...)` 在 `left == null` 時命中每筆單側列，塗錯最深那列）→ parent `ComparisonItem` 以參考傳入；順帶消掉每資料夾 O(n) 掃描（O(n²)）。
- 平行掃描孤兒執行緒 → `CancellationTokenSource.CreateLinkedTokenSource`，一側失敗即取消另一側。

### 走訪 / 傳輸
- **不再跟隨 junction/symlink**（`FileExtensions.IsReparsePoint` 套用於 TreeBuilder、copy/move/delete 遞迴、`CountDirectories`）— 連結指回祖先 = 不可捕捉的 `StackOverflowException`；連結出界 = 刪除真的刪掉目標。
- **覆寫複製不再破壞既有檔**：`Kernel32FileCopyStrategy`/`FileStreamStrategy` 先寫 `<dest>.dCompare.tmp` 再 `File.Move` 換入（同目錄、原子）；失敗/cancel 清暫存檔；先讀 `GetLastWin32Error()` 再做其他 Win32 呼叫。
- `FileStreamStrategy.MoveFile` 不再「先刪目標再 move」。
- `GetItemsForTransfer` 拒絕空/相對 `targetRoot`（原本右側路徑留空時「Copy to Left」寫進 exe 啟動目錄）。
- 新增 sharing-violation 重試：`FileOperationRetry`，只認 Win32 32/33 的 HResult（`0x80070020/21`，FACILITY_WIN32 精確比對），3 次 / 100ms。
- `LogTransfer` 的 `File.AppendAllText` 包 try/catch（原本失敗會把同一筆 transfer 同時放進 Completed 和 Failed）。
- 移除 `FileOperationHandler._activeJobs`/`jobIds`（只寫不讀，每批永久洩漏）。
- `CompareControl.RefreshNodeAsync` 快取清除補目錄分隔符（原本重新整理 `Data` 會連 `Database` 一起砍掉）。
- 刪除確認 `config.Options == null` 時不提示的 bug → `?? new ConfirmationOptions()` 回退。

### 壓縮檔比對
- Entry 改用 **size + SharpCompress CRC**（`FileSystemNode.IsArchiveEntry`/`Crc`）— 原本拿假的 `C:\a.zip\...` 路徑 `OpenRead` → 每條 entry 報 `Conflict`。
- 時間戳 `entry.LastModifiedTime ?? DateTime.MinValue`（原本 `?? DateTime.Now` → 相同 zip 恒報 Modified）。

### 設定 / 持久化
- **Portable 化**：新增 `Common/AppPaths.cs`，`dCompare.json`/`recentSessions.json`/`logs/` 一律以 `AppContext.BaseDirectory` 為基準；啟動一次性遷移（exe 旁沒有、cwd 有 → copy 不刪）；刻意不再讀 cwd（攻擊入口）；exe 目錄不可寫才回退 `%LOCALAPPDATA%\dCompare` + `Log.Warning`；Serilog 檔路徑用 in-memory config source 覆寫成絕對路徑。
- `ConfigurationService.SaveConfigurationAsync` 改「寫 tmp + `File.Move`」原子替換（寫一半斷電 → JSON 損毀 → 之後每次啟動崩潰且 Options 開不起來）。
- Serilog 補 `fileSizeLimitBytes`/`retainedFileCountLimit: 7`/`rollOnFileSizeLimit`（原本 log 無限成長）。

### 安全性
- `ToolFactory`：`Type.GetType` 後立即要求 `ITool` 且限 dCompare 組件（原本先 `constructor.Invoke` 再 cast → 設定檔可指定任意型別執行建構子副作用，如 `System.IO.StreamWriter` 先截斷檔案才報錯）。
- `ToolHandler`：`UseShellExecute = false`；`{file}/{left}/{right}` 一律加引號且不會雙重加引號；裸工具名在 app 目錄/System32/Program Files(x86)/PATH 解析；找不到 → `Log.Error` + 訊息（不露 Win32 錯誤）；`.bat/.cmd` 走 `cmd.exe /c`，且**整條命令外包一層引號**（`cmd /c` 的引號剝除規則會把路徑含空白時的外層引號吃掉 — 不外包必炸）。

### 其他
- `MainWindow.xaml.cs` 關閉確認讀錯選項（`CloseTab` → `CloseApplication`）並包 try/catch（`e.Cancel = true` + `async void` 拋例外 = 崩潰且視窗關不掉）。
- `MftFileSystemTreeBuilder`：回空樹的 stub（接上 = 比對結果全同 = 誘導刪真檔）→ `NotImplementedException`，後整個檔案刪除。
- 工具列 Copy to Left/Right + Edit→Copy 選單**原本永遠報「Method not found」**（反射找不存在的 `CompareControl.CopyTo{Left,Right}_Click`）→ 改接右鍵選單同一條 `ProcessFileOperationsAsync` 路徑。
- `MainWindowViewModel.ExecuteUcCompareMethod` 字串反射 dispatch 全部移除 → 編譯期檢查的直 call。
- 移除 sync-over-async、`_activeJobs` 洩漏（見上）。

## 功能 / UX（使用者核准的行為變更）
- **覆寫/刪除確認全線生效**：`FileOperationHandler` 注入 `IConfigurationService` + `IUserInteractionService`，一批只問一次（操作/件數/目的地/覆寫警告）；`overwrite` 不再寫死 `true`；`Confirmation.Copy/Move/CopyOverwrite/MoveOverwrite/DeleteFile` 五個設定全部真的被讀。
- **刪除走資源回收筒**：`FileOperation/RecycleBin.cs`（`Microsoft.VisualBasic.FileIO`，整個目錄 = 一個可還原項目）；不可用時 `Log.Warning` 退回永久刪除；含 junction 的目錄不進 shell。提示含件數 + 前 5 條路徑。
- 單項刪除後不再二次 `File.Delete`（`if (!RecycleBin.TryDelete(...)) _strategy.DeleteFile(...)`）。
- **暫停/取消**：`FileOperationControlViewModel` 改用 DI 的 `IFileManager`（不再自己 `new`）；`CopyAsync/MoveAsync/DeleteAsync` 加可選 `CancellationToken`；取消的列以 `cancelled: true` + `TransferCanceled` 回報，不記成功。
- **單項取消**：`FileTransfer` 帶自己的 CTS（linked 到 batch token）— panel 右鍵只停該列，大顆 Cancel 仍停全部。
- **大檔守門**：`FileExtensions.IsTooLargeToView`（100MB）套在 TextViewer / TextDiff / TextDiffPlex 三個入口；新字串 `ErrorFileTooLarge`（en/zh-CN/zh-TW）。
- 新字串：`TransferCanceled`、`ErrorFileTooLarge` 等（三語 + 手寫 Designer 屬性）。

## 套件升級（全部 net8.0 相容）
DiffPlex 1.8.0→1.9.0、DiffPlex.Wpf 1.4.0→1.9.1、CommunityToolkit.Mvvm 8.4.0→8.4.2、MetadataExtractor 2.8.1→2.9.3、Serilog 4.3.0→4.4.0、Serilog.Extensions.Hosting/Logging 9.x→10.0.0、Microsoft.Extensions.* 9.0.8→10.0.0（5 個）、System.IO.Hashing 9.0.8→10.0.0、Moq 4.20.72→4.21.0。
**未動**（無 net8.0 相容版/風險）：SharpCompress 0.50.7、AvalonEdit 6.3.1.120、Serilog.Sinks.File 7.0.0、Serilog.Settings.Configuration 9.0.0、Serilog.Enrichers.Thread 4.0.0、xunit.v3 4.0.2、**ImageSharp 維持 4.1.3**（4.4.0+ 要 .NET 10；Release 授權 key 待申請，見 todo）。

## 測試
- 78 → **119**（覆蓋率 18.3% → 23.9%）。新增：確認框矩陣（拒絕⇒零傳輸、`overwrite` 傳值、null Options 回退）、`RecycleBin` 契約、壓縮檔 CRC 對稱/非對稱/Identical、取消不記成功 + **單項取消只停該列/全局取消停全部**、重試只認 32/33 且不碰檔案、大檔守門、單側資料夾狀態、`xxHash64_Partial` 真的 hash、暫存檔不殘留。
- 修好兩個測試基建 flaky：messenger 訂閱加 `_testRoot` 過濾（全域 `WeakReferenceMessenger` + xunit 併行 → 訊息互相漏）；`CopyFileAsync_WithCancellation` 的 `GetLastWin32Error` 讀取時序（產品碼修，A3）。
- 既有凍結測試（FileManagerTests/Kernel32FileCopyStrategyTests）保留不動，僅改 1 支 mock 測試改測 fallback 分支。

## Build / 工具
- **`.gitattributes` 固定 EOL**（commit `12226c3`）：來源檔 `eol=crlf`、json/md/yml `eol=lf`、二進位 `binary` — 治本 `core.autocrlf=true` 造成的幽靈 diff。
- **CI workflow 改 MTP 內建覆蓋率**（`dCompare.Tests.exe --coverage --coverage-output-format cobertura`），ReportGenerator/Codacy 路徑同步 — 原 `--collect:"XPlat Code Coverage"` 在 xunit v3 MTP 下是死指令。（CI 仍紅：SixLabors key + x86 matrix，見 todo。）
- 死碼刪除：`Configuration/ToolService.cs` + `IToolService.cs`、`ParentPathConverter.cs`、`MftFileSystemTreeBuilder.cs`；Options 假「MFT」掃描選項移除（`ScanMethodOptions.MethodOptions` 保留以免舊 json 載不入）；`CompareControl` 的 `IUserInteractionService` 改建構子注入。
- 新增 `AGENTS.md`（專案指引）。

## 第三輪（2026-11-03）：TODO #1~#15（Herdr pane 內 supervised worker + 本 agent 驗收）

基線變化：build 0 error / **100 → 68 warnings**（CS8618 歸零）；測試 **119 → 172**；覆蓋率 **23.9% → 33.1%**。

### P1
- **#1 毁損 `dCompare.json` 崩潰**：`OptionsViewModel.LoadConfigurationAsync` / `TextViewerViewModel.ApplySettings`、`LoadFile` 不再用 `async void` 把例外丢到 Dispatcher；改為內部 try/catch + `IUserInteractionService` 錯誤提示（三語資源字串）。
- **#2 Session 載入失敗不再假報成功**：`LoadSession` 回傳成功與否，只有成功才顯示 `StatusLoadedSession`；全部 entry 被拒時回報「沒有有效項目」並回 false。
- **#3 UNC 自動比對洩 Net-NTLM2**：新增 `Common/SessionPathGuard.cs`（`IsAllowedComparePath` / `IsAllowedDefaultSessionFile`）— 載入 session 時先擋 `\\` 開頭與非 rooted 路徑（在任何程式碰它之前），被拒的 entry 不建 tab、不啟動自動比隊，預設 session 檔限定在 user profile 下。

### P2
- **#4 影像 diff 看不出小範圍差異**：`IsBlockDifferent` 除區塊平均外同時追蹤**最大單像素差**（單顆像素反白不再被 16×16 平均稀釋掉），`Threshold` 預設值不變。
- **#5 大圖 Show Diff O(N²) → 線性**：`while (foundAdjacent)` 全數重掃改為 `HashSet` 一次掃描合併相鄰區塊（4000×3000 @16 從 ~2×10⁹ 次降到線性）。
- **#6 `GenerateMaskImage` 逐像素寫 → raw pixel memory 整行填色**（輸出 bytes 不變，有鎖 hex 的迴歸測試；維持 ImageSharp v3/v4 相容）。
- **#7 比對中途可取消**：`CancellationToken` 貫通到 hash 與 `StreamsEqual`/`ReadFull`，每 8KB chunk 邊界檢查一次（不是每 byte）。取消一定**拋 `OperationCanceledException`**，不會把半完成的樹報成 `Identical`。
- **#8 複製後還原檔案屬性**：`FileStreamStrategy` 複製完成後還原 `File.SetAttributes`（唯讀來源不再變成可寫；唯讀目標第二次複製不再 `ERROR_ACCESS_DENIED`）+ **best-effort ACL**（try/catch + Log，失敗不影響複製；非 NTFS 靜默略過）。
- **#9 長路徑 >260**：`Kernel32FileCopyStrategy` 給 `CopyFileEx` 的兩側路徑在 >260 時先 `GetFullPath` 再加 `\\?\`（UNC 走 `\\?\UNC\` 形式，已帶前綴不重複加）— 不再只有「複製」失敗而比對成功。
- **#10 `TabIdMap` 改以 `TabItem` 實例為 key**（同 header 不再互撞；`AddTab` 改 DI 解析）。**未做** `TabControl.Items → ItemsSource` 大重構（明確排除）。
- **#11 測試品質**：刪除 `DirectoryComparerTests` 中因工廠是 static 而**從未被呼叫**的 `Mock<IFileSystemTreeBuilder>`（假信心）；補 `HashProvider` 邊界、`FileOperationRetry`、`TabIdMap`（+`InternalsVisibleTo`）測試。本 agent 補回 worker 漏掉的 **`ToolFactorySecurityTests`**：`System.IO.StreamWriter, System.Private.CoreLib` 當 Handler → `ToolCreationException` **且目標檔內容不變**（證明建構子從未執行）、測試組件內的 `ITool` 實作被拒且建構子未執行（同組件規則）、dCompare 內非 `ITool` 型別被拒、4 個真正內建工具不會被誤殺、不存在的型別回報「找不到型別」而非「不是內建工具」。
- **#13 杂項（限定四件）**：README 三處對齊現況（MetadataExtractor 其實在用、mask 高亮其實有、壓縮格式如實寫 4 種）；新增低爭議 `.editorconfig`（UTF-8 BOM/CRLF/4 空格/sort usings）；8 處 CS8618 以 `= null!;` 歸零（100→68 warnings，無新增類別）。
- **#14 DI 最小步**：`IHashProvider` 進 `ConfigureServices`、`DirectoryComparer` 建構子注入（保留無參構造讓既有測試不改）；`OnExit` 包 try/catch + `finally` 保證 log flush。`MainWindow`/`MainWindowViewModel` 的 Singleton 註冊**未動**（行為變更，需同意）。
- **測試基建修正（本 agent）**：`CompareAsync_CancelDuringStreamingComparison` 原本用 `CancelAfter(10ms)` 跟熱 cache 的 128MB 串流比對賽跑（~13ms 讀完）→ **3 次掛 1 次**；改為注入 `IFileSystem` 包裝、在第一次 `Read` 時要求取消（下一圈 chunk 邊界必定拋），完全確定性；檔從 64MB 縮到 4MB，測試反而更快。**172/172 × 6 連跑穩定**。

### 本輪刻意未做
- #12（ImageSharp key）、#15（SessionManager UI 執行緒 I/O，理由見 TODO）、`Ioc.Default` 清理、nullable 剩餘 68 警告、.git 歷史瘦身。

## 第四輪（2026-11-03）：全庫審查 20 項，修 #1~#16、#18、#19（跳過 #17、#20）

基線變化：build 68 → **52 warnings**、測試 **172 → 191** 全綠、行覆蓋率 **33.1% → 36.2%**。

### 壓縮檔比對（實測重現，非推測）
- **#1 非預設演算法把兩份完全相同的 zip 報成 `Conflict`**：`FileSystemNode.IsArchiveEntry`/`Crc` 從寫入後**全庫無人讀取**，`DetermineStatus` 拿假路徑 `C:\a.zip\dir\file.txt` 去 `OpenRead` → 拋例外 → 每條 entry 都是 `Conflict`（root 變 `Modified`）。改：`DetermineStatus` 在任何串流呼叫**之前**先走 entry 分支（size + CRC；CRC 缺失的格式如 tar／solid 7z 退回 size + 時間戳）。
- **#2 壓縮檔有檔案「看不見」**：entry key 不正規化，同一個 zip 可同時有 `Docs/` 與 `docs/`；`ArchiveFileSystemTreeBuilder` 比目錄名用**case-sensitive**，產生兩個兄弟節點，而比對端用 case-insensitive 名稱集合 + `Children.Find(OrdinalIgnoreCase)` 只取第一個 → 另一個目錄下的檔案從結果清單與所有傳輸操作裡消失。改：目錄比對改 `OrdinalIgnoreCase`（一個字）。
- **#16 壓縮檔路徑零測試**：新增 `ArchiveFileSystemTreeBuilderTests`（兩份相同 zip × 5 種演算法都必須 `Identical`；同大小不同內容必須 `Modified` —— 這條就是「寫了 Crc 但沒人讀」的守門；混合大小寫目錄兩個檔案都要出現）。
- **#5 壓縮檔 entry 列可刪除且「成功」**：entry 路徑無法開啟 → `RecycleBin.TryDelete` _false_ → `File.Delete` 對不存在路徑是 no-op → `SuccessCount=1` → 面板顯示「操作完成」但什麼都沒刪。改：`TreeNode.IsArchiveEntry` 作為唯一把關點，`GetItemsForTransfer`/`GetItemsForDeletion` 略過、`CompareControl` 與 `ToolHandler`（View/Diff）給明確訊息。測試：`FileOperationHandlerTransferGuardsTests`。

### 資料安全 / 大量操作
- **#4 資料夾刪除的「永久刪除」fallback**：`ContainsReparsePoint` 把任何 `IOException`/`UnauthorizedAccessException` 都當成有 link，於是回退走 `File.Delete` 整樹**永久刪除**——單一_locked_ 檔或 ACL 資料夾就把「可復原」降級成「救不回來」。改：與單檔分支一致，先 `RecycleBin.TryDelete(file)` 再永久刪除（一行）。
- **#6 選幾千個檔案就開幾千個平行工作**：`FileOperationHandler` 對每筆 `Task.Run` 整批、`Task.WhenAll`，無併發上限。改：`_transferGate = SemaphoreSlim(4)`，列全部照舊進面板、僅 I/O 限量；測試用假 `IFileManager` 量測尖峰併發（>1 且 ≤4）。
- **#7 讀不到的資料夾被報成「另一側缺檔」**：整個目錄迴圈被一個 `catch (UnauthorizedAccessException) {}` 包住，而列舉是 lazy —— 中途拋例外會**同時**丢掉後續的目錄與檔案，且不留痕跡。改：兩側分別列舉、每筆各自 try，失敗記 `FileSystemNode.ScanIncomplete`（向上傳播），該目錄比較結果為 `Conflict`。測試：`DotnetFileSystemTreeBuilderTests`。

### 正確性 / UX
- **#8 中文介面完全無法按欄排序**：`ColumnHeader_Click` 依**欄位文字** switch（`"Left Name"` → …），但欄頭綁資源檔，zh-CN 是「左侧档案名称」→ 一律掉到 `LeftDisplayName`。改：改以**欄位索引**對應屬性地名（`ColumnSortProperties`）。註：審查建議的 `GridViewColumn.SortMemberPath` **不存在**（那是 DataGrid 的 API），故未採用。
- **#9 右鍵「複製路徑/檔名」會讓程式掛掉**：`Clipboard.SetText` 在剪貼簿被其他程序佔用時拋 `ExternalException`（CLIPBRD_E_CANT_OPEN），兩支 `void` handler 無 try/catch → 例外逃到 WPF dispatcher。改：合併為一支 `CopySelectedPaths(sender, fileNameOnly)`（消掉一對近重複碼）+ try/catch + Log + 提示。
- **#10 進度列先白走一遍樹且無法取消**：`CountDirectories` 在比對前完整遞迴兩側（只為了進度分母），不接 token，`PathTooLongException`/`IOException` 會把整次比對打斷。改：計數**與掃描並行**（第三個 `Task.Run`）、接 token、非取消例外只記 log；分母改用 `Math.Max(totalDirs, processedDirs)` 防走超。
- **#11 每層目錄比對 O(n²)**：每個名稱對兩側各做一次 `List.Find` 線性搜尋。改：每層先 `IndexByName` 建一次字典（`OrdinalIgnoreCase`，同名取第一，語意與 `List.Find` 相同）。備註：1000/2000/8000 檔平鋪資料夾實測 0.2s/0.8s/3.4s（此時磁氣開啟主導），字典化消掉的是 3~5 萬檔以上的平方項。
- **#12 單側檔案做 diff 每次在 `%TEMP%` 留一個檔**：`CreateTemporaryEmptyFile` 每次產生 GUID 檔名且從不刪除。改：共用一個 `dCompare_Empty.txt`（无人寫入，存在即重用）。
- **#13 同一份 `dCompare.json` 有兩個讀取者**：`LoadConfigurationAsync`（System.Text.Json，會跑 `ValidateConfiguration`）vs `IConfigurationRoot.GetSection("Tools")`（不會）。`IConfigurationService` 的 6 個 section 查詢**全庫 0 呼叫端**，是 `ToolConfigService` 的重複實作 → 連 `_configuration` 欄位、`BuildConfiguration`、`GetConfiguration()` 一併刪除（-140 行）。
- **#14 View 直接摸 ViewModel 的 service 欄位**：`_configService`/`_sessionManager`/`_userInteractionService` 是 `public readonly` 欄位，`MainWindow` 自己讀設定跑離開確認。改：三個欄位改 private，新增 `Initialize(TabControl, MenuItem)` 與 `ConfirmExitAsync()`（含「設定損壞也要關得掉」）。
- **#18 最近 session 選單把路徑印兩次**（`$"{filePath}\\{fileName}"` → `C:\a\s.json\s.json`）；選單不存在時誤用「找不到 TabControl」訊息 → 改記 log。
- **#19 五處繞過資源檔的使用者文字**：tab 標題佔位 `"TODO"`（改用既有 `Untitled`）、`"Error copying/moving items"`、面板 `"Operation completed successfully."`、`ImageViewerViewModel` 的 `"Error comparing images: "`、`About()` 整段硬寫英文（**資源檔其實早有 `AboutMessage`/`AboutTitle`**，只是沒用；順帶修：三個語言的 `AboutMessage` 存的是字面 `\n`，resx 不解析跳脫 → 改真換行）。`"Welcome"` 字串散在 XAML/`SessionManager`/`CloseTab` 三處 → 統一 `WelcomeTabName`。新鍵 7 個 × 3 語言（zh 譯文待母语者覆核，見 TODO）。

### 首次執行 / 設定
- **#3 全新安裝沒有工具，View/Diff 全部無聲失效**：`AppConfiguration.Tools`/`FileTypeMapping` 預設是**空字典**，首輪 `LoadConfigurationAsync` 就把空的寫進 `dCompare.json`；唯一可用的 402 行設定是版控外的本機檔，而且它還引用**不存在的型別** `dCompare.Tools.Diff.ImageDiff` 與 `HexViewerHandler`。改：新增 `Configuration/DefaultConfiguration.cs` 種子 4 個內建工具 + Text/Image/Archive 對應（Archive 不給預設工具：沒有內建工具能開啟壓縮檔本身）。測試：`DefaultConfigurationTests`（每個 handler 必須是 dCompare 組件內的 `ITool`、每個 mapping 只能引用已定義工具、預設值可寫可讀並通過 `ValidateConfiguration`、`.txt`/`.cs`/`.png` 解析到對的工具）。

### 刪除的死碼（#15）
`FileManager.WhoAmI`（+`IFileManager` 對應宣告與如今無用的 `InstanceId`）、`TreeNodeManager.FindOrCreateNode`（僅測試呼叫，且 `TakeWhile`/`LeftDisplayName` 插入序對深度優先清單是錯的）+ 其 2 支測試、`CompareControl : ITool`（`LoadFile => throw NotImplementedException` 卻被當成可選工具）、`FileOperationHandler` 兩迴圈中從不 await 的 `TaskCompletionSource`（`TrySetException` 全是未觀察例外）、`ImageComparerFactory`（僅回傳 `new ImageSharpComparer()`）。

### 測試
172 → **191**（+21：archive 11、掃描完整性 2、傳輸把關 2、預設設定 6；-2：`FindOrCreateNode`）。`ToolFactorySecurityTests` 的 `FactoryWith` 先 `Clear()` 工具與對應表：安全守門測試不應依賴種子預設的內容。

### 本輪刻意未做
- 審查 **#17**（`OpenFolderDialog` 取代 `OpenFileDialog` + `"Folder Selection."` 偽裝，僅把魔法字串收斂成 `const`）與 **#20**（CI matrix `x86`／`SixLaborsLicenseKey`）依指示跳過。
- 未列入報告的小項（6 條）：hash 演算法未先比 size、`GetMaskImage` 的 `Lazy<Image>` 未 Dispose、`_isPaused` 未 volatile、`LoadChildren` 展開時 O(n) 掃描、`LoadSession` 無聲砍掉未儲存的檢視分頁、`RecentSessions` 選單索引算術假設第 0 項固定。

## 第五輪（2026-11-04）：全庫審查 14 項，全部修復（品質優先，逐項補測試）

> 基線：build **0 error**、測試 **191 → 219 全綠**。每個「必須修」都用「還原舊行為 → 測試變紅」驗證過，不是只測新版本。

### 必須修
- **Compare 分頁的演算法下拉選單綁死在靜態種子**：XAML 綁 `{x:Static ComparisonAlgorithms.EnabledAlgorithms}`（編譯期種子），而 Options | Compare 的開關寫進 `dCompare.json` 的副本；設定從磁碟讀回後兩者不再共用物件。**實測**：`dCompare.json` 寫 MD5 啟用／FullContents 停用，選單仍列 FullContents、不收 MD5（第一次執行看起來正常，因為淺複製共用 value 實例）。改：`CompareControlViewModel.LoadAlgorithms()` 由設定建清單並選預設（預設被停用時退回第一個啟用項），XAML 改綁 `AvailableAlgorithms`，刪除 `ComparisonAlgorithms.EnabledAlgorithms`。測試 5 支（`CompareControlViewModelTests`）。
- **進度條在預設平行模式第一個資料夾就跳 100%**：計數走查與掃描平行執行，分母 `totalDirs` 前段仍是 0，防除零的 `Math.Max(totalDirs, processedDirs)` 讓分母等於分子。**實測**（兩側各 780 資料夾）：平行回報 `[1, 1]`，循序回報 `0→0.9→1`。改：抽出 `DirectoryComparer.ProgressTenth()`，分母未發佈時回傳 -1（不回報），發佈後才 clamp 上限。舊測試只斷言 `progress > 0`，直接放過這個 bug → 新增 `DirectoryComparerProgressTests`（theory 7 案 + 循序爬升 + 平行不得提前 100%）。
- **`SessionPathGuard` 接受磁碟機相對路徑**：判斷用 `Path.IsPathRooted`，而 `C:evil` 在 Windows 被視為 rooted 卻依「該磁碟機的工作目錄」解析（`C:..\..\..\Windows` 可到全機任意位置）→ 正好繞過「絕不从 cwd 解析」不變量（UNC 那半仍擋得住）。改：`IsRootedLocalPath()` 要求真正的 `X:\` 根，`IsAllowedComparePath` 與 `IsAllowedDefaultSessionFile` 共用。測試補 6 案。
- **Options 存的語言不是啟動時讀的語言**：`ConfigurationService` 在 exe 目錄不可寫時寫進 `%LOCALAPPDATA%\dCompare`，但 `App.xaml.cs` 讀啟動文化碼用 `SetBasePath(AppPaths.BaseDirectory)` 只看 exe 目錄 → 裝在 Program Files 的使用者選了中文、重開仍英文（Options 卻顯示已選中文）。改：改傳 `AppPaths.ResolveExisting(...)` 完整路徑。順帶刪掉 `ConfigureAppConfiguration(AddConfiguration(...))`（全庫無人注入 `IConfiguration`）。

### 建議修
- **壓縮檔列的「重新命名」沒被擋**（`AGENTS.md` 宣稱擋了）：rename 不走 `GetItemsFor*`，直接 `RenameFileOrDirectory`，捏造路徑令 `File.Exists`/`Directory.Exists` 皆 false → 使用者輸入新名字之後才收到「找不到檔案」。改：`ContextMenu_Rename_Click` 先查 `selectedItem.IsArchiveEntry`（沿用既有資源字串）；`AGENTS.md` 對應描述已改精確（並點名 rename／Open in Explorer 繞過 `GetItemsFor*`）。
- **Options | Compare 按 Cancel 不會取消**：`CompareOptions.Algorithms` 是靜態種子的淺複製，勾選框直接改到共用的 `ComparisonAlgorithmInfo` 實例 → 同一次執行期間靜態種子已被改。改：`CompareOptionsViewModel` 建構子複製每筆 info。測試：`CompareOptionsViewModel_TogglingAnAlgorithmEditsNeitherTheSeedNorTheModel`（先確認確為共用實例，再驗種子與 model 都不動、`UpdateModel()` 仍會寫入）。
- **永遠不成立的英文字面字串比較**：`Description == "Quick (ModifiedDate and Size)"`，實際字串是 `"Quick (Modified Date and Size)"`（多一個空格）→ 條件永假，只是靠 `ComparisonPercentage == null` 才未影響行為；與已修過的欄位標題 `"Left Name"` 本地化 bug 同類。改：`IsPercentageSettingEnabled => _supportsPercentage`。
- **刪除死的記錄設定頁**：`LogOptionsViewModel` 只有 `IsValid`（該頁綁定 `IsEnabled`/`AvailableLogLevels`/`Level`/`RetentionDays` 全綁不到），`BuildMenu()` 也從未加入該節點；`LogOptions`/`SerilogWriteToOption`/`SerilogSinkArgs` 甚至連 `Options` 都沒有對應屬性，Serilog 實際由 `Serilog.json` 設定。刪：XAML DataTemplate、VM、三個設定類別、`LogSettings`/`EnableLogging`/`LogLevel`/`RetentionDays` × 3 語言 + Designer。
- **刪 `Compare/FileOperations.cs`（78 行）**：`ShowPropertiesDialog`/`OpenFileLocation` 是 `FileOperationHandler` 兩個方法的近複製，全庫（含測試、XAML）`FileOperations.` 引用 0 次；且死複製的 `OpenFileLocation` 少了 reparse/archive 把關。
- **單邊 diff 的替代空檔改 per process**（`dCompare_Empty_{ProcessId}.txt`）：固定檔名會讓兩個分頁／兩份 app 同時 diff 時互相鎖住同一個 `%TEMP%` 檔案，且一旦被截斷/刪除，之後所有單邊 diff 全壞（`Lazy` 已快取路徑、不再重建）。
- **長路徑前綴差一個字元**：`ToKernel32Path` 用 `path.Length <= 260`，但 `MAX_PATH` 的 260 含結束 NUL → 正好 260 字元的路徑開不了卻走未加前綴分支，失敗訊息是裸 Win32 error 3（`FileOperationRetry` 只重試 32/33）。改 `< 260`；`ToKernel32Path` 改 internal 並補 258/259/260/261 邊界測試。
- **「在檔案總管中開啟」對壓縮檔列**：捏造路徑只會得到「路徑不存在」。改：兩側選單收斂成 `OpenLocation()` 並先查 `IsArchiveEntry`。

### 精簡
`FileOperations.cs` 78 行、記錄設定頁與三個設定類別約 80 行、死的 Host 設定來源 4 行、死的 `EnabledAlgorithms` 6 行、`Name1`/`Color1`/`Bitmap1`/`Icon1` 死資源鍵 × 3 語言、測試檔內重複宣告的 `IFileOperationStrategy`（`FileManagerTests` 用的是正牌那個）；`DefaultConfiguration` 註解「five」改 four。資源鍵 394 → 386（三種語言一致，Designer 同步）。

## 已知取捨（刻意保留，非欠账）
- 覆寫複製的暫存檔 `<dest>.dCompare.tmp` 與目標**同目錄**：跨磁區 `File.Move` 非原子（更危險）；`+13` 字元會把 `CopyFileEx`（無 long-path）可用路徑窗口縮短 13 字元（併入 todo 長路徑項）。
- 外部工具啟動改 `UseShellExecute = false`：只寫 `notepad++.exe` 這類靠登錄解析的名字，現在必須在 app 目錄/System32/Program Files/PATH 找得到。
- 檔案刪除分支仍靠 `File.Delete` 對已消失檔案是 no-op 來滿足兩支凍結測試（清掉需連帶改測試）。
- `MainWindow` / `MainWindowViewModel` 註冊為 **Singleton 是正確的**，不是欠账：app 只有一個主視窗，且 per-tab 狀態都在 `CompareControl`（已明確 `AddTransient`，註解寫 "Crucial: Transient so each tab gets a new instance"）。改成 Transient 只會讓每次解析拿到不同的主視窗實例 = 純行為變更、零收益。hotkey/config 是在載入時重新 `LoadConfigurationAsync()` 讀檔、非建構子快取，所以也沒有「設定改了不生效」的單例副作用。（早期審查把它當嗅覺警訊列出，已結案。）
