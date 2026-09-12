# Framework-only checks

```powershell
dotnet run --project tools/framework-checks
```

保留 deterministic-simulation framework 契約，並直接驗證 runtime-observation、runtime-control、testability-oracles、testability-evidence 四個被動 module。此工具不引用任何 Game assembly，也不需要 Unity。
四行 PASS 代表 framework core、session/realtime ownership、四個 module，以及非 simulation／明確非同步 host 四組檢查入口，不是 NUnit 測試案例總數。完整 Unity 測試仍由 Test Runner 執行。

建置參照位於 `tools/arena-build`；每個框架/module 編譯成獨立 netstandard2.1 assembly，沒有合併來源來繞過 assembly 邊界。
