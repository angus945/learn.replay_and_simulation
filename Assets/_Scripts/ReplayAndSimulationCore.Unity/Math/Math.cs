using SimulationCore.SimulationActor.Application.Dto;
using UnityEngine;

namespace SimulationCore.Unity
{
    public static class MathExtensions
    {
        public static Vector3 ToUnity(this Float3 value)
        {
            return new Vector3(
                value.X,
                value.Y,
                value.Z);
        }

        public static Quaternion ToUnity(this FloatQuaternion value)
        {
            return new Quaternion(
                value.X,
                value.Y,
                value.Z,
                value.W);
        }
    }

    public readonly struct Pose
    {
        public readonly ActorHandle Actor;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;

        public Pose(ActorHandle actor, Vector3 position, Quaternion rotation)
        {
            Actor = actor;
            Position = position;
            Rotation = rotation;
        }
    }
}