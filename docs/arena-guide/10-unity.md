# 10 — Unity：輸入、畫面與播放接回同一核心

[上一章：Realtime](09-realtime.md) · [教材索引](README.md) · [能力與驗收清單](capabilities.md)

本章問題：如何讓真正玩家使用前九章的同一套入口，並把資料顯示出來，而不讓 Transform、HUD 或播放按鈕變成另一個遊戲世界？

Unity 是最外層 host：讀鍵盤、傳 frame 時間、綁定 snapshot views、提供 recording／Replay UI。它不決定權威狀態。

## 先看唯一場景與組裝入口

開啟 [ArenaDemo.unity](../../Assets/game/arena/scenes/ArenaDemo.unity)。場景建立邏輯在 [ArenaSceneBuilder](../../Assets/game/arena/src/Editor/ArenaSceneBuilder.cs)，正式 host 是 [ArenaHost](../../Assets/game/arena/src/Unity/ArenaHost.cs)。

- 場景提供 camera、參考格線、玩家／敵人 view templates 與 tick rate。
- Host.Initialize 建立 ArenaLiveSession、ArenaActorPresentation，先 Snap 初始 observation，再把 live.Diagnostics 交給面板。
- 這是受信任組裝。沒有在 Scene 放另一個持有 Health、Position 或 respawn timer 的遊戲 MonoBehaviour。
- 新場景應透過 Unity Editor／builder 建立與保存，不手寫 YAML 來猜 metadata 或物件引用。

若場景尚不存在，可在非 Play 且已保存開啟場景後使用 `Tools > Arena > Create Demo Scene`；已有 ArenaDemo 時 builder 只開啟，不覆寫其手工變更。`Tools > Arena > Build Windows Player` 是獨立 build 入口，不代表僅閱讀本頁就已執行 build。

場景、測試和 Player build 是獨立驗收層；本頁不以 headless 執行結果宣稱它們已通過。

## 接點一：Host 只交出輸入與時間

ArenaHost 使用 Input System 的 Keyboard.current。每次 Update 讀 WASD／方向鍵和 Space，失焦、無鍵盤、輸入路徑文字或 pause 時不送玩家按鍵。

純粹的轉交形狀如下，`host` 是已 Initialize 的 ArenaHost，這也是 integration test 可呼叫的公開入口：

```csharp
host.CaptureControls(1f, 0f, false);
host.AdvanceFrame(.5f);
host.RenderFrame();
```

CaptureControls 只交給第 9 章的 buffer adapter；AdvanceFrame 在 live 模式只推進 ArenaLiveSession，在 replay 模式只推進 TemplateReplay。不要在 FixedUpdate 再呼叫一次 Step，也不要在 Update 改 Actor.Position。

Host 捕捉 input／presentation adapter exception，保存第一次 AdapterFailure 並停止後續 frame 驅動。畫面錯誤不是普通 Attack rejection，也不應假裝成已成功重現的 domain failure。

## 接點二：由 observation 產生 view poses

[ArenaActorPresentation](../../Assets/game/arena/src/Unity/ArenaActorPresentation.cs) 實作 IActorPoseSource，將 ActorSnapshot 轉成 ActorPose，再交給 `UnityActorPresentation` 與 `UnityActorPool`。

它的責任是：

- 用 actor ID 對應 view，不用 Actors[0] 猜玩家；玩家身分來自 PlayerId。
- player／enemy 對應不同 view archetype；pool 擁有 instance 的生成、重用與清理。
- Present(previous, current, alpha) 使用真實前後 tick snapshot 插值。
- catch-up 時補上真正倒數第二 tick；非連續 tick 或跨 session 直接 Snap。
- committed observation 不再包含死者時解除 view；新 ID 出生時 snap，不從舊 instance 的位置滑過來。

Game ActorId、simulation registry handle、Unity pooled instance generation 是不同邊界。view adapter 可以把 game ID 映射成 pose ID，但不把 pool slot 當遊戲角色身分。

場景拖動 view 的 Transform，下次 Render 會重新套 observation。這是正確的方向：傳送角色應新增正式 game action，而不是把拖動後的 Transform 讀回 Domain。

## 接點三：診斷面板只接 reader

[ArenaDiagnosticsPanel](../../Assets/game/arena/src/Unity/ArenaDiagnosticsPanel.cs) 的 constructor 只接 `IDiagnosticReader<ArenaObservation>`，沒有 session、Admin 或 gameplay port。

