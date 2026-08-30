# Framework 穩定化與教學實作進度

更新：2026-08-30。[原始重構評估](assessments/determinism-testability-legacy-retirement-2026-08-30.md)保留為歷史；本頁記錄實際交付。行為與刻意變更詳見 [驗收矩陣](framework-guide/acceptance-matrix.md)。框架／教學穩定化先提交為 `22f6966`，其後依[退役方案](assessments/legacy-compatibility-retirement-options-2026-08-30.md)執行相容層清理；[舊檔政策](legacy-compatibility-retirement.md)保留原始證據及版本資訊。

## 本輪範圍

- **Protocol：只遷移 in-process game adapter**。直接改接現行 ports，game payload v2／envelope v1；transport、authentication、Unity pump、reconnect 仍 Deferred。
- 主線是 deterministic-simulation、testability、共用 game 規則、Unity lifecycle / presentation / sensors，以及同一 CharacterMovement 模型的累積教學。
- 不新增 DI container、空 BC、通用 ECS World、每個類別一個 interface。
- 不承諾 dynamic Rigidbody authority、physics outcome recording、cross-platform bitwise、snapshot restore / rollback、Explorer、transport。

## 階段 1：核心執行契約

- [x] dispatcher 拒絕重入與 dispatch 中 Clear，保留 handler enqueue 下一 wave 的契約。
- [x] 低階 Runner 的 owner thread / 重入 / failure latch / LastCompletedTick / 有界 catch-up，失敗後不能續跑 partial tick。
- [x] 多 participant 註冊順序、phase 後 reaction drain、event-only reaction 與 wave cap 有 executable checks。
- [x] 純 C# assembly 關閉 engine references；加入 `tools/verify-architecture.ps1` 檢查依賴循環、Module / Framework / Domain 方向，以及 metadata GUID 格式 / 唯一性。

## 階段 2：一份 game 規則與生命周期

- [x] `GameplayActions` 集中 Move / Attack 決策，`GameplayWorld` 組合 aggregate、repository、registry、RNG 與 spawn/despawn；`GameplayDefinition` 接框架。
- [x] 先把 `GameplaySession` 收斂為現行 runtime 的相容 facade，提交可追溯基準後，完成 consumer 遷移並刪除 façade／重複 ports；主線只使用 GameplayDefinition／TestableSimulationSession。
- [x] 預算以 scenario 為預設來源；default Reset 跟新 scenario 更新，explicit override 保留。候選 Reset 失敗時原世界 / limits / pending input 不變。
- [x] 非零與斜向移動、Move＋Attack 時序、range、死亡拒絕、延遲重生、spawn budget 與 RNG stream 都有驗收。
- [x] player 與 view 依 ID / PlayerId / FindActor 接線，不依陣列位置。

## 階段 3：testability / diagnostics / replay

- [x] 新增 InputExecutionContext、project metadata、結果分頁；domain 不依賴 trace framework。
- [x] 自訂 invariant factory 每 session 獨立，明確 policy identity；policy 不符在 tick 0 拒絕。
- [x] action → damage/death 的 sequence / actor / target 可追蹤；phase / hash / invariant / lifecycle notices 有一致 trace 欄位。
- [x] 正常 recording 與非 crash invariant failure 都能 JSON → replay；不同 frame schedules 比對同逐 tick results / hash / failure。
- [x] CLI `capture` / `capture-success` / `rerun` 只使用現行 TemplateRecording；`legacy-rerun` 已退役。CreateNew 不覆寫，檔案與執行預算有上限。
- [x] 舊 artifact DTO／projection／reader 已移除，原始 sample 的 SHA-256 不變；舊程式可在 `22f6966` 查閱。新增現代 failure fixture，沒有假裝無損轉檔。Tick 例外後 observation 是最近已捕捉 snapshot，附 ObservationTick，沒有 partial-world restore 承諾。

## 階段 4：Unity integration

