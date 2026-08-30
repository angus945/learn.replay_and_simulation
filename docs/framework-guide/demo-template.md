# Demo 模板整合

MovementDemoHost → MovementDemoSession → GameplayDefinition.CreateTestSession → TestableSimulationSession → SimulationSession → GameplayWorld。

## 分工

- `MovementDemoSession` 只負責 Unity-independent 的輸入取樣、固定 tick 累積與畫面插值，私有持有模板 session，不公開第二個 tick driver。
- `GameplayDefinition` 示範繼承模板：scenario/input codec、world 建立與 phase 組裝、不可變 Observation、canonical bytes、invariant、InputOutcome。
- `GameplayWorld` 擁有 movement/combat aggregate、registry、RNG、重生排程。Domain aggregate 沒有繼承 framework。
- `GameplayInput` 只有玩法 payload；session ID、sequence、target tick 由模板 envelope 處理。模板將 input 轉為 Intent → Internal Command；死亡 Domain Event 再觸發 SpawnEnemy command，出生／移除只在 StructuralCommit 完成。
- `TemplateReplay` 以相同 definition 建立獨立世界，驗證逐 tick hash、action results、failure；前一個 Observation 與 alpha 只供顯示插值。

Overlay 仍只依賴 `IDiagnosticReader<GameplayObservation>`。切換 replay 時重新 Bind，Restart 也重新取得 reader；回 Live 繼續原世界，不把重播狀態寫回。切換前清掉未消耗的按鍵／攻擊，Host 銷毀時 Dispose 兩個 session。

## 使用

Play Mode 中移動、Space 攻擊，按 Save recording 保存，Load path 載入後可 Play／Pause／Step／Restart／Return live。敵人血量仍是 20～40，重生延遲 1～3 秒。

Demo 新存檔為 `TemplateRecording`，不是舊 `ReplayArtifact`；舊 JSON 不自動轉換，需重新錄製。舊 `GameplaySession`／`ReplayPlayback` 暫留作 Protocol 與舊格式相容路徑，不是 Demo 的執行核心。兩條路徑的玩法一致性由 `DemoTemplateChecks` 比對，未來規則更新需同步或再遷移 Protocol。

## 驗證入口

`dotnet run --project tools/gameplay-checks/Gameplay.Checks.csproj` 與 Unity EditMode 的 `DemoUsesTemplateWithGameplayParityAndReplay`：160 ticks 新舊移動／攻擊／RNG／重生一致、JSON round trip、30/60/144 FPS 與長 frame 重播、單步與 Restart、Live 不被重播推進、切換時 pending attack 清理。

2026-08-30 驗證：純 .NET 通過、Unity 編譯無錯誤、EditMode 159/159。現有 Demo 場景 Play Mode 冒煙測試完成移動、擊殺與重生、1032 ticks 錄製存檔／載入、Step／Restart／播放至 Completed、Return live；未產生 replay difference。完成後恢復 Edit Mode，沒有修改場景資產。
