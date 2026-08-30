using System;
using DeterministicSimulation;

namespace DeterministicSimulation.Framework
{
    /// <summary>Single-threaded manually driven host. Owns world and pipeline, never exposes either.
    /// Stop preserves readable state; Dispose destroys it. Reset destroys then rebuilds, without rollback.</summary>
    public sealed class SimulationSession<TWorld, TScenario> : IDisposable where TWorld : class
    {
        private readonly SimulationDefinition<TWorld, TScenario> definition;
        private readonly int ownerThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private TWorld world;
        private SimulationPipeline pipeline;
        private SimulationRunner runner;
        private bool busy;
        private readonly SimulationDriveOwnership drive = new SimulationDriveOwnership();
        private readonly Action<SimulationPhase, bool> onPhase;
        private readonly Action<MessageDispatch> onDispatch;

        internal SimulationSession(SimulationDefinition<TWorld, TScenario> definition, TScenario scenario,
            Action<SimulationPhase, bool> onPhase = null, Action<MessageDispatch> onDispatch = null)
        {
            this.definition = definition;
            this.onPhase = onPhase; this.onDispatch = onDispatch;
            float delta = definition.Validate(scenario);
            busy = true;
            try { Initialize(scenario, delta); }
            finally { busy = false; }
        }

        public SimulationSessionState State { get; private set; }
        public ulong TickNumber => runner == null ? 0 : runner.TickNumber;
        public ulong LastCompletedTick { get; private set; }
        public Exception Failure { get; private set; }

        public void EnqueueIntent<T>(T intent) where T : IIntent
        {
            EnsureRunning();
            pipeline.EnqueueIntent(intent);
        }

        public void Step()
        {
            EnsureOwnerThread();
            drive.EnsureManual(); StepCore();
        }

        public RealtimeSimulationRunner CreateRealtimeRunner(int maxTicksPerFrame = 120,
            IRealtimeInputSource input = null, IRealtimePresentation presentation = null)
        {
            EnsureRunning();
            return drive.CreateRunner(new TickSource(this), maxTicksPerFrame, input, presentation);
        }

        private sealed class TickSource : ISimulationTickSource
        {
            private readonly SimulationSession<TWorld, TScenario> owner;
            internal TickSource(SimulationSession<TWorld, TScenario> owner) { this.owner = owner; }
            public float TickDelta => owner.runner.TickDeltaTime;
            public ulong TickNumber => owner.TickNumber;
            public bool PrepareTick() { owner.EnsureIdle(); return owner.State == SimulationSessionState.Running; }
            public void AdvanceTick() => owner.StepCore();
        }

        private void StepCore()
        {
            EnsureRunning();
            busy = true;
            try { runner.AdvanceTick(); LastCompletedTick = TickNumber; }
            catch (Exception error) { Fault(error); throw; }
            finally { busy = false; }
        }

        public TObservation Observe<TObservation>(ISimulationObserver<TWorld, TObservation> observer)
        {
            EnsureIdle();
            if (world == null) throw new InvalidOperationException("No world is available.");
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            busy = true;
            try { return observer.Observe(world); }
            finally { busy = false; }
        }

        public void Render(float alpha)
        {
            EnsureIdle();
            if (State == SimulationSessionState.Faulted) throw new InvalidOperationException("Reset a faulted session before rendering.");
            if (float.IsNaN(alpha) || float.IsInfinity(alpha) || alpha < 0 || alpha > 1)
                throw new ArgumentOutOfRangeException(nameof(alpha));
            busy = true;
            try { pipeline.Render(new SimulationTick(TickNumber, runner.TickDeltaTime), alpha); }
            catch (Exception error) { Fault(error); throw; }
            finally { busy = false; }
        }

        public void Stop()
        {
            EnsureIdle();
            if (State != SimulationSessionState.Faulted) State = SimulationSessionState.Stopped;
        }

        public void Reset(TScenario scenario)
        {
            EnsureOwnerThread();
            drive.EnsureManual();
            EnsureIdle();
            busy = true;
            try
            {
                // Invalid scenarios leave the existing world untouched.
                float delta = definition.Validate(scenario);
                try { ReleaseWorld(); Initialize(scenario, delta); }
                catch (Exception error) { Fault(error); throw; }
            }
            finally { busy = false; }
        }

        public void Dispose()
        {
            EnsureOwnerThread();
            if (State == SimulationSessionState.Disposed) return;
            drive.EnsureManual();
            EnsureIdle();
            busy = true;
            State = SimulationSessionState.Disposed;
            try { ReleaseWorld(); }
            finally { busy = false; }
        }

        private void Initialize(TScenario scenario, float delta)
        {
            TWorld created = definition.Create(scenario);
            try
            {
                SimulationBuilder builder = new SimulationBuilder(onPhase, onDispatch);
                definition.Compose(builder, created, scenario);
                SimulationPipeline nextPipeline = builder.Build();
                SimulationRunner nextRunner = new SimulationRunner(nextPipeline, delta);
                world = created; pipeline = nextPipeline; runner = nextRunner;
                LastCompletedTick = 0; Failure = null; State = SimulationSessionState.Running;
            }
            catch (Exception setupError)
            {
                try { definition.Destroy(created); }
                catch (Exception cleanupError) { throw new AggregateException(setupError, cleanupError); }
                throw;
            }
        }
        private void ReleaseWorld()
        {
            TWorld released = world;
            world = null; pipeline = null; runner = null; LastCompletedTick = 0;
            if (released != null) definition.Destroy(released);
        }
        private void Fault(Exception error)
        { Failure = Failure ?? error; State = SimulationSessionState.Faulted; }
        private void EnsureIdle()
        {
            EnsureOwnerThread();
            if (State == SimulationSessionState.Disposed) throw new ObjectDisposedException(GetType().Name);
            if (busy) throw new InvalidOperationException("Session callbacks cannot reenter the host.");
        }
        private void EnsureOwnerThread()
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != ownerThread)
                throw new InvalidOperationException("Use the simulation session owner thread.");
        }
        private void EnsureRunning()
        {
            EnsureIdle();
            if (State != SimulationSessionState.Running) throw new InvalidOperationException("Session is not running.");
        }
    }
}
