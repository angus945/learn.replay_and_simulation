using System;
using System.Globalization;

namespace Arena.Unity
{
    /// <summary>Wall-clock display counters only. Never supplies time to the simulation runner.</summary>
    public sealed class ArenaPerformanceMetrics
    {
        private const double SampleInterval = .5;
        private double startedAt;
        private ulong startedTick;
        private int frameCount;
        private bool initialized;
        public double FramesPerSecond { get; private set; }
        public double TicksPerSecond { get; private set; }
        public double PendingSeconds { get; private set; }
        public string Summary { get; private set; } = "FPS --  /  tick/s --  /  live debt -- ms";

        public void Reset(double realtime, ulong tick)
        {
            startedAt = realtime; startedTick = tick; frameCount = 0; initialized = true;
            FramesPerSecond = 0; TicksPerSecond = 0; PendingSeconds = 0;
            Summary = "FPS --  /  tick/s --  /  live debt -- ms";
        }

        public void Sample(double realtime, ulong tick, double pendingSeconds)
        {
            if (!initialized || realtime < startedAt || tick < startedTick)
            {
                Reset(realtime, tick);
                return;
            }
            frameCount++;
            double elapsed = realtime - startedAt;
            if (elapsed < SampleInterval) return;
            FramesPerSecond = frameCount / elapsed;
            TicksPerSecond = (tick - startedTick) / elapsed;
            PendingSeconds = Math.Max(0, pendingSeconds);
            Summary = string.Format(CultureInfo.InvariantCulture,
                "FPS {0:0.0}  /  tick/s {1:0.0}  /  live debt {2:0.0} ms",
                FramesPerSecond, TicksPerSecond, PendingSeconds * 1000);
            startedAt = realtime; startedTick = tick; frameCount = 0;
        }
    }
}
