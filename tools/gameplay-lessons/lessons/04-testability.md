# 04：同一角色加上正式控制與觀察

[上一章](03-simulation.md) · [課程索引](../README.md) · [下一章：Replay](05-replay.md)

問題：外部工具如何指定輸入發生的 tick，並區分排隊成功與遊戲執行成功？

## 執行

```powershell
dotnet run --project tools/gameplay-lessons -- testability
```

## 讀來源並跟著接

1. 讀現有 [GameplayDefinition](../../../Assets/game/gameplay-simulation/src/Runtime/GameplayDefinition.cs)、[GameplayInput](../../../Assets/game/gameplay-simulation/src/Contract/GameplayInput.cs) 與 [GameplayWorld](../../../Assets/game/gameplay-simulation/src/Runtime/GameplayWorld.cs)。World 仍使用同一 CharacterMovement；這章改用完整 game 組裝，沒有重寫移動。
2. [Stage04Testability.cs](../Stage04Testability.cs) 建立無敵人的 scenario，用 `CreateTestSession` 取得正式 template ports；建立完成即為 Running，不另呼叫 Start。
3. GameplayInput 只攜帶玩法資料；session ID、sequence、target tick 是 Submit 的 envelope。Scenario 的 4 tick／4 input 預算由 game Definition 映射。
4. 故意先提交 sequence 2 的向左，再提交 sequence 1 的向右，兩筆都指定 tick 2；另提交未知角色的 sequence 3。
5. 三個合法 envelope 都可 queued，但不立即移動。tick 1 無結果；tick 2 依序執行 1、2、3，位置 X=-1。未知角色得到 Rejected／actor.unknown，session 不會 fault。
6. 查詢結果並確認先前 observation 仍是 X=0。Admin.Reset 更換 identity，舊 session ID 的請求得到 session.stale。

依賴：`Lesson → GameplayDefinition → Testability / DeterministicSimulation`；game use cases 再指向原有 Domain。正式 Gameplay port 不提供任意 SetPosition／SetHealth；Admin 的世界重建也不是玩家操作。

## 你應看到什麼

最後一行為 `PASS 04 testability`。你已驗證 queued≠accepted、同 tick 順序、snapshot 不變、結果查詢及 Reset 隔離。

目前大部分新讀者應從此入口做 game 系統測試；第 3 章的基本 Session 用來理解最少接線。兩者不能同時推進同一個 world。
