using System;
using System.Collections.Generic;
using TickInputBuffering.Contract;

namespace TickInputBuffering
{
    public sealed class TickInputBuffer : ITickInputBuffer
    {
        private sealed class Button
        {
            internal bool Down;
            internal bool Pressed;
            internal bool Released;
        }

        private readonly SortedDictionary<int, Button> buttons = new SortedDictionary<int, Button>();
        private readonly SortedDictionary<int, float> axes = new SortedDictionary<int, float>();
        private bool consumed;
        private ulong lastTick;

        public bool IsSealed { get; private set; }

        public void RegisterButton(int id, bool initiallyDown = false)
        {
            EnsureConfigurable();
            ValidateId(id);
            if (buttons.ContainsKey(id)) throw new InvalidOperationException($"Button {id} already registered.");
            buttons.Add(id, new Button { Down = initiallyDown });
        }

        public void RegisterAxis(int id, float initialValue = 0f)
        {
            EnsureConfigurable();
            ValidateId(id);
            ValidateAxis(initialValue);
            if (axes.ContainsKey(id)) throw new InvalidOperationException($"Axis {id} already registered.");
            axes.Add(id, initialValue);
        }

        public void Seal() => IsSealed = true;

        public void CaptureButton(int id, bool isDown)
        {
            EnsureSealed();
            if (!buttons.TryGetValue(id, out Button button))
                throw new KeyNotFoundException($"Button {id} is not registered.");
            if (button.Down == isDown) return;
            if (isDown) button.Pressed = true;
            else button.Released = true;
            button.Down = isDown;
        }

        public void CaptureAxis(int id, float value)
        {
            EnsureSealed();
            ValidateAxis(value);
            if (!axes.ContainsKey(id)) throw new KeyNotFoundException($"Axis {id} is not registered.");
            axes[id] = value;
        }

        public TickInputFrame ConsumeTick(ulong tick)
        {
            EnsureSealed();
            if (consumed && tick <= lastTick)
                throw new ArgumentOutOfRangeException(nameof(tick), "Ticks must strictly increase.");

            var buttonStates = new ButtonInput[buttons.Count];
            var axisStates = new AxisInput[axes.Count];
            int index = 0;
            foreach (var pair in buttons)
                buttonStates[index++] = new ButtonInput(pair.Key, pair.Value.Pressed, pair.Value.Down, pair.Value.Released);
            index = 0;
            foreach (var pair in axes)
                axisStates[index++] = new AxisInput(pair.Key, pair.Value);

            var frame = new TickInputFrame(tick, buttonStates, axisStates);
            foreach (Button button in buttons.Values)
            {
                button.Pressed = false;
                button.Released = false;
            }

            consumed = true;
            lastTick = tick;
            return frame;
        }

        private void EnsureConfigurable()
        {
            if (IsSealed) throw new InvalidOperationException("Input registration is sealed.");
        }

        private void EnsureSealed()
        {
            if (!IsSealed) throw new InvalidOperationException("Seal input registration first.");
        }

        private static void ValidateId(int id)
        {
            if (id < 0) throw new ArgumentOutOfRangeException(nameof(id));
        }

        private static void ValidateAxis(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "Axis values must be finite.");
        }
    }
}
