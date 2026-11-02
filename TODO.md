# TODO

> 2026-11-02 由 AI 完成一次全專案審查（read + build + test + 2 位 subagent 分別查 async/正確性 與 安全/韌性）。
> 基線：`dotnet build dCompare.sln -c Debug` = 0 error / 54 warning；測試 78 全綠；行覆蓋率 **18.3%**。
> 本檔 = 審查結果（已修 / 待修）+ 原本就存在的待辦（保留在下方，未改寫）。
> 給 agent 的專案說明已另寫在 `AGENTS.md`。

---

## A. 本次已修復（尚未 commit，請 review）

### A1. 比對引擎 `dCompare/Compare/DirectoryComparer.cs`
- [x] 右側 archive 判斷誤用 `leftPath` → 總目錄數算錯、進度百分比錯。
- [x] 進度節流的 `double lastReportedProgress` 被兩條掃描執行緒同時讀寫（race）→ 改 `Interlocked` + `Volatile`。
- [x] **取消時 `CompareNodes` 回傳 false**，比較做到一半的樹被回報成 `Identical` → 使用者可能據此刪掉好檔。改為 `ThrowIfCancellationRequested()`。
- [x] `FullContents` 用 `File.ReadAllBytes` 把兩檔整併進記憶體（大檔直接 OOM）→ 改 8KB 串流比對（`StreamsEqual` / `ReadFull`）。
- [x] 移除 sync-over-async 的 `Compare()`（`.GetAwaiter().GetResult()`，UI 執行緒呼叫即死鎖的地雷）→ 呼叫端 `CompareControl.Refresh()` 改用 `CompareAsync`，進度改 `Dispatcher.InvokeAsync`（不再阻塞 worker）。
- [x] **`xxHash64_Partial` 從未 hash**：該枚舉掉進 `default:` 分支，實際上是「size + 修改時間」比較，而且 `ComparisonPercentage` 是完全沒人讀的死設定 → UI 上選了「Hash (xxHash64 - Partial)」卻得到 size+date 的判定。已補 case 並接回 `config.Options.Compare.Algorithms[algo].ComparisonPercentage`（Partial 預設 30%）。
- [x] **單側資料夾的狀態標錯**：`result.LastOrDefault(i => i.Left == left || i.Right == right)` 在 `left == null` 時等於比對 `i.Left == null`，會命中該子樹裡**每一筆** RightOnly/LeftOnly 列，於是把「最深的那一列」塗成 Modified，真正的父資料夾列卻不動。改為把 parent `ComparisonItem` 以參考傳入；順帶移除每個資料夾一次 O(n) 掃描（大樹是 O(n²)）。

### A2. 目錄走訪：不再跟隨 junction / symlink
`Common/FileExtensions.cs` 新增 `IsReparsePoint()`，套用到
`DirTreeSystem/DotnetFileSystemTreeBuilder.cs`、`FileOperation/FileManager.cs`（copy / move / delete 三處遞迴）、`DirectoryComparer.CountDirectories`。
理由：`Directory.GetDirectories()` 會跟隨 junction，(1) 連結指回祖先 → 無限遞迴到 `StackOverflowException`（**不可捕捉，進程直接死、無 log**）；(2) 連結指向比對範圍外（`mkjunction link D:\project`）時，**刪除會真的刪掉目標資料夾的內容**。
注意：属性讀不到時回傳 false（寧可走訪也不願靜態跳過目錄 —— 靜默跳過會被回報成「另一側不存在」，反而誘導刪檔）。

### A3. 複製/移動不再破壞既有檔
- `Strategies/Kernel32FileCopyStrategy.cs`：覆寫時改為「先寫 `<dest>.dCompare.tmp` 再 `File.Move` 換入」。原本 `CopyFileEx` 直接寫在目標上，取消或當機 midway 會留下一個**截斷的檔案蓋掉使用者原版**。失敗/cancel 會清掉暫存檔，且**先讀 `GetLastWin32Error()` 再做其他 Win32 呼叫**（否則 `File.Delete` 會洗掉 error code，測試 `CopyFileAsync_WithCancellation` 就是抓到這個）。
- `Strategies/FileStreamStrategy.cs`：同樣改暫存檔 + 換入；`MoveFile` 不再「先刪目標再 move」（move 失敗就兩份都沒了），改 `File.Move(src, dst, overwrite)`。

