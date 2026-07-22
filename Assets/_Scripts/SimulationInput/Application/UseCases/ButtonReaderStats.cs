using System.Collections.Generic;
using SimulationInput.Domain;

namespace SimulationInput.Application
{
    internal sealed class ButtonReaderStats
    {
        internal readonly List<IButtonStatePuller> buttonStatePullers = new();
        internal readonly List<IAxisStatePuller> axisStatePullers = new();
        internal readonly List<ButtonStateReader> buttonStateReader = new();
        internal readonly List<AxisStateReader> axisStateReader = new();

        internal bool hasCommittedTick;
        internal ulong lastCommittedTick;
    }
}
