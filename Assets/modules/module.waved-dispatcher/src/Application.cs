using System;
using System.Collections.Generic;

namespace WavedDispatcher
{
    /// <summary>
    /// Double-buffered queue. Items enqueued while a wave is being consumed are
    /// deferred to the next wave, which makes re-entrant production deterministic.
    /// </summary>
    public sealed class WaveBuffer<T>
    {
        private List<T> current = new List<T>();
        private List<T> pending = new List<T>();

        public bool HasPending => pending.Count > 0;

        public void Enqueue(T item)
        {
            pending.Add(item);
        }

        public IReadOnlyList<T> BeginWave()
        {
            current.Clear();
            List<T> previousCurrent = current;
            current = pending;
            pending = previousCurrent;
            return current;
        }

        public void Clear()
        {
            current.Clear();
            pending.Clear();
        }
    }

    public sealed class WaveDispatcher<T>
    {
        public const int DefaultMaxWaves = 32;

        private readonly WaveBuffer<T> buffer = new WaveBuffer<T>();

        public WaveDispatcher(int maxWaves = DefaultMaxWaves)
        {
            if (maxWaves <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxWaves));
            }

            MaxWaves = maxWaves;
        }

        public int MaxWaves { get; }
        public bool HasPending => buffer.HasPending;

        public void Enqueue(T item)
        {
            buffer.Enqueue(item);
        }

        public void DispatchAll(Action<int, T> dispatch)
        {
            if (dispatch == null)
            {
                throw new ArgumentNullException(nameof(dispatch));
            }

            int wave = 0;

            try
            {
                while (buffer.HasPending)
                {
                    if (wave >= MaxWaves)
                    {
                        throw new InvalidOperationException(
                            $"Maximum dispatch wave count ({MaxWaves}) was exceeded.");
                    }

                    IReadOnlyList<T> items = buffer.BeginWave();
                    for (int i = 0; i < items.Count; i++)
                    {
                        dispatch(wave, items[i]);
                    }

                    wave++;
                }
            }
            catch
            {
                buffer.Clear();
                throw;
            }
        }

        public void Clear()
        {
            buffer.Clear();
        }
    }
}