### A4. 其他明確錯誤
- [x] `FileOperation/FileOperationHandler.cs`：`GetItemsForTransfer` 現在拒絕空/相對的 `targetRoot`（`ArgumentException`）。原本右側路徑留空時按「Copy to Left」，檔案會被寫進 **exe 的啟動目錄**。
- [x] `FileOperationHandler`：移除 `_activeJobs` / `jobIds`（只寫不讀，每次複製/刪除永久洩漏一筆 + 一個 TCS）。
- [x] `Compare/CompareControl.xaml.cs`：`RefreshNodeAsync` 的快取清除 `StartsWith(RelativePath)` 少了目錄分隔符 → 重新整理 `Data` 會把 `Database` 底下所有快取列一起砍掉（下次展開就憑空消失）。
- [x] `Configuration/ConfigurationService.cs`：`SaveConfigurationAsync` 改為「寫 tmp + `File.Move`」原子替換（寫到一半斷電 → JSON 損毀 → 之後每次啟動都丟 `InvalidDataException`，而唯一能修它的 Options 視窗也開不起來）。
- [x] `MainWindow.xaml.cs`：關閉確認讀錯選項（`Confirmation.CloseTab` → `Confirmation.CloseApplication`，後者原本是死碼），並把整段包進 try/catch —— `e.Cancel = true` + `async void` 拋例外會**同時崩潰且讓視窗關不掉**。
- [x] `DirTreeSystem/MftFileSystemTreeBuilder.cs`：原本是回傳空樹的 stub（若哪天被接上 → 比對結果=「全部相同」→ 使用者刪真檔）。改為 `throw new NotImplementedException`。
- [x] `dCompare/Serilog.json`：補 `fileSizeLimitBytes` / `retainedFileCountLimit: 7` / `rollOnFileSizeLimit`（原本 log 無限成長）。

### A5. 套件升級（全部維持 `net8.0-windows`，沒有拉到 .NET 10；已 restore + build + 78 tests 全綠）
DiffPlex 1.8.0→1.9.0、DiffPlex.Wpf 1.4.0→1.9.1、CommunityToolkit.Mvvm 8.4.0→8.4.2、MetadataExtractor 2.8.1→2.9.3、Serilog 4.3.0→4.4.0、Serilog.Extensions.Hosting 9.0.0→10.0.0、Serilog.Extensions.Logging 9.0.2→10.0.0（被 Hosting 10 的傳遞相依逼著一起升）、Microsoft.Extensions.* 9.0.8→10.0.0（5 個）、System.IO.Hashing 9.0.8→10.0.0、Moq 4.20.72→4.21.0。
尚未升（原封不動）：SharpCompress 0.50.7、AvalonEdit 6.3.1.120、Serilog.Sinks.File 7.0.0、Serilog.Settings.Configuration 9.0.0、Serilog.Enrichers.Thread 4.0.0、xunit.v3 4.0.2。

---

## B. 待修 P1 —— 資料遺失 / 功能根本沒作用

1. **[最大風險] 複製與移動永遠無條件覆寫，而且「覆寫前要確認」的設定全是死碼。**
   `FileOperation/FileOperationHandler.cs:178-179` 硬編 `overwrite: true`；`Configuration/AppConfiguration.cs` 的 `ConfirmationOptions.Copy / Move / CopyOverwrite / MoveOverwrite / DeleteFile` 只有 Options 頁面在寫，**全專案沒人讀**。全 app 只有「刪除」有確認框。
   → 一次誤點「Move to Right」就直接毀掉對側版本並刪掉來源，無提示、無復原，而且使用者在 Options 裡勾了也沒用。
   最小修法：在 `ExecuteTransferAsync` 讀 `config.Options.Confirmation`，目標已存在時依 `CopyOverwrite/MoveOverwrite` 提示，並把該值傳下去取代寫死的 `true`。

