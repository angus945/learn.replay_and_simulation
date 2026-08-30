# Character Movement

專案自己的 Movement bounded context，不是共用 module。

- `src/Domain`：Character aggregate、CharacterId、位置與方向值物件；不依賴 Unity 或 simulation interface。
- `src/Application/API`：Repository 呼叫介面。
- `src/Application/Runtime`：記憶體 Repository 與移動 application service。
- `src/Integration/Contract`：外部移動 Intent 與 presentation view port。
- `src/Integration/Runtime`：Tick Input → Intent、Intent handler、PrePhysics 更新、tick snapshot 插值。
- `tests`：Domain、application、presentation 邊界測試。

## 規則與保證

CharacterId 非零、Repository 不允許重複 ID，更新依 ID 遞增順序。
方向限制在單位圓內，保留類比輸入幅度，避免斜向加速。
速度與時間必須是有限非負值，位置與方向必須有限。
未知角色的移動請求會被 application 拒絕，不終止 simulation。
每個 tick 的 PrePhysics 更新位置；Unity Transform 只接收插值结果，不是權威狀態。
插值使用前後兩個 tick，刻意落後至多一個 tick；capture tick 不連續時直接 snap。

此切片沒有需要派送的 Internal Command 或 Domain Event，不以移動 Intent 混充另外兩種訊息。
未包含碰撞、物件生成銷毀、Replay、snapshot restore 或跨平台 bitwise determinism 保證。
