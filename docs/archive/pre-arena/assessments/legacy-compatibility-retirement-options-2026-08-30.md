# GameplaySession 與舊 artifact 的退役方案評估

> 本文保留退役前的評估；方案 A 後續已執行，現況見 [退役政策](../legacy-compatibility-retirement.md)與[實作／驗證紀錄](../implementation-progress.md)。本文的舊 source 路徑與行號對應基準 `22f6966`，部分型別已從目前工作目錄移除。

日期：2026-08-30。性質：方案建議；本輪未修改 runtime、Protocol、檔案格式或 Unity 資產。

本評估以目前工作目錄的實作為準，包括上一輪尚未提交的重構；不是只看 Git HEAD。沿用「Protocol 暫緩、避免過度工程」的範圍，不把恢復 Protocol 開發當成本輪已獲授權的實作。

## 建議結論

**採用有退出條件的過渡保留：主線只使用現代 API，暫時凍結相容層；先轉移必要的行為測試，之後只遷移 Protocol 的 in-process adapter，最後一次退役舊 API／artifact 工具。** 不需要為此再造一個 framework，也不建議現在建立永久通用轉檔器或一套新的 session 抽象。

先前「等 Protocol 遷移及舊檔政策確定才能刪除」應說得更精確：

- `GameplaySession` 的主要 production consumer 是現有 `GameplayProtocolAdapter`；移除它需要遷移或停用這個 consumer，**不需要先完成 transport、authentication、Unity pump、reconnect 等整套 Protocol 功能**。
- 舊 artifact 的保留與否，是資料支援政策。Protocol 目前沒有 artifact 讀寫 route，兩個決策可分開排程。
- 但目前 `GameplaySession` 仍直接建立 `FailureArtifact`、保存舊 hash／history，兩者在程式上仍有耦合；**不能直接刪除 artifact 型別而期待 Protocol 繼續編譯**。可以先拆掉這部分 API，但若很快就要退役 façade，做兩次破壞性改動通常不划算。
- 主 runtime、正式 Demo 與五階段教學的執行路徑已不需要這個 façade；這是相容收尾，不是 deterministic-simulation 或 testability 缺少另一個核心機制。

## 現況與影響

| 部分 | 目前實際角色 | 刪除前要處理的事 |
| --- | --- | --- |
| `Old_Simulation` | 不參與現行編譯／匯入的歷史封存 | 與本次 API 退役分開；不用把裡面的舊 runtime 再搬回主線 |
| `GameplaySession` | 建立同一 `GameplayDefinition → TestableSimulationSession`，另做舊 API、policy、hash、artifact 投影 | 遷移 adapter 與必要測試；不能把類別改名後繼續保留同一整層 |
| `ReplayArtifact`／`FailureArtifact` 及 readers | 舊 JSON 契約、回放／故障比對工具 | 決定是否只封存資料，或必須繼續執行舊檔 |
| `TemplateRecording`／`TemplateFailure` | 現行錄製、成功／故障 replay 的正式路徑 | 保留；不加入遊戲專屬的舊格式相容責任 |
| `GameplayProtocolAdapter` | 11 個 in-process operations，仍直接接舊 session；沒有網路服務 | 改接現代 session／ports，或明確封存 adapter；核心 Protocol 可繼續 Deferred |
| 測試與 CLI | 新舊並存 | 搬走玩法／框架行為測試，留下少量真正驗證相容契約的測試，最後隨相容功能退役 |

證據：

- [GameplaySession](../../Assets/game/gameplay-simulation/src/Runtime/GameplaySession.cs)：20、80–86 行使用現代 core；97–98 行轉 admission code；113–125 行轉舊 hash 與 failure。
- [GameplayProtocolAdapter](../../Assets/game/gameplay-protocol/src/Runtime/GameplayProtocolAdapter.cs)：13–15 行直接依賴 façade；20–54 行列出現有 routes；33 行把舊 StateHash 傳到 wire DTO。
- [MovementDemoSession](../../Assets/game/movement-demo/src/Composition/MovementDemoSession.cs)：直接建立現代 test session；[Stage04](../../tools/gameplay-lessons/Stage04Testability.cs)／[Stage05](../../tools/gameplay-lessons/Stage05Replay.cs) 同樣走現代 API。
- [教學 csproj](../../tools/gameplay-lessons/Gameplay.Lessons.csproj) 第 24 行仍 glob 全部 gameplay-simulation source。**不執行 legacy，不代表編譯集合已排除 legacy**；Unity 的新舊型別也還在同一個 Game.GameplaySimulation assembly。
- [CLI 主入口](../../tools/gameplay-checks/Program.cs) 的無參數 regression 仍執行舊 façade／Protocol／artifact checks；[RecordingCli](../../tools/gameplay-checks/RecordingCli.cs) 的 `capture`、`capture-success`、`rerun` 才是現代路徑，另有明確的 `legacy-rerun`。

