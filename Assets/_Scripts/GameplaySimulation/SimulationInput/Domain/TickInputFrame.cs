using System;
using System.Collections.Generic;

namespace SimulationInput.Domain
{
    /// <summary>
    /// Reusable per-tick input buffer. Do not keep this instance beyond the tick that produced it.
    /// </summary>
    public sealed class TickInputFrame
    {
        private readonly IReadOnlyDictionary<Type, int> buttonInputIndexByKey;
        private readonly IReadOnlyDictionary<Type, int> axisInputIndexByKey;

        public ulong Tick { get; private set; }
        public ButtonInputState[] Buttons { get; private set; }
        public AxisInputEvent[] Axes { get; private set; }

        public TickInputFrame(ulong tick, ButtonInputState[] buttons, AxisInputEvent[] axes) : this(new Dictionary<Type, int>(), new Dictionary<Type, int>(), buttons, axes)
        {
            Tick = tick;
        }

        internal TickInputFrame(IReadOnlyDictionary<Type, int> buttonInputIndexByKey, IReadOnlyDictionary<Type, int> axisInputIndexByKey, ButtonInputState[] buttons, AxisInputEvent[] axes)
        {
            this.buttonInputIndexByKey = buttonInputIndexByKey;
            this.axisInputIndexByKey = axisInputIndexByKey;
            Buttons = buttons ?? Array.Empty<ButtonInputState>();
            Axes = axes ?? Array.Empty<AxisInputEvent>();
        }

        internal void SetTick(ulong tick)
        {
            Tick = tick;
        }

        public ButtonInputState GetButton<TKey>()
        {
            Type keyType = typeof(TKey);
            if (!buttonInputIndexByKey.TryGetValue(keyType, out int index))
            {
                throw new KeyNotFoundException(
                    $"Button input key {keyType.FullName} is not registered.");
            }

            return Buttons[index];
        }

        public AxisInputEvent GetAxis<TKey>()
        {
            Type keyType = typeof(TKey);
            if (!axisInputIndexByKey.TryGetValue(keyType, out int index))
            {
                throw new KeyNotFoundException(
                    $"Axis input key {keyType.FullName} is not registered.");
            }

            return Axes[index];
        }

        public bool TryGetButton<TKey>(out ButtonInputState input)
        {
            if (buttonInputIndexByKey.TryGetValue(typeof(TKey), out int index))
            {
                input = Buttons[index];
                return true;
            }

            input = default;
            return false;
        }

        public bool TryGetAxis<TKey>(out AxisInputEvent input)
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

}
