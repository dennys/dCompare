# TODO（未決項目）

> 已完成項目記錄在 `CHANGELOG.md`。
> 基線（2026-11-03 第四輪結束）：build **0 error / 52 warnings**（CS8618 已歸零）；測試 **191 全綠**；行覆蓋率 **36.2%**；net8.0-windows。未 push commit 數請用 `git rev-list --count origin/main..HEAD` 查（文件不寫死，會漂移）。
> 給 agent 的專案說明在 `AGENTS.md`。

## P1 — 尚未處理

（原 3 項 P1 已全部完成，見 CHANGELOG「第三輪」。）

## P2 — 尚未處理

- [ ] **本機 `dCompare.json` 仍指向已不存在的 handler**：`dCompare/bin/x64/Debug/net8.0-windows/dcompare.json`（版控外、非建置產物之外的本機設定）引用 `dCompare.Tools.Diff.ImageDiff` 與 `dCompare.Compare.CompareControl`（後者已不再是 `ITool`）。種子預設值只在檔案不存在時生效 → **刪掉該檔**讓 `DefaultConfiguration` 重建，或在 Options 裡自行改。
- [ ] **本輪新增的 7 個 resx 鍵的中文譯文由 agent 撰寫**（`ErrorCopyingItems`、`ErrorMovingItems`、`OperationCompletedSuccessfully`、`ArchiveEntryOperationNotSupported`、`ErrorComparingImages`、`ErrorCopyingToClipboard`、`WelcomeTabName`）→ 請母语者覆核 zh-CN/zh-TW。順帶：`AboutMessage` 三個語言原本是字面的 `\n`（resx 不解析跳脫字元，會直接顯示），已改真換行。
- [ ] **`IFileManager.CopyMultipleAsync`/`MoveMultipleAsync` 無呼叫端**（只有 `DeleteMultipleAsync` 有測試使用）。屬公開 API，未擅自刪除；要嘛接回 UI，要嘛刪。

- [ ] **CI 仍是紅的**：(a) Release build 被 SixLabors 擋（見 #1）；(b) matrix 跑 `x86` 但 `dCompare.csproj` 的 `<Platforms>` 只有 `AnyCPU;x64`；(c) 改過的 workflow 尚未在 Windows runner 上驗證過（首跑盯 ReportGenerator 有沒有吃到 `**/TestResults/*.cobertura.xml`）。
- [ ] **剩餘 68 個 nullable 警告**（CS8600/8601/8603/8604/8625，皆「參數/回傳可能為 null」類）：消解需改簽名或加判斷，超出上一輪「安全子集」授權。→ 開獨立條目逐檔處理，低優先。
- [ ] **`SessionManager` 在 UI 執行緒做檔案 I/O**（#15，評估後刻意跳過）：session 檔是 KB 級 JSON，真正重活是建 `CompareControl`/`TabItem`（必須留 UI 執行緒）；非同步化要改 `ISessionManager.LoadSession` 簽名（波及呼叫端 + 凍結測試）或加 fire-and-forget + dispatch-back 的跨執行緒複雜度 → 風險大於收益。若日後 session 檔變大再做。
- [ ] **`Ioc.Default` service-locator 殘留**：`ToolHandler` / `ToolFactory` / `MainWindow` 仍有 service locator 呼叫。清 `ToolHandler` 需改 `CompareControl` 建構子並連動 tab 測試，非零風險。
- [ ] **`.git` 55MB 瘦身（BFG / git filter-repo）**：**已凍結，你要再想想** — 成因、代價（38 個 tag 要重打、所有未 push commit、所有 clone 作廢）、兩條可貼的指令與前後檢查清單見下方 **#5**。本輪只做零風險那半：`.gitignore` 加 `*.zip`（`a659d5d`）。
- [ ] **ACL 還原範圍**（#8 現況為 best-effort：try/catch + Log，NTFS 完整複製、其他檔案系統靜默略過、失敗不影響複製）：若要改成「失敗就警告使用者」或「中斷該項」需使用者決定。**建議維持現況**。
- [ ] `DirectoryComparerTests` 的 static 工廠仍不可注入（該輪是「刪掉從未被呼叫的死 mock」而非「讓它能被注入」）→ tree-builder 層仍無法用 mock 驗證。要做需給 `DirectoryComparer` 加 builder 注入點。

## 1. ImageSharp 4.1.3 授權 key（取得後 Release/CI 才能建置）

Debug 開發、跑測試不受影響；Release/CI 被擋：`error: No Six Labors license found`。

- [ ] https://licensing.sixlabors.com/ 申請免費 Community key（開源/個人/非營利/營收<100 萬美元）
- [ ] key 是 assembly-scoped，**AssemblyName 必填 `dCompare`**
- [ ] 驗證：`dotnet build dCompare -c Release -p:SixLaborsLicenseKey=<KEY>`
- [ ] 長期（避免 key 進版控，擇一）：CI 設環境變數 `SixLaborsLicenseKey`；或 `sixlabors.lic` 放 `dCompare/`（已在 .gitignore）

回退（一行）：`dCompare.csproj` 改回 `SixLabors.ImageSharp 3.1.11`（程式碼 v3/v4 相容；但 3.1.11 的 4 個 HIGH CVE 會回來）。

## 2. CI 覆蓋率（指令已修，兩條限制）

