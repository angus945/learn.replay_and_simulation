namespace SimulationCore.World.Domain
{
    internal enum EntitySlotState : byte
    {
        Free,
        Reserved,
        Alive,
        PendingDestroy
    }
}
