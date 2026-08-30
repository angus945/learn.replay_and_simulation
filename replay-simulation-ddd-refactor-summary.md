# Replay／Simulation 從 ECS-like 架構轉向 DDD：摘要與重構基準

> 專案：`angus945/learn.replay_and_simulation`  
> 文件定位：整理目前架構判斷、目標邊界與建議遷移順序。  
> 核心目標：保留 Fixed Tick、Replay 與確定性基礎設施，將遊戲規則從通用 ECS World 改由各 Bounded Context 的 Domain Model 表達。

## 1. 核心結論

不應把整個專案推翻重寫，也不應讓各 BC 各自管理 Unity Instance 與全域物件生命週期。

建議採用：

> 中央 Simulation Runtime 管理時間、Phase、全域身份、穩定順序與結構變更；各 BC 管理 Aggregate、業務規則與自身 Repository；Unity Infrastructure 管理 GameObject、Physics、Pool 與呈現。

目前 ECS-like 架構同時解決了兩類不同問題：

1. **確定性基礎設施問題**：固定順序、Slot 重用、Spawn／Destroy、Instance 對應。
2. **業務建模問題**：角色移動、戰鬥、狀態、任務等規則。

轉向 DDD 時應保留第一類能力，只替換第二類建模方式。

## 2. 現有架構評估

### 2.1 應保留

| 現有能力 | 決策 | 原因 |
| --- | --- | --- |
| `SimulationRunner` | 保留並泛化 | Fixed Tick 與 Phase 排程不屬於 ECS |
| External Command Acquire | 改為 Intent Acquire | 玩家、AI、Replay、Debug 都是外部意圖來源 |
| `CommandHandlerRegistry` | 保留 | 已具備型別路由與固定註冊能力 |
| `CommandBuffer`／Wave Dispatch | 保留並修正 | 適合確定性連鎖處理 |
| Physics Adapter | 保留 | Physics 是外部技術能力 |
| Simulation Actor／Pool | 保留 | Unity Instance 不應進入 Domain |
| Presentation Interpolation | 保留 | 屬於呈現與引擎整合 |
| Entity 的 Slot／Sequence 思路 | 保留並重新命名 | 可防止 stale handle 並維持穩定順序 |

### 2.2 應替換或降級

| 現有結構 | 建議 |
| --- | --- |
| `EcsWorld` | 不再作為整個遊戲的 Domain World |
| `IComponent`／`ComponentStores` | 從主要 Domain 移除；必要時保留為獨立技術模組 |
| `EntityFilter` | 改由 BC Repository 提供有語義的查詢 |
| `IEntityRecipe` | 改為 Aggregate Factory／Application Coordinator |
| 通用 `ISystem` | 拆成 Intent Handler、Application Service 與 Simulation Participant |
| `PlayerTag + Components = Player` | 改成具身份與行為的 Aggregate |

現有 `World/Domain` 實際上是 ECS 儲存與查詢機制，不是 DDD 意義上的 Domain Model。資料夾名為 `Domain` 不代表其中已有領域模型。

## 3. 建議的整體責任邊界

```text
Simulation Core
├── Fixed Tick／Phase Pipeline
├── Game Intent Scheduler／Router
├── Internal Command Buffer
├── Event Dispatch
├── Global Simulation Object Identity
├── Stable Ordering
├── Structural Change Commit
└── Snapshot／Hash Coordination

各 Bounded Context
├── Aggregate／Entity／Value Object
├── Domain Rule
├── Deterministic Repository
├── Intent Integration Handler
├── Domain／Application Event
└── Simulation Phase Participant

Unity Infrastructure
├── GameObject／Prefab
├── Instance Pool
├── Rigidbody／Physics Adapter
├── Actor Binding
└── Presentation Adapter
```

Simulation Core 不理解攻擊、移動、道具或任務規則；BC 不理解 GameObject、Prefab、Rigidbody 或 Pool Slot。

## 4. Game Intent、Internal Command 與 Event

三種訊息必須明確分離：