- [ ] 暫勿用 `dotnet test ... -- --coverage`（MTP 2.5.1 + MSBuild node-mode bug，會卡 `test-app-info` pipe；upstream 修後才可恢復）。
- [ ] CI 首跑驗證 ReportGenerator 吃到 `**/TestResults/*.cobertura.xml`。

## 3. 觀察項（不急）

- [ ] `pi packages update`

## 4. 候選：Omnidiff 作為可選 diff engine（2026-07 評估，未來再決定）

結論：不取代 DiffPlex（AGPL v3、太新、單人維護、無 .NET API），作為 config 可選 tool 與現有 `Tools` 字典架構吻合。

- [ ] `Tools/Diff/OmnidiffDiff.cs` 實作 `ITool`：shell out `omnidiff --headless --color never LEFT RIGHT`，plain text → read-only AvalonEdit；binary 不在 PATH → `ToolCreationException`；不捆 binary
- [ ] config `Tools` 加一筆 `dCompare.Tools.Diff.OmnidiffDiff`；`DefaultDiffTool` 維持 DiffPlex
- [ ] 前置：先手動裝 prebuilt Windows binary 感受品質
- [ ] （刻意暫不做）`--mode json` → side-by-side UI；偵測 + 一鍵下載 binary
- 注意：AGPL 以外部獨立 process 使用是最乾淨姿勢；商業化發布前再確認。

## 5. （已凍結 — 你要再想想）用 BFG / git filter-repo 把 .git 從 55MB 瘦到 ~2MB

**狀態：建議先不做。** 本輪只做零風險的那一半（`.gitignore` 加 `*.zip`，commit `a659d5d`）。

### 成因（實測，非推測）
- `.git` = **55MB**（pack 56MB）；commit 總數才 **247** → **與 commit 數量無關**。
- 元凶是單一檔案：`dCompare/dCompare.zip` 被提交 **3 次**（23.3 + 23.3 + 10.8 MB = **57.5MB raw blob**），幾乎等於整個 pack。
- 已在 `c457e6f "Remove zip file"` 自 HEAD 刪除，但 git 的歷史永不消失 → 那 57MB 永久躺在裡面。
- HEAD 目前很乾淨：最大追蹤檔只有 1.9MB 圖示，無 zip/exe/dll/pdb。

### 做了得到什麼
- clone / fetch / CI checkout 從 ~55MB 降到 ~2MB（實質就是省幾秒到幾十秒）。
- **別無其他好處**。不修也不會壞：單檔 23.3MB 未觸發 GitHub 限制（單檔硬擋 100MB、警告 50MB），不影響 build / test / Release。

### 要做就要付的代價（所以要凍結）
| 項目 | 數量 / 影響 |
|---|---|
| 所有 commit hash 改寫 | **未 push 的 commit 必須先全部 push 完**（數量用 `git rev-list --count origin/main..HEAD` 查），否則 hash 改寫後要重做 |
| **tag 要全部重打** | **38 個**（0.1 ~ v0.1.31），含 GitHub Release 依賴的那個 |
| 遠端分支要 rebase | `main` + `0.1.26` + `0.1.28` + `remove-unused-supported-extensions` |
| 所有 clone 作廢 | 每台機器要**重新 clone**（`git fetch --force` 不會清掉本地舊 object） |
| branch protection | 要先暫時關閉 `main` 的 protection 才能 `push --force`（本機無 `gh` CLI，得在網頁關） |
| CI 快取 | 全部失效，首跑變慢 |
| GitHub 端 | 舊 pack 仍留在伺服器，直到它自己的 housekeeping；要立即縮小得開 ticket 請 GitHub 跑 gc |

### 真要動手時的兩條路（擇一）

**A. BFG**（要 Java；BFG 只準對 bare/mirror 倉庫動手，一定在複製上做，別對工作目錄做）
```powershell
git clone --mirror https://github.com/dennys/dCompare dCompare-mirror
java -jar bfg.jar --delete-files dCompare.zip dCompare-mirror
cd dCompare-mirror
git reflog expire --expire=now --all
git gc --prune=now --aggressive
git push --force    # 需先暫時關閉 main 的 branch protection
```

**B. git-filter-repo**（官方推薦、不需 Java、會自動 gc）
```powershell
pip install git-filter-repo
git clone --mirror https://github.com/dennys/dCompare dCompare-mirror
cd dCompare-mirror
git filter-repo --invert-paths --path dCompare/dCompare.zip --force
# 確認無誤後：git push --force
```

### 做之前請全部打勾
- [ ] 本地 **所有未 push 的 commit 已 push**（否則 hash 改寫後要重做）
- [ ] 先備份：`git bundle create backup.bundle --all`（重寫失敗能回退）
- [ ] 記下 38 個 tag 的名稱與對應的 GitHub Release（重寫後全部重打並重推）
- [ ] 網頁上暫時關閉 `main` 的 branch protection
- [ ] 挑一個只有你自己在動的時間窗（重寫期間他人 push = 災難性衝突）
- [ ] 重寫後驗證：`git count-objects -vH` 看 `size-pack`；另外 clone 一份新的跑 build 確認
- [ ] 通知其他使用者重新 clone

### 我的建議
**先別做。** 先拿到 ImageSharp key（#1）、把 CI 弄成綠的（#2）、並實際跑幾個月。等你真的覺得 clone 慢到煩、或要公開這個倉庫時再回頭做 — 屆時倉庫只會更大，38 個 tag 也會更多，成本只升不降。
