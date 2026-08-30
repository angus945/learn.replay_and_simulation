# 驗證與交付檢查表

[回到索引](README.md)

## 每個功能的最低測試

- [ ] Domain：合法／非法輸入、邊界值、不變條件、拒絕不改狀態。
- [ ] Application：目標不存在、死亡、跨物件協調、穩定處理順序。
- [ ] Simulation：指定 tick 才執行、phase 邊界、同 tick 的多個 action。
- [ ] Lifecycle：生成／移除與 repositories 一致、舊 ID 不指向新物件。
- [ ] 隨機／排程（若有）：seed、stream、Reset、到期前後、預算、Stop/Fault。
- [ ] Diagnostics：唯讀、不重算 invariant、資料有 tick／stream identity。
- [ ] Replay（若啟用）：scenario JSON round trip、同输入 hash 序列、不同 frame schedule、首個差異停止。
- [ ] Unity adapter：輸入不漏／重複、呈現不回寫 domain、單一 clock owner。

## 新增狀態欄位時

回答下列問題，而不是只把欄位加到 observation：

1. 誰擁有這個狀態？允許在哪個 phase 修改？
2. 是否影響下一 tick／未來的決策？若是，hash 是否包含？
3. Start／Reset 怎樣重建？Stop／Fault 留下什麼證據？
4. Replay 能從 scenario／seed／輸入導出嗎？不能的外部依賴需轉成記錄輸入。
5. 舊 artifact 缺欄位時如何處理？policy／schema 是否需要變更？
6. Snapshot 是不可變複本嗎？容量是否有界？

## 建議驗證順序

1. 跑純 .NET：`dotnet run --project tools/gameplay-checks/Gameplay.Checks.csproj`。
2. 在 Unity Test Runner 執行 EditMode 全部測試；CLI／CI 流程依專案環境配置。
3. 最後手測視覺與輸入；手測成功不能替代時序、RNG 與 Replay 測試。

純 .NET 檢查不驗證 Unity assembly import、場景綁定或畫面；EditMode 全通過也不代表已量測效能。
測試數量會變動，以當次報告為準，不把文件中的過往數字當成這輪已跑。

## Replay 失敗時依序排查

1. scenario、build、runtime、policy 是否一致？
2. 首個差異 tick 的 ActionResult 是否先不同？檢查 TargetTick／Sequence／執行時驗證。
3. ActionResult 一致但 hash 不同？檢查排序、RNG 呼叫次數、待執行排程與非權威資料誤入 hash。
4. 是否讀取 wall clock、UnityEngine.Random、Transform 或未記錄外部輸入？
5. 是否在 Observe／Render／診斷時修改 domain？

保留首次 failure artifact，不覆寫為後續錯誤；Faulted 後先保存，再 Reset 重跑。
目前沒有完整逐欄位差異定位、process watchdog 或 snapshot restore，不將這些列為已支援。

## 給新專案的 AI 協作規則範本

以下可按專案調整後放入自己的 AGENTS.md；本次不修改目前 repository 的治理規則。

```text
- Domain 不引用 Unity 或 simulation framework。
- 玩家與測試共用正式操作入口，不加入測試專用 gameplay setter。
- 新功能先說明 state owner、輸入與執行 phase，再實作。
- 新 RNG 規則指定 stream、抽取時機、Reset 與 hash 策略。
- 新排程說明 tick 邊界與停止／失敗行為。
- 改動 authoritative state 時評估 observation、hash、Replay 相容性。
- 不預設每個功能都需要新 module、repository 或 domain event。
- 新增或修改的 C# 宣告使用明確型別，不使用 var。
- 交付列出已執行的測試與尚未驗證部分，不宣稱未實作的能力。
```

## 指南維護

API／namespace 改名時更新範例與連結；食譜新增規則時附測試來源；保持「框架保證」和「專案策略」分開。
真正需要產碼工具之前，先讓下一個玩法能照指南獨立完成，再找出值得自動化的重複步驟。
