# 06 — Observation：唯讀狀態與 canonical bytes

[上一章：Lifecycle／RNG](05-lifecycle.md) · [教材索引](README.md) · [下一章：Diagnostics](07-diagnostics.md)

本章問題：測試和 UI 需要看世界，但不能拿到 Actor 後直接改血量；Replay 則要比較「未來仍會做相同決定」的狀態，而不是只比較畫面看起來一樣。

因此建立明確的 observation，再定義唯一、穩定的 canonical encoding。Arena host 負責何時 capture 與計算 digest；`module.verification.state-snapshot` 只負責 publish／reference／lookup。

## 接點一：複製成不可變 read model

[ArenaObservation](../../Assets/game/arena/src/Integration/ArenaObservation.cs) 由 ArenaRuntime 建立，但不保存 runtime／Actor 引用：

- ActorSnapshot 複製 ID、kind、位置、持續方向、速度、Health、MaxHealth。
- Actors 依 repository 的 ActorId 順序複製，只列 commit 後仍存在的角色。
- PendingRespawnTicks 與 RegistryEvidence 複製成新的 read-only collection。
- Tick、PlayerId、LastActorId、EnemiesSpawned、兩個 RNG state 也明確保存。
- 不可變 ArenaRules 與 TickDelta 一併提供，讓尚未影響當前畫面的規則差異也能進入比較；Rules 本身沒有 setter，因此可安全共享不可變值。
- RegistryActiveCount 用於整合 invariant；不是讓 UI 直接操作 registry。

`IReadOnlyList<Actor>` 仍會洩漏可變 Actor，只有把元素也變成 ActorSnapshot 才完成隔離。Application.Actors 是可信內部邊界，不直接分發給外部 consumer。

在 [ArenaObservationAdapter](../../Assets/game/arena/src/Integration/ArenaObservationAdapter.cs) 內，capture 很短：

```csharp
public StateSnapshotReference Publish(SimulationSession<ArenaRuntime, ArenaScenario> session, string sessionId, long epoch)
{
    ArenaObservation observation = new ArenaObservation(session.World);
    return publisher.Publish(observation);
}
```

ArenaSession 在初始化 tick 0 publish 一次 snapshot，之後每個 tick 的 pipeline 完成後重新 publish。外部 `session.Observe()` 與 `ObservationReader.Read(...)` 只讀已保存 snapshot，不重新 capture、不推進 tick、不重跑 oracle。

## 接點二：明確列出影響未來的 state

[ArenaCanonicalState.Encode](../../Assets/game/arena/src/Integration/ArenaEvidence.cs) 使用固定順序的 BinaryWriter，不依賴一般 JSON 字典列舉或物件 GetHashCode。

編碼內容依序是：

1. canonical schema marker。
2. Tick、PlayerId、TickDelta 與完整不可變 ArenaRules。
3. LastActorId、EnemiesSpawned、health／delay RNG state。
4. 待重生 ticks 的數量與有序值。
5. registry allocator／binding／generation 的明確 evidence。
6. 有序 ActorSnapshot 的數量及各欄位。

為什麼要超過 X/Y／Health？

- 相同位置但不同持續方向，下一 tick 的位置會不同。
- 相同敵人血量但不同 RNG state，下次重生可能不同。
- 相同活動角色但不同 due tick，未來哪時出生會不同。
- 相同畫面但不同 LastActorId 或 slot generation，未來身分行為可能不同。
- 相同初始角色但 Damage、AttackRange 或 TickDelta 不同，後續輸出會不同；所以規則也進 canonical state，不等第一次 Attack 才暴露差異。

registry evidence 是此 adapter 為比較提供的資料，不是 framework 承諾可還原的 snapshot 格式。RegistryActiveCount 可由活動狀態衍生，額外用 oracle 檢查 repository 一致性；不要把「每一個診斷欄位都寫一次」誤當 hash 設計。

## 由 adopter 計算 canonical digest

[ArenaStateDigest](../../Assets/game/arena/src/Integration/ArenaEvidence.cs) 明確組合 canonical encoding 與 SHA-256：

```csharp
public static string Compute(ArenaObservation observation)
{
    byte[] bytes = ArenaCanonicalState.Encode(observation);
    // SHA-256 and lowercase hexadecimal encoding
}
```

Arena 對 bytes 計算 SHA-256。Arena 先拒絕非有限 float，並把正零／負零統一成 0；它沒有因此取得跨 runtime／CPU／Unity physics bitwise determinism。

下列資料刻意不進 gameplay hash：

- session GUID、trace cursor、UI 捲動位置。
- frame accumulator、presentation alpha、Unity Transform。
- 尚未到期的外部 input queue；重現還需要 recording 保存的輸入。

完整 scenario／seed 另存於 recording；其中影響未來的不可變遊戲規則與 TickDelta 也已納入 canonical state。PolicyId 識別程式規則／codec／canonical schema／invariant 組裝，不能代替 scenario 值的比較。hash 不是完整 session checkpoint，也不是只有 hash 就能重播。

## 驗證先前 snapshot 不會跟著變

以下片段放在測試／console 方法中：

```csharp
using System;
using Arena.Application;
using Arena.Composition;
using Arena.Integration;

using (ArenaSession session = new ArenaDefinition().CreateSession(new ArenaScenario(tickDelta: .25f)))
{
    ArenaObservation before = session.Observe();
    session.Submit(new ArenaInput(ArenaAction.Move, before.PlayerId, x: 1f), 1);
    ArenaTickEvidence tick = session.Step();
    ArenaObservation after = session.Observe();

    Console.WriteLine(before.FindActor(before.PlayerId).X); // 0
    Console.WriteLine(after.FindActor(after.PlayerId).X);   // 1
    byte[] bytes = ArenaCanonicalState.Encode(after);
    Console.WriteLine(bytes.Length > 0);                   // True
    Console.WriteLine(string.IsNullOrEmpty(tick.Digest));   // False
}
```

這裡不預先寫死一串 hash 常數。讀者應理解哪些欄位改變造成 hash 不同，而不是把新的預期 hash 填回測試就宣稱相容。

## 時間與失敗邊界

成功流程為 pipeline → publish observation → digest → evaluate oracles。若 oracle 失敗，該 tick 可能已有新 observation 和 digest；若較早的 phase 或 capture 失敗，讀取可能仍是前一 tick 的 snapshot。

因此 `ArenaDiagnosticSnapshot.ObservationTick` 與嘗試中的 Tick 是不同資訊。不要從「Observe 還能回物件」推論得到了失敗中途世界，也不要把前一 tick 的 PASS 當本 tick 成功。

## 執行與反例

```powershell
dotnet run --project tools/arena-checks -- observation
```

預期驗證 snapshot 獨立、順序穩定、同條件初始狀態可比較、關鍵未來狀態參與 canonical encoding。這項檢查不等同 snapshot restore。

反例：從 canonical bytes 刪掉 PendingRespawnTicks，再比較兩個只有 due tick 不同的 snapshot。hash 若不再區分它們，就是遺漏未來狀態，不是更寬鬆的正確 determinism。

新增任何狀態時都先問：誰擁有它、是否影響下一個決定、如何初始化新 session、如何 observation／canonical encode，再決定需不需要新增欄位。下一章將這份 snapshot 交給 post-tick oracle 和唯讀診斷 consumer。
