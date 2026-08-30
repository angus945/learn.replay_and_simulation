using System;
using System.Collections.Generic;
using Arena.Integration;
using DeterministicSimulation;
using DeterministicSimulation.Framework;
using DeterministicSimulation.Unity;
using SimulationObjects.Contract;
using UnityEngine;

namespace Arena.Unity
{
    /// <summary>Maps detached committed observations to views. Unity transforms never flow back into gameplay.</summary>
    public sealed class ArenaActorPresentation : IActorPoseSource, IDisposable
    {
        private readonly UnityActorPool pool;
        private readonly UnityActorPresentation presentation;
        private ArenaObservation source;
        private ArenaObservation lastPresented;
        private float tickDelta;

        public ArenaActorPresentation(GameObject playerPrefab, GameObject enemyPrefab, float tickDelta,
            int enemyCapacity = 16)
        {
            SetTickDelta(tickDelta);
            pool = new UnityActorPool("Arena / pooled observation views");
            try
            {
                pool.RegisterPrefab(0, playerPrefab, 1);
                pool.RegisterPrefab(1, enemyPrefab, enemyCapacity);
                pool.Seal();
                presentation = new UnityActorPresentation(pool, this);
            }
            catch { pool.Dispose(); throw; }
        }

        public int ActiveCount => pool.ActiveCount;

        public void SetTickDelta(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            tickDelta = seconds;
        }

        /// <summary>Use the last two completed ticks, including when one frame advanced many ticks.</summary>
        public void Present(ArenaObservation previous, ArenaObservation current, float alpha)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            if (!ReferenceEquals(lastPresented, current))
            {
                bool adjacent = previous != null && previous.Tick != ulong.MaxValue && current.Tick == previous.Tick + 1;
                if (!adjacent)
                {
                    Snap(current);
                }
                else
                {
                    // A catch-up frame can skip several displayed observations. Seed the actual penultimate tick.
                    if (!ReferenceEquals(lastPresented, previous)) Snap(previous);
                    source = current;
                    presentation.CaptureTickState(Context(SimulationPhase.PresentationCapture));
                    lastPresented = current;
                }
            }
            presentation.Render(Context(SimulationPhase.PresentationRender), alpha);
        }

        /// <summary>Session changes are discontinuities even if their tick numbers happen to be consecutive.</summary>
        public void Snap(ArenaObservation observation)
        {
            source = observation ?? throw new ArgumentNullException(nameof(observation));
            presentation.SnapToCurrent(Context(SimulationPhase.PresentationCapture));
            lastPresented = observation;
        }

        public IReadOnlyList<ActorPose> ReadPoses()
        {
            if (source == null) throw new InvalidOperationException("Capture an observation before reading poses.");
            List<ActorPose> poses = new List<ActorPose>(source.Actors.Count);
            foreach (ActorSnapshot actor in source.Actors)
            {
                float lengthSquared = actor.DirectionX * actor.DirectionX + actor.DirectionY * actor.DirectionY;
                Quaternion rotation = lengthSquared > .000001f
                    ? Quaternion.Euler(0, 0, Mathf.Atan2(actor.DirectionY, actor.DirectionX) * Mathf.Rad2Deg - 90)
                    : Quaternion.identity;
                poses.Add(new ActorPose(new SimulationObjectId(actor.Id), actor.Enemy ? 1 : 0,
                    new Vector3(actor.X, actor.Y, 0), rotation));
            }
            return poses.AsReadOnly();
        }

        public bool TryGetView(ulong actorId, out GameObject view)
        {
            foreach (ActorBinding binding in pool.GetActiveBindings())
                if (binding.Id.Value == actorId) return pool.TryGetInstance(binding.Instance, out view);
            view = null;
            return false;
        }

        private SimulationContext Context(SimulationPhase phase)
            => new SimulationContext(new SimulationTick(source.Tick, tickDelta), phase);

        public void Dispose() => pool.Dispose();
    }
}
