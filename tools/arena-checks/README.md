# Arena executable chapters

```powershell
dotnet run --project tools/arena-checks -- all
dotnet run --project tools/arena-checks -- lifecycle
dotnet run --project tools/arena-checks -- capture example.json
dotnet run --project tools/arena-checks -- rerun example.json
dotnet run --project tools/arena-checks -- capture-failure failure.json
dotnet run --project tools/arena-checks -- rerun failure.json
```

章節 selectors：domain、application、simulation、input、lifecycle、observation、diagnostics、replay、realtime。
預設 all。錯誤 selector 回傳 2；斷言或 replay divergence 回傳 1；驗證成功回傳 0。
錄製檔使用 CreateNew，不會覆寫既有檔案；failure 是明確 opt-in 的教學 oracle policy。

所有遊戲使用正式分層 assemblies；此 host 只編譯共享驗收程式，不複製 gameplay。九行 PASS 是九組章節檢查，不是 NUnit 案例總數。
從 [完整教材](../../docs/arena-guide/README.md) 開始閱讀；框架自己的檢查另在 [framework-checks](../framework-checks/README.md)。
