using SeededRandom.Contract;

namespace SeededRandom
{
    public interface ISeededRandom
    {
        ulong NextUInt64();
        uint NextUInt32();
        uint NextUInt32(uint exclusiveMax);
        int NextInt(int inclusiveMin, int exclusiveMax);
        float NextSingle();
        double NextDouble();
        RandomState CaptureState();
        void RestoreState(RandomState state);
    }
}