2. **刪除是永久刪除（無資源回收筒）、確認框不顯示路徑與數量。**
   `FileManager.DeleteDirectoryAsync` + `Kernel32FileCopyStrategy.DeleteFile/DeleteDirectory` 走 `File.Delete`/`Directory.Delete`；唯一的保護是 `CompareControl.xaml.cs:623` 一個不含路徑的 `ConfirmDelete`。
   建議：改 `Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile/DeleteDirectory(..., RecycleOption.SendToRecycleBin)`（專案已經參考 Microsoft.VisualBasic），並把路徑/數量塞進提示文字。

3. **從比對清單發起的傳輸，暫停與取消完全無效。**
   `App.xaml.cs` 註冊的是 `IFileManager` 單例，但 `FileOperation/FileOperationControlViewModel.cs:37` 自己 `new FileManager(new Kernel32FileCopyStrategy())`（另一份 `_isPaused` / `_cancellationTokenSource`）；而 `FileOperationHandler` 呼叫 `CopyAsync/MoveAsync/DeleteAsync` 時**根本沒有傳 CancellationToken**。
   → 數 GB 的複製無法取消。修法：VM 注入共用的 `IFileManager`，並把面板的 token 貫通到 `FileOperationHandler`。

4. **壓縮檔的內容比對 100% 失效。** `DirTreeSystem/ArchiveFileSystemTreeBuilder.cs:51,66` 把 `FullPath` 造為 `C:\a.zip\dir\file.txt`，`DetermineStatus` 拿它去 `OpenRead` → `FileNotFoundException` → 每條 entry 都被 `_fileSystem` 的 catch 回報成 `Conflict (X)` 並各寫一筆 Log.Error。同一個假路徑也被丟給外部工具開啟（`Compare/ToolHandler.cs:42-44`）。
   修法：其中一側是 archive 時，改用 size + entry CRC 比較，不要去 Open 假路徑。

5. **壓縮檔時間戳是編出來的**（`ArchiveFileSystemTreeBuilder.cs:69` `entry.LastModifiedTime ?? DateTime.Now`）→ 兩個內容相同的 zip 也會報 Modified；且有值時是 local time 而 zip 存的是 1980-based 欄位，1 秒容差吸收不掉。
   修法：`entry.LastModifiedTime?.UtcDateTime ?? DateTime.MinValue`，archive 一律以 size（+CRC）判等。

6. **設定檔與 log 的位置取決於「exe 怎麼被啟動」。** `App.xaml.cs:24-26,46-48` 與 `ConfigurationService.cs:30` 用 `Directory.GetCurrentDirectory()`，`Serilog.json` 的 `logs/dCompare.log` 也是相對路徑；但 `Session/SessionManager.cs:42,60` 用 `AppDomain.CurrentDomain.BaseDirectory` —— **兩個設定家**。
   影響：從「傳送到 / 右鍵開啟」啟動時 cwd 是別的地方 → 設定憑空消失、`recentSessions.json` 讀不到；裝在 Program Files 時每次存檔都拋例外；更糟的是**任何人在可寫入的 cwd 放一個 `dCompare.json`，就能餵動第 7、8 點**。
   ✅ **已定案（2026-11-02，portable 取向）**：不搬 %APPDATA%。統一用 **`AppContext.BaseDirectory`（exe 同資料夾）** 作為 `dCompare.json` / `recentSessions.json` / session 檔 / `logs/` 的唯一基準，一处 helper 全部共用（順便把「兩個設定家」一起收掉）。
   兩處要留意：(a) 舊行為是把 `dCompare.json` 放在 **cwd**，所以要留一個遷移/回退：exe 旁邊找不到、但 cwd 有舊檔時就讀它（並拷一份到 exe 旁邊，不刪舊檔）；(b) 若放進受保護目錄（Program Files）寫不入，才回退 `%LOCALAPPDATA%\dCompare` 並 `Log.Warning` —— 這是錯誤處理，不是新增模式。

7. **`Tools/ToolFactory.cs:74` 先实例化、後才 cast `ITool`。** `Type.GetType(handler)`（可用 assembly-qualified name 載入 exe 旁邊的任意組件）接著 `:77-133` 用「能從 args/DI 湊到的最多參數的建構子」`constructor.Invoke`，`(ITool)` 是之後才檢查的。
   → 設定裡寫 `"Handler": "System.IO.StreamWriter, System.Private.CoreLib"`，就能在使用者雙擊某檔時先把該檔**截斷**，然後才顯示「tool creation failed」。
   最小修法（2 行，接在 74 行之後）：`if (!typeof(ITool).IsAssignableFrom(type) || type.Assembly != typeof(ITool).Assembly) throw new ToolCreationException(...)`。

