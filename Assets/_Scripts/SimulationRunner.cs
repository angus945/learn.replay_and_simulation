using UnityEngine;
using UnityEngine.InputSystem;

public sealed class SimulationRunner : MonoBehaviour
{
    const float TickRate = 60f;
    const float TickDeltaTime = 1f / TickRate;

    [SerializeField] float timeScale = 1f;

    double accumulator;
    ulong tick;

    private void Awake()
    {
        Physics.simulationMode = SimulationMode.Script;

        // 避免 Transform 修改在不可控時間自動同步至 PhysX。
        // Physics.autoSyncTransforms = false;
    }

    private void Update()
    {
        accumulator += Time.unscaledDeltaTime * timeScale;

        while (accumulator >= TickDeltaTime)
        {
            ExecuteTick();
            accumulator -= TickDeltaTime;
        }

        // 只處理渲染插值，不修改 Simulation State。
        UpdatePresentation();
    }

    private void ExecuteTick()
    {
        // 1. 讀取此 Tick 已記錄的輸入
        // SimulationInput input = InputRecorder.GetInput(tick);

        // 2. 執行遊戲邏輯、生成、銷毀、施力
        // 所有操作必須使用穩定順序
        // SimulationWorld.PrePhysicsTick(tick, input);

        // 3. 若有直接修改 Transform，明確同步
        Physics.SyncTransforms();

        // 4. 推進一次 Unity Physics
        Physics.Simulate(TickDeltaTime);

        // 5. 處理碰撞結果與遊戲狀態
        // SimulationWorld.PostPhysicsTick(tick);

        // 6. 計算 Hash 或儲存 Snapshot
        // ReplayRecorder.RecordTick(tick);

        tick++;
    }

    private void UpdatePresentation()
    {
        // Camera、動畫、粒子、畫面插值等非模擬內容。
    }
}