# Arena 重建驗證 — 2026-08-30

[回到專案入口](../../README.md) · [連續教材](../arena-guide/README.md) · [機器可讀結果與逐項測試](arena-rebuild-2026-08-30.json)

這是新 Arena 的實際驗證，不沿用舊 game 的通過數字。框架 production C# 沒有隨重建修改；framework／module 的原有測試保留並一起執行。

## 交付內容

- 唯一 game 是 `Assets/game/arena`：Domain、Application、Infrastructure、Integration、Composition、Unity、Editor，使用獨立 asmdef 限制依賴。
- Domain／Application 不引用 Unity 或 framework。正式 manual、live、recording、replay 共用 ArenaDefinition。
- 十章中文教材逐步說明接點、目的、ownership、呼叫時機、失敗行為與可執行驗證。全文沒有 Markdown table。
- `tools/arena-build` 的 14 個 library projects 編譯正式 sources；不是將所有 C# 合併成一個測試 assembly。
- 舊 game 六個子資料夾及舊 gameplay-checks／gameplay-lessons 的來源已移除，不保留相容層。可由 Git 歷史恢復；被忽略的舊 bin／obj 快取可能仍留在本機，不屬於有效工具或交付。
- 舊教學與原有文件修改保存在 [archive](../archive/README.md)，不再是主教學入口。

## 實際結果

- .NET 分層建置：零 warning、零 error；Arena headless 9 組、獨立 framework headless 3 組全部通過。
- Architecture guard：33 個 asmdef 無循環；Domain／Application 依賴 allowlist、Unity／headless references 比對、明確 C# 變數型別全部通過。
- Unity 完整 EditMode：124／124 通過，無失敗、跳過或 inconclusive；其中新 Arena 是 32 項。
- Unity 完整 PlayMode：8／8 通過；其中新 Arena 是 5 項，其餘是保留的 Unity framework native checks。
- 第 1–9 章的完整 C# 片段以記憶體 Roslyn 編譯、引用正式 DLL，9／9 執行輸出符合文件。零散 member 摘錄對照實作人工核對。
- 文件 guard：26 份 active 文件、141 個本機連結與 26 個 Arena C# 區塊通過；檢查連結存在、表格寬度及明確變數型別，不檢查遠端網址或 anchor。
- Unity 場景 smoke：攻擊、死亡、延遲重生、移動，保存至 1,367 ticks；新 session 重播 Completed、無 difference，原 Live tick 沒有被推進。Restart 回 tick 0，Return live 恢復原 snapshot。
- Unity console error：0。完整 Game view 的 HUD、角色、唯讀 diagnostics 與 replay controls 已截圖檢視，無遮擋或文字重疊。
- Windows x64 Player：Succeeded，零 error；啟動後 76.4 秒仍回應，log 未發現 Exception／Error／crash，驗證後停止測試程序。

Player 的唯一建置警告是場景沒有 RuntimePipelineManager，Unity CLI 的 runtime Pipeline 在 Player 關閉。這是刻意不加入的開發控制服務，Arena 遊戲不依賴它。第一次建置另有未編譯變更提示；確認 recompile up-to-date 後重新建置，最終只剩上述一項警告。

Player startup 使用 `-batchmode -nographics`，因此不當成 Windows Player 的鍵盤／GPU 畫面驗證。畫面驗證來自實際 Unity Editor Game view；輸入及操作切換由 production host 的 PlayMode 測試與場景 smoke 驗證。

![Arena 的實際 Game view，包含唯讀 diagnostics 與錄製／播放 controls](arena-demo.png)

## 反向案例

- 正常錄製：Completed，tick 8，CLI exit 0。
- 教學 oracle 錄製：ReproducedFailure，tick 2，CLI exit 0；此狀態表示故障被正確重現，不是遊戲成功完成。
- 篡改 input 或 hash：tick 1 Diverged／state_hash，exit 1。
- 未知 policy：拒絕，exit 1；Unity 載入未知 policy 不破壞目前 replay。
- 保存到已存在檔案：exit 1，原檔 hash 未改變。
- 未知 CLI selector：exit 2。
- 新增 regression：停止／fault 後呈現最後可用 snapshot、非 owner thread／disposed input 拒絕、ClearInput 後的新 press 不遺失、反序列化非法 scenario 的 Reset 不破壞目前 session。
- Canonical state 包含不可變 rules 與 TickDelta；相同初始畫面但不同傷害政策不能得到相同 initial hash。

## 重跑

在 repository 根目錄，需要可建置 net9.0 的 .NET SDK；本次 SDK 是 10.0.302，Unity 是 6000.3.20f1。

```powershell
dotnet build tools/arena-build/Game.Arena.Composition
dotnet run --project tools/framework-checks
dotnet run --project tools/arena-checks -- all
./tools/verify-architecture.ps1
./tools/verify-docs.ps1
```

連線中的 Unity Editor：一次只跑一個測試工作，等 test_status completed 後再啟動下一個。

```powershell
unity command recompile --format json
unity command recompile_status --format json
unity command run_tests --mode editor --async_tests true --format json
unity command test_status --format json
unity command run_tests --mode playmode --async_tests true --format json
unity command test_status --format json
```

停止 Play 後，可用 `Tools > Arena > Build Windows Player` 建置到 `.utmp/ArenaPlayer/Arena.exe`。本次實際驗證使用連線 CLI 的 async build；結果由 build_status 取得，而不是把 queued 當成功。

```powershell
unity command build --target StandaloneWindows64 `
  --outputPath .utmp/ArenaPlayer/Arena.exe `
  --scenes Assets/game/arena/scenes/ArenaDemo.unity `
  --confirm true --format json
unity command build_status --format json
```

## 邊界

不宣稱跨平台 bitwise determinism、snapshot restore、rollback、任意 tick seek、dynamic physics authority、網路 transport 或舊 recording 相容。Physics sensors 仍是 framework 的獨立選配能力，沒有被假裝成 Arena 已接入的玩法；完整範圍見 [能力清單](../arena-guide/capabilities.md)。
