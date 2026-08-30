# 02：需要指定角色時才加入 repository

[上一章](01-domain.md) · [課程索引](../README.md) · [下一章：固定 tick](03-simulation.md)

問題：現在有兩個角色，輸入只應影響其中一個；找不到角色應有可預期的結果。

## 執行

```powershell
dotnet run --project tools/gameplay-lessons -- application
```

## 讀來源並跟著接

1. 讀 [ICharacterMovementRepository](../../../Assets/game/character-movement/src/Application/API/ICharacterMovementRepository.cs) 與[實作／MovementApplication](../../../Assets/game/character-movement/src/Application/Runtime/CharacterMovementRepository.cs)。Domain 本身沒有變。
2. [Stage02Application.cs](../Stage02Application.cs) 建立兩個現有 CharacterMovement，故意先加入 ID=2，再加入 ID=1。
3. 建立 MovementApplication 並注入 repository；用 `TrySetDirection` 選 ID=1，再嘗試不存在的 ID=99。
4. 有效角色得到 true，未知角色得到 false；推進前都不會改位置。
5. `Advance(.25f)` 後，ID=1 的 X=1，ID=2 的 X=0。最後驗證 repository 提供 1、2 的穩定順序。

依賴：`Lesson → Application → Domain`。repository 是用來找到角色，不是每個欄位都需要的一層；這章仍沒有 simulation phase 或 Unity。

## 你應看到什麼

最後一行為 `PASS 02 application`。你已把「哪個角色接受請求」放到 Application，移動規則仍由 Domain 擁有。

練習：把有效方向送給第二個角色，先預測哪個斷言會失敗，再執行並還原；不要讓兩個角色共用同一個 Aggregate instance。
