using System;
using System.Collections.Generic;
using SimulationCore.Contracts;
using SimulationCore.SimulationActor.Application.Dto;
using SimulationCore.SimulationActor.Application.Port;
using SimulationCore.SimulationActor.Contract;
using SimulationCore.SimulationActor.Infrastructure;
using SimulationCore.Unity;
using SimulationCore.World.API;
using SimulationCore.World.Contract;
using UnityEngine;
using Pose = SimulationCore.Unity.Pose;

namespace SimulationCore.SimulationActor.Presentation
{
    public sealed class UnitySimulationPresentation : ISimulationPresentation
    {
        private readonly IEcsWorld world;
        private readonly IActorBindingPort bindingPort;
        private readonly UnityActorInstancePort unityActorPort;

        private readonly Dictionary<EntityHandle, Pose> previousPoses = new Dictionary<EntityHandle, Pose>();

        private readonly Dictionary<EntityHandle, Pose> currentPoses = new Dictionary<EntityHandle, Pose>();

        private readonly List<EntityHandle> renderOrder = new List<EntityHandle>();

        private bool hasCapturedTick;
        private ulong capturedTick;

        public UnitySimulationPresentation(IEcsWorld world, IActorBindingPort bindingPort, UnityActorInstancePort unityActorPort)
        {
            this.world = world
                ?? throw new ArgumentNullException(nameof(world));

            this.bindingPort = bindingPort
                ?? throw new ArgumentNullException(nameof(bindingPort));

            this.unityActorPort = unityActorPort
                ?? throw new ArgumentNullException(nameof(unityActorPort));
        }

        public void CaptureTickState(ulong tick)
        {
            bool isContinuous = hasCapturedTick && tick == capturedTick + 1;

            previousPoses.Clear();

            if (isContinuous)
            {
                foreach (KeyValuePair<EntityHandle, Pose> pair in currentPoses)
                {
                    previousPoses.Add(pair.Key, pair.Value);
                }
            }

            currentPoses.Clear();
            renderOrder.Clear();

            for (int i = 0; i < bindingPort.ActiveActorCount; i++)
            {
                ActorBinding binding = bindingPort.GetActiveBinding(i);

                if (!world.TryGetComponent(binding.Entity, out ActorTransformState transformState))
                {
                    continue;
                }

                Pose pose = new Pose(
                    binding.Actor,
                    transformState.Position.ToUnity(),
                    transformState.Rotation.ToUnity());

                currentPoses.Add(binding.Entity, pose);
                renderOrder.Add(binding.Entity);
            }

            if (!isContinuous)
            {
                // Initial capture, replay seek or snapshot restore:
                // snap instead of interpolating across unrelated states.
                previousPoses.Clear();

                foreach (KeyValuePair<EntityHandle, Pose> pair
                    in currentPoses)
                {
                    previousPoses.Add(pair.Key, pair.Value);
                }
            }
            else
            {
                // Newly spawned entities must not interpolate from origin.
                for (int i = 0; i < renderOrder.Count; i++)
                {
                    EntityHandle entity = renderOrder[i];

                    if (!previousPoses.ContainsKey(entity))
                    {
                        previousPoses.Add(
                            entity,
                            currentPoses[entity]);
                    }
                }
            }

            capturedTick = tick;
            hasCapturedTick = true;
        }

        public void Render(float interpolationAlpha)
        {
            float alpha = Mathf.Clamp01(interpolationAlpha);

            for (int i = 0; i < renderOrder.Count; i++)
            {
                EntityHandle entity = renderOrder[i];
                Pose current = currentPoses[entity];

                Pose previous;

                if (!previousPoses.TryGetValue(entity, out previous))
                {
                    previous = current;
                }

                Vector3 position = Vector3.LerpUnclamped(
                    previous.Position,
                    current.Position,
                    alpha);

                Quaternion rotation = Quaternion.SlerpUnclamped(
                    previous.Rotation,
                    current.Rotation,
                    alpha);

                Transform target = unityActorPort.GetPresentationTransform(current.Actor);

                target.SetPositionAndRotation(position, rotation);
            }
        }


    }
}