- [x] 新增 `Framework.DeterministicSimulation.Unity`，只依賴 framework / module，不引用 Game。
- [x] 有界 prefab pool、stable ID / instance generation 分離、spawn/despawn/reuse、owner thread 與清理。
- [x] 多 actor position / rotation interpolation、出生 snap、跨 session 明確 SnapToCurrent。
- [x] 獨立 local PhysicsScene，只手動推進自己的 kinematic/static sensors；不改 global simulation mode。
- [x] native callback facts 正規化：同 pair/contact family 每 tick 一筆，Enter 優先於 Stay；先正規化再計算容量。unbound / foreign / stale binding 不轉 gameplay facts。
- [x] Exit callback 不提供原 generation，明確不接收；despawn 由 lifecycle snapshot 表達。
- [x] Demo 透過 GameplayActorPresentation 使用共用 pool，live capture、Replay 前後 snapshot、return live snap 都有 PlayMode 驗收。Input / presentation adapter 例外只記錄一次並停止 frame 驅動，HUD 呈現原因。
- [x] Editor 核對 Missing Script；四份舊 scene/prefab 連 `.meta` 逐位元複製驗證後封存在 `Old_Simulation/LegacyUnityAssets`，正式匯入區 Missing Script=0。
- [x] Build Settings 改為 CharacterMovementDemo；實際 Play 啟動、950 tick 錄製、載入 / 完整重播 / 返回同 tick live 成功，adapter failure 為 null。
- [x] Windows Development Player build 成功（0 errors），背景啟動持續運作且無 runtime exception；已關閉僅為測試啟動的 Player。

## 階段 5：同一範例的可執行課程

- [x] `tools/gameplay-lessons` 有獨立 CLI，1–5 或 domain/application/simulation/testability/replay 可單獨執行。
- [x] 01 Domain、02 Application、03 Definition / phase / observer 沿用現有模型；沒有另一份 Player / Cube 世界。
- [x] 04 正式 ports / target tick / sequence / results / Reset。
- [x] 05 同一 game 加 attack / death、seeded health / delayed respawn、recording、三種 frame schedule、divergence、custom oracle failure。
- [x] Unity 延伸章沿現有 Demo 與已驗證的 pool/sensor adapters；不複製第三套 gameplay。
- [x] 更新主要 architecture / recipes / contracts / template / Demo 指南，移除舊接線入口；教學與檢查工具的 active source 也完全排除退役型別。

## 相容層退役：使用者與契約

- [x] GameplayBehavior 20、Lifecycle 15、Replay 12、ToolControl 11、Overlay 6 個案例直接驗證現代 API；原本 63 個案例成為 64 個，沒有丟掉必要玩法覆蓋。
- [x] 退休舊 capability catalog 一個案例；原 aggregate rerun 差異案例改為三個 first-difference cases。舊故障禁止錄製／固定 Manual／driver 跨 Reset 等要求改為現代契約。
- [x] Protocol 保留原六個場景，新增 payload version、有效 limits、admission、執行期間 drive ownership 四組；共用 checks 同時被 NUnit 與 headless 執行。
- [x] `HasRealtimeDriver` 讀取真實 driver ownership，不依賴 client 提供 mode。每個 mutation 在 owner-thread pump 執行時再檢查，Pause 不會交出 tick authority。
- [x] game payload 嚴格要求 Version=2，僅 capabilities discovery 可用空 object。ModernHash／Policy、limits 與 admission code 明確改版，未知／舊版本不產生 mutation。
- [x] 13 份舊 source asset 與 metadata、空 legacy API folder 已透過 AssetDatabase 移除；保留共用 Scenario／Observation／ActionResult／codec，將 invariant 與行為測試改為適切名稱。

## 基準 commit 22f6966 的驗證證據（歷史）

| 層次 | 結果 | 範圍 |
| --- | --- | --- |
| Headless contract suite | 11 行 PASS；exit 0 | 既有及新增 core、template、modern game、lifecycle、compat regression；不是 NUnit 總數 |
| 累積教學 CLI | 5/5 階段 PASS；exit 0 | 各章可單跑；錯誤 selector 非零退出 |
| CLI recording | 正常 Completed tick 8；oracle ReproducedFailure tick 2 | legacy sample Matches；篡改 hash / policy 回報差異；不覆寫既有檔 |
| Unity 編譯 | 無 C# compiler error | 包括 game PlayMode assembly，確認已被 Editor 匯入 |
| Unity EditMode | **181/181 PASS，0 skip** | 包括 core、game、testability、pool / presentation、physics facts；含保留的 Protocol regression |
| Unity PlayMode | **5/5 PASS，0 skip** | 3 個 local physics native scenarios、2 個 game presentation / replay scenarios |
| Editor scene smoke | PASS | 正式 Demo、2 active views、950 tick recording / replay / return live；畫面確認 player / enemy 正常 |
| 資產與依賴 | 40 asmdefs / 306 metadata checks PASS；Missing Script=0 | 無 Module→Framework/Game、Framework→Game 或循環；純 assembly 禁 Unity references |
| Player build / startup | **Succeeded，0 errors；背景啟動 PASS** | StandaloneWindows64 / Development / 正式 Demo；唯一 warning 為未啟用 CLI Player 控制端，符合預期 |

