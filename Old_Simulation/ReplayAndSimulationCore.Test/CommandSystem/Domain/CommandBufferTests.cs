using System.Collections.Generic;
using NUnit.Framework;
using SimulationCore.Contracts;

namespace ReplayAndSimulationCore.Test.CommandSystem.Domain
{
    public sealed class CommandBufferTests
    {
        [Test]
        public void Add_WhenCommandIsAdded_MarksBufferAsPending()
        {
            CommandBufferAccessor buffer = new();

            buffer.Add(Metadata(1), new TraceCommand("first"));

            Assert.IsTrue(buffer.HasPending);
            Assert.IsEmpty(buffer.CurrentCommands);
        }

        [Test]
        public void BeginNextWave_MovesPendingCommandsToCurrentInEnqueueOrder()
        {
            CommandBufferAccessor buffer = new();

            buffer.Add(Metadata(1), new TraceCommand("first"));
            buffer.Add(Metadata(2), new TraceCommand("second"));
            buffer.BeginNextWave();

            CollectionAssert.AreEqual(
                new[] { "first", "second" },
                Labels(buffer.CurrentCommands));
            CollectionAssert.AreEqual(
                new ulong[] { 1, 2 },
                Ticks(buffer.CurrentMetadata));
            Assert.IsFalse(buffer.HasPending);
        }

        [Test]
        public void Add_WhenCurrentWaveIsBeingRead_QueuesCommandForNextWave()
        {
            CommandBufferAccessor buffer = new();

            buffer.Add(Metadata(1), new TraceCommand("first"));
            buffer.Add(Metadata(2), new TraceCommand("second"));
            buffer.BeginNextWave();
            buffer.Add(Metadata(3), new TraceCommand("third"));

            CollectionAssert.AreEqual(
                new[] { "first", "second" },
                Labels(buffer.CurrentCommands));
            Assert.IsTrue(buffer.HasPending);

            buffer.BeginNextWave();

            CollectionAssert.AreEqual(new[] { "third" }, Labels(buffer.CurrentCommands));
            CollectionAssert.AreEqual(new ulong[] { 3 }, Ticks(buffer.CurrentMetadata));
            Assert.IsFalse(buffer.HasPending);
        }

        [Test]
        public void ClearAll_WhenCurrentAndPendingCommandsExist_ClearsBoth()
        {
            CommandBufferAccessor buffer = new();

            buffer.Add(Metadata(1), new TraceCommand("current"));
            buffer.BeginNextWave();
            buffer.Add(Metadata(2), new TraceCommand("pending"));
            buffer.ClearAll();

            Assert.IsFalse(buffer.HasPending);
            Assert.IsEmpty(buffer.CurrentCommands);

            buffer.Add(Metadata(3), new TraceCommand("fresh"));
            buffer.BeginNextWave();

            CollectionAssert.AreEqual(new[] { "fresh" }, Labels(buffer.CurrentCommands));
            CollectionAssert.AreEqual(new ulong[] { 3 }, Ticks(buffer.CurrentMetadata));
        }

        [Test]
        public void ReplayingSameBufferOperations_ProducesSameWaveTrace()
        {
            string[] expected =
            {
                "wave:0:root-a:1",
                "wave:0:root-b:2",
                "wave:1:child-a:3",
                "wave:1:child-b:4",
                "wave:1:child-c:5",
                "wave:2:grandchild:6"
            };

            for (int i = 0; i < 20; i++)
            {
                CollectionAssert.AreEqual(expected, RunDeterministicBufferScenario());
            }
        }

        private static List<string> RunDeterministicBufferScenario()
        {
            CommandBufferAccessor buffer = new();
            List<string> trace = new();

            buffer.Add(Metadata(1), new TraceCommand("root-a"));
            buffer.Add(Metadata(2), new TraceCommand("root-b"));

            int wave = 0;
            while (buffer.HasPending)
            {
                buffer.BeginNextWave();

                IReadOnlyList<ICommand> commands = buffer.CurrentCommands;
                IReadOnlyList<CommandMetadata> metadata = buffer.CurrentMetadata;
                for (int i = 0; i < commands.Count; i++)
                {
                    TraceCommand command = (TraceCommand)commands[i];
                    trace.Add($"wave:{wave}:{command.Label}:{metadata[i].Tick}");

                    if (command.Label == "root-a")
                    {
                        buffer.Add(Metadata(3), new TraceCommand("child-a"));
                    }
                    else if (command.Label == "root-b")
                    {
                        buffer.Add(Metadata(4), new TraceCommand("child-b"));
                        buffer.Add(Metadata(5), new TraceCommand("child-c"));
                    }
                    else if (command.Label == "child-b")
                    {
                        buffer.Add(Metadata(6), new TraceCommand("grandchild"));
                    }
                }

                wave++;
            }

            return trace;
        }

        private static CommandMetadata Metadata(ulong tick)
        {
            return CommandMetadata.Internal(tick, CommandSource.Gameplay);
        }

        private static List<string> Labels(IReadOnlyList<ICommand> commands)
        {
            List<string> labels = new();
            for (int i = 0; i < commands.Count; i++)
            {
                labels.Add(((TraceCommand)commands[i]).Label);
            }

            return labels;
        }

        private static List<ulong> Ticks(IReadOnlyList<CommandMetadata> metadata)
        {
            List<ulong> ticks = new();
            for (int i = 0; i < metadata.Count; i++)
            {
                ticks.Add(metadata[i].Tick);
            }

            return ticks;
        }

        private sealed class TraceCommand : ICommand
        {
            internal readonly string Label;

            internal TraceCommand(string label)
            {
                Label = label;
            }
        }
    }
}

