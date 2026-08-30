# Gameplay Simulation

現行入口是 `GameplayDefinition.CreateTestSession`。同一份 `GameplayWorld` / `GameplayActions` 提供 Demo、headless、錄製與重播的玩法；`GameplaySession` 只保留舊控制面與 schema-1 artifact 轉接，不再擁有第二份 actor、queue、RNG 或 tick runtime。

從 [累積教學](../../../docs/framework-guide/learning-path.md) 開始；實際驗收見 [實作進度](../../../docs/implementation-progress.md)。Protocol 暫緩，保留相容入口不代表仍使用舊遊戲 runtime。

## 最小接線

```csharp
using Testability;
using Testability.Templates;
using GameplaySimulation;

GameplayDefinition definition = new GameplayDefinition();
using (TestableSimulationSession<GameplayWorld, GameplayScenario, GameplayInput, GameplayObservation> session =
    definition.CreateTestSession(new GameplayScenario(tickDelta: .25f)))
{
    ulong player = session.Observe().PlayerId;
    SubmissionResult queued = session.Gameplay.Submit(session.Id, 1, 1,
        new GameplayInput(GameplayActionKind.Move, player, x: 1));
    TemplateTick report = session.Simulation.Step();
    GameplayObservation state = session.Gameplay.Observe();
    TemplateActionLookup result = session.Results.Find(session.Id, 1);
    TemplateRecording recording = session.CaptureRecording();
}
```

只有 tick 改變權威狀態；Submit 是 admission，不是 gameplay 成功。Manual `Step` 與 `CreateRealtimeRunner` 持有的時鐘權限互斥。Domain 不認識 Unity、trace、replay 或測試框架。

## 責任與順序

| 類別 | 責任 |
| --- | --- |
| CharacterMovement / Combatant | 位移、方向、生命值與死亡的領域規則 |
| GameplayActions | Move / Attack 用例、actor/target 驗證與距離判定；不寫 trace、不推進時鐘 |
| GameplayWorld | project composition、registry/repository lifecycle、RNG streams、死亡後重生 |
| GameplayDefinition | 組裝、codec、canonical state、invariants、診斷 metadata |
| TestableSimulationSession | 外部輸入排序、tick、limits、結果、失敗與 recording |
| GameplaySession | 舊 ports / artifact projection；供保留的 consumer 過渡 |

每 tick：外部 input → Intent → InternalCommand → GameplayActions → DomainEvent reactions → PrePhysics 位移 → StructuralCommit → observation / hash / invariant。攻擊使用本 tick 位移前的位置；同 tick 先 Move 只更新方向。

死亡立即禁止後續行為。StructuralCommit 移除 repository/registry 活動成員；observation 保留 Active=false 的 tombstone 作診斷。新敵人只能在下一 tick 操作，stable ID 不重用。不要以 `Actors[0]` 或固定數字判斷 player；用 `PlayerId` / `FindActor`。

## 行為與限制

| 行為 | 成功 | 業務拒絕 |
| --- | --- | --- |
| Move(actor,x,y) | move.applied | actor.unknown / actor.dead |
| Attack(actor,target) | attack.applied | actor.unknown / actor.dead / target.self / target.unknown / target.dead / target.out_of_range |

未知種類為 `action.unknown`，actor=0 或非有限軸值為 `parameters.invalid`；有限方向限制到單位圓。Attack 不使用 x/y，但仍要求有限值。

scenario 預設 36,000 ticks / 40,000 inputs / 512 trace entries，是未指定 limits 時的唯一來源。上限各為 100,000 ticks / 100,000 inputs / 65,536 trace entries；超限配置直接拒絕。顯式傳入 `TemplateLimits` 是刻意覆寫執行預算，會一併錄製；不改 gameplay 規則。

Sequence 非零且 session 內唯一，同 tick 依 sequence 排序。TargetTick 必須大於 CurrentTick 且不超過 limits。一般入口回傳 `sequence.invalid_or_duplicate` / `input.capacity`；舊 facade 為保留 consumer 格式才映射為 `sequence.invalid`、`sequence.duplicate` / `action.capacity`。

Stop/Fault 取消未執行輸入；Reset 產生新 world、session ID、trace stream 與獨立 invariant 實例。失敗不 rollback，禁止繼續 Step。例外時 Observe 保留最近成功捕捉的 snapshot；用 Diagnostics 的 ObservationTick 區別失敗 tick，不能把它當失敗中途的完整世界。

## RNG、診斷與重播

敵人血量與延遲重生使用不同 SplitMix64 stream。延遲為 1–3 simulation 秒（tick 精度），spawn budget 有界。DomainEvent trace 保留外部 sequence、actor、target；lifecycle notices 反映 commit，無外部原因者 sequence=0。Phase trace 為 `Stage=Phase, Type=phase, Code=begin/end`；wave 是單次 drain 的區域索引。

自訂 invariant 透過 `new GameplayDefinition(factories, policyId)` 組裝；factory 每 session / Reset 產生新實例，必須給明確 policyId。Replay 不載入 artifact 指定的任意程式，也不忽略 policy 不符。

現行 `TemplateRecording` 記錄 scenario、limits、外部 inputs、逐 tick results/hash、首次 failure 與有界 trace。Command / Event 是診斷，不是重播輸入。Canonical state 包含有序 actor、RNG 與 pending respawn；只承諾相同 runtime / 規則下的邏輯重現，不是跨平台 bitwise、snapshot restore 或 rollback。

`ReplayArtifact` / `FailureArtifact` 舊格式仍透過 facade 投影原本 hash schema；兩套格式不能互讀。CLI `capture` / `capture-success` / `rerun` 使用現行格式，`legacy-rerun` 明確讀舊 failure。既有 sample 的 legacy regression 仍保留；超過現行上限的舊 scenario 不支援。詳見 [CLI 與相容契約](../../../docs/testability/control-and-rerun.md)。

```text
dotnet run --project tools/gameplay-checks/Gameplay.Checks.csproj
dotnet run --project tools/gameplay-lessons/Gameplay.Lessons.csproj -- all
```

前者是既有與新增契約驗收，後者是同一模型的教學步驟。Unity integration 另有 EditMode / PlayMode tests，不能以 headless PASS 取代。