| 類型 | 語義 | 來源 | Replay 是否記錄 |
| --- | --- | --- | ---: |
| Game Intent | 外界希望遊戲嘗試某項行為 | 玩家、AI、Replay、Debug、測試工具 | 是 |
| Internal Command | 模擬已決定後續應執行的操作 | Handler、Application Coordinator、System | 通常否 |
| Domain／Application Event | 某件事情已經發生 | Domain／Application | 通常否 |

典型流程：

```text
PlayerAttackIntent
→ AttackIntentHandler
→ Combat Aggregate.TryAttack()
→ AttackSucceeded
→ SpawnProjectileCommand
→ Projectile Runtime
→ ProjectileHit
→ ApplyDamage
→ ActorDied
```

只有最上層外部 Intent 應成為 Replay Input。內部 Command 與 Event 應由重播時重新推導，否則容易重複生成 Projectile、Damage 或 Death。

### 4.1 Handler Framework

Simulation Core 提供：

```csharp
public interface IIntentHandler<in TIntent>
    where TIntent : IGameIntent
{
    IntentExecutionResult Handle(
        TIntent intent,
        SimulationContext context);
}
```

各 BC 的 Integration Adapter 在 Composition Root 註冊 Handler。Intent 通常只有一個主要 Handler；Event 才允許多個訂閱者。

```text
GameIntent
→ Simulation Scheduler
→ Intent Router
→ BC Integration Handler
→ BC Application／Domain
```

Handler 不應放入 BC Domain，因為它依賴 Simulation Contract。應位於 BC 的 Integration／Adapter 模組。

### 4.2 兩階段驗證

- **Admission Validation**：Schema、來源權限、Target Tick、重複 ID、Queue 限制；由 Gateway／Scheduler 負責。
- **Execution Validation**：角色是否存活、冷卻、距離、資源與當下狀態；由對應 BC 在執行 Tick 負責。

不要依賴 `Validate()` 後再分開 `Enqueue()`，因為兩次呼叫之間狀態可能變化。對外應提供原子的 `Submit()`。

## 5. Simulation Core 與 BC 的 Tick 整合

不要再以單一 `ISimulationWorld` 容納所有 BC。Simulation Core 應提供按 Phase 註冊的 Participant：

```csharp
public interface IPrePhysicsParticipant
{
    void Tick(SimulationTick tick, FixedDeltaTime delta);
}

public interface IPostPhysicsParticipant
{
    void Tick(SimulationTick tick, FixedDeltaTime delta);
}

public interface IStructuralCommitParticipant
{
    void Commit(SimulationTick tick);
}
```

Composition Root 明確指定順序，初始化後鎖定：

```csharp
pipeline.AddPrePhysics(
    SimulationOrder.CharacterMovement,
    movementParticipant);

pipeline.Seal();
```

不能單純依賴 `Dictionary`、反射掃描、Unity Hierarchy 或非明確的註冊順序。

## 6. 確定性 Identity、Ordering 與 Instance

### 6.1 三種身份

| 身份 | 所屬 | 用途 |
| --- | --- | --- |
| `SimulationObjectId` | Simulation Core | 跨 BC 關聯、全域排序、Snapshot、Hash |
| `CharacterId`／`ProjectileId` | 對應 BC | Aggregate 身份與領域語義 |
| `InstanceHandle` | Unity Infrastructure | Pool Slot、Generation、GameObject 對應 |

範例：

```text
SimulationObjectId 42
├── Movement BC → CharacterId 17
├── Combat BC   → CombatantId 31
└── Unity       → InstanceHandle (8, 3)
```

三個身份可能互相映射，但不應假設數值或 Array Index 永遠相同。

### 6.2 Simulation Core 統一管理的內容

中央 Registry 應管理：

- 全域唯一 Simulation Object ID。
- Spawn Sequence 與穩定排序。
- Alive／Pending Spawn／Pending Destroy 狀態。
- 延後 Structural Commit。
- Snapshot 與 State Hash 的標準遍歷順序。
- BC 狀態與 Unity Instance 的關聯入口。