面板顯示 observation、invariant report、fault 與 cursor trace。它不重跑 checks，不執行 Step。來源 overwrite、漏讀與本地 history 淘汰分開顯示；讀不到資料不是綠色成功。

這是第 7 章唯讀 consumer 的真實用途：不同 UI 可以替換，但不需要改 Domain，也不能透過輪詢改變錄製結果。

## 接點四：錄製與 Replay 明確切換 session

[ArenaReplayControls](../../Assets/game/arena/src/Unity/ArenaReplayControls.cs) 是 ArenaHost 的另一個 partial 檔案；UI 行為順序是：

1. Live 中以 WASD／方向鍵移動，Space 攻擊。所有輸入由 tick buffer → Gameplay.Submit 保存。
2. 按 Save recording，檔案寫到 `Application.persistentDataPath/ArenaRecordings/arena-<UTC>-<GUID>.json`，CreateNew 不覆寫。
3. 按 Load path，讀 TemplateRecording，按明確已知 policy 選 Definition，建立獨立 replay session。
4. pause 原 live、清輸入、Snap replay 初始狀態，並重新 bind replay.Diagnostics。
5. Play／Pause／Step +1／Restart replay 操作播放世界。Restart 後取得新 Diagnostics reader，不能保留舊 session facade。
6. Return live Dispose replay、恢復原 live 的 pause 狀態／時間權威、清輸入、Snap live observation，重新 bind live.Diagnostics。

播放期間 live tick 不前進；Return live 不把 replay state 寫回 live。兩個 session 可以同時存在，但每個 world 都是獨立的，沒有共用可變 Actor。

正常 policy 與教材 training oracle 是明確允許的兩種組裝。未知 policy 不動態載入任意程式，也不假裝相容。已知 oracle recording 的 ReproducedFailure 是預期結果；它與正常 Completed 分開顯示。

## 操作與逐項驗收

先執行純 C#：

```powershell
dotnet run --project tools/arena-checks -- all
```

再在 Unity 驗證：

- 編譯沒有 missing script／assembly reference 錯誤，場景引用正確。
- 正常移動、斜向限速、Space 按下只攻擊一次；長 frame 不重複消耗同一 edge。
- 敵人死亡 view 消失，到期以新 actor ID 出生；view pool 重用不混淆舊身分。
- Pause／Step／Restart 與 Return live 後畫面 snap 正確，live tick 未被 replay 偷推進。
- 多次 diagnostics polling 不改 tick/hash；trace gap 明確顯示。
- 正常 recording 完整重播；已知 oracle 故障重現；未知 policy 拒絕。
- Player build 使用 ArenaDemo 為 entry，並獨立檢查啟動／例外。Editor 可玩不等於 Player 已驗證。

相關自動化來源在 [ArenaPresentationTests](../../Assets/game/arena/tests/PlayMode/ArenaPresentationTests.cs)；執行數字以當次報告為準，不以檔案存在推定通過。

## Physics 不是為了填滿 phase 而啟用

Arena 權威位置是純 C#，沒有 Rigidbody authority 或碰撞傷害。Framework.DeterministicSimulation.Unity 的 local physics sensors 是選配，且有自己獨立的測試。

若未來真的加入感測規則，先定義 facts 怎樣正規化、在哪個 phase 回到 Application、哪些外部結果須錄製，才註冊 adapter。不要因為 pipeline 有 Physics phase，就宣稱本遊戲已把所有 Unity 物理變成決定性。

## 最後練習：沿同一條接線擴充

選一個小規則，例如新增「必須付出資源的技能」，按這個順序規劃，不要先從 HUD 寫起：

1. Domain：資源由哪個 aggregate 擁有？拒絕時是否維持原狀態？
2. Application：新增 request／結果／facts；不引用 frame、trace 或 framework context。
3. Integration：新增 payload mapping、必要反應；界定 input、movement、commit 的順序。
4. Observation／canonical state：所有影響未來的狀態是否完整？PolicyId 是否需更新？
5. Diagnostics：哪些是 Domain 自己保護的 invariant，哪些是 post-tick 整合 oracle？
6. Replay：成功、拒絕、失敗在 JSON round trip 與不同 frame schedules 下能否重現？
7. Unity：只新增輸入／顯示，透過相同 Submit，不另扣一次資源。

若能按此順序完成，而不修改 framework 來認識技能或玩家，就已掌握這份教材的目的：DDD／Clean Architecture 保持內層規則清楚，兩個 framework 提供外圍一致的執行、控制與證據流程。
