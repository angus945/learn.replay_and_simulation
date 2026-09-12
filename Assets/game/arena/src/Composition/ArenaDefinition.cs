using System;
using System.Collections.Generic;
using Arena.Integration;
using DeterministicSimulation.Framework;
using Module.Verification.Oracle;

namespace Arena.Composition
{
    /// <summary>Adopter-owned composition for simulation, observation, control, oracles, evidence and playback.</summary>
    public sealed class ArenaDefinition
    {
        private sealed class CoreDefinition : SimulationDefinition<ArenaRuntime, ArenaScenario>
        {
            protected override void ValidateScenario(ArenaScenario scenario)
            {
                scenario.Validate();
            }

            protected override float GetTickDelta(ArenaScenario scenario)
            {
                return scenario.TickDelta;
            }

            protected override ArenaRuntime CreateWorld(ArenaScenario scenario)
            {
                return new ArenaRuntime(scenario);
            }

            protected override void Configure(SimulationBuilder builder, ArenaRuntime world, ArenaScenario scenario)
            {
                ArenaSimulationWiring.Configure(builder, world);
            }

            protected override void DestroyWorld(ArenaRuntime world)
            {
            }
        }

        public const string DefaultPolicy = "arena-v2/modules-v1/canonical-v1/splitmix64-streams-1-2/lifetime-v1";
        private readonly bool failureOracle;
        private readonly CoreDefinition core = new CoreDefinition();

        public ArenaDefinition(bool failureOracle = false)
        {
            this.failureOracle = failureOracle;
        }

        public string PolicyId
        {
            get { return DefaultPolicy + (failureOracle ? "/training-position-oracle-v1" : string.Empty); }
        }

        public ArenaSession CreateSession(ArenaScenario scenario = null, ArenaLimits limits = null)
        {
            ArenaScenario actualScenario = scenario ?? new ArenaScenario();
            return new ArenaSession(this, actualScenario, limits);
        }

        public ArenaReplay CreateReplay(ArenaRecording recording)
        {
            return new ArenaReplay(this, recording);
        }

        internal SimulationSession<ArenaRuntime, ArenaScenario> CreateCoreSession(ArenaScenario scenario, Action<SimulationPhase, bool> phaseObserver, Action<MessageDispatch> dispatchObserver)
        {
            return core.CreateSession(scenario, phaseObserver, dispatchObserver);
        }

        internal string EncodeScenario(ArenaScenario scenario)
        {
            return ArenaCodecs.Encode(scenario);
        }

        internal ArenaScenario DecodeScenario(string payload)
        {
            return ArenaCodecs.Decode<ArenaScenario>(payload);
        }

        internal string EncodeInput(ArenaInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            return ArenaCodecs.Encode(input);
        }

        internal ArenaInput DecodeInput(string payload)
        {
            return ArenaCodecs.Decode<ArenaInput>(payload);
        }

        internal ArenaLimits CreateLimits(ArenaScenario scenario)
        {
            return new ArenaLimits(scenario.MaxTicks, scenario.MaxInputs, scenario.TraceCapacity, 65536, 16777216);
        }

        internal ArenaTraceMetadata DescribeInput(ArenaInput input)
        {
            return new ArenaTraceMetadata(input.Kind.ToString(), actor: input.Actor, target: input.Target);
        }

        internal ArenaTraceMetadata DescribeMessage(object message)
        {
            return ArenaSimulationWiring.Describe(message);
        }

        internal OracleSet<ArenaObservation> CreateOracleSet()
        {
            List<ITestOracle<ArenaObservation>> oracles = new List<ITestOracle<ArenaObservation>>();
            oracles.Add(new ArenaInvariantOracle(new ArenaInvariant()));
            if (failureOracle) oracles.Add(new ArenaInvariantOracle(new TrainingPositionOracle()));
            return new OracleSet<ArenaObservation>(PolicyId + "/oracles", oracles);
        }
    }
}