## 三個可選方案

| 方案 | 做法 | 代價與風險 | 適用情境 |
| --- | --- | --- | --- |
| **A．過渡保留後退役（建議）** | 不再增加舊 API 使用者；先遷移行為測試，下一次開放 Protocol 工作時只改 adapter 接線；舊檔封存，不要求現行主版本永久讀取 | 暫時仍有 façade／舊 hash 的維護成本；必須有明確退出條件，不能只寫「以後再說」 | 目前以框架穩定與教學清楚為主，沒有已確認的外部舊檔支援承諾 |
| B．保留獨立 legacy 工具 | 主線完成 A 的遷移，但把所需 reader／rerun 與相應版本封存在 Assets 外的工具或固定版本中 | 多一個需驗證的執行環境；長期追著新 runtime 更新就不再是單純封存。主線可退役舊架構，但整個 repo／發行物仍保留相容程式 | 確實需要重查歷史事故、客戶錄製或長期 regression corpus |
| C．直接停止支援 | 同時封存尚未遷移的 game Protocol adapter 及其 tests，搬走必要行為測試後移除 façade／舊 artifact API；Framework.GameplayProtocol 核心不必刪 | 舊 C# consumer、`legacy-rerun` 及舊檔開啟能力停止；不能把停用 adapter 說成「已遷移 Protocol」 | 明確接受破壞性變更，而且不想讓暫緩的 adapter 留在 active source |

不推薦永久在主 session 同時輸出兩套 recording，也不推薦維護 v1／v2 雙 runtime。現況已共用一份玩法，再增加相容平台只會擴大清理成本。

## 舊檔政策：保留證據，不等於保留可執行相容性

目前 repo 找到的持久化舊 golden artifact 是 [failure-example.json](../testability/failure-example.json)；正常舊 replay 主要由測試在記憶體或暫存檔產生。這不能證明使用者磁碟、外部工具或其他分支沒有舊錄製；Demo 的錄製目錄在 repo 外的 `Application.persistentDataPath/Replays`。

本輪以既有 CLI binary 執行 `legacy-rerun` 此 sample，結果 `Executed=true`、`Matches=true`，但仍回報 `build.unverified`、`policy.unverified`。這不是重新編譯後的全套測試；[FailureRerun](../../Assets/game/gameplay-simulation/src/Runtime/FailureRerun.cs) 43–75 行也沒有比對原始 actors snapshot、完整 exception stack 或 trace，因此 Matches 不代表所有歷史診斷內容完全重現。

建議預設政策：

1. 從現在起新增錄製只使用 `TemplateRecording`；新的教學 failure fixture 也用現行 policy／oracle 產生。
2. 舊原始檔不覆寫、不改 hash、不重新貼上現代 schema 標籤；作為歷史證據保留。
3. 若沒有持續重播舊檔的需求，在主線退役時停止舊格式支援。封存文件記錄可用的來源版本、依賴、runtime 與已驗證的 fixture。
4. 若確有歷史重現需求，才採方案 B，且先列出要支援的 fixture／policy／runtime 範圍。只把目前 façade 封存起來，不能保證它能重現所有更早版本的行為。

**不做直接 JSON 轉檔的原因：**

- [舊 GameplayStateHasher](../../Assets/game/gameplay-simulation/src/Runtime/GameplayDiagnostics.cs) 35–76 行會把 scenario、條件式 lifecycle layout 等寫入 hash；[現代 canonical state](../../Assets/game/gameplay-simulation/src/Runtime/GameplayDefinition.cs) 76–100 行採另一份 layout。同一場景／狀態的 hash 也不能拿來互相比較。
- Policy identity、limits 與 failure envelope 不同；舊 sample 沒有明確的 DiagnosticPolicy／FailureStage／LastCompletedTick，不能自行填一組值就宣稱保存了原失敗語義。
- 舊 failure 原始 exception、trace、snapshot 是當時的證據。重新執行得到的是新證據；修正過的程式可能不再失敗，自訂 invariant 也需要相同實作與 policy 才能比較。
- 兩個不同格式的 schema 數值都可能是 1，不能僅憑版本數字判斷格式或相容性。

