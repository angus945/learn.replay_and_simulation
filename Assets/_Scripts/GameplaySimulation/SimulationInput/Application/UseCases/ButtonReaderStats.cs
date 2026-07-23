using System;
using System.Collections.Generic;
using SimulationInput.Domain;

namespace SimulationInput.Application
{
    internal sealed class ApplicationStats
    {
        internal readonly Dictionary<Type, int> buttonReaderIndexByKey = new();
        internal readonly Dictionary<Type, int> axisReaderIndexByKey = new();
        internal readonly List<IButtonStatePuller> buttonStatePullers = new();
        internal readonly List<IAxisStatePuller> axisStatePullers = new();
        internal readonly List<ButtonStateReader> buttonStateReader = new();
        internal readonly List<AxisStateReader> axisStateReader = new();
        internal TickInputFrame reusableFrame;
        internal FrameSnapShot snapshot;

        internal readonly Dictionary<Type, int> commandAcquireIndexByType = new();

        internal bool initialized;
        internal bool hasCommittedTick;
        internal ulong lastCommittedTick;

        internal void Initialize()
        {
            if (initialized)
                throw new InvalidOperationException("Simulation input is already initialized.");

            reusableFrame = new TickInputFrame(
                buttonReaderIndexByKey,
                axisReaderIndexByKey,
                new ButtonInputState[buttonStateReader.Count],
                new AxisInputEvent[axisStateReader.Count]);

            snapshot = new FrameSnapShot(reusableFrame);

            initialized = true;
        }

        internal void EnsureCanRegister()
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "Cannot register input pullers after simulation input is initialized.");
            }
        }

        internal void EnsureInitialized()
        {
            if (!initialized)
                throw new InvalidOperationException("Simulation input is not initialized.");
        }



    }
}
