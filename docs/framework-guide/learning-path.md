# 同一 CharacterMovement 的 DDD／Clean Architecture 接線教學

[回到指南](README.md) · [實作進度與暫緩範圍](../implementation-progress.md)

五個純 C# 階段已有獨立可執行入口，直接引用專案現有 CharacterMovement、Application 與 framework 來源，不再新增 Player／CubeActor 模型。

在 repository 根目錄執行，需 .NET 9 SDK 或相容 SDK：

```powershell
dotnet run --project tools/gameplay-lessons -- all
```

也可選階段名稱或 1–5，例如：

```powershell
dotnet run --project tools/gameplay-lessons -- simulation
```

每章自行建立乾淨世界，不需要先執行上一章。`all` 依序跑五章；斷言失敗回傳非零 exit code。

## 從一個角色逐步增加責任

| 階段 | 新增概念 | 可執行來源／操作說明 | 驗收結果 |
| --- | --- | --- | --- |
| 01 Domain | 方向、時間、速度與規則 | [Stage01](../../tools/gameplay-lessons/Stage01Domain.cs)／[說明](../../tools/gameplay-lessons/lessons/01-domain.md) | 改方向不立刻移動；.25 秒 X=1；負時間拒絕；斜向不加速 |
| 02 Application | 指定角色與 repository | [Stage02](../../tools/gameplay-lessons/Stage02Application.cs)／[說明](../../tools/gameplay-lessons/lessons/02-application.md) | unknown actor 拒絕；兩角色隔離；遍歷順序明確 |
| 03 Simulation | Definition、handler、participant、observer | [Stage03](../../tools/gameplay-lessons/Stage03Simulation.cs)／[說明](../../tools/gameplay-lessons/lessons/03-simulation.md) | enqueue≠執行；phase 順序；Reset；缺 handler 初始化失敗 |
| 04 Testability | target tick／sequence、results、正式 ports | [Stage04](../../tools/gameplay-lessons/Stage04Testability.cs)／[說明](../../tools/gameplay-lessons/lessons/04-testability.md) | 指定 tick；sequence 排序；queued≠accepted；預算；舊 identity 拒絕 |
| 05 Replay | 現行 recording codec、逐 tick 比對、seeded RNG／重生、故障 oracle | [Stage05](../../tools/gameplay-lessons/Stage05Replay.cs)／[說明](../../tools/gameplay-lessons/lessons/05-replay.md) | JSON round trip；不同 frame 排程重現血量與延遲重生；Diverged tick 1；ReproducedFailure tick 2 |

第 3 章鏈接已有 [MovementDefinitionExample](../../tools/gameplay-checks/MovementDefinitionExample.cs)，沒有複製其 world／definition；第 4–5 章改用現行 GameplayDefinition／GameplayWorld，其內仍是同一 CharacterMovement 型別，並使用 game 已有的 HP／生命週期規則。

所有階段編譯於同一個 [教學 csproj](../../tools/gameplay-lessons/Gameplay.Lessons.csproj)。章節展示責任逐步增加，不宣稱是五份已隔離的 assembly；此 executable 也不代替 asmdef、完整 NUnit 或 Unity 驗證。更多依賴細節見 [課程 README](../../tools/gameplay-lessons/README.md)。

## 接 Unity：沿用現有 Demo

完成第 3 章可以理解基本 realtime 接線，第 4–5 章則對應目前 Demo 使用的正式控制面與錄製路徑：

```text
MovementDemoHost → MovementDemoSession → GameplayDefinition
→ TestableSimulationSession → GameplayWorld → CharacterMovement
```

- [現行 Demo 整合](demo-template.md)：輸入、錄製及回放模式。
- [RealtimeRunner](realtime-runner.md)：唯一 tick 驅動權與 frame／tick callbacks。
- [MovementDemoHost](../../Assets/game/movement-demo/src/Unity/MovementDemoHost.cs)：Unity 輸入及畫面入口。
- [GameplayActorPresentation](../../Assets/game/movement-demo/src/Unity/GameplayActorPresentation.cs)：把已提交 observation 的 active IDs 映射至共用 UnityActorPresentation／Pool；切換 session／回到 Live 時明確 Snap。
- [Unity adapters](../../Assets/framework.deterministic-simulation.unity/README.md)：多物件呈現與獨立 3D sensor scene 的接線契約；沒有 dynamic-body state readback。
- [CharacterMovementDemo 場景](../../Assets/game/movement-demo/scenes/CharacterMovementDemo.unity)：已有實際 Demo，不另建第三套 lesson game。

上述 Unity adapter 與 Demo 接線的 EditMode／PlayMode／Player build 證據分別記在 [實作進度](../implementation-progress.md)。本 CLI 不操作 Unity，不能以 CLI PASS 取代 Unity 驗收。Physics 限定獨立 scene 的 logical-authority sensors，不等於已還原舊 Apply／Capture TODO 或讓 Demo 依賴物理權威狀態。

第 5 章已在同一範例加入 seeded 血量與延遲重生，沒有另建遊戲模型；現有規格可讀 [生命週期與 RNG](../testability/simulation-lifecycle-phase-random.md)。Protocol adapter 使用現行 ports／game payload v2，transport 仍 **Deferred（暫緩）**；不作教學前置條件。

## 驗證與範圍

以上 `all` 命令依序執行五階段斷言，當次結果見 [實作進度](../implementation-progress.md)。教學直接建立 GameplayDefinition，只使用 TemplateRecording，不編入 Protocol／Unity 來源。舊 GameplaySession／artifact API 已退役，歷史基準及舊樣本處理見 [退休政策](../legacy-compatibility-retirement.md)。

Domain 不依賴 simulation／testability／Unity；接線在外圍。GameplayWorld 是每個 session 的組裝與狀態容器，不因名稱為 World 就成為 Aggregate。C# 宣告使用明確型別，不用 `var`。

進階 Unity 接線與退休條件依 [驗收矩陣](acceptance-matrix.md)界定；不承諾 snapshot restore、rollback 或跨平台 bitwise determinism。
