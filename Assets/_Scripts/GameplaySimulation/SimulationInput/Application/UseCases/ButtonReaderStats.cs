using System;
using System.Collections.Generic;
using Logging.API;
using SimulationInput.Domain;

namespace SimulationInput.Application
{
    internal sealed class ApplicationStats
    {
        internal const string LogCategory = "SimulationInput";

        internal readonly ILogger logger;
        internal readonly Dictionary<Type, int> buttonReaderIndexByKey = new();
        internal readonly Dictionary<Type, int> axisReaderIndexByKey = new();
        internal readonly List<Type> buttonKeyTypes = new();
        internal readonly List<Type> axisKeyTypes = new();
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

        internal ApplicationStats(ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        internal void Initialize()
        {
            if (initialized)
            {
                logger.Warning(
                    "Initialize rejected. Simulation input is already initialized.",
                    LogCategory);

                throw new InvalidOperationException("Simulation input is already initialized.");
            }

            reusableFrame = new TickInputFrame(
                buttonReaderIndexByKey,
                axisReaderIndexByKey,
                new ButtonInputState[buttonStateReader.Count],
                new AxisInputEvent[axisStateReader.Count]);

            snapshot = new FrameSnapShot(reusableFrame);

            initialized = true;

            logger.Info(
                $"Initialized simulation input. Buttons: {buttonStateReader.Count}, axes: {axisStateReader.Count}.",
                LogCategory);
        }

        internal void EnsureCanRegister()
        {
            if (initialized)
            {
                logger.Warning(
                    "Registration rejected. Simulation input is already initialized.",
                    LogCategory);

                throw new InvalidOperationException(
                    "Cannot register input pullers after simulation input is initialized.");
            }
        }

        internal void EnsureInitialized()
        {
            if (!initialized)
            {
                logger.Warning(
                    "Operation rejected. Simulation input is not initialized.",
                    LogCategory);

                throw new InvalidOperationException("Simulation input is not initialized.");
            }
        }

        internal string GetButtonKeyName(int index)
        {
            return GetKeyName(buttonKeyTypes, index, "Button");
        }

        internal string GetAxisKeyName(int index)
        {
            return GetKeyName(axisKeyTypes, index, "Axis");
        }

        private static string GetKeyName(List<Type> keyTypes, int index, string fallbackPrefix)
        {
            if ((uint)index < (uint)keyTypes.Count)
                return keyTypes[index].FullName ?? keyTypes[index].Name;

            return $"{fallbackPrefix}[{index}]";
        }

    }
}