現有 `Entities` 中值得保留的是：

- Slot 重用。
- Sequence／Generation 防止 stale handle。
- 穩定 Spawn Sequence。
- 延後 Spawn／Destroy。

但應移除其 Component Store 與 Entity Filter 責任。

建議明確拆開：

```csharp
public readonly record struct SimulationObjectId(ulong Value);

public readonly record struct SimulationObjectHandle(
    int Slot,
    uint Generation);
```

- `SimulationObjectId`：持久身份、Replay、Snapshot、Hash、排序。
- `SimulationObjectHandle`：快速存取、Pool 與 stale reference 防護。

不要再讓同一個 `SequenceId` 同時表示物件身份、Slot Generation 與出生順序。

### 6.3 各 BC 自己管理的內容

各 BC 擁有自己的 Repository 與 Aggregate 容器，但必須保證明確順序：

```csharp
public interface ICharacterMovementRepository
{
    CharacterMovement Get(CharacterId id);
    IReadOnlyList<CharacterMovement> GetActiveOrdered();
}
```

Repository 可以使用 Array、Sparse Set、Sorted List 或 Dictionary + Ordered ID List；這是儲存實作選擇，不代表 Domain 必須是 ECS。

確定性不來自「一定要用 ECS」，而來自：

- 明確身份。
- 明確排序鍵。
- 固定的 Add／Remove 時機。
- 固定的 Phase 與 Handler 順序。
- 不依賴未定義的容器遍歷順序。

### 6.4 Unity Instance 不由各 BC 建立

BC 不應直接建立或持有：

- `GameObject`
- `Transform`
- `Rigidbody`
- Prefab
- Pool Index

正確流程：

```text
BC／Application 決定建立遊戲物件
→ Simulation Lifecycle Coordinator
→ Structural Commit
→ Actor Instance Adapter
→ Unity Pool／GameObject／Physics
```

不要讓各 BC 各自做 GameObject Pool。各 BC 可以有自己的 Aggregate Repository，但 Unity Instance 綁定應由共享 Infrastructure 統一管理。

## 7. Player 垂直切片的轉換

目前模型：

```text
PlayerTag
+ ActorArchetypeComponent
+ ActorTransformState
+ PlayerSystem
= Player 行為
```

DDD 目標：

```text
PlayerMoveIntent
→ PlayerMoveIntentHandler
→ CharacterMovement Aggregate
→ MovementPrePhysicsParticipant
→ Physics Port
→ Actor／Presentation Adapter
```

`PlayerSystem` 應拆成：

1. `PlayerMoveIntentHandler`：將外部 Intent 轉入 Movement BC。
2. `MovementPrePhysicsParticipant`：每 Tick 推進 Movement Aggregate。
3. `ICharacterMovementRepository`：保存並提供穩定排序。
4. Physics／Actor Adapter：處理 Unity 狀態同步。

## 8. 建議模組結構

```text
Simulation
├── Contracts
│   ├── SimulationTick
│   ├── SimulationPhase
│   ├── IGameIntent
│   ├── IIntentHandler<T>
│   ├── IInternalCommand
│   └── ISimulationParticipant
├── Runtime
│   ├── SimulationRunner
│   ├── IntentScheduler
│   ├── IntentRouter
│   ├── InternalCommandBuffer
│   ├── EventDispatcher
│   └── SimulationObjectRegistry
└── Replay

CharacterMovement
├── Domain
│   ├── CharacterId
│   ├── CharacterMovement
│   └── MovementState
├── Application
│   ├── ICharacterMovementRepository
│   └── MovementPrePhysicsParticipant
└── Integration
    ├── PlayerMoveIntent
    └── PlayerMoveIntentHandler

UnityRuntime
├── UnityCharacterRepositoryAdapter
├── PhysicsAdapter
├── ActorInstanceAdapter
├── ActorBinding
└── PresentationAdapter
```

第一輪只需要一個 `CharacterMovement` BC。不要先為尚未出現的 Combat、Inventory、Quest 等需求建立空殼 BC。