曾發現且已修：舊 trace assertion、optional constructor 帶來的 assembly 依賴、無效 test GUID、Reset 預算未更新、compound collider 同 tick Enter/Stay 重複、相鄰 tick 的跨 session 插值。最終數字只來自修正後完整測試。

## 退役後的完整驗證

| 層次 | 結果 | 證據範圍 |
| --- | --- | --- |
| Headless checks | 11 組輸出 PASS，exit 0 | 包含 10 組 game Protocol v2 契約；不載入 Unity assemblies |
| 五章教學 | all 與 1–5 各章 PASS | 未知 selector exit 2；active source 沒有相容層 |
| CLI 錄製 | Completed tick 8／ReproducedFailure tick 2 | 篡改 hash → tick 1；篡改 policy → tick 0；不覆寫、舊格式與舊命令拒絕 |
| Unity 編譯 | 無 compiler errors | 補齊 Overlay／Protocol 測試 assembly 的直接引用 |
| Unity EditMode | **186/186 PASS，0 skip** | 比基準淨增 5；必要玩法／lifecycle／Overlay 覆蓋保留 |
| Unity PlayMode | **5/5 PASS，0 skip** | 獨立 physics scene、presentation、死亡／重生、replay／return live |
| 正式 Demo | **2,203 tick 完整回放 PASS** | 實際 SaveRecording、載入 tick 0、單步／restart、完整回放、返回同 tick live；2 active views，adapter failure 為 null |
| Player build／啟動 | **Succeeded，0 errors；約 31 秒背景啟動 PASS** | StandaloneWindows64／Development；唯一 warning 是未啟用 CLI Player 控制端；測試啟動的 Player 已關閉 |
| 資產／依賴 | 40 asmdefs、293 GUIDs、176 active source／project／assembly files PASS | 無退役 API／Old_Simulation 引用、無循環或禁止方向；正式 scene Missing Script=0 |

機器可讀結果、各 NUnit case 與 CLI 差異：[退役驗證紀錄](verification/legacy-retirement-2026-08-30.json)。Player 啟動使用 batchmode／nographics，只證明啟動與例外檢查；互動相關流程另由 Editor smoke 與 PlayMode 驗證。

## 現在已退役與仍暫緩的部分

主線不再依賴 `Old_Simulation`，讀者可以只看五階段教材與現行 Demo。封存資料是考古／還原用途，不參與 import、compile 或 build。

`GameplaySession`、舊 ReplayArtifact／FailureArtifact、reader／writer、舊 hash 與控制 ports 已從主線刪除。現代 Demo、教學、CLI、game Protocol adapter 與測試不再依賴它們；loaded game assembly 也檢查不到舊 session／artifact／hasher／playback 型別。

原始 failure sample 不變，現代 sample 另存；舊工具與來源由基準 commit 追溯，不在主線永久維護 converter。transport／authentication／Unity pump／reconnect、Explorer、dynamic-body physics／snapshot restore 仍依使用需求另排，不以本次 PASS 宣稱支援。

基準版本的完整 NUnit 結果與 build 摘要另保留於 [framework 穩定化紀錄](verification/framework-stabilization-2026-08-30.json)。目前 Windows 輸出保存在本機 .utmp/Player，不提交 build 產物。

Unity 首次 build 自動更新了 URP 的序列化欄位／shader prefilter cache 與 Player 的 input preload／batching 設定，保留 Editor 產生的有效設定；測試生成的 PerformanceTestRun Resources 已移除，Unity Connect 恢復原本 disabled 狀態。原有 Assembly-CSharp 只含未使用的 IsExternalInit compiler shim，沒有舊遊戲程式。

Editor 保留在正式 Demo、非 Play、場景未修改狀態；生成的 PerformanceTestRun Resources 已透過 AssetDatabase 清除。Unity 生成的 metadata 空欄位可能保留尾端空白，來源與文件另做 diff whitespace 檢查。
