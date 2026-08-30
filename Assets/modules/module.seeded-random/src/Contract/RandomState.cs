namespace SeededRandom.Contract
{
    /// <summary>Versioned raw generator state, captured between draws. Default/version zero is invalid.</summary>
    public readonly struct RandomState
    {
        public RandomState(int algorithmVersion, ulong value)
        {
            AlgorithmVersion = algorithmVersion;
            Value = value;
        }

        public int AlgorithmVersion { get; }
        public ulong Value { get; }
    }
}
