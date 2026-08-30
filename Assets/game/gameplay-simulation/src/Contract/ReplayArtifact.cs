using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Testability;

namespace GameplaySimulation
{
    [DataContract]
    public sealed class ReplayArtifact
    {
        public ReplayArtifact(GameplayScenario scenario, string policy, ulong endTick,
            IEnumerable<GameplayRequest> actions, IEnumerable<ActionResult> results, IEnumerable<HashCheckpoint> hashes)
        {
            SchemaVersion = 1; Scenario = scenario; DiagnosticPolicy = policy; EndTick = endTick;
            Runtime = Environment.Version + " / " + Environment.OSVersion;
            this.actions = new List<GameplayRequest>(actions).ToArray();
            this.results = new List<ActionResult>(results).ToArray();
            this.hashes = new List<HashCheckpoint>(hashes).ToArray();
            Validate();
        }
        [DataMember(Order = 1)] public int SchemaVersion { get; private set; }
        [DataMember(Order = 2)] public GameplayScenario Scenario { get; private set; }
        [DataMember(Order = 3)] public string DiagnosticPolicy { get; private set; }
        [DataMember(Order = 4)] public string Runtime { get; private set; }
        [DataMember(Order = 5)] public ulong EndTick { get; private set; }
        [DataMember(Order = 6)] private GameplayRequest[] actions;
        [DataMember(Order = 7)] private ActionResult[] results;
        [DataMember(Order = 8)] private HashCheckpoint[] hashes;
        public IReadOnlyList<GameplayRequest> Actions => Array.AsReadOnly(actions);
        public IReadOnlyList<ActionResult> Results => Array.AsReadOnly(results);
        public IReadOnlyList<HashCheckpoint> Hashes => Array.AsReadOnly(hashes);

        public void Validate()
        {
            if (SchemaVersion != 1 || Scenario == null || string.IsNullOrWhiteSpace(DiagnosticPolicy) || string.IsNullOrWhiteSpace(Runtime))
                throw new ArgumentException("Unsupported or incomplete replay metadata.");
            Scenario.Validate();
            if (EndTick > (ulong)Scenario.MaxTicks || EndTick > 1000000 || actions == null || results == null || hashes == null ||
                actions.Length > Scenario.MaxActions || actions.Length > 1000000 || results.Length > actions.Length || hashes.LongLength != (long)EndTick + 1)
                throw new ArgumentException("Replay history exceeds bounds or lacks checkpoints.");
            Dictionary<ulong, GameplayRequest> bySequence = new Dictionary<ulong, GameplayRequest>();
            int executed = 0;
            foreach (GameplayRequest action in actions)
            {
                if (action == null || action.Sequence == 0 || action.TargetTick == 0 || action.TargetTick > (ulong)Scenario.MaxTicks || bySequence.ContainsKey(action.Sequence))
                    throw new ArgumentException("Invalid replay action identity/tick.");
                bySequence.Add(action.Sequence, action);
                if (action.TargetTick <= EndTick) executed++;
            }
            if (results.Length != executed) throw new ArgumentException("Missing executed action results.");
            ulong lastTick = 0, lastSequence = 0;
            foreach (ActionResult result in results)
            {
                if (result == null || !bySequence.TryGetValue(result.Sequence, out GameplayRequest action) || result.Tick != action.TargetTick || result.Tick > EndTick ||
                    result.Tick < lastTick || (result.Tick == lastTick && result.Sequence <= lastSequence))
                    throw new ArgumentException("Invalid result order or action correlation.");
                lastTick = result.Tick; lastSequence = result.Sequence;
            }
            for (int i = 0; i < hashes.Length; i++)
                if (hashes[i] == null || hashes[i].Tick != (ulong)i || string.IsNullOrWhiteSpace(hashes[i].Hash))
                    throw new ArgumentException("Invalid checkpoint order.");
        }
    }

    public enum ReplayPlaybackState { Paused, Playing, Completed, Diverged }
}