若必須把舊案例帶到新測試流程，應走「驗證舊資料 → 取 scenario／inputs → 用指定現代 definition 重跑 → 重新錄製 → 另存比較報告」。報告區分完成、故障、拒絕執行與行為差異，並保留原檔／來源 hash／原版本；稱為**重跑並重新錄製**，不是無損轉檔，也不是原事故已重現。

版本資訊尤其要分清：sample 的 Build 是 `17181f44922147a0db7480cb7c4a0ef4227c62df+phase1-3-working-tree`，不是一個足以直接還原全部工作目錄的乾淨 revision。新 recording 會寫入目前 runtime，但直接重用舊 scenario 也會帶入原 Build 字串。重新錄製的報告應分別記錄來源 SHA-256、原 Runtime／Build、實際執行 Runtime／Build、舊比對與新 replay 結果；不必為此擴張 framework schema。

## 最小工作順序

### 批次 1：把主線測試與教學從相容 API 撤離

- 將 `FrameworkGuideExamples.ControlledMovement` 改為現行 ports，避免舊接線範例和新五章互相矛盾。
- 將仍只透過 façade 驗證的 Move／Attack、sequence／target tick、Reset、停止／取消、invariant、lifecycle／RNG、trace／diagnostics 行為搬到現代 API。可重用既有 assertions，不必機械式複製全部舊測試。
- 舊專屬測試只留下舊 code 映射、舊 hash／policy、舊檔 reader、相容 API lifecycle 等；退役時可一併移除。
- 整理 mixed files，再讓 lessons 的 compile list 能排除 legacy。只標 Legacy 資料夾不等於依賴隔離。
- 不先建獨立 legacy asmdef：會連帶修改 Protocol references，也要處理 internal access；若下一批就刪，收益小。

此批次不必恢復 Protocol 功能開發，也不改現有 wire／檔案相容行為。

最容易漏掉的 coverage 是：

- [LifecycleAndRandomTests](../../Assets/game/gameplay-simulation/tests/LifecycleAndRandomTests.cs)：同 tick 重複擊殺只生成一次、出生前操作、舊 ID 不指向 replacement、等待／拒絕／預算耗盡不多抽 RNG、Reset 還原兩條 stream。
- [GameplaySessionTests](../../Assets/game/gameplay-simulation/tests/GameplaySessionTests.cs)：NaN、未知 action、self／unknown／out-of-range target、死亡後同 tick 再操作、拒絕不改狀態；其中純 Combat／Invariant tests 也要保留。
- [DiagnosticReaderTests](../../Assets/game/debug-overlay/tests/DiagnosticReaderTests.cs)：Poll 不增加 tick／trace／invariant 次數、source gap 與本地 eviction、Reset 清流、故障時顯示上次 invariant tick。
- 舊 API 語義不能直接變成 modern 的要求：舊 `CaptureReplay` 拒絕 Faulted，現代 `CaptureRecording` 支援故障錄製；舊 realtime driver 可跨 Reset，現代需先釋放 driver。按既定現代契約改 assertions，不強迫新 API 模仿舊行為。
- [ReplayTests](../../Assets/game/gameplay-simulation/tests/ReplayTests.cs) 也包含已經使用現代 Demo 的 replay 測試，不能整份視為 legacy 刪掉。

### 批次 2：Protocol 只改 adapter 接線，不擴張功能

本項是待安排的後續工作，不代表本輪已執行，也不把它和完整遠端 Protocol 綁成一包。

| 現有依賴／契約 | 最小遷移方式與注意事項 |
| --- | --- |
| `GameplaySession`／`Start` | adapter 使用已由 `GameplayDefinition.CreateTestSession` 建立的現代 session；不要另造一層相同 façade。現代建立後已 Running，不需要 Start |
| `GameplayRequest`／`TickReport`／result page | DTO 映射到 `GameplayInput` 與現代 ports；step 用 `TemplateTick`，結果仍是共用 `ActionResult` |
| Capabilities | Move／Attack catalog 留在 game adapter／integration；從現代 session 的實際 `Limits` 回報有效預算，不只讀 scenario 的預設值 |
| Reset scenario | 由可信 composition 保留／提供已配置 scenario；不要為 Protocol 向 framework 加一堆遊戲欄位。Reset 後新 identity、lease 失效及重試語義必須保留 |
| Realtime 唯讀 | 由 host 的實際驅動權接線決定能否控制；不得讓 client 自報 mode，也不能只靠不會更新的旗標而允許搶 tick |
| Admission code | 舊 façade 將 `input.capacity` 轉成 `action.capacity`，並拆分 invalid／duplicate sequence；明確選擇保留 wire mapping 或版本化變更，不可悄悄改 |
| StateHash | 舊 step 回傳 legacy hash；現代 hash 不同。若無外部 v1 client，建議版本化 game payload 契約並標明 policy；不需要因此重寫 transport-neutral framework envelope |
| Diagnostics／trace | 直接接現有 reader；保留 cursor、reset stream、fault snapshot 的行為 |

