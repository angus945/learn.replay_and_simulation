using System;
using DeterministicSimulation;

namespace DeterministicSimulation.Framework
{
    /// <summary>Low-level owner-thread driver. A failed tick cannot be retried; rebuild the pipeline and runner.
    /// Prefer a SimulationSession for world ownership, reset, and realtime drive exclusivity.</summary>
    public sealed class SimulationRunner
    {
        private readonly SimulationPipeline pipeline;
        private readonly int ownerThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private double accumulator;
        private ulong tickNumber;
        private bool busy;

        public SimulationRunner(SimulationPipeline pipeline, float tickDeltaTime = 1f / 60f,
            int maxTicksPerAdvanceTime = 120)
        {
            this.pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));

            if (tickDeltaTime <= 0f || float.IsNaN(tickDeltaTime) || float.IsInfinity(tickDeltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(tickDeltaTime));
            }
            if (maxTicksPerAdvanceTime < 1)
                throw new ArgumentOutOfRangeException(nameof(maxTicksPerAdvanceTime));

            TickDeltaTime = tickDeltaTime;
            MaxTicksPerAdvanceTime = maxTicksPerAdvanceTime;
        }

        public float TickDeltaTime { get; }
        public ulong TickNumber => tickNumber;
        public ulong LastCompletedTick { get; private set; }
        public Exception Failure { get; private set; }
        public int MaxTicksPerAdvanceTime { get; }
        public float Accumulator => (float)accumulator;
        public float PresentationAlpha => Clamp01((float)(accumulator / TickDeltaTime));

        public void AdvanceTime(float elapsedTime)
        {
            EnsureReady();
            if (elapsedTime < 0f || float.IsNaN(elapsedTime) || float.IsInfinity(elapsedTime))
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedTime));
            }

            busy = true;
            try
            {
                accumulator += elapsedTime;
                int advanced = 0;
                while (accumulator >= TickDeltaTime && advanced < MaxTicksPerAdvanceTime)
                {
                    accumulator -= TickDeltaTime;
                    ExecuteNextTick();
                    advanced++;
                }
            }
            catch (Exception error) { Fault(error); throw; }
            finally { busy = false; }
        }

        public void AdvanceTick()
        {
            EnsureReady();
            busy = true;
            try { ExecuteNextTick(); }
            catch (Exception error) { Fault(error); throw; }
            finally { busy = false; }
        }

        public void UpdatePresentation()
        {
            EnsureReady();
            busy = true;
            try { pipeline.Render(new SimulationTick(tickNumber, TickDeltaTime), PresentationAlpha); }
            catch (Exception error) { Fault(error); throw; }
            finally { busy = false; }
        }

        private void ExecuteNextTick()
        {
            tickNumber = checked(tickNumber + 1);
            pipeline.ExecuteTick(new SimulationTick(tickNumber, TickDeltaTime));
            LastCompletedTick = tickNumber;
        }

        private void Fault(Exception error)
        {
            Failure = Failure ?? error;
            accumulator = 0;
        }

        private void EnsureReady()
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != ownerThread)
                throw new InvalidOperationException("Use the simulation runner owner thread.");
            if (busy) throw new InvalidOperationException("Runner callbacks cannot reenter the driver.");
            if (Failure != null)
                throw new InvalidOperationException("Rebuild the pipeline and runner after a failure.", Failure);
        }

        private static float Clamp01(float value)
        {
            if (value <= 0f)
            {
                return 0f;
            }

            return value >= 1f ? 1f : value;
        }
    }
}
