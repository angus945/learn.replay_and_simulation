# Unity simulation adapters

這個 assembly 補上 Unity instance、呈現與受控 physics facts，不接管遊戲規則。它只依賴 simulation framework 與穩定 object identity；不依賴任何 Game assembly、ECS World 或 Testability。

## 多物件呈現

1. 建立 `UnityActorPool`，用 `RegisterPrefab(archetype, prefab, capacity)` 設定有界容量，然後 `Seal()`。預先建立的 instances 依 archetype 排序配置 slot。
2. 專案實作 `IActorPoseSource.ReadPoses()`，從邏輯 observation 映射出所有 active object 的 `ActorPose`；回傳的是完整集合，缺席的 ID 代表 despawn。
3. 建立 `UnityActorPresentation(pool, source)`，在 Definition 的 `SimulationBuilder.RegisterPresentationParticipant` 註冊，或由既有 realtime presentation adapter 呼叫它的 `CaptureTickState`／`Render`。
4. Session 結束時 Dispose pool；Reset 建立新的 pool 或先以空 snapshot 清除舊 bindings。切換 Session／Replay 或還原狀態時呼叫 `SnapToCurrent(context)`，不能只依 tick gap 判斷，因為不同 Session 的 tick 也可能剛好相鄰。

`ActorPose` 的 archetype 是 prefab 選擇鍵，不是玩家／敵人規則。Object ID、domain ID、`InstanceHandle(Slot, Generation)` 是不同身份；不要用相同 array index 對齊。Pool 每次釋放 instance 會增加 generation；舊 handle 不可讀到重用後的 object。Object ID 排序決定 bind 順序；配置容量不足、重複 ID、未知 prefab 或 active ID 改 archetype 都明確失敗。

Presentation 複製並驗證來源 snapshots，插值 position 與 rotation。新出生 object 直接顯示出生 pose；不連續 tick（例如 Reset／Replay restart）會 snap。它不讀 Transform 作為遊戲狀態。Prefab 應是被動 view，不能在 Update／OnEnable 修改 Domain 或另行驅動 simulation。

## 獨立 3D sensor scene

這是可選的整合，不是讓 movement/combat 範例依賴 PhysX 的要求。

```csharp
UnityActorPool proxies = new UnityActorPool("Logical physics proxies");
proxies.RegisterPrefab(0, sensorPrefab, 32);
proxies.Seal();
LocalPhysicsParticipant physics = new LocalPhysicsParticipant(proxies, poseSource, factSink);
builder.RegisterPhysicsParticipant(physics);
```

`LocalPhysicsParticipant` 必須在 pool 第一次 bind 前建立。它擁有一個獨立的 3D physics scene，並接管傳入 proxy pool 的生命週期；Dispose 會先清理 pool，再要求 Unity 非同步卸載 scene。不要把同一 pool 交给兩個 physics participant。World cleanup/Host OnDestroy 必須 Dispose participant。

- 每 tick 開始先由 pose source 重新套用 logical poses，再手動模擬自己的 `PhysicsScene`。不修改全域 `Physics.simulationMode`，不推進 default physics scene。
- 支援 logical-authority sensors：Prefab 可包含 static collider 或 **kinematic Rigidbody**。Trigger 接觸至少一方應有 Rigidbody。Dynamic Rigidbody 會在接線時被拒絕，因為此版沒有 velocity、睡眠狀態、physics snapshot／restore 等權威状态。
- Callback 僅收集 facts；只有手動 Simulate 期間的 callbacks 有效。碰撞双方必須有目前有效、屬於同一 participant 的 binding；未綁定 world geometry、已 despawn object 的延遲 callback 不產生 gameplay fact。
- 每 tick 同一 unordered object pair／contact family（Trigger 或 Collision）最多一個 fact。Compound collider 可能在同 tick 回報 Enter 與 Stay；正規化一律讓 **Enter 優先於 Stay**，不依 callback 抵達順序。正規化先於容量檢查，單一 pair 的 Enter+Stay 不會錯算成兩個名額。結果依 object IDs 與 kind 排序，再於 Simulate 返回後呼叫 `IPhysicsFactSink.PublishPhysicsFacts`。
- Unity relay **只接收 Enter／Stay，不接收 CollisionExit／TriggerExit**。Exit callback 可能在 collider 重用後抵達，而且沒有攜帶原 binding generation，不能安全把它標為新 ID 的事件。Despawn 由生命週期 snapshot 表達；需要離開接觸的業務語義時，另以受控 overlap 狀態或保存完整 lifetime identity 的來源建模，不能假設這個 relay 提供可靠 Exit。
- Fact sink 是專案的 integration adapter：需要時把 fact 映射為內部 event/command。Unity callbacks 不直接執行遊戲規則。
- 唯一 facts 有容量上限。Unity callback 中只保存 overflow error，回到受控 Simulate 邊界才丟出，避免 Unity 吞掉 callback exception 後留下部分批次。
- Simulate 要求 increasing tick 與 Physics phase，拒絕重入；發生例外後停止接受下一 tick，必須重建。

PhysX 的 fact sequence **不保證跨平台 exact replay**。需要完全可重現的 logical tests 時，使用不依賴 physics 的 Domain 或可控的專案 fact provider；若要把 physics 結果當外部輸入保存，應另設正式錄製契約。本模組没有實作舊架構的 Apply/Capture TODO，也不提供 dynamic-body state readback、rollback、snapshot restore。

Scene 隔離依照 Unity 6.3 的 [LocalPhysicsMode.Physics3D](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SceneManagement.LocalPhysicsMode.Physics3D.html) 與 [PhysicsScene.Simulate](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/PhysicsScene.Simulate.html) API；固定 step 不等於跨機器物理決定性。

## 驗證

EditMode tests 驗證多 object ID 排序、容量拒絕前不破壞既有 bindings、slot/generation 重用、stale handle、snapshot 複本、spawn/despawn、rotation interpolation、tick gap snap 和 fact canonicalization。`PhysicsFactContractChecks` 不依賴 Unity native calls／NUnit，可在純 C# 驗證 Enter/Stay 兩種 callback 順序得到相同結果、family 區分、容量與不可變批次。

PlayMode tests 驗證獨立 physics scene 的 compound trigger callbacks 去重、default scene 不被手動推進、global mode 不變、呈現後重套 logical pose、容量超限從受控邊界失敗、Dispose 卸載 local scene。另驗證已接觸 instance 的 ID/generation 重用、遠處新 ID 不被舊 callback 錯標、非收集期間不發送 facts，並用實際 native callbacks 證明 unbound／不同 physics owner 的 collider 被拒絕。請由 Unity Test Runner 執行；普通 dotnet build 只能驗證編譯，不能執行 Unity native 行為。