Framework.Testability 已有 gameplay、simulation、admin、results、diagnostics ports；這項工作主要在 game adapter、DTO、composition 與測試。不應把 game action catalog 或 v1 hash 搬進 framework。

驗收除既有 6 個 adapter tests 外，須針對遷移風險確認：新舊 admission 選定契約、hash policy、實際 limits、realtime control 拒絕，以及 reset retry／新 lease／舊 session ID。既有 endpoint 去重與權限 checks 保持通過；**不需要**新增 socket、登入服務或 Unity pump。

### 批次 3：依舊檔政策退役

- A：保留歷史原檔與版本資訊；移除 façade、舊 reader／writer／replay helpers、`legacy-rerun` 和只驗證已刪相容功能的 tests。
- B：把明確需要的歷史工具移出 active Unity source，固定依賴與驗證語料；主線依然做相同刪除。
- C：略過 adapter 遷移，先把 adapter／其 tests 明確封存，再完成同樣的主線清理；保留 Framework.GameplayProtocol 的獨立核心與 tests。

刪除以**型別和使用點**為單位，不能整個檔案一概移走：

- `GameplayContracts.cs` 同時有現代需要的 `GameplayActionKind`、`GameplayScenario`、`ActorObservation`、`GameplayObservation`，以及舊 `GameplayRequest`、`TickReport`、`HashCheckpoint`、`FailureArtifact`。
- `GameplayDiagnostics.cs` 的 `GameplayInvariant` 仍被現代 definition 使用；舊 hasher／ScenarioRerun 才是退役候選。
- `ActionResult`、`SessionState`、`SubmissionResult`、`TraceEntry`、`ArtifactJson` 仍屬現代共用功能，不能因名稱或位置而誤刪。
- `ActionDescriptor`／capability catalog 如仍供 adapter 使用，應留下需要的 game 描述，而不是連同舊整份 `GameplayCapabilities`／DriveMode／port 體系保留。
- 最後再掃描 framework 的 `ITestSession`／`IStateObserver` 等舊介面是否還有 consumer；不把所有名字相似的 interface 視作同一批垃圾。

## 完成門檻與工作量

這是約 **3 個可獨立驗收的收尾批次**，不是再做一輪主框架重構。批次 1 主要是 coverage／source 邊界；批次 2 是單一 game adapter 的契約遷移；批次 3 才是資料支援決策與刪除。是否存在外部 C#／wire consumer、額外 artifact 與自訂 policy，會比檔案行數更影響工期，因此不以「還差 5%」表示完成度。

可以宣稱主線完全不依賴舊 API 的條件：

1. Demo、五章、現代 CLI、仍啟用的 adapter／tests 不再引用 legacy 型別；modern-only 編譯集合也通過，不只是 runtime 沒走到。
2. 原本透過 façade 驗證的必要行為已有現代測試覆蓋；測試數下降能逐項說明是相容功能退役，而非丟失玩法驗收。
3. 舊檔政策、已知 fixture、支援版本與不支援的情況明文記錄；原始證據未被覆寫。
4. Headless checks、五章、Unity 編譯／EditMode／PlayMode、Demo 錄製與 replay 通過；若移動 source／assembly 或調整 build 範圍，再驗證 Player build 與啟動。
5. 無 Module→Framework/Game、Framework→Game 或新的依賴循環；主流程不靠 legacy hash、reader 或 façade。

本輪做 source／consumer 評估及上述既有 CLI binary 的唯讀 sample 比對，沒有重新執行 Unity。上一輪 181 EditMode、5 PlayMode 與 Player 驗證，仍以 [既有驗證紀錄](../verification/framework-stabilization-2026-08-30.json) 為準，不能代替未來刪除後的驗收。
