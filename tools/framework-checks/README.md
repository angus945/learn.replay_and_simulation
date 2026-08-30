# Framework-only checks

```powershell
dotnet run --project tools/framework-checks
```

保留原本混在 gameplay 工具中的 framework/module 契約檢查。此工具不引用任何 Game assembly，也不需要 Unity。
三行 PASS 代表三組檢查入口，不是 NUnit 測試案例總數。完整 native／Unity 測試仍由 Test Runner 執行。

建置參照位於 `tools/arena-build`；每個框架/module 編譯成獨立 netstandard2.1 assembly，沒有合併來源來繞過 assembly 邊界。
