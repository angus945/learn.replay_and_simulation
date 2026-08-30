using System;
using System.Collections.Generic;
using DeterministicSimulation.Framework;
using SimulationObjects.Contract;
using UnityEngine;

namespace DeterministicSimulation.Unity
{
    /// <summary>Interpolates detached logical snapshots. New objects and discontinuous ticks snap to their current pose.</summary>
    public sealed class UnityActorPresentation : IPresentationParticipant
    {
        private readonly UnityActorPool pool;
        private readonly IActorPoseSource source;
        private SortedDictionary<SimulationObjectId, ActorPose> previous = new SortedDictionary<SimulationObjectId, ActorPose>();
        private SortedDictionary<SimulationObjectId, ActorPose> current = new SortedDictionary<SimulationObjectId, ActorPose>();
        private ulong capturedTick;
        private bool captured;

        public UnityActorPresentation(UnityActorPool pool, IActorPoseSource source)
        { this.pool = pool ?? throw new ArgumentNullException(nameof(pool)); this.source = source ?? throw new ArgumentNullException(nameof(source)); }

        public void CaptureTickState(SimulationContext context)
        {
            SortedDictionary<SimulationObjectId, ActorPose> next = UnityActorPool.CopyPoses(source.ReadPoses());
            pool.Reconcile(new List<ActorPose>(next.Values));
            previous = captured && capturedTick != ulong.MaxValue && context.Tick.Number == capturedTick + 1 ? current : next;
            current = next;
            captured = true;
            capturedTick = context.Tick.Number;
        }

        /// <summary>Use when switching sessions or restoring state, even if tick numbers happen to be consecutive.</summary>
        public void SnapToCurrent(SimulationContext context)
        {
            captured = false;
            CaptureTickState(context);
        }

        public void Render(SimulationContext context, float interpolationAlpha)
        {
            if (float.IsNaN(interpolationAlpha) || float.IsInfinity(interpolationAlpha) || interpolationAlpha < 0 || interpolationAlpha > 1)
                throw new ArgumentOutOfRangeException(nameof(interpolationAlpha));
            foreach (ActorBinding binding in pool.GetActiveBindings())
            {
                if (!current.TryGetValue(binding.Id, out ActorPose after)) continue;
                if (!previous.TryGetValue(binding.Id, out ActorPose before)) before = after;
                if (!pool.TryGetInstance(binding.Instance, out GameObject instance)) continue;
                instance.transform.SetPositionAndRotation(
                    Vector3.LerpUnclamped(before.Position, after.Position, interpolationAlpha),
                    Quaternion.SlerpUnclamped(before.Rotation, after.Rotation, interpolationAlpha));
            }
        }
    }
}
