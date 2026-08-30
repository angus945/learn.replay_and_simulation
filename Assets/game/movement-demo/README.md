# Character Movement Demo

開啟 `scenes/CharacterMovementDemo.unity`，按 Play，點擊 Game View，以 WASD／方向鍵移動。
預設 60 ticks/s、4 units/s，可在 Movement Composition Root Inspector 調整（Play 前）。
HUD 顯示權威位置、tick 與插值 alpha。失去焦點或沒有鍵盤時送零方向。

## 執行路徑

Unity Keyboard → TickInputBuffer → PlayerMoveIntent → MovementApplication → Character aggregate
→ PrePhysics 位移 → CaptureTickState → Render 插值 → Unity Transform。

`src/Composition` 組裝純 C# session；`src/Unity` 只處理 Unity 輸入與畫面；`tests` 測試整條流程。
Domain、Application、Integration、Composition 各有 noEngineReferences assembly 邊界。
單一角色的 CharacterId 在 composition 指定為 1；尚未接全域 registry 的生命週期。
seeded-random 不參與移動，此切片不需要隨機數。

## 輸入與時間限制

- 每個畫面 frame 擷取當下鍵盤狀態；每 tick 使用最後擷取的軸值，補跑 tick 沿用該值。
- 相同初始狀態、tick delta 與逐 tick 輸入可產生相同結果；不保證不同 frame 排程將真實鍵盤事件分配到同一 tick。
- frame 之間按下又放開的極短移動輸入可能不被擷取；此 adapter 尚非具時間戳的輸入事件記錄器。
- simulation 使用 float 與現有 runner；尚不保證跨平台浮點一致性，也沒有 Replay／rollback。
- 不使用 Rigidbody、Physics step 或 FixedUpdate；後續物理整合需另外設計。

場景產生器位於 `src/Unity/Editor`，透過 Unity Editor API 建立素材與場景。
Tools → Movement Demo → Create Demo Scene 僅在場景不存在且所有場景已儲存時建立，不覆寫既有場景。

## 驗證

新增 12 項 EditMode 測試：方向、數值限制、停止、repository、未知 ID、插值、tick 輸入、補跑、render 排程、鍵盤與失焦。
連同既有 module/framework 測試共 41 項通過。
