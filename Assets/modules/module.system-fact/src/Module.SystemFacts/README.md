# module.system-facts

`module.system-facts` 定義跨模組共用的「系統事實（System Fact）」契約。

System Fact 表示：

> 系統中已經發生的事實。

例如：

```text
DamageApplied
QuestCompleted
SaveLoaded
CommandCompleted
TickCompleted
```

本 Module 只定義事實的分類，不負責發布、傳遞或處理。

## Public API

```csharp
namespace Module.SystemFacts
{
    public interface ISystemFact
    {
    }

    public interface IDomainFact : ISystemFact
    {
    }

    public interface IApplicationFact : ISystemFact
    {
    }

    public interface IExecutionFact : ISystemFact
    {
    }
}
```

### `IDomainFact`

描述 Domain 中已經發生的事情。

```text
DamageApplied
CharacterKilled
QuestCompleted
```

### `IApplicationFact`

描述 Application Use Case 或協調流程產生的事情。

```text
SaveImported
DefinitionReloaded
GameSessionStarted
```

### `IExecutionFact`

描述 Runtime 或執行機制發生的事情。

```text
CommandStarted
CommandCompleted
CommandFailed
TickStarted
TickCompleted
```

## Fact 的擁有權

具體 Fact 應由真正擁有該語意的 Module 定義。

```text
module.combat
└── DamageApplied : IDomainFact

module.quest
└── QuestCompleted : IDomainFact

module.fixed-tick
└── TickCompleted : IExecutionFact
```

`module.system-facts` 不集中保存所有 Fact。

## 設計規則

Fact 應描述「已經發生」的事情：

```text
DamageApplied     ✓
QuestCompleted    ✓

ApplyDamage       ✗
CompleteQuest     ✗
```

Fact 建立後應保持不可變。

Fact 本身不包含 Observation Metadata，例如：

```text
Timestamp
Tick
Sequence
TraceId
CorrelationId
```

這些應由外部 Observability 系統附加。

## 不負責

本 Module 不提供：

```text
Publisher
Subscriber
Observer
Dispatcher
Event Bus
Logging
Metrics
Tracing
Replay
Serialization
Unity Integration
```

這些能力應由其他 Module 或 Framework 提供。

## 核心原則

`module.system-facts` 只回答：

> **這是一個什麼類型的、已經發生的系統事實？**

它不處理：

> 誰要收到？如何傳遞？如何記錄？如何觀察？
