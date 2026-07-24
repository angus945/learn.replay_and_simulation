using System;
using System.Collections.Generic;
using SimulationCore.ExternalCommands.PlayerInput.Domain;

namespace SimulationCore.ExternalCommands.PlayerInput.Application
{
    internal sealed class InputStats
    {
        internal readonly Dictionary<Type, int> buttonReaderIndexByKey = new();
        internal readonly Dictionary<Type, int> axisReaderIndexByKey = new();
        internal readonly List<ButtonStateReader> buttonStateReader = new();
        internal readonly List<AxisStateReader> axisStateReader = new();
        internal TickInputFrame reusableFrame;
        internal FrameSnapShot snapshot;

        internal void Initialize()
        {
            reusableFrame = new TickInputFrame(
                buttonReaderIndexByKey,
                axisReaderIndexByKey,
                new ButtonInputState[buttonStateReader.Count],
                new AxisInputEvent[axisStateReader.Count]);

            snapshot = new FrameSnapShot(reusableFrame);
        }

        internal void AddButtonStateReader(Type type, int index)
        {
            buttonReaderIndexByKey.Add(type, index);
            buttonStateReader.Add(new ButtonStateReader());
        }

        internal void AddAxisStateReader(Type type, int index)
        {
            axisReaderIndexByKey.Add(type, index);
            axisStateReader.Add(new AxisStateReader());
        }

        internal void CaptureRawButtonState(int i, bool isPressed)
        {
            buttonStateReader[i].CaptureRawState(isPressed);
        }

        internal void CaptureRawAxisState(int i, float value)
        {
            axisStateReader[i].CaptureRawState(value);
        }

    }
}
