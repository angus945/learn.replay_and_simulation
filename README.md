# Replay and Simulation

以純 C# domain 與固定 tick simulation 為核心的 Unity 遊戲架構參考專案。玩家操作、測試與 Replay 共用正式玩法路徑。

## 開始閱讀

- **[DDD 遊戲框架開發指引](docs/framework-guide/README.md)**：入門、可執行範例、組裝、契約、食譜與驗證檢查表。
- [Module 命名對照](docs/module-naming.md)。
- [Unity Movement Demo](Assets/game/movement-demo/README.md)。
- [Gameplay Simulation](Assets/game/gameplay-simulation/README.md)。

## 無 Unity 的範例與回歸檢查

```powershell
dotnet run --project tools/gameplay-checks/Gameplay.Checks.csproj
```

需要 .NET 9 SDK 或相容 SDK。包含兩個指南範例及既有 gameplay／Replay 檢查；Unity 視覺與場景需另外驗證。
Protocol 目前僅為核心與 in-process adapter，沒有外部網路 listener；本專案不宣稱跨平台 bitwise determinism 或完整 snapshot restore。
