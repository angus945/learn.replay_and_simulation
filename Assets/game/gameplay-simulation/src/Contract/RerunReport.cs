using System.Collections.Generic;

namespace GameplaySimulation
{
    public sealed class RerunDifference
    {
        public RerunDifference(string category, ulong? tick, string expected, string actual)
        { Category = category; Tick = tick; Expected = expected; Actual = actual; }
        public string Category { get; }
        public ulong? Tick { get; }
        public string Expected { get; }
        public string Actual { get; }
    }

    public sealed class RerunReport
    {
        public RerunReport(bool executed, IEnumerable<RerunDifference> differences, IEnumerable<string> warnings)
        {
            Executed = executed;
            Differences = new List<RerunDifference>(differences).AsReadOnly();
            Warnings = new List<string>(warnings).AsReadOnly();
            foreach (RerunDifference difference in Differences)
                if (difference.Tick.HasValue && (!FirstDivergentTick.HasValue || difference.Tick.Value < FirstDivergentTick.Value))
                    FirstDivergentTick = difference.Tick;
        }
        public bool Executed { get; }
        public bool Matches => Executed && Differences.Count == 0;
        public ulong? FirstDivergentTick { get; }
        public IReadOnlyList<RerunDifference> Differences { get; }
        public IReadOnlyList<string> Warnings { get; }
    }
}