8. **外部工具啟動未做引號/shell 處理。** `Compare/ToolHandler.cs:92-102`：`ViewArguments.Replace("{file}", leftPath)` 後直接 `ProcessStartInfo { FileName = toolConfig.Path, Arguments = arguments, UseShellExecute = true }`。`{file}` 只有在使用者範本自己加引號時才有引號 → 檔名含空白就拆成額外參數（對 Notepad++/VS Code 這類吃 flag 的工具是參數注入）；`Path` 指向 `.bat/.cmd/cmd.exe` 時 ShellExecute 會走 `cmd.exe`，檔名裡的 `& | ^` 變成命令分隔符。也沒有 `File.Exists(toolConfig.Path)` 檢查。
   檔名是可被外部影響的（網路共享、解壓出來的下載檔、zip 裡的檔案名）。
   修法：`UseShellExecute = false` + 用引號包住替換後的路徑（或改用 `ArgumentList`）。⚠️ 這可能影響你現有的外部工具設定（見 D 段）。

9. **`dCompare.json` 一毀損就崩潰。** `ConfigurationService.LoadConfigurationAsync` 把 `JsonException`／驗證失敗一律轉成 `InvalidDataException` 丟出，而多數呼叫端是 `async void`（`Options/OptionsViewModel.cs:39 LoadConfigurationAsync`、`Tools/Viewer/TextViewerViewModel.cs:56 ApplySettings`（在建構子裡叫）與 `:71 LoadFile`）→ 例外逃到 Dispatcher 直接結束進程；`ToolHandler` 的 try/catch 也接不到（它看不到那個 task）。
   修法：`ApplySettings`/`LoadFile` 改 `async Task` 讓呼叫端 await；讀入失敗時在 Options 顯示錯誤而不是炸掉。

10. **Session 載入失敗卻回報成功。** `Session/SessionManager.cs:249-300` 抓住 `Exception`、彈訊息框、然後**正常返回**；`MainWindowViewModel.LoadSessionFile` 之後無條件設 `StatusMessage = StatusLoadedSession`。recent sessions 失敗也只有 `Log.Warning`。
    修法：`LoadSession` 回傳 `bool`（或直接丟出），成功才顯示成功訊息。

11. **啟動時會拿不可信的 session 路徑去開 UNC → Net-NTLM2 外洩。** `SessionManager.cs:249` 反序列化 `Dictionary<string, SessionConfig>`，`MainWindowViewModel` 依 `Session.DefaultSessionFile` + `AutoCompareAfterSessionLoad` 直接把 Left/Right 餵給 `DirectoryComparer` → 對 `\\attacker\share` 走 SMB 驗證（可被中繼），並在攻擊者選的位置做 hash。
    修法：載入時拒絕非本機/UNC 的 Left、Right（`Path.IsPathRooted` 且排除 `\\`），並限定預設 session 檔必須在 user profile 下。

---

## C. 待修 P2

