# Character Movement Demo

開啟 `scenes/CharacterMovementDemo.unity`，Play 後點 Game View，以 WASD／方向鍵移動、Space 攻擊附近紅色敵人。它也是正式 Build Settings 的啟動場景。

敵人出生血量為 seeded RNG 的 20–40，攻擊傷害 10、距離 2；死亡移除後經 1–3 simulation 秒重生。兩個 RNG stream 分開管理。預設 60 ticks/s、4 units/s，可在 Play 前調整 Composition Root。

Editor / Development Build 提供 F3 唯讀 diagnostics，包含 observation、快取 invariant、增量 trace。底部控制可保存 recording、載入、播放、暫停、逐步、Restart、Return live。Replay 不推进原 live session；返回 live 明確 snap，不混合兩個 session 的位置。

## 接線

```text
Keyboard → TickInputBuffer → GameplayInput
  → GameplayDefinition / TestableSimulationSession
  → Intent → InternalCommand → GameplayActions → Movement / Combat
  → DomainEvent → GameplayWorld lifecycle → committed GameplayObservation
  → GameplayActorPresentation → UnityActorPresentation / UnityActorPool
  → stable ID 對應的 Unity Transform
```

`src/Composition/MovementDemoSession` 是純 C# frame/input adapter。每個完成 tick 將 immutable observation 交給 presentation callback；catch-up 也逐 tick 捕捉。`src/Unity/GameplayActorPresentation` 將 PlayerId 與 active actor 映射到 view archetype；framework pool 管理 instance generation、重用與清理，不保管生命值或攻擊規則。

本場景以兩個 sprite template 註冊 player/enemy view，inactive 原始 template 不參與遊戲；相同 adapter 可接受 prefab。原 character transform 只作相機跟隨 anchor。所有遊戲判定用 ID，不依 Actors 陣列索引。多 actor、出生 snap、死亡移除、pool reuse 與 replay/live 切換有獨立 integration tests。

## 物理與時間邊界

此 Demo 的權威是純 C# 邏輯位置，沒有 Rigidbody movement / FixedUpdate。可選的 [Unity framework integration](../../framework.deterministic-simulation.unity/README.md) 提供隔離 local PhysicsScene 的 kinematic/static sensors 與排序去重 facts，另有 PlayMode 範例測試；此遊戲未憑空加入碰撞傷害規則。

相同 scenario、tick delta 與逐 tick input 可重現；不同 frame 排程未必會把真實鍵盤事件分配到同一 tick。每 frame 取最後軸值、catch-up 沿用，極短移動輸入可能未被擷取。Attack 的按下邊緣只消耗一次。失焦／無鍵盤時送零方向。

Recording / replay 已存在；snapshot restore、rollback 與跨平台 bitwise 浮點一致性不在本輪承諾內。

## 教學與驗證

從 [五階段累積教學](../../../docs/framework-guide/learning-path.md) 接到此場景。場景產生器 `src/Unity/Editor/MovementDemoSceneBuilder` 僅在不存在且已儲存場景時建立，不覆寫使用者場景。

- `tests/`：input、frame schedule、playback / multi actor wiring。
- `gameplay-simulation/tests/`：共用 gameplay、invariants、limits、recording / replay。
- `framework.deterministic-simulation.unity/tests/`：pool / presentation / local physics。

當次執行證據見 [實作進度](../../../docs/implementation-progress.md)。舊 SampleScene / prefab 已保存在 Assets 外的 `Old_Simulation/LegacyUnityAssets`，不再是正式匯入或 build 依賴。
