# Gameplay Simulation：步驟 1–3

以既有 Movement 切片加入正式控制面、固定傷害戰鬥、diagnostics；不做 test-protocol。

## 使用

```csharp
GameplaySession session = new GameplaySession();
session.Admin.Start(new GameplayScenario(tickDelta: .25f));
SubmissionResult queued = session.Gameplay.Submit(new GameplayRequest(
    session.Id, sequence: 1, targetTick: 1, GameplayActionKind.Move, actor: 1, x: 1));
TickReport report = session.Simulation.Step();
GameplayObservation state = session.Gameplay.Observe();
ActionLookup result = session.Results.Find(session.Id, 1);
```

玩家 adapter 和測試都呼叫 Submit，再由同一個 tick pipeline 執行；測試不需要鍵盤或 Unity。
玩家使用 Realtime clock authority，測試使用 Manual Simulation.Step；同一 session 不允許混用兩個時鐘。
`session.Diagnostics` 提供獨立唯讀 facade，供 Overlay／工具取得 immutable DiagnosticSnapshot 與 cursor-based trace。
Facade 不能直接 cast 成 GameplaySession／IGameplayControl；讀取不重新評估 invariant，也不記錄新 trace。
Session reset 後同一 facade 指向新 session，trace stream identity 更新，consumer 必須清掉舊 stream cache。
`IGameplayControl` 不暴露 Reset、Start、Stop 或任意 setter。這是程式邊界，不是對同程序惡意程式的 security sandbox。
Admin composition 才持有完整 GameplaySession，透過 scenario 建立條件。

## 行為目錄

| 行為 | 參數 | 成功 | 業務拒絕 |
|---|---|---|---|
| Move | actor、x、y | `move.applied`：更新持續移動方向 | `actor.unknown`、`actor.dead` |
| Attack | actor、target | `attack.applied`：立即套用固定傷害 | `actor.unknown`、`actor.dead`、`target.self`、`target.unknown`、`target.dead`、`target.out_of_range` |

未知種類為 InvalidRequest `action.unknown`；actor=0 或非有限軸值為 `parameters.invalid`。
Move 將方向限制到單位圓，不把大於 1 的有限輸入當作 malformed。
Attack 的 x/y 不参与戰鬥，但仍必須是有限值。第一版沒有 cooldown、碰撞、武器、AI 或攻擊動畫。

## Queue Contract

- `SessionId` 必須等於本次 session identity；Reset 產生新 identity，旧要求拒絕。
- Sequence 必須非零且 session 內唯一；呼叫者明確指定，不用 arrival order 決定結果。
- `CurrentTick < TargetTick <= MaxTicks`；同 tick 按 sequence 遞增處理。
- 排隊不改 gameplay state，也不代表 gameplay 已接受。
- Queue 拒絕：`session.not_running`、`session.busy`、`request.null`、`session.stale`、`sequence.invalid`、`sequence.duplicate`、`tick.out_of_range`、`action.capacity`。
- Queue 拒絕不進重跑輸入歷史；已排隊的非法 gameplay request 仍會記錄並產生執行結果。
- 历史保留所有已排隊輸入，包括尚未到期者，達 MaxActions 後拒絕新輸入，不截斷重現資料。
- Stop 清除尚未執行的 queue；不為未到期要求捏造已執行結果。

## Tick／生命週期規格

1. Step 選出下一 tick 的要求，按 sequence 入列 RequestIntent。
2. Intent handler 只建立 ExecuteAction Internal Command。
3. 內部工作依相同順序驗證、改方向或攻擊，回傳 ActionResult；受傷／死亡發出 Domain Event。
4. 死亡立即停止方向並禁止後續 action；ActorDied adapter 請求 registry destroy。
5. PrePhysics 只移動活著的角色。攻擊距離使用本 tick 位移前的位置，即使同 tick 先送 Move 也不會提前位移。
6. StructuralCommit 使 destroy 正式生效；唯讀 observation 保留 tombstone（Active=false），方便診斷死亡原因。
7. Tick 結束建立 state hash、評估 invariants、保存 trace；沒有 Physics adapter。

