# 08 — Replay：保存輸入並重新證明結果

[上一章：Diagnostics](07-diagnostics.md) · [教材索引](README.md) · [下一章：Realtime](09-realtime.md)

本章問題：如何證明「重播相同」不只是畫面相似？如果只保存位置，便無法驗證 Attack 的拒絕理由、RNG 消耗、重生排程或首次失敗。

Testability 保存外部輸入及逐 tick 證據；Replay 以同一 Definition 建立新世界，再走同一 Application 和 pipeline，重新推導內部結果。

## 接點一：穩定 codec 與明確 PolicyId

[ArenaCodecs](../../Assets/game/arena/src/Integration/ArenaEvidence.cs) 使用既有 ArtifactJson utility 序列化 ArenaScenario／ArenaInput。這個 utility 的名字不表示 Arena 支援任何舊 game artifact；現行保存格式只有 TemplateRecording。

ArenaDefinition 覆寫這四個 hooks：

```csharp
protected override string EncodeScenario(ArenaScenario scenario)
    => ArenaCodecs.Encode(scenario);
protected override ArenaScenario DecodeScenario(string payload)
    => ArenaCodecs.Decode<ArenaScenario>(payload);
protected override string EncodeInput(ArenaInput input)
    => ArenaCodecs.Encode(input ?? throw new ArgumentNullException(nameof(input)));
protected override ArenaInput DecodeInput(string payload)
    => ArenaCodecs.Decode<ArenaInput>(payload);
```

這些成員位於 ArenaDefinition，引用 System 和 Arena.Integration。codec 必須無副作用，decode 回獨立物件；不要在 decode 抽亂數或呼叫 use case。

PolicyId 由 `ArenaDefinition.DefaultPolicy` 及選配 oracle suffix 組成，描述規則、canonical schema、兩條 RNG streams、lifecycle 與檢查政策。它不是自動程式碼 hash；修改上述行為時要明確評估改版，不能只改測試預期的 hash。

## 錄製包含什麼、不包含什麼

TemplateRecording 保存：

- encoded scenario、Policy、Runtime、TickDelta、實際 TemplateLimits。
- initial hash 與 admitted external inputs 的 sequence／tick／payload。
- 每個 TemplateTick 的 hash、ActionResults，包含沒有輸入的尾段。
- 首次 TemplateFailure 與有界 trace。

它不保存整個 Actor object graph、不保存 Unity frame delta、不把 RespawnCommand／ArenaFactMessage 當新輸入，不保存任意 observation 作 restore checkpoint。

已排隊但尚未到期的 input 可以保留在 recording；Replay 只跑到最後一個已錄製 tick，不會為了未來 input 擅自延長情境。CaptureRecording 不停止正常 session；Reset 會清空該 session 的舊歷史，因此跨 Reset 要分開保存。

## 接點二：從正式輸入得到真正的 JSON round trip

以下片段放在測試／console 方法中，只使用 production Definition，沒有 replay 專用 gameplay：

```csharp
using System;
using System.IO;
using Arena.Application;
using Arena.Composition;
using Arena.Integration;
using Testability.Templates;

ArenaDefinition definition = new ArenaDefinition();
TemplateRecording recording;
using (TestableSimulationSession<ArenaRuntime, ArenaScenario,
    ArenaInput, ArenaObservation> session = definition.CreateTestSession(
    new ArenaScenario(tickDelta: .25f)))
{
    ulong player = session.Observe().PlayerId;
    session.Gameplay.Submit(session.Id, 1, 1,
        new ArenaInput(ArenaAction.Move, player, x: 1f));
    session.Gameplay.Submit(session.Id, 2, 3,
        new ArenaInput(ArenaAction.Move, player));
    for (int tick = 0; tick < 8; tick++) session.Simulation.Step();

    using (MemoryStream stream = new MemoryStream())
    {
        TemplateRecordingIO.Write(stream, session.CaptureRecording());
        stream.Position = 0;
        recording = TemplateRecordingIO.Read(stream);
    }
}
using (TemplateReplay<ArenaRuntime, ArenaScenario,
    ArenaInput, ArenaObservation> replay = definition.CreateReplay(recording))
{
    replay.Play();
    for (int frame = 0; frame < 1000 &&
        replay.State == TemplateReplayState.Playing; frame++)
        replay.AdvanceTime(1f / 144f);

    Console.WriteLine(replay.State); // Completed
    Console.WriteLine(replay.FirstDifference == null); // True
    Console.WriteLine(replay.Observe().FindActor(replay.Observe().PlayerId).X); // 2
}
```

