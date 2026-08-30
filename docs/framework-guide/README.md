# DDD 遊戲框架開發指引

目標：不必先讀完框架實作，就能建立一個純 C#、可推進 tick、可測試的玩法，再接上 Unity 與 Replay。
本指南對應此 repository 的現有 API；不是已發布的獨立套件安裝說明。

## 建議閱讀順序

**想直接繼承並讓編譯器指出必填責任：先讀 [Definition／Session 模板](definition-template.md)。** 已提供實際 abstract class、組裝 builder 與契約檢查。

需要正式控制面、診斷、錄製與失敗重現：使用 [Testability／Replay 模板](testability-replay-template.md)，不必自行重寫 session 流程。

1. [入門與可執行範例](getting-started.md)：先跑最小移動，再看正式控制面。
2. [架構、DDD 邊界與組裝](architecture.md)：決定程式放哪裡、依賴誰。
3. [契約與專案策略](contracts.md)：哪些不可違反，哪些只是 Demo 的選擇。
4. [功能食譜](recipes.md)：新增 action、跨領域事件、RNG、生命週期及觀察。
5. [驗證與交付檢查表](verification.md)：如何避免新功能破壞重現性。

## 依任務選擇

| 你要做的事             | 從哪裡開始                                     |
| ---------------------- | ---------------------------------------------- |
| 建立第一個玩法         | 入門的範例 A；暫不加入 Protocol、RNG、registry |
| 加入正式操作與測試入口 | 入門的範例 B、action 食譜                      |
| 加入攻擊、死亡、生成   | 跨領域與生命週期食譜                           |
| 接上 Unity             | 架構的三層組裝、現有 MovementDemoSession       |
| 排查 Replay 不一致     | 驗證檢查表、契約中的 hash 邊界                 |
| 未來接外部 AI／Fuzzer  | 先完成正式控制面，再讀 Protocol README         |

## 現有參考實作

- [module／assembly／namespace 對照](../module-naming.md)。
- [GameplaySession 與控制面](../../Assets/game/gameplay-simulation/README.md)。
- [Testability](../../Assets/framework.testability/README.md)。
- [完整 Demo](../../Assets/game/movement-demo/README.md)。
- [生命週期、phase、RNG 與重生排程](../testability/simulation-lifecycle-phase-random.md)。
- [Protocol 核心](../../Assets/framework.gameplay-protocol/README.md)與[專案 adapter](../../Assets/game/gameplay-protocol/README.md)。目前沒有網路 listener，也未接到 Demo。

## 使用範圍

本輪提供指南與可執行組裝範例，不新增遊戲機制、不提供 generator、不搬移原有領域程式。
範例分成「最小機制展示」與「完整專案參考」；不要把完整 GameplaySession 整份複製後當成所有遊戲的標準模型。
目前目標是相同 runtime／規則下重現，不承諾跨平台 bitwise determinism、完整 snapshot restore 或 rollback。

維護原則：修改契約時，同時更新本指南與對應測試；程式範例以實際參與編譯的來源為準。
