# `module.deterministic-simulation` 影響與重複實作評估

評估日期：2026-09-02

Replay Lab 基準：`dd9f748`

共用模組基準：`002949a8e74e749defee21585826f077371f06e6`

研究文件只作架構參考；本評估的結論以實際 repository、公開 API、依賴宣告與當次執行結果為準。

## 結論

新模組已用 Git submodule 固定在 [`modules/module.deterministic-simulation`](modules/module.deterministic-simulation)，但**只有安裝，尚未整合**：它位於 Unity 的 `Assets`／`Packages` 匯入範圍之外，沒有被任何 asmdef、csproj、Framework 或 Game 引用。因此目前沒有編譯期、執行期或 Unity asset 影響，也沒有已載入的重複型別。

新模組不應取代 `Framework.DeterministicSimulation`。fixed tick、delta time、phase、message waves、session lifecycle、drive ownership 與 realtime runner 仍由 Lab framework 負責。新模組適合承接無時間的 Step 座標、history identity、snapshot catalog／replay plan、fork lineage 與 divergence primitives。

若後續正式採用，正確的依賴入口是：

```text
Game / Arena Composition
          |
          v
Framework.Testability  --->  module.deterministic-simulation
          |
          v
Framework.DeterministicSimulation  --->  Module.SimulationPrimitives
```

Game、Arena Domain/Application 與 `Framework.DeterministicSimulation` 不需要直接引用新模組。

## 能力重疊

| Replay Lab 現有能力 | 新模組能力 | 判斷 |
| --- | --- | --- |
| `RecordedInput`：sequence、target tick、凍結後的 encoded payload | `RecordedInput<TInput>`：sequence、target step、generic payload | 高度重疊，但新模組未保證 payload 已凍結或 detached；第一階段只能由 adapter 投影。 |
| `TemplateTick` + `ActionResult` + `TemplateFailure` | `RecordedStep<TInput, TEvidence>` + `StepCompletion` | 高度重疊；Lab 的 evidence 比新模組預設欄位更完整。 |
| Session 內的 inputs／ticks history | `DeterministicHistory<TInput, TEvidence, TSnapshot>` | 高度重疊；新模組增加 HistoryId、generation、snapshot 與 fork。 |
| `TemplateDifference`、`TemplateReplay` 內的 first-difference compare | `DeterministicReplayDivergence`、`ReplayVerifier` | 直接重複候選；需先證明 action result／failure／policy 的比較語意沒有退化。 |
| `PolicyId` | `SemanticRevision` | 同一概念，可由 adapter 明確映射。 |
| `SimulationTick(number, deltaTime)` | `StepIndex` | 不是可互刪的重複。前者是 Lab 的固定 tick 執行資料；後者是跨 domain 的無時間 history 座標。 |
| `TemplateRecording` schema、codec、limits、trace、scenario、runtime metadata | `DeterministicHistory` | 只有部分重疊。既有 artifact 契約與資源上限仍須保留。 |
| 尚未實作的 authoritative snapshot restore／seek／fork | snapshot descriptor、retention、replay plan、fork | 新能力，但目前只是資料結構，尚未完成 Lab world 的完整 capture／restore。 |

`Module.SimulationPrimitives` 的 intent／command／event contracts 與 tick lifecycle 也不是新模組的重複實作，不應因名稱相近而刪除。

## 正式接入前的阻擋問題

### 1. 無 snapshot 的 replay plan 會失敗

