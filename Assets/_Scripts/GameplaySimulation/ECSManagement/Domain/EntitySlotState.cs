namespace ECSManagement.Domain
{
    internal enum EntitySlotState : byte
    {
        Free,
        Reserved,
        Alive,
        PendingDestroy
    }
}