只有兩筆外部 input；tick 3–8 的停止狀態仍屬錄製邊界。改成 30 FPS 或長 frame，應保持相同逐 tick 結果，因為播放 frame 只決定這次呼叫跑幾個固定 tick。

注意這證明的是「相同已分配到 tick 的輸入」可重現；不同真實鍵盤 frame 排程未必產生同一個 target tick，下一章會區分。

## 三種結果必須分開

- Completed：正常錄製的全部 ticks 與預期相符。
- ReproducedFailure：在預期 tick 重現相同 failure fingerprint，也通過相應的 results／hash 比對。重現成功，但原遊戲情境仍失敗。
- Diverged：第一個 policy／initial hash／tick hash／result／failure 差異，保存 FirstDifference 並停止。

拿第 7 章 oracle failure 的 recording，用 `new ArenaDefinition(failureOracle: true)` 重播，應得到 ReproducedFailure。改成普通 `new ArenaDefinition()`，policy 不同，應在 tick 0 Diverged，而不是少跑一個 oracle 後宣稱成功。

Replay 不對外提供 Submit，因此播放時沒有即時玩家輸入混入。Restart 建立新 session；先前取得的 Diagnostics reader 屬於舊 session，consumer 必須重新 bind。

## 接點三：讓檔案邊界可預期

`TemplateRecordingIO` 操作 caller 提供的 stream；路徑、關閉 stream、不覆寫政策由 host 負責。Arena CLI／Unity 使用新檔寫入，不把一次新故障覆蓋成舊錄製。

```powershell
dotnet run --project tools/arena-checks -- capture .utmp/arena-success.json
dotnet run --project tools/arena-checks -- capture-failure .utmp/arena-failure.json
dotnet run --project tools/arena-checks -- rerun .utmp/arena-success.json
dotnet run --project tools/arena-checks -- rerun .utmp/arena-failure.json
```

先確認目錄存在，capture 路徑應是尚不存在的新檔；重跑時換檔名，不為了示範刪除原證據。CLI 只建構已知 policy 的 ArenaDefinition，不從 JSON 載入任意 C# 類別或 oracle。

CLI 的 capture 範例以致死 Attack 錄製 8 ticks，涵蓋 seeded 重生；capture-failure 在 tick 2 觸發 training oracle。這是同一 Definition 的兩個 scenario，不是另一套錄製用 gameplay。成功／符合證據回 exit 0，斷言、IO 或 divergence 回 1，錯誤用法／selector 回 2；見 [CLI 來源](../../tools/arena-checks/Program.cs)。

recording reader 在反序列化前限制 bytes，limits 另限制 ticks、inputs 和 payload。合法 payload 的 JSON 仍有結構／escaping 開銷；預算不等於精準 heap 上限，長錄製可能遇到檔案讀取上限，應明確拒絕，不能靜默截斷。Runtime warning 不代表支援跨平台 bitwise 一致。

## 執行與反例

```powershell
dotnet run --project tools/arena-checks -- replay
```

預期包含正常錄製、JSON、不同播放 frame 排程、第一個差異、policy 拒絕及 invariant failure 重現。第 5 章的致死 Attack／seeded 重生也必須透過同一路徑驗證，不能只驗證直線移動。

反例：只改錄製中的某個 input payload，保留原始預期 hash／results，應得到 Diverged。不要同時重新計算預期證據，否則只是驗證新的遊戲，不是在驗證原錄製。

下一章把手動 session 接上可組裝 realtime runner，仍保留這整套 recording／hash／invariant 流程，而不是另做只能在 Unity 玩的 loop。
