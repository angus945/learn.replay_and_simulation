# 舊 gameplay API 與錄製格式退役

本輪依 [退役評估](assessments/legacy-compatibility-retirement-options-2026-08-30.md) 的方案 A 執行：先提交基準，再遷移使用者，最後移除主線相容程式。舊資料保留為歷史證據，不再由現行 API 讀取。

## 可追溯的基準

退役前版本：`22f6966f62a6d8af375b639597eb426ea435105a`。

該 commit 包含使用現代 runtime 的 `GameplaySession` 相容 façade、舊 artifact reader／writer／replay、`legacy-rerun`、原始樣本，以及退役前的測試與教學。這是一個可找到來源的相容基準，不代表它能無條件重現所有更早版本。

需要考古時，使用獨立 checkout／worktree 查閱或執行這個版本，不把舊程式重新加回目前的 Assets、教學 CLI 或 framework。既有 `Old_Simulation` 仍是另一代架構的封存，和這次退役的相容 API 不同。

## 現行入口與刻意的破壞性變更

| 退役入口 | 現行入口／政策 |
| --- | --- |
| `new GameplaySession(); Start(scenario)` | `new GameplayDefinition().CreateTestSession(scenario)`；建立成功即為 Running |
| `GameplayRequest` | session ID、sequence、target tick 外加 `GameplayInput`，經 `Gameplay.Submit` 提交 |
| `TickReport`／舊 StateHash | `TemplateTick`／現代 `Hash`；不可比較兩種 hash layout |
| 舊 capabilities／results／admin ports | 現代 ports 與 game adapter 的 action catalog；framework 不接收遊戲專屬描述 |
| `CaptureReplay`／`ReplayArtifact`／`FailureArtifact` | `CaptureRecording`／`TemplateRecording`，含可選 `TemplateFailure` |
| 舊 `ReplayPlayback`／`FailureRerun` | `GameplayDefinition.CreateReplay(recording)`／`TemplateReplay` |
| `legacy-rerun` | 從現行 CLI 移除；歷史用途查閱上述基準 |
| game Protocol payload v1 | v2；transport-neutral envelope 仍是 v1，transport／authentication／pump／reconnect 仍暫緩 |

現代 recording 可以保存 faulted session，不沿用舊 `CaptureReplay` 的拒絕規則。Realtime runner 擁有 tick 時，需先 Dispose runner，才能手動 Step／Reset／Dispose session；不能從 client 宣告 mode 來取得驅動權。

Protocol 的 v2 payload 與 policy／hash／limits 契約，見 [game adapter 文件](../Assets/game/gameplay-protocol/README.md)。這次改接 in-process adapter，不宣稱已有遠端服務。

## 舊檔支援政策

保留 [原始 failure-example.json](testability/failure-example.json) 的內容與路徑；它是歷史樣本，不是現代教學的預設輸入。退役前 SHA-256：

```text
E7608979B3B6A7DBE959D8ADF1BF9877E5395EA20DD93744214C885B9277464C
```

該樣本在基準工具的既有比對中可得到 Matches，但有 `build.unverified`／`policy.unverified`。Comparer 並未比對完整 stack、actors snapshot 與 trace。樣本的 Build 包含 `+phase1-3-working-tree`，不是足以直接重建原始事故環境的乾淨 revision。

現行主版本不支援舊檔讀取，也不提供永久 converter。Repo 外的錄製，包括 `Application.persistentDataPath/Replays`，不會被本次退役刪除、覆寫或批次轉換。若之後需要長期歷史重現，先指定 fixture、來源版本及 runtime，再獨立安排工具支援。

舊／新格式雖然都可能有 schema 數值 1，但資料 layout、hash 與 failure metadata 不同。不得靠替換欄位或 policy 字串偽裝成新格式。需要帶入新測試時，保留原檔，使用指定現代 definition 重跑並另存 recording／比較報告；原事故是否重現要另外判斷。

## 現代錄製教學

[failure-template-example.json](testability/failure-template-example.json) 是現代 CLI 重新產生的非 crash oracle 範例，不是從舊 failure sample 轉換。它在 tick 2 觸發 `cli.position_limit`。

在專案根目錄執行：

```powershell
dotnet run --project tools/gameplay-checks -- rerun docs/testability/failure-template-example.json
dotnet run --project tools/gameplay-checks -- capture .utmp/new-failure.json
dotnet run --project tools/gameplay-checks -- capture-success .utmp/new-success.json
```

Capture 使用 CreateNew；目標已存在會拒絕，不能用它覆寫歷史證據。`GAMEPLAY_BUILD` 可標記這次執行的來源版本；未提供或不同時，重跑會回報 build 的驗證限制，不會偷偷宣稱版本相同。

## 驗收

`tools/verify-architecture.ps1` 除了檢查 assembly 方向／GUID，也檢查 active C#、csproj、asmdef 沒有退役 API 或 `Old_Simulation` 引用。歷史文件與原始 fixture 刻意不在此 gate 內。

刪除後的 headless、五章、Unity EditMode／PlayMode、Demo replay 與 Player 驗證，記錄於 [實作進度](implementation-progress.md)。必須以本次結果為準，不能用基準版本的 PASS 代替刪除後的驗收。
