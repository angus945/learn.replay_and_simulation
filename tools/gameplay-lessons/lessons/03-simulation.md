# 03：Definition 只負責把角色接到固定 tick

[上一章](02-application.md) · [課程索引](../README.md) · [下一章：Testability](04-testability.md)

問題：如何讓「先接收方向，再移動」每個 tick 都以相同順序執行？

## 執行

```powershell
dotnet run --project tools/gameplay-lessons -- simulation
```

## 讀來源並跟著接

1. 開啟現有 [MovementDefinitionExample.cs](../../gameplay-checks/MovementDefinitionExample.cs)。課程專案直接鏈接它，不維護第二份 world／definition。
2. MovementWorld 用上一章的 Aggregate、repository、Application 組裝；`Configure` 註冊現有 [PlayerMoveIntentHandler／MovementPrePhysicsParticipant](../../../Assets/game/character-movement/src/Integration/Runtime/MovementAdapters.cs)。
3. [Stage03Simulation.cs](../Stage03Simulation.cs) 用 Definition 建立 `.25f` tick 的 Session，透過 observer 讀位置。
4. 入列 PlayerMoveIntent 時 X=0；第一個 Step 得到 X=1，第二個得到 X=2。這次不再由 lesson 直接呼叫 Application.Advance。
5. phase callback 只記錄進入順序，驗證 Input acquisition → Intent handling → PrePhysics → Physics → PostPhysics → StructuralCommit → Presentation capture。
6. Reset 回到 tick 0／X=0；刻意只 RequireIntent、不註冊 handler 的小型 definition 使用同一 MovementWorld，建立時必須明確失敗。

依賴：`Lesson → Definition → Movement Integration / Application / Domain`，Definition 同時依賴 simulation framework。Domain 不實作 phase interface。

Physics phase 存在不代表已執行 Unity PhysX；本章沒有 physics participant。Session 會封存接線，不需 lesson 再手動 Seal。世界狀態屬每次 Session，不能放在可共用的 Definition 欄位中。

## 你應看到什麼

最後一行為 `PASS 03 simulation`。你已驗證 phase 順序、持續方向與 session 重建；尚未加入 target tick／sequence、ActionResult 或 Replay。

接 Unity 時使用同一 Session 的 RealtimeRunner 取得唯一驅動權，不在 Update／FixedUpdate 另開一條 Step 迴圈。完整現行例子見 [Demo 接線](../../../docs/framework-guide/demo-template.md)；其 Unity 驗證狀態另見進度清單。
