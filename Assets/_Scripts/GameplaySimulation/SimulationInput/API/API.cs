using SimulationInput.Contract;

namespace SimulationInput.API
{
    public interface ISimulationInputRuntime
    {
        void CaptureRenderInput();
        IInputSnapshot ConsumeSnapshot(ulong tick);
    }
    public interface IInputSnapshot
    {
        ButtonState GetButtonState<TKey>() where TKey : IButtonInputKey;
        AxisState GetAxisState<TKey>() where TKey : IAxisInputKey;
    }
}