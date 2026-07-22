namespace SimulationInput
{
    public struct CaptureInputCommand
    {

    }
    public struct ConsumeInputCommand
    {
        public readonly ulong tick;

        public ConsumeInputCommand(ulong tick)
        {
            this.tick = tick;
        }
    }
}
