using SimulationCore.World.Contract;

namespace SimulationCore.SimulationActor.Contract
{

    public struct ActorArchetypeComponent : IComponent
    {
        public int ArchetypeId { get; }

        public ActorArchetypeComponent(int archetypeId)
        {
            ArchetypeId = archetypeId;
        }
    }
    public struct ActorTransformState : IComponent
    {
        public Float3 Position { get; }
        public FloatQuaternion Rotation { get; }

        public ActorTransformState(Float3 position, FloatQuaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }
    }
}