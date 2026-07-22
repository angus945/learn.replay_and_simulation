using SimulationInput;

namespace SimulationInput.Domain
{
    public sealed class ButtonStateReader
    {
        private bool currentState;

        private bool pressedPending;
        private bool releasedPending;

        /// <summary>
        /// 在 Render/Input Update 呼叫。
        /// 收集狀態改變，但不立即提交給 Simulation Tick。
        /// </summary>
        public void CaptureRawState(bool isPressed)
        {
            bool nextState = isPressed;

            if (nextState == currentState)
                return;

            if (nextState)
            {
                pressedPending = true;
            }
            else
            {
                releasedPending = true;
            }

            currentState = nextState;
        }

        /// <summary>
        /// 每個 Simulation Tick 呼叫一次。
        /// Pressed / Released 在提交後被消耗。
        /// Down 狀態則會持續存在。
        /// </summary>
        public ButtonInputEvent ConsumeTickInput()
        {
            ButtonInputEvent result = new ButtonInputEvent(
                isPressed: pressedPending,
                isDown: currentState,
                isReleased: releasedPending
            );

            pressedPending = false;
            releasedPending = false;

            return result;
        }
    }
}
