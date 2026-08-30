using System;
using System.Collections.Generic;
using DeterministicSimulation.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeterministicSimulation.Unity
{
    /// <summary>Owns an isolated 3D sensor scene and its proxy pool. It never changes global simulationMode.
    /// Logical poses are reapplied before each step; dynamic rigidbody authority and state readback are not supported.</summary>
    public sealed class LocalPhysicsParticipant : IPhysicsParticipant, IDisposable
    {
        private readonly int ownerThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private readonly UnityActorPool pool;
        private readonly IActorPoseSource source;
        private readonly IPhysicsFactSink sink;
        private readonly Scene scene;
        private readonly PhysicsScene physics;
        private readonly PhysicsFactBuffer pending;
        private bool collecting;
        private bool busy;
        private bool disposed;
        private bool stepped;
        private ulong lastTick;
        private Exception callbackFailure;

        public LocalPhysicsParticipant(UnityActorPool pool, IActorPoseSource source, IPhysicsFactSink sink, int maxFactsPerTick = 4096)
        {
            this.pool = pool ?? throw new ArgumentNullException(nameof(pool));
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
            if (maxFactsPerTick < 1) throw new ArgumentOutOfRangeException(nameof(maxFactsPerTick));
            pending = new PhysicsFactBuffer(maxFactsPerTick);
            scene = SceneManager.CreateScene("Simulation Sensors " + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            try
            {
                physics = scene.GetPhysicsScene();
                pool.AttachPhysics(this, scene);
            }
            catch
            {
                SceneManager.UnloadSceneAsync(scene);
                throw;
            }
        }
        public Scene Scene => scene;
        public Exception Failure { get; private set; }
        public IReadOnlyList<PhysicsFact> LastFacts { get; private set; } = Array.AsReadOnly(Array.Empty<PhysicsFact>());

        public void Simulate(SimulationContext context)
        {
            EnsureAvailable();
            if (context.Phase != SimulationPhase.Physics || (stepped && context.Tick.Number <= lastTick))
                throw new InvalidOperationException("Run local physics once per increasing tick in the Physics phase.");
            if (context.Tick.DeltaTime <= 0 || float.IsNaN(context.Tick.DeltaTime) || float.IsInfinity(context.Tick.DeltaTime))
                throw new ArgumentOutOfRangeException(nameof(context));
            busy = true;
            try
            {
                // Rendering may have interpolated these transforms. Restore logical poses before querying contacts.
                pool.Reconcile(source.ReadPoses());
                pending.Clear(); callbackFailure = null;
                Physics.SyncTransforms();
                collecting = true;
                try { physics.Simulate(context.Tick.DeltaTime); }
                finally { collecting = false; }
                if (callbackFailure != null) throw callbackFailure;
                LastFacts = pending.Capture();
                sink.PublishPhysicsFacts(context.Tick.Number, LastFacts);
                lastTick = context.Tick.Number; stepped = true;
            }
            catch (Exception error) { Failure = error; throw; }
            finally { busy = false; }
        }

        internal void Record(PhysicsFact fact)
        {
            if (!collecting || disposed || callbackFailure != null) return;
            try { pending.Add(fact); }
            catch (Exception error)
            {
                // Unity catches callback exceptions; defer failure until Simulate returns to the framework.
                callbackFailure = error;
            }
        }

        public void Dispose()
        {
            EnsureOwnerThread();
            if (disposed) return;
            if (busy) throw new InvalidOperationException("Cannot dispose physics from its callbacks.");
            disposed = true;
            pool.Dispose();
            pending.Clear();
            if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
        }

        private void EnsureAvailable()
        {
            EnsureOwnerThread();
            if (disposed) throw new ObjectDisposedException(nameof(LocalPhysicsParticipant));
            if (busy) throw new InvalidOperationException("Cannot recursively simulate physics.");
            if (Failure != null) throw new InvalidOperationException("Recreate the physics adapter after failure.", Failure);
        }
        private void EnsureOwnerThread()
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != ownerThread)
                throw new InvalidOperationException("Use the local physics owner thread.");
        }
    }
}
