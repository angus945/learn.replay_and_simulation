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

例如目前 Combatant 管理 HP；攻擊距離需 Movement 位置，因此由 GameplaySession 協調。
Combatant 不引用 IDomainEvent；協調層在扣血後把事實映射成 ActorDamaged／ActorDied。
未來領域可有自己的事件資料，再由 adapter 映射，不必讓 domain 為框架服務。

## 三層 composition root

1. Unity host 建立 view 與玩家 adapter，不建另一份權威 domain。
2. 玩家 adapter 建立 Realtime session、輸入 buffer 與 accumulator，領取唯一 clock driver。
3. GameplaySession 建立 domain、repository、registry、RNG、pipeline、checks、trace，註冊後 Seal。

Manual 測試與 Replay 不需要前兩層，直接組裝同一個 GameplaySession。
目前是 constructor injection 與明確 new／Register，不需要 DI container 或全域 singleton。
擴大專案時可把註冊拆成 composition helper，但 helper 不應在不明確的時機偷偷改寫 domain。

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
