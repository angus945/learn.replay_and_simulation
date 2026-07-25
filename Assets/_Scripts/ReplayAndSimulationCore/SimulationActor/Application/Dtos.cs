using SimulationCore.World.Contract;

namespace SimulationCore.SimulationActor.Application.Dto
{
    public struct ActorHandle
    {
        public ActorHandle(int archetypeId, int slotId)
        {
            ArchetypeId = archetypeId;
            SlotId = slotId;
        }

        public int ArchetypeId { get; }
        public int SlotId { get; }
    }
    public struct ActorBinding
    {
        public ActorBinding(EntityHandle entity, ActorHandle actor)
        {
            Entity = entity;
            Actor = actor;
        }

        public EntityHandle Entity { get; }
        public ActorHandle Actor { get; }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
        public static bool operator ==(ActorBinding left, ActorBinding right)
        {
            return left.Equals(right);
        }
        public static bool operator !=(ActorBinding left, ActorBinding right)
        {
            return !(left == right);
        }
    }
}