# CharacterMovement：五階段可執行接線教學

在 repository 根目錄執行，需 .NET 9 SDK 或相容 SDK：

```powershell
dotnet run --project tools/gameplay-lessons -- all
```

也可選 `1`–`5`、`01`–`05` 或階段名稱；每個階段都自行建立乾淨物件，不依賴前一階段先執行。省略參數等同 `all`，`--help` 顯示用法。任何斷言失敗回傳 exit code 1，未知階段回傳 2。

| 階段 | 執行參數 | 本章新增責任 | 操作說明 |
| --- | --- | --- | --- |
| 01 | `domain` | 方向、位置、速度與領域驗證 | [Domain](lessons/01-domain.md) |
| 02 | `application` | 找到指定角色、repository、穩定順序 | [Application](lessons/02-application.md) |
| 03 | `simulation` | Definition、intent handler、PrePhysics、observer | [固定 tick 接線](lessons/03-simulation.md) |
| 04 | `testability` | GameplayDefinition、target tick／sequence、result、Reset | [正式控制面](lessons/04-testability.md) |
| 05 | `replay` | seeded 血量／重生、現行錄製 JSON、hash／result 比對、故障重現 | [錄製與 Replay](lessons/05-replay.md) |

成功時會看到 `PASS 01 domain` 至 `PASS 05 replay`。這些是教學斷言，不是整個專案測試總數。

## 依賴怎樣增加

```text
01 CharacterMovement.Domain
02 Application -> Domain
03 Lesson -> 現有 MovementDefinitionExample -> Movement Integration / Application
                                          -> DeterministicSimulation / modules
04 Lesson -> GameplayDefinition -> Testability -> DeterministicSimulation
                               -> GameplayWorld / GameplayActions -> 同一 Movement Domain
05 Lesson -> TemplateRecordingIO / TemplateReplay -> 同一 GameplayDefinition
```

上圖列的是各章使用的責任與 API，**不是五份獨立 assembly**。[csproj](Gameplay.Lessons.csproj)以 source link 編譯所有階段的依賴，不複製 Domain／framework 實作；因此單章執行不代表只編譯該章的最小依賴。真正的 assembly 邊界仍需專案的 asmdef／架構檢查驗證。

第 3 章直接引用既有 [MovementDefinitionExample](../gameplay-checks/MovementDefinitionExample.cs)；沒有另造 Player／CubeActor 世界。第 4–5 章用真實 GameplayDefinition／GameplayWorld，其中仍持有同一 CharacterMovement 型別；開始包含 game 的 HP／生命週期組裝，不改寫移動公式。

教學編入 gameplay-simulation 的現行來源，直接使用 GameplayDefinition／TemplateRecording；舊 GameplaySession、ports 與 artifact API 已移除，也不編入或執行 Protocol／Unity 來源。舊文件與樣本的處理見 [退休政策](../../docs/legacy-compatibility-retirement.md)。

## Unity 與後續內容

五階段是已可執行的純 C# 路線。Unity 入口連到 [Movement Demo](../../Assets/game/movement-demo/README.md)與[Realtime Runner](../../docs/framework-guide/realtime-runner.md)，不在 CLI 啟動 Editor。

已有 [Unity instance／Physics sensor／presentation adapters](../../Assets/framework.deterministic-simulation.unity/README.md)，Demo 經 [GameplayActorPresentation](../../Assets/game/movement-demo/src/Unity/GameplayActorPresentation.cs)映射 active IDs 並在模式切換時 Snap。Unity EditMode、PlayMode 與 Windows build 的各層證據分別記在 [實作進度](../../docs/implementation-progress.md)，不能以本教學 CLI PASS 代替 Unity 驗收。

第 5 章已在同一 scenario 納入 seeded 血量與延遲重生，驗證 RNG／生命週期由相同輸入重新推導，不另建教學遊戲。Protocol adapter 已接現行 ports；transport 維持 **Deferred**，不作教學前置條件。

## 本輪驗證

`dotnet run --project tools/gameplay-lessons -- all` 依序檢查正常玩法、拒絕、固定 phase、指定 tick、Reset、現行 JSON round trip、不同回放 frame 排程、第一個差異及 injected invariant failure 重現。當次結果見 [實作進度](../../docs/implementation-progress.md)，不代替全部 NUnit／Unity 驗證。