[`BuildReplayPlan`](modules/module.deterministic-simulation/src/Narrative.DeterministicSimulation/DeterministicHistory.cs#L150) 在沒有 snapshot 時把起始 step 設為 0，隨後要求讀取不存在的 recorded step 0。黑箱探針對 target step 1 得到：

```text
InvalidOperationException: Missing recorded step 0; cannot build replay plan.
```

Snapshot 在契約中是 optional capability，而 Replay Lab 現況也尚未有 authoritative snapshot restore；此問題會直接阻擋第一階段採用。

### 2. 公開 history 與 input／snapshot 不是 detached value

[`Steps`／`Snapshots`](modules/module.deterministic-simulation/src/Narrative.DeterministicSimulation/DeterministicHistory.cs#L73) 直接回傳內部 `List<T>` 的 `IReadOnlyList<T>` view；呼叫端可轉型回 `List<T>` 並清除內容。`RecordedStep` 直接保存 caller 提供的 input list，`SnapshotDescriptor<T>` 也直接保存 snapshot reference。

黑箱探針證明：

```text
INPUT_COUNT_AFTER_SOURCE_MUTATION=0
STEPS_RUNTIME_MUTABLE=True;COUNT_AFTER_CLEAR=0
SNAPSHOT_COUNT_AFTER_SOURCE_MUTATION=2
```

這會讓 capture 後的外部 mutation 改寫歷史。現有 Lab 會在 admission 時 encode／decode payload，artifact contracts 也會複製為 arrays；不能為了共用 API 反而失去此保證。

### 3. fork 的 snapshot identity 沒有跟 generation 對齊

[`ForkAt`](modules/module.deterministic-simulation/src/Narrative.DeterministicSimulation/DeterministicHistory.cs#L193) 建立新 generation，卻直接複用舊 generation 的 snapshot descriptor。探針建立 generation 1 的 fork，產生的 replay plan 仍回傳 generation 0 snapshot。若 `semanticRevision` overload 改版，複用的 snapshot revision 也可能不同。

在定義 snapshot compatibility／ancestor lineage 前，不能把這個 fork 當成共用權威。

### 4. `StepIndex` 溢位會靜默回到 0

[`StepIndex.Next()`](modules/module.deterministic-simulation/src/Narrative.DeterministicSimulation/StepIndex.cs#L24) 沒有 checked arithmetic：

```text
new StepIndex(ulong.MaxValue).Next().Value == 0
```

這破壞嚴格單調的 Step 契約，應明確拒絕 overflow。

### 5. 同一步輸入只檢查 duplicate，沒有驗證穩定順序

[`EnsureInputsConsistent`](modules/module.deterministic-simulation/src/Narrative.DeterministicSimulation/DeterministicHistory.cs#L242) 會拒絕重複 sequence，卻接受 `2, 1` 的輸入順序。Lab 現有 session 在執行 tick 前會依 sequence 排序。共用模組應選定一種明確契約：要求 caller 已排序並驗證，或建立 detached copy 後自行排序。

### 6. `AddSnapshot` 失敗不是 transactional

[`AddSnapshot`](modules/module.deterministic-simulation/src/Narrative.DeterministicSimulation/DeterministicHistory.cs#L105) 先把 snapshot 加入並排序，之後才驗證 retention policy、計算總 bytes 與執行淘汰。若 policy 無效或 checked byte sum 溢位，方法會拋例外，但 history 已被改動。應先完成所有可失敗的驗證／計算，再一次 commit 新集合狀態。

### 7. `ReplayVerifier` 的預設 evidence equality 不適合直接套現有 DTO

[`ReplayVerifier`](modules/module.deterministic-simulation/src/Narrative.DeterministicSimulation/ReplayVerifier.cs#L68) 預設使用 `EqualityComparer<TEvidence>.Default`。`TemplateTick`、`ActionResult` 與 failure DTO 沒有完整的 value equality；若直接把它們當 `TEvidence`，即使內容相同也可能因 reference equality 被判 divergence。Adapter 必須建立 immutable value evidence 或提供明確 comparer，並保留現有 first-difference 欄位順序。

### 8. 發布與 Unity 消費邊界尚未完成

上游只提交 netstandard2.1 csproj 與 xUnit tests，沒有 Unity package／asmdef；production namespace 與 assembly 又是 `Narrative.DeterministicSimulation`。API 雖未引用 Narrative domain type，命名仍不符合跨專案中性的目標。

直接把 submodule 放進 `Assets` 會讓上游 xUnit test source 進入 Unity import，且沒有合法 assembly boundary。因此目前刻意安裝在專案根層。正式接入前應先決定可重現的 Unity delivery：例如經版本化的已編譯 assembly，或符合模組治理規範的 Unity integration package；不應在 submodule 內留下未追蹤 asmdef 作為永久方案。

### 9. 驗收覆蓋不足

模組目前只有 4 個 xUnit tests：nearest snapshot、fork count、duplicate／gap、單步 digest divergence。它們全數通過，但沒有覆蓋上列失敗情境、snapshot detached、fork compatibility、revision mismatch、retention 邊界與 overflow。

## 建議接入順序

1. **先修正上游 kernel**：補 no-snapshot plan、checked StepIndex、detached collections／payload／snapshot、input order、fork lineage／compatibility，再把研究文件列出的 kernel gates 做成同一組 xUnit contracts。
2. **完成中性命名與交付邊界**：將 production namespace／assembly 從 `Narrative.*` 中性化，建立 Replay Lab 可重現取得、但不污染 Base repository 的 Unity 消費方式。
3. **只在 `Framework.Testability` 建 adapter**：`PolicyId → SemanticRevision`、`ulong tick → StepIndex`、encoded string → `RecordedInput<string>`、`TemplateTick`／failure／results → project-owned immutable evidence，並提供明確 value comparer。同步在 Unity asmdef 與 headless csproj 宣告同一依賴。
4. **保留現有 artifact 外殼**：第一階段不改 `TemplateRecording` schema、JSON codec、limits、trace、scenario 與舊檔相容政策，只替換內部 history／difference 的單一權威。
5. **補 Arena authoritative snapshot**：snapshot 必須包含完整 world、RNG、identity counters、pending structural work 與其他可還原狀態；Observation／state hash 只能驗證結果，不能代替 snapshot。現有 [`CaptureEvidence`](Assets/game/arena/src/Infrastructure/RegistryLifecycle.cs#L55) 也明確不是 restore format。
6. **用 equivalence gates 遷移**：從 initial replay 與 snapshot + suffix 必須得到相同 hash、ActionResults、failure 與 final observation；同時保留現有 failure、policy mismatch、limits、不同 frame schedule 與 realtime ownership contracts。
7. **最後才刪重複 DTO／compare**：只有在 Lab 與 Narrative 都以共用 contracts 通過驗收後，才移除 `TemplateDifference`、內部 history lists 或其他已被完整取代的實作。

## 當次驗證

| 驗證 | 結果 |
| --- | --- |
| 遠端／本機 submodule commit | `origin/main`、remote HEAD 與本機皆為 `002949a8e74e749defee21585826f077371f06e6` |
| 新模組 xUnit | 4/4 PASS |
| Replay Lab framework checks | 3/3 組 PASS |
| Arena headless checks | 9/9 組 PASS |
| Architecture guard | 33 asmdefs、無循環、無 Module→Framework/Game、261 unique asset GUIDs，PASS |
| 新模組額外黑箱探針 | 成功重現 no-snapshot、overflow、alias mutation、fork identity 與 unordered input 缺口 |

目前架構 guard 只掃描既有 `Assets`／`tools` 邊界，所以 submodule 尚未整合時不會納入它的公開契約檢查。正式接入時必須同步擴充 guard：未知 assembly reference 應 fail closed、`Framework.Testability` 的 Unity asmdef／headless ProjectReference 必須一致，且新依賴只能出現在允許的方向。
