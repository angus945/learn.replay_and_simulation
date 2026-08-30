# Framework 穩定化與教學實作進度

更新：2026-08-30。[原始重構評估](assessments/determinism-testability-legacy-retirement-2026-08-30.md)保留為歷史；本頁記錄實際交付。行為與刻意變更詳見 [驗收矩陣](framework-guide/acceptance-matrix.md)。

## 本輪範圍

- **Protocol：Deferred**。保留程式及既有 regression；不新增 transport、不把 Protocol 遷移當本輪完成條件。
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
- [x] `GameplaySession` 改為同一 TestableSimulationSession 的相容 facade；移除重複 actor、queue、runner、RNG 與玩法實作。
- [x] 預算以 scenario 為預設來源；default Reset 跟新 scenario 更新，explicit override 保留。候選 Reset 失敗時原世界 / limits / pending input 不變。
- [x] 非零與斜向移動、Move＋Attack 時序、range、死亡拒絕、延遲重生、spawn budget 與 RNG stream 都有驗收。
- [x] player 與 view 依 ID / PlayerId / FindActor 接線，不依陣列位置。

## 階段 3：testability / diagnostics / replay

- [x] 新增 InputExecutionContext、project metadata、結果分頁；domain 不依賴 trace framework。
- [x] 自訂 invariant factory 每 session 獨立，明確 policy identity；policy 不符在 tick 0 拒絕。
- [x] action → damage/death 的 sequence / actor / target 可追蹤；phase / hash / invariant / lifecycle notices 有一致 trace 欄位。
- [x] 正常 recording 與非 crash invariant failure 都能 JSON → replay；不同 frame schedules 比對同逐 tick results / hash / failure。
- [x] CLI `capture` / `capture-success` / `rerun` 走現行 TemplateRecording，`legacy-rerun` 明確讀舊格式；CreateNew 不覆寫，檔案與執行預算有上限。
- [x] 舊 artifact projection / sample regression 保留；兩種格式不混讀。Tick 例外後 observation 是最近已捕捉 snapshot，附 ObservationTick，沒有 partial-world restore 承諾。

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
- [x] 更新主要 architecture / recipes / contracts / template / Demo 指南，移除「兩套 runtime 同步」的過渡指示。

## 當次驗證證據

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

## 現在可退役與仍保留的部分

主線不再依賴 `Old_Simulation`，讀者可以只看五階段教材與現行 Demo。封存資料是考古／還原用途，不參與 import、compile 或 build。

`GameplaySession`、舊 ReplayArtifact / FailureArtifact 及其 reader 仍保留給既有 consumer。它們已共用現行 runtime；刪除這些型別要等 Protocol consumer 遷移，以及明確決定舊檔支援期限。這是剩餘相容工作，不是再補一個主框架。

Protocol adapter 遷移、transport / authentication / Unity pump / reconnect、Explorer、dynamic-body physics / snapshot restore 等，依使用需求另排，不以目前 PASS 宣称支援。

完整 NUnit 結果與 build 摘要：[機器可讀驗證紀錄](verification/framework-stabilization-2026-08-30.json)。Windows 輸出保存在本機 .utmp/Player，不提交 build 產物。背景啟動只驗證啟動／例外，不宣稱代替互動 Player 測試；畫面與錄製操作另在 Editor smoke 及 PlayMode 驗證。

Unity 首次 build 自動更新了 URP 的序列化欄位／shader prefilter cache 與 Player 的 input preload／batching 設定，保留 Editor 產生的有效設定；測試生成的 PerformanceTestRun Resources 已移除，Unity Connect 恢復原本 disabled 狀態。原有 Assembly-CSharp 只含未使用的 IsExternalInit compiler shim，沒有舊遊戲程式。

收尾檢查：203 個本輪非歷史文件本地連結全部有效；git diff --check 通過。Editor 保留在正式 Demo、非 Play、場景未修改狀態。
