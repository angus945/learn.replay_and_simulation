using System;

namespace DeterministicSimulation.Framework
{
    /// <summary>Project integration template. Domain types need not inherit any framework type.
    /// Definitions may be reused; mutable per-session state must belong to the world.</summary>
    public abstract class SimulationDefinition<TWorld, TScenario> where TWorld : class
    {
        protected abstract void ValidateScenario(TScenario scenario);
        protected abstract float GetTickDelta(TScenario scenario);
        protected abstract TWorld CreateWorld(TScenario scenario);
        protected abstract void Configure(SimulationBuilder builder, TWorld world, TScenario scenario);
        protected abstract void DestroyWorld(TWorld world);

        public SimulationSession<TWorld, TScenario> CreateSession(TScenario scenario)
            => new SimulationSession<TWorld, TScenario>(this, scenario);

        public SimulationSession<TWorld, TScenario> CreateSession(TScenario scenario,
            Action<SimulationPhase, bool> onPhase, Action<MessageDispatch> onDispatch)
            => new SimulationSession<TWorld, TScenario>(this, scenario, onPhase, onDispatch);

        internal float Validate(TScenario scenario)
        {
            if (ReferenceEquals(scenario, null)) throw new ArgumentNullException(nameof(scenario));
            ValidateScenario(scenario);
            float delta = GetTickDelta(scenario);
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta <= 0)
                throw new ArgumentOutOfRangeException(nameof(scenario), "Tick delta must be finite and positive.");
            return delta;
        }

        internal TWorld Create(TScenario scenario) => CreateWorld(scenario)
            ?? throw new InvalidOperationException("CreateWorld must return a fresh non-null world.");
        internal void Compose(SimulationBuilder builder, TWorld world, TScenario scenario) => Configure(builder, world, scenario);
        internal void Destroy(TWorld world) => DestroyWorld(world);
    }

    /// <summary>Optional observation capability. Return an immutable snapshot; do not mutate the world.</summary>
    public interface ISimulationObserver<in TWorld, out TObservation>
    {
        TObservation Observe(TWorld world);
    }
}
