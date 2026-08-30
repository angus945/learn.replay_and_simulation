# 01：角色不需要知道 framework

[課程索引](../README.md) · [下一章：Application](02-application.md)

問題：角色收到向右方向時，應立即移動，還是等待遊戲推進時間？

## 執行

在 repository 根目錄：

```powershell
dotnet run --project tools/gameplay-lessons -- domain
```

## 讀來源並跟著接

1. 先讀現有 [CharacterMovement](../../../Assets/game/character-movement/src/Domain/CharacterMovement.cs) 與 [MovementValues](../../../Assets/game/character-movement/src/Domain/MovementValues.cs)，找出它們的狀態和行為。
2. 開啟 [Stage01Domain.cs](../Stage01Domain.cs)：直接建立 CharacterId=1、速度 4 的同一型別。這裡沒有 world、repository 或 framework。
3. `SetDesiredDirection` 只設定持續方向；斷言位置仍為 0。
4. `Advance(.25f)` 才改位置，得到 X=1。接著提交負時間，檢查拒絕後位置沒有變化。
5. 檢查斜向輸入長度為 1，送零方向後再推進，位置仍為 1。

依賴：`Stage01 → CharacterMovement.Domain`。`LessonAssert` 只是拋出失敗例外的教學 helper，不是正式遊戲 API。

## 你應看到什麼

最後一行為 `PASS 01 domain`。你已驗證方向與時間分離、領域拒絕及斜向限速；還沒有 fixed tick、Unity 或 Replay。

練習：把 lesson 中的推進時間暫改為 `.5f`，但保留 X=1 的斷言，應失敗；觀察後還原。修改的是 lesson 輸入，不是 Domain 公式。
