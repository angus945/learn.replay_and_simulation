# Arena UI Toolkit 驗證 — 2026-08-30

[Unity 接線教材](../arena-guide/10-unity.md) · [時間與量測](../arena-guide/09-realtime.md)

本次替換 Arena 的 IMGUI 呈現；沒有更換 Domain、Application、framework tick pipeline 或 Replay 比對規則。場景既有的 static flags 修改原樣保留。

## 結果摘要

- 原本 160 筆 trace 的 Editor 畫面約 43–45 FPS；相同解析度、暫停輪詢後的新畫面約 60 FPS。
- 新 ListView 保留 160 筆資料，但此次 viewport 只建立 13 個列元素。清空畫面資料再恢復 160 筆，沒有重現舊 UI 的明顯掉幀。
- 正常開啟約 10 Hz 輪詢時，新畫面量到 58.18 FPS、59.98 tick/s。
- 原本量測的 simulation 本來也接近 60 tick/s。因此這是已證實的畫面負擔改善，不是「修好了所有 tick rate 問題」的證明。

## 這次實際改動

- `ArenaHost` 不再有 `OnGUI`。`ArenaHudView` 擁有 UIDocument、PanelSettings 複本與一次建立的 UI 樹。
- `ArenaDiagnosticsPanel` 是純唯讀 presenter，只接 IDiagnosticReader，提供快取字串、revision 與固定的 read-only IList。
- 歷史上限 160；ListView 用 42 高度虛擬化。新 record 才格式化，trace revision 改變且可見才 RefreshItems，不逐 frame Rebuild。
- 隱藏停止自動輪詢；重新顯示沿 cursor 續讀並報 gap。明確呼叫 Poll 仍可讀取。
- 點選 trace 顯示完整、固定的 selected evidence，列回收或 history 淘汰不會改掉已選內容。runtime 不依靠原生 tooltip。
- Actor view 對照快取，角色標籤重用；失焦後重複 ClearInput 不持續重建乾淨的 buffer。
- FPS／tick/s 每至少 .5 秒採樣 wall clock。live debt 是 runner 的 PendingSeconds，包含不足一 tick 的餘數；Replay 時傳 0，不代表量測 Replay accumulator。

## 效能方法與原始資料

環境：Windows、Unity 6000.3.20f1、同一個 ArenaDemo、Editor Game view 1050 × 824、Profiler 開啟、Deep Profile 關閉、vSync 0、targetFrameRate -1、timeScale 1、maximumDeltaTime 約 .3333 秒。

每個樣本區間約 5 秒。透過 EditorApplication.update，每個不同 Time.frameCount 記一次 unscaledDeltaTime、live tick 增量與 ProfilerRecorder；區間間有暖機等待。程式不在採樣中逐 frame 寫 log，量測結束釋放 recorder 並恢復輪詢／itemsSource。

- [舊 IMGUI 原始資料](arena-ui-imgui-baseline-2026-08-30.json)：在修改 UI 前取得，停止 diagnostics polling，只變更歷史列數 160 → 0 → 160。
- [新 UI Toolkit 原始資料](arena-ui-toolkit-profile-2026-08-30.json)：停止 polling，ListView itemsSource 160 → 0 → 160；最後一段恢復正常 polling。第三方 Editor 工作與作業系統負載並未完全隔離。

可比的「停止 polling、160 列」樣本：

- 舊第一段：42.93 FPS；60.11 tick/s；frame P95 32.90 ms；OnGUI 平均 6.89 ms；整幀 GC allocation 平均 484,640 bytes。
- 舊第三段：44.73 FPS；59.91 tick/s；frame P95 30.14 ms；OnGUI 平均 6.66 ms；整幀 GC allocation 平均 477,774 bytes。
- 新第一段：59.87 FPS；60.07 tick/s；frame P95 19.95 ms；整幀 GC allocation 平均 60,792 bytes；13 個列元素。
- 新第三段：59.97 FPS；59.97 tick/s；frame P95 21.06 ms；整幀 GC allocation 平均 60,687 bytes；13 個列元素。

