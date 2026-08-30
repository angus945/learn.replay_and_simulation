using System;
using DeterministicSimulation;

namespace DeterministicSimulation.Framework
{
    public sealed class SimulationRunner
    {
        private readonly SimulationPipeline pipeline;
        private double accumulator;
        private ulong tickNumber;

        public SimulationRunner(SimulationPipeline pipeline, float tickDeltaTime = 1f / 60f)
        {
            this.pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));

            if (tickDeltaTime <= 0f || float.IsNaN(tickDeltaTime) || float.IsInfinity(tickDeltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(tickDeltaTime));
            }

            TickDeltaTime = tickDeltaTime;
        }

        public float TickDeltaTime { get; }
        public ulong TickNumber => tickNumber;
        public float Accumulator => (float)accumulator;
        public float PresentationAlpha => Clamp01((float)(accumulator / TickDeltaTime));

        public void AdvanceTime(float elapsedTime)
        {
            if (elapsedTime < 0f || float.IsNaN(elapsedTime) || float.IsInfinity(elapsedTime))
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedTime));
            }

            accumulator += elapsedTime;

            while (accumulator >= TickDeltaTime)
            {
                AdvanceTick();
                accumulator -= TickDeltaTime;
            }
        }

        public void AdvanceTick()
        {
            tickNumber++;
            pipeline.ExecuteTick(new SimulationTick(tickNumber, TickDeltaTime));
        }

        public void UpdatePresentation()
        {
            pipeline.Render(new SimulationTick(tickNumber, TickDeltaTime), PresentationAlpha);
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
