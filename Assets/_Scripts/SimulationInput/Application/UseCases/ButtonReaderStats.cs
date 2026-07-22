using System;
using System.Collections.Generic;
using SimulationInput.Domain;

namespace SimulationInput.Application
{
    internal sealed class ApplicationStats
    {
        internal readonly Dictionary<Type, int> buttonReaderIndexByKey = new();
        internal readonly Dictionary<Type, int> axisReaderIndexByKey = new();
        internal readonly Dictionary<Type, int> inputCommandIndexByKey = new();
        internal readonly List<IButtonStatePuller> buttonStatePullers = new();
        internal readonly List<IAxisStatePuller> axisStatePullers = new();
        // This list defines command production order. Dictionaries are only used for typed lookup.
        internal readonly List<IRegisteredInputCommandBuilder> inputCommandBuilders = new();
        internal readonly List<ButtonStateReader> buttonStateReader = new();
        internal readonly List<AxisStateReader> axisStateReader = new();
        internal TickInputFrame reusableFrame;
        internal TickInputFrameCommands reusableCommands;

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
                new ButtonInputEvent[buttonStateReader.Count],
                new AxisInputEvent[axisStateReader.Count]);

            IInputCommand[] commandBuffer = new IInputCommand[inputCommandBuilders.Count];
            for (int i = 0; i < inputCommandBuilders.Count; i++)
            {
                commandBuffer[i] = inputCommandBuilders[i].CreateCommand();
            }

            reusableCommands = new TickInputFrameCommands(
                inputCommandIndexByKey,
                commandBuffer);

            initialized = true;
        }

        internal void RegisterInputCommandBuilder<TCommand>(IInputCommandBuilder<TCommand> builder)
            where TCommand : class, IInputCommand
        {
            EnsureCanRegister();

            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            Type commandType = typeof(TCommand);
            if (inputCommandIndexByKey.ContainsKey(commandType))
            {
                throw new InvalidOperationException(
                    $"Input command {commandType.FullName} is already registered.");
            }

            int index = inputCommandBuilders.Count;
            inputCommandIndexByKey.Add(commandType, index);
            inputCommandBuilders.Add(new RegisteredInputCommandBuilder<TCommand>(builder));
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

        internal void EnsureTickInputFrameConsumed()
        {
            if (!hasCommittedTick)
            {
                throw new InvalidOperationException(
                    "Cannot consume input commands before consuming a tick input frame.");
            }
        }
    }

    internal interface IRegisteredInputCommandBuilder
    {
        IInputCommand CreateCommand();
        void UpdateCommand(TickInputFrame inputFrame, IInputCommand command);
    }

    internal sealed class RegisteredInputCommandBuilder<TCommand> : IRegisteredInputCommandBuilder
        where TCommand : class, IInputCommand
    {
        private readonly IInputCommandBuilder<TCommand> builder;

        internal RegisteredInputCommandBuilder(IInputCommandBuilder<TCommand> builder)
        {
            this.builder = builder;
        }

        public IInputCommand CreateCommand()
        {
            TCommand command = builder.CreateCommand();
            if (command == null)
            {
                throw new InvalidOperationException(
                    $"Input command builder {builder.GetType().FullName} returned null.");
            }

            return command;
        }

        public void UpdateCommand(TickInputFrame inputFrame, IInputCommand command)
        {
            builder.UpdateCommand(inputFrame, (TCommand)command);
        }
    }
}