初始 player=1，enemy=2（可關閉），都由 registry 配發 ID；敵人在 (1,0)。Spawn 僅發生在初始化。
預設 Health=30、Damage=10、Range=2；沒有 RNG draw，Seed 僅保留在 scenario，沒有偽造隨機消耗。
Frame adapter 持續輸入 Move，測試可只改一次方向，後續 tick 沿用。短按 Attack 以一次 pending intent 消耗，不在補跑 tick 重複攻擊。

## Session／錯誤

Created → Start → Running；Stop → Stopped；tick exception/invariant violation → Faulted。
Reset 以新 world、pipeline、registry、history、trace 與 session identity 重建，不能在 tick 內 reenter。
額外 invariant 以 factory 在 Start 前註冊，每次 Reset 建立新實例；factory 不應擷取上一個 session 的可變狀態。
Tick exception 不回滾先前已完成的行為；未執行的當 tick 要求標為 Failed `tick.aborted`。
Failed tick 的 CurrentTick 是嘗試執行的 tick，而非最後成功 tick。Faulted 禁止繼續 Step，只能 Reset。
Tick-level invariant／位移錯誤的 ActionSequence 為 0，不把最後一個 action 誤認為原因；可由 tick 內 action/command/event trace 回查。
MaxTicks 到期後下一次 Step 停止並明確拋出 budget error；不是遊戲缺陷。
預設 36,000 ticks／40,000 admitted actions／512 trace entries。沒有 process watchdog，不能強制終止卡住的 callback。

## State hash／Invariant／Artifact

Hash schema v1：SHA-256，little-endian，固定欄位順序與 ID 排序；包含 tick、gameplay config/seed、所有角色的 ID/位置/持續方向/速度/HP/Active。
有限浮點採精確 float bits、-0 正規化為 +0；只承諾相同 runtime 的重跑比較，不宣稱跨平台 bitwise deterministic。
排除 session GUID、build label、render interpolation、wall clock、trace、未來外部输入 queue、測試預算。
這是 GameplayStateHash，不是可 restore 的完整 runtime snapshot。

Invariant：`actor.id_unique`、`health.bounds`、`movement.finite`、`movement.unit_direction`、`lifecycle.committed`、`dead.stationary`。
失敗保存 schema/build/config/scenario/seed/runtime、外部輸入、ActionResult、hash checkpoints、有限 trace、當下 actor observation、失敗 tick/code 與 exception type/detail。
Command/Event 僅記診斷 trace，不當成重跑輸入。重跑只重新提交外部要求。
本版 trace 的 wave 是每次 dispatcher drain 的區域 wave index，不是整個 tick 的全域 wave 編號。

`ArtifactJson.Write/Read` 使用 caller-owned stream；`ScenarioRerun.VerifyFailure` 比對失敗 tick/code/exception type、ActionResult 序列和全部已存 hash checkpoints。
這是同 build／相同規則的 in-process regression helper，不會自行啟動舊 build、載入任意診斷插件或還原任意 snapshot。
自訂 invariant 必須由相同 composition 重建；缺少該 policy 時 VerifyFailure 回傳 false。production 應提供明確 Build label，預設 unspecified 不代表已識別 commit。

## 純 .NET 驗證

在專案根目錄：

```text
dotnet run --project tools/gameplay-checks/Gameplay.Checks.csproj
```

需要 .NET 9 SDK 或相容 SDK，無 NuGet package 或 Unity assembly 依賴。
傳入 `capture <new-artifact.json>` 會輸出 JSON failure example（CreateNew，不覆寫舊檔）。
傳入 `rerun <artifact.json>` 會輸出結構化 JSON 比較報告。
控制 facade、clock ownership、結果查詢與重跑契約詳見 `docs/testability/control-and-rerun.md`。
Unity EditMode tests 位於 `tests/`；原 Movement demo 也已改用此控制面。
