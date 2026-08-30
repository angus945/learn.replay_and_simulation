# Character Movement Demo

開啟 `scenes/CharacterMovementDemo.unity`，按 Play，點擊 Game View，以 WASD／方向鍵移動，空白鍵攻擊附近紅色敵人。
敵人預設 30 HP，攻擊固定 10 傷害、距離 2；死亡後不再顯示。重新 Play 可重建示範。
Editor／Development Build 自動顯示唯讀 Diagnostics Overlay，F3 切換；顯示時隱藏舊 HUD。
面板包含 Session／tick、角色 Observation、快取 invariant 結果與增量 trace，不提供任何管理操作。
預設 60 ticks/s、4 units/s，可在 Movement Composition Root Inspector 調整（Play 前）。
HUD 顯示權威位置、tick 與插值 alpha。失去焦點或沒有鍵盤時送零方向。

## 執行路徑

Unity Keyboard → TickInputBuffer → GameplayRequest → GameplaySession.Submit
→ IIntent → Internal Command → Movement／Combat → Domain Event → StructuralCommit
→ Observation → Render 插值 → Unity Transform。

`src/Composition` 組裝純 C# session；`src/Unity` 只處理 Unity 輸入與畫面；`tests` 測試整條流程。
Domain、Application、Integration、Composition 各有 noEngineReferences assembly 邊界。
GameplaySession 透過 registry 在初始化配發 player=1／enemy=2，死亡時在 StructuralCommit 移除。
MovementDemoSession 的無敵人模式仍保留供原本 Movement 測試使用；Unity host 開啟敵人。
seeded-random 不參與移動，此切片不需要隨機數。

## 輸入與時間限制

- 每個畫面 frame 擷取當下鍵盤狀態；每 tick 使用最後擷取的軸值，補跑 tick 沿用該值。
- 相同初始狀態、tick delta 與逐 tick 輸入可產生相同結果；不保證不同 frame 排程將真實鍵盤事件分配到同一 tick。
- frame 之間按下又放開的極短移動輸入可能不被擷取；此 adapter 尚非具時間戳的輸入事件記錄器。
- simulation 使用 float 與現有 runner；尚不保證跨平台浮點一致性。已有 in-process scenario 重跑，沒有通用 Replay／rollback。
- 不使用 Rigidbody、Physics step 或 FixedUpdate；後續物理整合需另外設計。

場景產生器位於 `src/Unity/Editor`，透過 Unity Editor API 建立素材與場景。
Tools → Movement Demo → Create Demo Scene 僅在場景不存在且所有場景已儲存時建立，不覆寫既有場景。

## 驗證

原 12 項 Movement EditMode 測試保留；控制面、戰鬥與 diagnostics 測試位於 game/gameplay-simulation/tests。
測試結果與限制見專案根目錄 docs/testability/phase1-3.md。
