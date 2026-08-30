# 舊 Unity 資產封存

2026-08-30 由 Unity Editor 核對並封存；不是現行遊戲的依賴。

| 原路徑 | Editor 確認的 Missing Script 數 |
| --- | ---: |
| `Assets/Scenes/SampleScene.unity` | 1 |
| `Assets/Prefab/Player.prefab` | 2 |
| `Assets/Prefab/Coin.prefab` | 1 |
| `Assets/Prefab/Enemy.prefab` | 1 |

此處保留原本 `Assets/` 相對路徑、原檔與 `.meta`。移除匯入區前已逐位元比對複本；未修改序列化內容。正式 Build Settings 改用 `Assets/game/movement-demo/scenes/CharacterMovementDemo.unity`。

若需研究歷史，請在另開的工作副本把檔案與 `.meta` 複製回原路徑；這只恢復資產，無法恢復已不存在的腳本實作。不要直接將本目錄搬進正式 Assets。歷史評估中的舊路徑可在本封存區對照。
