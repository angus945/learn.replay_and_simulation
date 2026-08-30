# Demo 模板整合

[回到索引](README.md) · [同一模型的五章接線](learning-path.md)

MovementDemoHost → MovementDemoSession → GameplayDefinition.CreateTestSession → TestableSimulationSession → SimulationSession → GameplayWorld → GameplayActions／Domain。

## 分工

- `MovementDemoSession` 負責 Unity-independent 的輸入取樣與畫面插值；固定 tick 累積已委派給 framework 的 [RealtimeSimulationRunner](realtime-runner.md)，私有持有模板 session，不公開第二個 tick driver。
- `GameplayDefinition` 示範繼承模板：scenario/input codec、world 建立與 phase 組裝、不可變 Observation、canonical bytes、invariant、InputOutcome。
- [GameplayActions](../../Assets/game/gameplay-simulation/src/Runtime/GameplayActions.cs)處理移動與攻擊決策、actor／target 活性和距離檢查，呼叫既有 MovementApplication／Combatant，回傳 outcome，不管理 clock／trace。
- [GameplayWorld](../../Assets/game/gameplay-simulation/src/Runtime/GameplayWorld.cs)組合 movement/combat aggregates、registry、RNG、重生排程，把 outcome 映射為事件與 InputOutcome，並註冊 phase／reaction。Domain aggregate 沒有繼承 framework。
- `GameplayInput` 只有玩法 payload；session ID、sequence、target tick 由模板 envelope 處理。模板將 input 轉為 Intent → Internal Command；死亡 Domain Event 再觸發 SpawnEnemy command，出生／移除只在 StructuralCommit 完成。
- `TemplateReplay` 以相同 definition 建立獨立世界，驗證逐 tick hash、action results、failure；前一個 Observation 與 alpha 只供顯示插值。

Overlay 仍只依賴 `IDiagnosticReader<GameplayObservation>`。切換 replay 時重新 Bind，Restart 也重新取得 reader；回 Live 繼續原世界，不把重播狀態寫回。切換前清掉未消耗的按鍵／攻擊，Host 銷毀時 Dispose 兩個 session。

呈現由 [GameplayActorPresentation](../../Assets/game/movement-demo/src/Unity/GameplayActorPresentation.cs)將 active actor IDs 映射到共用 UnityActorPresentation／instance pool。玩家以 PlayerId 查找，原 character transform 只作 camera anchor；回 Live 時 Snap 至 live observation，避免沿用 replay 的插值歷史。Demo 不接 dynamic-body physics；選配的隔離 sensor adapter 契約見 [Unity framework](../../Assets/framework.deterministic-simulation.unity/README.md)。

## 使用

開啟 [CharacterMovementDemo](../../Assets/game/movement-demo/scenes/CharacterMovementDemo.unity)，Play Mode 中移動、Space 攻擊，按 Save recording 保存，Load path 載入後可 Play／Pause／Step／Restart／Return live。敵人血量 20～40，重生延遲 1～3 秒。Build Settings 已改用此 Demo；舊 SampleScene／Player／Coin／Enemy 資產封存於 [Old_Simulation/LegacyUnityAssets](../../Old_Simulation/LegacyUnityAssets/README.md)，不由 Assets 編譯或載入。

Demo 只保存及讀取 `TemplateRecording`，正常與失敗錄製共用此格式。舊 ReplayArtifact／FailureArtifact reader、GameplaySession 與 ReplayPlayback 已退役；舊 JSON 不自動轉換，請保留原檔並依 [退休政策](../legacy-compatibility-retirement.md)使用歷史基準工具，或以現行 Demo 重新錄製。

玩法只修改 GameplayActions／GameplayWorld，再驗證 manual／Demo 的相同行為、現行 hash 與 PolicyId。Protocol adapter 已使用相同 ports；game payload v2、envelope v1，transport 仍 Deferred。Demo 不經過 Protocol，不因這次遷移增加第二個 session 或輸入來源。

## 驗證入口

在根目錄執行：

```powershell
dotnet run --project tools/gameplay-lessons -- all
dotnet run --project tools/gameplay-checks/Gameplay.Checks.csproj
```

- [Stage05](../../tools/gameplay-lessons/Stage05Replay.cs)：三筆外部輸入涵蓋非零移動、攻擊／死亡、seeded 血量及延遲重生；JSON round trip、不同 frame 排程、首次分歧與 invariant failure 重現。
- [DemoTemplateChecks](../../Assets/game/gameplay-simulation/tests/DemoTemplateChecks.cs)：manual template session／Demo 比較，包含攻擊與多次 RNG／重生；非零位移另由 ModernGameplayContractChecks 驗證。
- [ModernGameplayContractChecks](../../Assets/game/gameplay-simulation/tests/ModernGameplayContractChecks.cs)：非零／斜向移動與 stop、frame 分割、事件因果、policy／invariant 隔離及預算。
- [GameplayPresentationTests](../../Assets/game/movement-demo/tests/PlayMode/GameplayPresentationTests.cs)：Unity actor binding、死亡／重生與呈現。純 .NET 成功不能取代這批 Unity 驗證。

當次測試結果與尚未驗收項目集中於 [實作進度](../implementation-progress.md)。本頁描述接線及驗證入口，不把先前較小測試集合的通過數當作本輪最終證據。
