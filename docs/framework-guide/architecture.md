# 架構、DDD 邊界與組裝

[回到索引](README.md)

## 責任與依賴

| 層                       | 放什麼                                      | 不放什麼                                    |
| ------------------------ | ------------------------------------------- | ------------------------------------------- |
| Module                   | 小型泛用機制：buffer、RNG、registry、checks | 玩家、武器、重生政策                        |
| Framework                | phase、訊息派送、診斷或協定流程             | 本專案 domain 型別與玩法                    |
| Domain                   | Value Object、Aggregate、不變條件           | MonoBehaviour、dispatcher、simulation phase |
| Application              | 使用案例、repository port、跨物件協調       | 鍵盤或 Transform 操作                       |
| Integration／Composition | domain 與 framework 的映射、註冊與生命週期  | 把所有規則搬進通用 framework                |
| Unity adapter            | 輸入來源、時間來源、畫面呈現                | 權威 HP／位置的第二份實作                   |

典型編譯依賴由外往內：Unity → 專案組裝 → Application／Domain 與 framework → modules。
純 Domain 不應反向依賴 Unity 或 simulation framework。跨 bounded context 透過明確協調／資料契約合作，避免彼此存取內部 repository。

## DDD 不是資料夾模板

- 一個 module 不等於 bounded context；bounded context 由語言與模型邊界決定。
- 一個 assembly 也不一定等於 bounded context。asmdef 限制編譯依賴，資料夾協助閱讀。
- Aggregate 封裝一致性，不是任意資料集合，也不必實作 IPrePhysicsParticipant。
- 不為每個欄位建立 repository；只有需要管理／查找 aggregate 的使用案例才引入。
- 不為每個方法增加 command/event。只有需要派送、排序或跨邊界反應時才建立訊息。
- 小切片可以先合併 application／integration 組裝類別，但應保留責任可辨識性。

例如目前 Combatant 管理 HP；攻擊距離需要 Movement 位置，因此由 [GameplayActions](../../Assets/game/gameplay-simulation/src/Runtime/GameplayActions.cs)協調。它驗證 actor／target、距離與死亡狀態，呼叫 MovementApplication／Domain，再回傳 GameplayOutcome；不操作 tick driver、trace 或 dispatcher。
Combatant 不引用 IDomainEvent；[GameplayWorld.Execute](../../Assets/game/gameplay-simulation/src/Runtime/GameplayWorld.cs)把 outcome 映射為 InputOutcome 與 ActorDamaged／ActorDied，保留輸入 sequence 的因果關係。
未來領域可有自己的事件資料，再由 adapter 映射，不必讓 domain 為框架服務。

目前 GameplayActions 放在 game 的 simulation assembly，角色是專案 application 協調類別；不需要為了資料夾對稱另建 assembly 或每類一個 interface。GameplayWorld 組合多個 aggregate 與服務，不因名稱為 World 就成為 DDD Aggregate。

## 從外往內的組裝

1. [MovementDemoHost](../../Assets/game/movement-demo/src/Unity/MovementDemoHost.cs)建立 Unity view／pool 與輸入入口，依已提交 observation 呈現 active IDs，不建立另一份權威 domain。
2. [MovementDemoSession](../../Assets/game/movement-demo/src/Composition/MovementDemoSession.cs)建立輸入 buffer，呼叫 GameplayDefinition.CreateTestSession，再取得唯一 RealtimeSimulationRunner。frame accumulator 在 framework runner 內；此 adapter 負責取樣與 tick 後的呈現 callback。
3. [GameplayDefinition](../../Assets/game/gameplay-simulation/src/Runtime/GameplayDefinition.cs)提供 scenario／input codec、world factory、canonical state、invariant factories 與 metadata。它建立 GameplayWorld；world 組合 aggregates、repository、registry、RNG、GameplayActions，並向 builder 註冊 PrePhysics／StructuralCommit 與死亡 reaction。

[TestableSimulationSession](../../Assets/framework.testability/src/API/TestableSimulationSession.cs)負責 admission、輸入排序、逐 tick 結果、hash／invariant、trace／recording；底層 SimulationSession 負責 world／pipeline 生命週期。玩法判斷保留在 game，框架不認識玩家或敵人。

Manual 測試直接使用 GameplayDefinition.CreateTestSession；TemplateReplay 也用相同 definition 建立自己的乾淨世界。它們共用實作，但不共用可變 world。
目前是 constructor injection 與明確 new／Register，不需要 DI container 或全域 singleton。
擴大專案時可把註冊拆成 composition helper，但 helper 不應在不明確的時機偷偷改寫 domain。

## 相容 facade 的界線

[GameplaySession](../../Assets/game/gameplay-simulation/src/Runtime/GameplaySession.cs)現在只把舊 request／ports／artifact 接到 GameplayDefinition → TestableSimulationSession，並投影既有 hash 格式。它沒有自己的 actor collection、RNG、移動／攻擊規則或另一條 pipeline。

玩法改動應進入 GameplayActions／GameplayWorld，接線與序列化改動進入 GameplayDefinition，再驗證現行與相容入口。Protocol **Deferred**；保留 facade 是為了既有 consumer，不要求先遷移 Protocol 才能完成新玩法或教學，也不在 facade 補寫第二份規則。

## 新專案建議骨架（示意，不是已存在的檔案）

```text
Assets/game/<bounded-context>/src/Domain/
Assets/game/<bounded-context>/src/Application/API/
Assets/game/<bounded-context>/src/Application/Runtime/
Assets/game/<bounded-context>/src/Integration/
Assets/game/<bounded-context>/tests/
Assets/game/<game>-simulation/src/API/
Assets/game/<game>-simulation/src/Contract/
Assets/game/<game>-simulation/src/Runtime/
Assets/game/<game>-simulation/tests/
Assets/game/<game>-host/src/Unity/
```

API 描述可呼叫的能力；Contract 描述訊息、資料語義與保證；Runtime 是實作。
不是所有 interface 都放 API：IIntent 是訊息分類契約。也不要求每個資料夾都要單獨 asmdef。

## 什麼時候抽成 module？

先問：移除玩家、敵人、HP 等遊戲語言後，責任是否仍完整？有沒有第二個實際 consumer？是否能獨立測試？
RNG、trace buffer 符合；GameplayObservation、敵人重生政策、目前的 Overlay 不符合。
不要為了未來可能重用而先做大型抽象層。
