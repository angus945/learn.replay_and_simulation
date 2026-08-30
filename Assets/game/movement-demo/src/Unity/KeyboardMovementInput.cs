using UnityEngine.InputSystem;

namespace MovementDemo.Unity
{
    public static class KeyboardMovementInput
    {
        public static void Capture(Keyboard keyboard, bool focused, MovementDemoSession session)
        {
            float x = 0f, y = 0f;
            if (focused && keyboard != null)
            {
                x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
                y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
            }
            session.CaptureAxes(x, y);
            session.CaptureAttackButton(focused && keyboard != null && keyboard.spaceKey.isPressed);
        }
    }
}
