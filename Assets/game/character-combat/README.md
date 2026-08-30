# Character Combat

純 C# Health aggregate：正數 MaxHealth、Health 不低於零，overkill 只扣剩餘血量。
Domain 不依賴 Unity、Intent、registry 或 simulation phase。

近距離攻擊驗證、Movement 協調、訊息映射與死亡移除由 game/gameplay-simulation 的 Application/Integration composition 負責。
Domain 測試位於 tests；完整戰鬥與死亡流程測試位於 game/gameplay-simulation/tests。
