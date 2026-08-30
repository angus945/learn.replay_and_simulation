using System;
using System.Collections.Generic;

namespace TickInput.Contract
{
    public readonly struct ButtonInput
    {
        public ButtonInput(int id, bool pressed, bool down, bool released)
        {
            Id = id;
            Pressed = pressed;
            Down = down;
            Released = released;
        }

        public int Id { get; }
        public bool Pressed { get; }
        public bool Down { get; }
        public bool Released { get; }
    }

    public readonly struct AxisInput
    {
        public AxisInput(int id, float value)
        {
            Id = id;
            Value = value;
        }

        public int Id { get; }
        public float Value { get; }
    }

    /// <summary>
    /// Owned immutable snapshot, ordered by numeric input ID within each category.
    /// Edges mean "occurred at least once since the previous consume", not pulse counts.
    /// </summary>
    public sealed class TickInputFrame
    {
        internal TickInputFrame(ulong tick, ButtonInput[] buttons, AxisInput[] axes)
        {
            Tick = tick;
            Buttons = Array.AsReadOnly((ButtonInput[])buttons.Clone());
            Axes = Array.AsReadOnly((AxisInput[])axes.Clone());
        }

        public ulong Tick { get; }
        public IReadOnlyList<ButtonInput> Buttons { get; }
        public IReadOnlyList<AxisInput> Axes { get; }

        public ButtonInput GetButton(int id)
        {
            for (int i = 0; i < Buttons.Count; i++)
                if (Buttons[i].Id == id) return Buttons[i];
            throw new KeyNotFoundException($"Button {id} is not registered.");
        }

        public AxisInput GetAxis(int id)
        {
            for (int i = 0; i < Axes.Count; i++)
                if (Axes[i].Id == id) return Axes[i];
            throw new KeyNotFoundException($"Axis {id} is not registered.");
        }
    }
}
