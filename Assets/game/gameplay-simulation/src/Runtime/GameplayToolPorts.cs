using System;
using System.Collections.Generic;
using Testability;

namespace GameplaySimulation
{
    public sealed partial class GameplaySession
    {
        private bool realtimeClaimed;
        public SimulationDriveMode DriveMode { get; }
        public IGameplayControl Gameplay { get; }
        public ISimulationControl Simulation { get; }
        public ITestSession<GameplayScenario> Admin { get; }
        public IActionResultReader Results { get; }
        public IGameplayCapabilities Capabilities { get; }

        public IRealtimeTickDriver ClaimRealtimeDriver()
        {
            if (DriveMode != SimulationDriveMode.Realtime || realtimeClaimed)
                throw new InvalidOperationException("Realtime clock authority is unavailable or already claimed.");
            realtimeClaimed = true;
            return new RealtimePort(this);
        }

        private sealed class GameplayPort : IGameplayControl
        {
            private readonly GameplaySession owner;
            internal GameplayPort(GameplaySession owner) { this.owner = owner; }
            public string Id => owner.Id;
            public ulong CurrentTick => owner.CurrentTick;
            public SubmissionResult Submit(GameplayRequest request) => owner.Submit(request);
            public GameplayObservation Observe()
            {
                if (owner.stepping) throw new InvalidOperationException("Read between ticks.");
                return owner.Observe();
            }
        }
        private sealed class SimulationPort : ISimulationControl
        {
            private readonly GameplaySession owner;
            internal SimulationPort(GameplaySession owner) { this.owner = owner; }
            public SimulationDriveMode DriveMode => owner.DriveMode;
            public TickReport Step() => owner.Step();
        }
        private sealed class RealtimePort : IRealtimeTickDriver
        {
            private readonly GameplaySession owner;
            internal RealtimePort(GameplaySession owner) { this.owner = owner; }
            public TickReport AdvanceTick() => owner.StepCore();
        }
        private sealed class AdminPort : ITestSession<GameplayScenario>
        {
            private readonly GameplaySession owner;
            internal AdminPort(GameplaySession owner) { this.owner = owner; }
            public string Id => owner.Id;
            public SessionState State => owner.State;
            public void Start(GameplayScenario scenario) => owner.Start(scenario);
            public void Reset(GameplayScenario scenario) => owner.Reset(scenario);
            public void Stop() => owner.Stop();
        }
        private sealed class CapabilitiesPort : IGameplayCapabilities
        {
            private readonly GameplaySession owner;
            internal CapabilitiesPort(GameplaySession owner) { this.owner = owner; }
            public GameplayCapabilities Describe() => new GameplayCapabilities(owner.Id, owner.State, owner.DriveMode, owner.scenario);
        }
        private sealed class ResultsPort : IActionResultReader
        {
            private readonly GameplaySession owner;
            internal ResultsPort(GameplaySession owner) { this.owner = owner; }
            public ActionLookup Find(string sessionId, ulong sequence)
            {
                if (owner.stepping) throw new InvalidOperationException("Read results between ticks.");
                if (sessionId != owner.Id) return new ActionLookup(ActionLookupState.StaleSession);
                ActionResult result = owner.resultHistory.Find(item => item.Sequence == sequence);
                if (result != null) return new ActionLookup(ActionLookupState.Completed, result);
                if (!owner.sequences.Contains(sequence)) return new ActionLookup(ActionLookupState.Unknown);
                return new ActionLookup(owner.State == SessionState.Running ? ActionLookupState.Pending : ActionLookupState.Cancelled,
                    cancellationReason: owner.State == SessionState.Running ? null : owner.cancellationReason);
            }
            public ActionResultPage Read(string sessionId, int afterIndex, int maxItems)
            {
                if (owner.stepping || sessionId != owner.Id) throw new InvalidOperationException("Busy or stale session.");
                if (afterIndex < 0 || afterIndex > owner.resultHistory.Count) throw new ArgumentOutOfRangeException(nameof(afterIndex));
                if (maxItems < 1 || maxItems > 1024) throw new ArgumentOutOfRangeException(nameof(maxItems));
                int count = Math.Min(maxItems, owner.resultHistory.Count - afterIndex);
                List<ActionResult> items = owner.resultHistory.GetRange(afterIndex, count);
                return new ActionResultPage(owner.Id, items, afterIndex + count, afterIndex + count < owner.resultHistory.Count);
            }
        }
    }
}
