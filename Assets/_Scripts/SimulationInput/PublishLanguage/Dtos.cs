using System;
using System.Collections.Generic;

namespace SimulationInput
{
    /// <summary>
    /// Reusable per-tick input buffer. Do not keep this instance beyond the tick that produced it.
    /// </summary>
    public sealed class TickInputFrame
    {
        private readonly IReadOnlyDictionary<Type, int> buttonInputIndexByKey;
        private readonly IReadOnlyDictionary<Type, int> axisInputIndexByKey;

        public ulong Tick { get; private set; }
        public ButtonInputEvent[] Buttons { get; private set; }
        public AxisInputEvent[] Axes { get; private set; }

        public TickInputFrame(ulong tick, ButtonInputEvent[] buttons, AxisInputEvent[] axes) : this(new Dictionary<Type, int>(), new Dictionary<Type, int>(), buttons, axes)
        {
            Tick = tick;
        }

        internal TickInputFrame(IReadOnlyDictionary<Type, int> buttonInputIndexByKey, IReadOnlyDictionary<Type, int> axisInputIndexByKey, ButtonInputEvent[] buttons, AxisInputEvent[] axes)
        {
            this.buttonInputIndexByKey = buttonInputIndexByKey;
            this.axisInputIndexByKey = axisInputIndexByKey;
            Buttons = buttons ?? Array.Empty<ButtonInputEvent>();
            Axes = axes ?? Array.Empty<AxisInputEvent>();
        }

        internal void SetTick(ulong tick)
        {
            Tick = tick;
        }

        public ButtonInputEvent GetButton<TKey>() where TKey : IButtonKey
        {
            Type keyType = typeof(TKey);
            if (!buttonInputIndexByKey.TryGetValue(keyType, out int index))
            {
                throw new KeyNotFoundException(
                    $"Button input key {keyType.FullName} is not registered.");
            }

            return Buttons[index];
        }

        public AxisInputEvent GetAxis<TKey>() where TKey : IAxisKey
        {
            Type keyType = typeof(TKey);
            if (!axisInputIndexByKey.TryGetValue(keyType, out int index))
            {
                throw new KeyNotFoundException(
                    $"Axis input key {keyType.FullName} is not registered.");
            }

            return Axes[index];
        }

        public bool TryGetButton<TKey>(out ButtonInputEvent input) where TKey : IButtonKey
        {
            if (buttonInputIndexByKey.TryGetValue(typeof(TKey), out int index))
            {
                input = Buttons[index];
                return true;
            }

            input = default;
            return false;
        }

        public bool TryGetAxis<TKey>(out AxisInputEvent input) where TKey : IAxisKey
        {
            if (axisInputIndexByKey.TryGetValue(typeof(TKey), out int index))
            {
                input = Axes[index];
                return true;
            }

            input = default;
            return false;
        }
    }

    public readonly struct ButtonInputEvent
    {
        public readonly bool IsPressed;
        public readonly bool IsDown;
        public readonly bool IsReleased;

        public ButtonInputEvent(bool isPressed, bool isDown, bool isReleased)
        {
            IsPressed = isPressed;
            IsDown = isDown;
            IsReleased = isReleased;
        }
    }

    public readonly struct AxisInputEvent
    {
        public readonly float Value;

        public AxisInputEvent(float value)
        {
            Value = value;
        }
    }

    /// <summary>
    /// Reusable per-tick input command buffer. Do not keep this instance beyond the tick that produced it.
    /// </summary>
    public sealed class TickInputFrameCommands
    {
        private readonly IReadOnlyDictionary<Type, int> commandIndexByKey;

        public ulong Tick { get; private set; }
        /// <summary>
        /// Commands in input command builder registration order.
        /// </summary>
        public IInputCommand[] Commands { get; }

        public TickInputFrameCommands(ulong tick, IInputCommand[] commands)
            : this(new Dictionary<Type, int>(), commands)
        {
            Tick = tick;
        }

        internal TickInputFrameCommands(
            IReadOnlyDictionary<Type, int> commandIndexByKey,
            IInputCommand[] commands)
        {
            this.commandIndexByKey = commandIndexByKey;
            Commands = commands ?? Array.Empty<IInputCommand>();
        }

        internal void SetTick(ulong tick)
        {
            Tick = tick;
        }

        public TCommand Get<TCommand>() where TCommand : IInputCommand
        {
            Type commandType = typeof(TCommand);
            if (!commandIndexByKey.TryGetValue(commandType, out int index))
            {
                throw new KeyNotFoundException(
                    $"Input command {commandType.FullName} is not registered.");
            }

            return (TCommand)Commands[index];
        }

        public bool TryGet<TCommand>(out TCommand command) where TCommand : IInputCommand
        {
            if (commandIndexByKey.TryGetValue(typeof(TCommand), out int index))
            {
                command = (TCommand)Commands[index];
                return true;
            }

            command = default;
            return false;
        }
    }
}