空清單的新第二段：60.11 FPS；整幀 GC allocation 平均 60,606 bytes。正常輪詢的新第四段：58.18 FPS；59.98 tick/s；P95 24.50 ms；整幀 GC allocation 平均 85,381 bytes。

新第一段的 runtime UI panel update／repaint marker 分別平均 .111／.286 ms；第四段分別 .135／1.016 ms。這些 marker 與舊 OnGUI 的涵蓋範圍不同，不直接當成同一個函式的加速倍率。LateUpdate 另含 actor presentation、輪詢、HUD 更新，原始資料保留其數值。

GC 數字是整幀，不是 UI 專屬，也不是 retained memory。來源包含 simulation recording、Editor 與量測本身。此短時間 Editor A/B/A 用來確認問題方向；不等同 Player benchmark、最低硬體承諾或長時間壓力測試。

## 自動化驗收

- Unity recompile：成功，0 compile errors。
- EditMode：124/124 passed。
- PlayMode：21/21 passed，包括既有 framework physics／Arena presentation 與新增 13 個 diagnostics／retained UI tests。
- Headless：`dotnet run --project Tools/arena-checks -- all` 九組全部 PASS；補強 ClearInput 後 `-- realtime` 再次 PASS。
- Architecture：33 assemblies 無依賴環、內圈限制正確、Unity／headless reference 對齊、explicit C# variable types 檢查通過。

新 UI 測試透過實際 UI Toolkit event target 與 callback 驗證，不直接呼叫 host 來假裝按鈕成功。涵蓋：

- 虛擬列數有界、控制項與 itemsSource 身分持續。
- 完整 trace detail 在 history 淘汰後仍保持原選取內容。
- Hide／Show、文字欄位焦點與編輯保留。
- Save／Load／Pause／Play／Step／Restart／Return live 的按鈕接線。
- UI 讀檔錯誤不摧毀既有 live／replay，不停止 host adapter。
- Dispose 解除 rows 綁定、釋放 UIDocument 與 cloned PanelSettings，保留共用資產。
- ClearInput 冪等、清除 held state、不吞掉下一次新 press；乾淨 buffer 仍拒絕跨執行緒操作。

## Player 與畫面

- Windows x64 Development Player：Succeeded，30.507 秒，0 errors、1 warning；產物為 `.utmp/ArenaRetainedPlayer/Arena.exe`。
- 唯一 build warning 是場景沒有 RuntimePipelineManager，因此 Player 不提供 Unity CLI runtime server。此次沒有為了驗證而新增遊戲內遠端控制元件。
- 啟動 smoke：啟動上述新產物，確認程序存活、完成 assemblies／圖形／輸入初始化，沒有 Arena exception 或 missing UI resource。log 有一行 D3D12 debug info queue 查詢失敗訊息；沒有把它隱藏成「完全無訊息」。只終止此次建立的 smoke process。
- Player smoke 不等於 Player UI 全流程或效能 benchmark；按鈕、focus、Replay 與虛擬化的完整自動化驗證是在 Unity PlayMode。
- Editor Game view 實際擷取並檢查 HUD、角色標籤、控制列與 trace viewport。以下是暖機後截圖；畫面上的瞬時採樣不是前述 5 秒區間統計。

![Arena UI Toolkit Editor 畫面](arena-ui-toolkit-2026-08-30.png)

## 邊界與後續判讀

Host 仍餵 `Time.deltaTime`，runner 每 frame 上限仍是 120 ticks；沒有改成背景執行緒或強制 wall-clock catch-up。Unity 截斷的 delta 沒進 runner，不能由 debt 追回。若仍遇到 tick/s 驟降，應另記錄 long frame、timeScale、maximumDeltaTime、runner fault／budget 與 Player profiler，不能只繼續替換 UI 系統。

本報告是新 UI 的當次驗證，舊的 Arena 重建驗收文件保留為歷史，不改写成新 UI 已測的證據。