- [ ] **大檔沒有大小上限**：`Tools/Viewer/TextViewerViewModel.cs:73 File.ReadAllText`（之後 `TextViewer.xaml.cs:49` 又 `textEditor.Load()` 載入第二次）、`Tools/Diff/TextDiffViewModel.cs:108-109`、`Tools/Diff/TextDiffPlex.xamlViewModel.cs:83-84` 都是整檔進記憶體 → 在比對清單雙擊一個 2GB log 或誤標 .txt 的 .raw，會帶走整個 app（連同在跑的複製，參考 A3 為何那很危險）。提議 `new FileInfo(p).Length > 100MB` 就訊息跳出。
- [ ] **沒有 sharing-violation 重試**：全專案搜不到 retry/attempt。`File.Delete/Move/CopyFileEx` 各只呼叫一次，AV/Explorer/OneDrive 縮圖常態性鎖檔幾百 ms → 整個操作回報失敗、清單狀態變錯。提議一個 8 行 `RetryOnSharing(Action)`（3 次、100ms backoff、只吃 Win32 32/33）。
- [ ] **ImageComparer 看不出小範圍差異**：`Compare/ImageComparer.cs:230-260` 拿 16×16 區塊的**平均**差值和 `Threshold`（預設 30）比 → 單顆像素反白平均只有 `255/256 ≈ 1`，完全看不出，`SimilarityPercentage` 仍是 100%。這正是 diff 工具最不該有的失效模式。修法：`IsBlockDifferent` 同時追蹤最大單像素差值。
- [ ] **Show Diff 在大圖會卡很久**：`ImageComparer.cs:309-340` 用 `do { for j in 0..N } while (foundAdjacent)` 反覆重掃全部候選矩形 → O(N²)；4000×3000 @BlockSize=16 ≈ 47k blocks（~2×10⁹ 次），進度一直停在 0。修法：一次性 `HashSet<(int,int)>` + union-find。
- [ ] **`GenerateMaskImage` 逐像素寫**（本次升級 v4 時改成的 `maskImage[x,y] = red`）：大圖可改用 `DangerousGetPixelRowMemory`/row span，整行填同一顏色。
- [ ] **平行掃描的孤兒執行緒**：`DirectoryComparer.CompareAsync` 平行分支 `await Task.WhenAll(tasks)` 只在第一個失敗時丟出、不會取消兄弟；而 `CompareControl.Refresh` 的 `finally` 已把 CTS 設成 null → 一側 zip 壞掉時，另一側的完整遞迴掃描繼續跑且**Cancel 無效**。修法：`CancellationTokenSource.CreateLinkedTokenSource` + 在 catch/finally 取消。
- [ ] **比對中途無法取消**：hash 與串流比對都不收 token，只在檔案之間檢查 → 超大檔按 Cancel 看起來壞掉。
- [ ] **`FileOperationControlViewModel.LogTransfer`（:284-293）在 `Dispatcher.Invoke` 裡同步 `File.AppendAllText` 到 `~/Documents/FileOperationLog.txt`**：寫入失敗（OneDrive redirect、AV 鎖檔）會讓例外沿 `_messenger.Send` 回到 worker 的 catch，把同一筆 `FileTransfer` **同時**放進 Completed 和 Failed。而且這個 log 繞過 Serilog、無限成長。修法：包 try/catch + 改用既有 Serilog。
- [ ] **屬性/ACL 未還原**：`FileStreamStrategy` 只還原三個時間戳，沒有 `File.SetAttributes` → 唯讀來源複製成可寫檔；唯讀目標第二次複製會 `ERROR_ACCESS_DENIED`。
- [ ] **長路徑 >260 只有預設策略失敗**：BCL 路徑 OK（.NET 8 自己加 `\\?\`），但 `Kernel32FileCopyStrategy` 把原始字串給 `CopyFileEx`（無 longPathAware manifest）→ 深層節點只有「複製」失敗、比對卻成功。
- [ ] **MVVM 破窗**：`MainWindowViewModel.cs:143-149` 直接 `new TabItem` / `new CompareControl(...)` 並操作 `TabControl.Items`、用 `TabCounter` 當 header 字串當 key（同 header 會撞 `TabIdMap`）；`ExecuteUcCompareMethod` 用方法名字串跑非公開反射（改名只會在執行期變成「Error executing action」）。`Compare/CompareControl.xaml.cs` 仍有 999 行 code-behind。
- [ ] **死碼待清**：`Configuration/ToolService.cs`（0 個外部參考）、`ParentPathConverter.cs`（0 個）、`MftFileSystemTreeBuilder` + Options 裡的 "MFT" 掃描選項（選了完全沒反應，還寫著「faster scanning」）、`FileStreamStrategy`（只被測試用，DI 註冊的是 Kernel32）、`ToolHandler.cs` 註解掉的 MessageBox、`CompareControl` 註解掉的時間戳區塊。
- [ ] **測試品質**：覆蓋率 18.3%。`DirectoryComparerTests` 每個測試都 `Setup` 了 `Mock<IFileSystemTreeBuilder>`，但工廠是 static、那個 mock 永遠不會被呼叫 → 假信心（也等於 A1-7、B4、B5、A1-6 這些全都測不到）。`SessionManager`、`ToolFactory`、`FileOperationHandler`、三個 TreeBuilder、`MainWindowViewModel`、`CompareControlViewModel` 完全沒有測試。原有 1 個取消測試是註解掉的。
- [ ] **CI 目前是紅的**（詳見下方既有待辦 #2 與 #1）：`.github/workflows/dotnet-desktop.yml` 還在用已移除的 `--collect:"XPlat Code Coverage"`，也沒有傳 `SixLaborsLicenseKey`；matrix 跑 `x86` 但 `dCompare.csproj` 的 `<Platforms>` 只有 `AnyCPU;x64`。
- [ ] **杂項**：54 個 nullable 警告（多數是 CS8618 未初始化的 `[ObservableProperty]`/DI 欄位）；沒有 `.editorconfig`/格式化設定；`.git` 已經 90MB（歷史裡有二進位）；README「3dr party libraries」把 MetadataExtractor 列在「Not used yet」但其實 `ImageViewerViewModel.cs:285+` 在用；README Known Issues 說「沒有像素級差異高亮」但 `ImageComparer` 其實有產 mask（文件與實作不同步）；`FileExtensions._archiveExtensions` 只有 `.zip/.rar/.7z/.tar`，與 README 宣稱的 16 種格式不符（SharpCompress 其實能讀更多）。
- [ ] `App.xaml.cs`：`OnStartup`/`OnExit` 是 `async void`；`MainWindow` 與 `MainWindowViewModel` 都註冊成 Singleton；`IHashProvider` 沒進 DI（`DirectoryComparer` 自己 new）；`ToolHandler`/`ToolFactory`/`MainWindow` 裡混用 `Ioc.Default` service locator。
- [ ] `SessionManager` 在 UI 執行緒做檔案 I/O（小檔，暫不緊急）。

---

## D. 需要你決定（我不敢擅自改語意／會變更既有行為）

1. **覆寫確認的 UX 與文案**（B1）：要現行「比對清單右鍵複製/移動前，若目標已存在就提示」嗎？需要在 `Resources.resx` / `zh-CN` / `zh-TW` 新增字串。點頭我就做（含測試）。
2. ~~設定檔搬到 `%APPDATA%\dCompare`~~ → **已決定：保持 portable**，統一用 exe 同資料夾（`AppContext.BaseDirectory`），見 B6。
3. **刪除改為資源回收筒**（B2）：對「同步/清理」取向的工具來說語意改變不小。
4. **`UseShellExecute = false`**（B8）：安全得多，但可能讓你現有的外部工具（尤其是 `.bat`、或只寫 `notepad++.exe` 這種要靠 PATH/登錄解析的）開不起來。要改的話請給我一份你實際在用的外部工具設定。
5. **死碼刪除**（C 段最後一條）：`ToolService`、`ParentPathConverter`、MFT stub 與 Options 的 "MFT" 選項、`FileStreamStrategy` 是否保留？
6. **`Confirmation.CloseApplication` 修正後**：原本勾「Close Tab」會出現關閉應用程式確認框的舊行為消失了（那是讀錯選項造成的），如需保留請說。

---

## 1. ImageSharp 4.1.3 授權 key(取得後 Release/CI 才能建置)

目前 `SixLabors.ImageSharp` 已是 4.1.3。**Debug 開發、跑測試不受影響;Release / CI 會被擋**:
`error: No Six Labors license found. Set $(SixLaborsLicenseKey)...`

- [ ] 到 https://licensing.sixlabors.com/ 申請 **免費 Community key**
      (資格:開源 / 個人 / 非營利 / 公司年營收 < 100 萬美元;付費僅限封閉源碼+營利+營收>100萬)
- [ ] 申請時注意:community key 是 assembly-scoped,**AssemblyName 必須填 `dCompare`**
- [ ] 取得 key 後驗證 Release 可建置:
      ```powershell
      dotnet build dCompare -c Release -p:SixLaborsLicenseKey=<你的KEY>
      ```
- [ ] 長期做法(擇一,避免 key 進版控):
      - CI 設環境變數 `SixLaborsLicenseKey`(MSBuild 會自動當成 property 讀取)
      - 或把 `sixlabors.lic` 檔放 `dCompare/` 資料夾(加入 .gitignore,本地/CI 各自放置)

**放棄升級的回退方式**(一行):`dCompare/dCompare.csproj` 改回
`<PackageReference Include="SixLabors.ImageSharp" Version="3.1.11" />`
(程式碼已改寫成 v3/v4 皆相容,回退不用改程式;但 3.1.11 的 4 個 HIGH CVE 會回來)

## 2. CI 覆蓋率指令更新

`coverlet.collector` 已移除(xunit v3 的 MTP runner 不支援 VSTest collector),改用內建 CodeCoverage:

- [ ] CI 腳本把旧的 `/p:CollectCoverage=true` 换成:
      ```powershell
      dotnet build dCompare.Tests -c Debug
      dCompare.Tests\bin\Debug\net8.0-windows\dCompare.Tests.exe --coverage --coverage-output-format cobertura
      ```
      報告產在 `dCompare.Tests\bin\Debug\net8.0-windows\TestResults\*.cobertura.xml`
- [ ] 暫勿用 `dotnet test ... -- --coverage`(MTP 2.5.1 + MSBuild node-mode 有 bug,會卡 test-app-info pipe; upstream 修復後才可恢復)
- [ ] （審查補記）本機已驗證上述指令可用,覆蓋率 18.3%。CI 的 `dotnet test ... --collect:"XPlat Code Coverage"` 請一併換掉,否則 coverage 那步永遠拿不到檔。

## 3. 觀察項(不急)

- [ ] 曾觀察到 1 次單次 flaky 失敗(之後 4 次全綠,疑似計時敏感的 `CreateFileAsync_Test`);CI 若重現再抓名字處理
- [ ] （審查補記）`Kernel32FileCopyStrategyTests.CopyFileAsync_WithCancellation_ShouldThrowWin32Exception` 也是計時敏感型:它靠 `CopyFileEx` 的回調觸發取消,10KB 的檔有可能在回調前就複製完。本輪曾出現一次 `NativeErrorCode` 為 0 的失敗,根因是**我們這邊**在讀 `GetLastWin32Error()` 之前先做了 `File.Delete`(已修,A3);若之後還偶發,就是測試本身需要改用更大的檔或明確注入取消。
- [ ] `pi packages update`(本機 Windows 的 pi 套件比遠端 v5llm 舊)

## 4. 候選：Omnidiff 作為可選 diff engine（2026-07 評估，未來再決定）

背景：評估過 https://github.com/ivankovic/omnidiff（Rust CLI，tree-sitter 結構化 diff + 圖片/字型/PDF/壓縮檔 content-aware diff，p50 ~4ms）。**結論：不取代 DiffPlex**（太新、單人維護、不接受 contribution、AGPL v3、無 .NET API），但**作為 config 裡的可選 diff tool** 與現有架構完全吻合：`Tools` 字典 + 每 file type 的 `DefaultDiffTool`/`PreferredTools.Diff` 已是 option 模式，DiffPlex（`Tools/Diff/TextDiffPlex`）只是其中一個 handler。

最小可行版本（約一個下午）：
- [ ] 新 `Tools/Diff/OmnidiffDiff.cs` 實作 `ITool`：shelling out `omnidiff --headless --color never LEFT RIGHT`，plain text 輸出丟進 read-only AvalonEdit
- [ ] binary 不在 PATH 時 throw `ToolCreationException`（走現有錯誤路徑，不自動捆 binary，使用者自裝）
- [ ] config `Tools` 加一筆 `{ "Handler": "dCompare.Tools.Diff.OmnidiffDiff" }`；所有 `DefaultDiffTool` 維持指 DiffPlex 不動
- [ ] 前置：先手動裝 prebuilt Windows binary，拿實際檔案感受品質再動手

刻意暫不做（日後有人真的愛用再升級）：
- [ ] 解析 `--mode json` 輸出映射回 side-by-side / inline diff UI（大工程）
- [ ] 偵測 + 一鍵下載 binary 的選項

注意：AGPL 以「外部可選 binary + 獨立 process」方式使用，是法律面最乾淨的姿勢；若未來要商業化發布再確認一次。