## 9. 建議遷移順序

### 階段 1：拆分訊息語義

- 新增 `IGameIntent`、`IInternalCommand`、`IEvent`。
- 將 `PlayerMoveCommand` 改成 `PlayerMoveIntent`。
- Replay 只記錄 Game Intent。
- 暫時保留舊 `EcsWorld` 與 `PlayerSystem`。

### 階段 2：建立第一個 Aggregate

- 新增 `CharacterMovement`。
- 新增 `ICharacterMovementRepository`。
- 新增 `PlayerMoveIntentHandler`。
- 移除 `PlayerSystem` 內部保存的最新 Direction。

### 階段 3：以 Participant 取代 System

- 新增 `MovementPrePhysicsParticipant`。
- 用 Aggregate 行為取代直接讀寫 Component。
- 明確定義 Participant 順序並在初始化後 `Seal()`。

### 階段 4：抽離全域身份與生命週期

- 將 `Entities` 縮減為 `SimulationObjectRegistry`。
- 分離 `SimulationObjectId`、`SimulationObjectHandle`、Generation 與 Spawn Sequence。
- 保留延後 Spawn／Destroy 與 Structural Commit。

### 階段 5：移除 Player ECS 表達

- 移除 `PlayerTag`、`SpawnPlayerRecipe` 與 Player 專用 Component Query。
- 使用 Aggregate Factory／Application Coordinator 建立角色。
- 透過 Actor Binding 對應 Unity Instance。

### 階段 6：泛化 Simulation Pipeline

- 以 Phase Participant 集合取代單一 `ISimulationWorld`。
- 保留 Physics、Actor Reconciliation 與 Presentation 的明確 Phase。

### 階段 7：隔離或移除 ECS Runtime

- 若不再需要，刪除 `SimulationCore.World` 的 ECS 部分。
- 若仍要比較兩種模型，改名為可插拔的 `SimulationCore.EcsRuntime`，不要再讓它代表 Simulation Core 的預設 Domain。

## 10. 現有 Dispatch 實作需優先修正

目前 `DispatchAll()` 只以 `commandBuffer.HasPending` 作為迴圈條件，但 Event 使用獨立 `eventBuffer`，可能造成：

- 只有 Event、沒有 Command 時不派發。
- 最後一個 Command 產生 Event 後，Event 留在 Buffer。
- 清理流程只清 Command Buffer。

至少應改成同時檢查兩個 Buffer，並明確定義每個 Wave 的順序，例如：

```text
1. Dispatch Commands
2. Collect Events
3. Dispatch Events
4. Collect Next-wave Internal Commands
5. Repeat until both buffers are empty
```

同時保留最大 Wave 數，避免 Event／Command 循環失控。

## 11. 最終決策摘要

| 問題 | 決策 |
| --- | --- |
| 是否完全刪除 ECS 思路 | 否；保留身份、Slot、Generation、順序與結構變更能力 |
| 是否保留 Component Domain Model | 原則上否；改由 BC Aggregate 表達業務 |
| Simulation 是否統一派發 Intent | 是；中央排程與路由，BC Handler 執行 |
| 連鎖反應是否繼續走 Game Intent | 否；使用直接呼叫、Internal Command 或 Event |
| 各 BC 是否自行建立 Unity Instance | 否；由共享 Unity Infrastructure 管理 |
| 各 BC 是否有自己的容器 | 是；透過 Deterministic Repository 管理 Aggregate |
| 各 BC Array Index 是否需要一致 | 否；使用穩定 ID 映射，不靠 Index 偶然對齊 |
| 全域排序由誰管理 | Simulation Core 管結構順序，BC Repository 管行為遍歷順序 |
| Replay 記錄什麼 | 外部 Game Intent；內部結果重新推導 |

最終架構不是「純 ECS」或「各 BC 完全自治」的二選一，而是：

> DDD 負責業務語義；Simulation Runtime 負責確定性時間與結構；Unity Adapter 負責實體 Instance 與引擎整合。
