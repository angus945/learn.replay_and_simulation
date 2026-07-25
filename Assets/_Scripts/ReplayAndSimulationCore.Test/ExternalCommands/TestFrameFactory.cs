using System;
using System.Collections.Generic;
using System.Reflection;
using SimulationCore.ExternalCommands.PlayerInput.Domain;

namespace ReplayAndSimulationCore.Test.ExternalCommands
{
    internal static class TestFrameFactory
    {
        internal static TickInputFrame CreateFrame(
            IReadOnlyDictionary<Type, int> buttonInputIndexByKey,
            IReadOnlyDictionary<Type, int> axisInputIndexByKey,
            ButtonInputState[] buttons,
            AxisInputEvent[] axes)
        {
            ConstructorInfo constructor = typeof(TickInputFrame).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(IReadOnlyDictionary<Type, int>),
                    typeof(IReadOnlyDictionary<Type, int>),
                    typeof(ButtonInputState[]),
                    typeof(AxisInputEvent[])
                },
                null);

            return (TickInputFrame)constructor.Invoke(
                new object[]
                {
                    buttonInputIndexByKey,
                    axisInputIndexByKey,
                    buttons,
                    axes
                });
        }

        internal static void SetTick(TickInputFrame frame, ulong tick)
        {
            MethodInfo setTick = typeof(TickInputFrame).GetMethod(
                "SetTick",
                BindingFlags.Instance | BindingFlags.NonPublic);

            setTick.Invoke(frame, new object[] { tick });
        }
    }
}
