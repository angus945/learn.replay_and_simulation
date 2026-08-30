using System;
using System.Collections.Generic;
using DeterministicSimulation;
using DeterministicSimulation.Framework;
using DeterministicSimulation.Unity;
using GameplaySimulation;
using SimulationObjects.Contract;
using UnityEngine;

namespace MovementDemo.Unity
{
    /// <summary>Game-owned mapping from committed gameplay snapshots to reusable Unity presentation.</summary>
    public sealed class GameplayActorPresentation : IActorPoseSource, IDisposable
    {
        private readonly UnityActorPool pool;
        private readonly UnityActorPresentation presentation;
        private readonly float tickDelta;
        private GameplayObservation observation;

        public GameplayActorPresentation(GameObject playerPrefab, GameObject enemyPrefab, float tickDelta, int enemyCapacity = 1)
        {
            this.tickDelta = tickDelta;
            pool = new UnityActorPool("Gameplay Actor Views");
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
        public void Capture(GameplayObservation snapshot)
        {
            observation = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            presentation.CaptureTickState(Context(SimulationPhase.PresentationCapture));
        }
        public void Snap(GameplayObservation snapshot)
        {
            observation = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            presentation.SnapToCurrent(Context(SimulationPhase.PresentationCapture));
        }
        public void Render(float alpha) => presentation.Render(Context(SimulationPhase.PresentationRender), alpha);
        public IReadOnlyList<ActorPose> ReadPoses()
        {
            List<ActorPose> poses = new List<ActorPose>();
            foreach (ActorObservation actor in observation.Actors)
                if (actor.Active)
                    poses.Add(new ActorPose(new SimulationObjectId(actor.Id), actor.Id == observation.PlayerId ? 0 : 1,
                        new Vector3(actor.X, actor.Y, 0), Quaternion.identity));
            return poses.AsReadOnly();
        }
        private SimulationContext Context(SimulationPhase phase) => new SimulationContext(new SimulationTick(observation.Tick, tickDelta), phase);
        public void Dispose() => pool.Dispose();
    }
}
