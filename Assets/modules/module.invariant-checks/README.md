# module.invariant-checks

純 C# 規則評估機制，無 Unity、simulation、testability framework 依賴。

- `src/API/IInvariant<T>`：對 observation 評估，通過回傳 null，失敗回傳 InvariantViolation。
- `src/Contract`：穩定 code 與診斷 detail。
- `src/Runtime/InvariantRegistry<T>`：重複檢查、Seal、ordinal code 排序、唯讀結果。
- `tests`：鎖定、順序、結果副本與 exception 行為。

規則的 Code 必須在註冊後保持不變；Evaluate 不應修改被觀察的 gameplay。
Seal 後不可增加規則，Seal 前不可 Evaluate。例外直接交給 caller，不偽裝成一般 invariant violation。
Module 不定義健康值、角色生命週期或任何遊戲規則；不決定評估時機、Session Faulted 政策、trace 或 UI。

遷移：IInvariant、InvariantViolation、InvariantRegistry 由 Testability namespace 改到 InvariantChecks，consumer 必須直接引用 Module.InvariantChecks。
InvariantViolation 保留舊 serialization contract identity；JSON 的 code/detail 欄位不變。
