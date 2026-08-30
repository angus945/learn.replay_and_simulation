using System;
using Arena.Integration;
using DeterministicSimulation.Framework;
using InvariantChecks;
using Testability.Templates;

namespace Arena.Composition
{
    /// <summary>The one production composition used by live play, tests, recording and replay.</summary>
    public sealed class ArenaDefinition : ReplayableSimulationDefinition<ArenaRuntime, ArenaScenario, ArenaInput, ArenaObservation>
    {
        public const string DefaultPolicy = "arena-v1/canonical-v1/splitmix64-streams-1-2/lifetime-v1";
        private readonly bool failureOracle;
        public ArenaDefinition(bool failureOracle = false) { this.failureOracle = failureOracle; }
        public override string PolicyId => DefaultPolicy + (failureOracle ? "/training-position-oracle-v1" : "");
        protected override void ValidateScenario(ArenaScenario scenario) => scenario.Validate();
        protected override float GetTickDelta(ArenaScenario scenario) => scenario.TickDelta;
        protected override ArenaRuntime CreateWorld(ArenaScenario scenario) => new ArenaRuntime(scenario);
        protected override void DestroyWorld(ArenaRuntime world) { } // All world resources are managed and session-owned.
        protected override void ConfigureWorld(SimulationBuilder builder, ArenaRuntime world, ArenaScenario scenario) => ArenaSimulationWiring.Configure(builder, world);
        protected override InputOutcome ExecuteInput(ArenaRuntime world, ArenaInput input, InputExecutionContext context) => ArenaSimulationWiring.Execute(world, input, context);
        protected override ArenaObservation CaptureObservation(ArenaRuntime world) => new ArenaObservation(world);
        protected override byte[] EncodeCanonicalState(ArenaObservation observation) => ArenaCanonicalState.Encode(observation);
        protected override void ConfigureInvariants(InvariantRegistry<ArenaObservation> invariants)
        {
            invariants.Register(new ArenaInvariant());
            if (failureOracle) invariants.Register(new TrainingPositionOracle());
        }
        protected override TemplateLimits CreateDefaultLimits(ArenaScenario scenario) =>
            new TemplateLimits(scenario.MaxTicks, scenario.MaxInputs, scenario.TraceCapacity, maxTotalPayloadBytes: 16777216);
        protected override TemplateTraceMetadata DescribeInput(ArenaInput input) => new TemplateTraceMetadata(input.Kind.ToString(), actor: input.Actor, target: input.Target);
        protected override TemplateTraceMetadata DescribeMessage(object message) => ArenaSimulationWiring.Describe(message);
        protected override string EncodeScenario(ArenaScenario scenario) => ArenaCodecs.Encode(scenario);
        protected override ArenaScenario DecodeScenario(string payload) => ArenaCodecs.Decode<ArenaScenario>(payload);
        protected override string EncodeInput(ArenaInput input) => ArenaCodecs.Encode(input ?? throw new ArgumentNullException(nameof(input)));
        protected override ArenaInput DecodeInput(string payload) => ArenaCodecs.Decode<ArenaInput>(payload);
    }
}
