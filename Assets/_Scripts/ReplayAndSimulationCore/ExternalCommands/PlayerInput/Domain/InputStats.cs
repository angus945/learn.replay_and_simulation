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

        public bool isInitialized { get; private set; }

        internal void Initialize()
        {
            if (isInitialized)
            {
                throw new InvalidOperationException("InputStats has already been initialized.");
            }

            reusableFrame = new TickInputFrame(
                buttonReaderIndexByKey,
                axisReaderIndexByKey,
                new ButtonInputState[buttonStateReader.Count],
                new AxisInputEvent[axisStateReader.Count]);

            snapshot = new FrameSnapShot(reusableFrame);

            isInitialized = true;
        }

        internal void AddButtonStateReader(Type type, int index)
        {
            if (isInitialized)
            {
                throw new InvalidOperationException("Cannot add button state reader after InputStats has been initialized.");
            }

            buttonReaderIndexByKey.Add(type, index);
            buttonStateReader.Add(new ButtonStateReader());
        }

        internal void AddAxisStateReader(Type type, int index)
        {
            if (isInitialized)
            {
                throw new InvalidOperationException("Cannot add axis state reader after InputStats has been initialized.");
            }

            axisReaderIndexByKey.Add(type, index);
            axisStateReader.Add(new AxisStateReader());
        }

        internal void CaptureRawButtonState(int i, bool isPressed)
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("InputStats has not been initialized. Call Initialize() before capturing input.");
            }

            buttonStateReader[i].CaptureRawState(isPressed);
        }

        internal void CaptureRawAxisState(int i, float value)
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("InputStats has not been initialized. Call Initialize() before capturing input.");
            }

            axisStateReader[i].CaptureRawState(value);
        }

    }
}
