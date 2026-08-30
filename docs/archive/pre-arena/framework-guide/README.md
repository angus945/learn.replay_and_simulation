# DDD 遊戲框架開發指引

目標：不必先讀完框架實作，就能建立一個純 C#、可推進 tick、可測試的玩法，再接上 Unity 與 Replay。
本指南對應此 repository 的現有 API；不是已發布的獨立套件安裝說明。

## 唯一推薦的累積路線

從 [CharacterMovement 累積教學路線](learning-path.md)開始：同一個角色依序接上 Application、固定 tick、Testability、攻擊／死亡與 Replay，Unity 則連接現有 Demo，不在中途另造 Player／CubeActor 模型。

五個純 C# 階段已可獨立執行：在根目錄執行 `dotnet run --project tools/gameplay-lessons -- all`，或用 `domain`、`application`、`simulation`、`testability`、`replay` 選一章。[逐章說明與來源](../../tools/gameplay-lessons/README.md)列出每次增加的責任與斷言。

Unity 沿用現行 Demo，已新增[多物件／Pool 與獨立 Physics sensor adapters](../../Assets/framework.deterministic-simulation.unity/README.md)，最終測試證據另行整理；五章 PASS 不代表整個進階課程與 Unity 已完成。各項驗收見 [分階段實作進度](../implementation-progress.md)。

| 入口                                                                                     | 定位                                                                                  |
| ---------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------- |
| [MovementDefinitionExample.cs](../../tools/gameplay-checks/MovementDefinitionExample.cs) | 已有純 C# 接線來源；沿用 CharacterMovement，以 Definition 建立基本 Session            |
| [Definition／Session 模板](definition-template.md)                                       | 基本接線 API；Domain 不需要繼承 framework                                             |
| [Testability／Replay 模板](testability-replay-template.md)                               | 正式輸入、診斷、錄製與失敗重現；現行 Demo 的方向                                      |
| [可組裝的即時 Runner](realtime-runner.md)                                                | Unity frame 到固定 tick；唯一驅動權與呈現 callbacks                                   |
| [現行 Demo 整合](demo-template.md)                                                       | MovementDemoSession → GameplayDefinition → TestableSimulationSession 的完整範例       |
| [入門與可執行範例](getting-started.md)                                                   | 五章主線、低階 Pipeline 補充與現行正式操作導讀                                        |
| [架構](architecture.md)、[契約](contracts.md)、[功能食譜](recipes.md)                    | GameplayActions／GameplayWorld 的責任、phase／hash 邊界，以及新增功能時實際修改的接點 |
| [驗證與交付檢查表](verification.md)                                                      | 區分純 .NET、Unity 編譯、PlayMode 與 Player Build 證據                                |

## 機制參考與歷史導讀

以下素材保留供查閱，不是三條都必須完成的入門課：

- [極簡接線範例](minimal-wiring.md)：獨立 Player 模型，已有編譯來源；只用來比較最少接點。
- [從零建立 Unity 接線案例](minimal-unity-wiring.md)：獨立 CubeActor 文件範例；該頁明示尚未完成其 Unity 編譯／Play Mode 驗證。
- [FrameworkGuideExamples](../../tools/gameplay-checks/FrameworkGuideExamples.cs) 中的 ControlledMovement 是現行 ports 的短版範例；使用 GameplayDefinition，不需要另外學習舊控制面。

## 依任務選擇

| 你要做的事                | 從哪裡開始                                                                      |
| ------------------------- | ------------------------------------------------------------------------------- |
| 建立第一個玩法            | 累積路線第 1–3 章；先不加 RNG、registry 或 Protocol                             |
| 加入正式操作與測試入口    | 可執行第 4 階段、Testability／Replay 模板                                       |
| 理解攻擊、死亡與錄製      | 可執行第 5 階段；包含 seeded 血量、延遲重生與現行 GameplayWorld                 |
| 接上 Unity                | 累積路線的 Unity 連接、RealtimeRunner、MovementDemoSession                      |
| 排查 Replay 不一致        | 驗證檢查表、契約中的 hash 邊界                                                  |
| 接工具或未來的 AI／Fuzzer | Protocol adapter 的 game payload v2／現行 ports；transport 與外部 client 仍暫緩 |

## 現有參考實作

- [module／assembly／namespace 對照](../module-naming.md)。
- [Gameplay Simulation 與正式控制面](../../Assets/game/gameplay-simulation/README.md)；現行 Demo 路徑見 [Demo 整合](demo-template.md)。
- [Testability](../../Assets/framework.testability/README.md)。
- [完整 Demo](../../Assets/game/movement-demo/README.md)。
- [生命週期、phase、RNG 與重生排程](../testability/simulation-lifecycle-phase-random.md)。
- [Protocol 核心](../../Assets/framework.gameplay-protocol/README.md)與[專案 adapter](../../Assets/game/gameplay-protocol/README.md)：envelope v1／game payload v2，直接接正式 ports；**transport Deferred（暫緩）**，沒有網路 listener，也未接到 Demo。
- [舊控制面與格式退休政策](../legacy-compatibility-retirement.md)：現行版本不再讀舊 artifact；歷史基準為 `22f6966`。

## 使用範圍

本輪依實作進度分階段整理 framework 與教學；導航完成不等於接線、測試或全部課程完成。Protocol adapter 的 ports 遷移已納入本輪，transport／外部 client 另列暫緩，不由現有測試推定完成。

範例分成「最小機制展示」與「現行 Demo」；舊 facade／artifact 只留在歷史基準，不是第三條現行教學路線。先學所需接點，不整份複製完整遊戲組裝。
目前目標是相同 runtime／規則下重現，不承諾跨平台 bitwise determinism、完整 snapshot restore 或 rollback。

維護原則：修改契約時，同時更新本指南與對應測試；程式範例以實際參與編譯的來源為準。
