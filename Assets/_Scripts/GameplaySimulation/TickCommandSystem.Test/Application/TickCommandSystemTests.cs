using System;
using System.Collections.Generic;
using NUnit.Framework;
using TickCommandSystem.Contract;
using TickCommandSystem.Test.Fixtures;

using CommandDispatcher = global::TickCommandSystem.Application.TickCommandSystem;

namespace TickCommandSystem.Test.Application
{
    [TestFixture]
    public sealed class TickCommandSystemTests
    {
        [Test]
        public void Constructor_NonPositiveMaxWaves_Throws()
        {
            // Act / Assert
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CommandDispatcher(0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CommandDispatcher(-1));
        }

        [Test]
        public void DispatchCommands_NoPendingCommands_ReturnsZero()
        {
            // Arrange
            CommandDispatcher dispatcher = new CommandDispatcher();

            // Act
            int waveCount = dispatcher.DispatchCommands();

            // Assert
            Assert.AreEqual(0, waveCount);
        }

        [Test]
        public void EnqueueCommand_NullCommand_Throws()
        {
            // Arrange
            CommandDispatcher dispatcher = new CommandDispatcher();

            // Act / Assert
            Assert.Throws<ArgumentNullException>(
                () => dispatcher.EnqueueCommand(
                    CommandData.Internal(1, CommandType.Gameplay),
                    null));
        }

        [Test]
        public void RegisterCommandHandler_DuplicateCommandType_Throws()
        {
            // Arrange
            CommandDispatcher dispatcher = new CommandDispatcher();
            List<CommandRecord> records = new List<CommandRecord>();
            dispatcher.RegisterCommandHandler(
                new RecordingCommandHandler<TestCommand>(records, "test"));

            // Act / Assert
            Assert.Throws<InvalidOperationException>(
                () => dispatcher.RegisterCommandHandler(
                    new RecordingCommandHandler<TestCommand>(records, "test")));
        }

        [Test]
        public void DispatchCommands_DispatchesQueuedCommandsInEnqueueOrder()
        {
            // Arrange
            CommandDispatcher dispatcher = new CommandDispatcher();
            List<CommandRecord> records = new List<CommandRecord>();
            CommandData firstData = CommandData.Internal(1, CommandType.Gameplay);
            CommandData secondData = CommandData.External(2, CommandType.Physics);

            dispatcher.RegisterCommandHandler(
                new RecordingCommandHandler<TestCommand>(records, "test"));
            dispatcher.EnqueueCommand(firstData, new TestCommand(10));
            dispatcher.EnqueueCommand(secondData, new TestCommand(20));

            // Act
            int waveCount = dispatcher.DispatchCommands();

            // Assert
            Assert.AreEqual(1, waveCount);
            CollectionAssert.AreEqual(new[] { 10, 20 }, CopyTestCommandValues(records));
            CollectionAssert.AreEqual(new[] { 1ul, 2ul }, CopyTicks(records));
        }

        [Test]
        public void DispatchCommands_CommandsQueuedDuringDispatch_RunInNextWave()
        {
            // Arrange
            CommandDispatcher dispatcher = new CommandDispatcher();
            List<CommandRecord> records = new List<CommandRecord>();

            dispatcher.RegisterCommandHandler(
                new RecordingCommandHandler<TestCommand>(
                    records,
                    "parent",
                    (data, command) => dispatcher.EnqueueCommand(
                        CommandData.Internal(data.Tick + 100, CommandType.LifeCycle),
                        new SecondaryTestCommand(command.Value * 10))));
            dispatcher.RegisterCommandHandler(
                new RecordingCommandHandler<SecondaryTestCommand>(
                    records,
                    "child"));

            dispatcher.EnqueueCommand(
                CommandData.Internal(1, CommandType.Gameplay),
                new TestCommand(1));
            dispatcher.EnqueueCommand(
                CommandData.Internal(2, CommandType.Gameplay),
                new TestCommand(2));

            // Act
            int waveCount = dispatcher.DispatchCommands();

            // Assert
            Assert.AreEqual(2, waveCount);
            CollectionAssert.AreEqual(
                new[] { "parent", "parent", "child", "child" },
                CopyLabels(records));
            CollectionAssert.AreEqual(
                new[] { 1, 2, 10, 20 },
                CopyCommandValues(records));
            CollectionAssert.AreEqual(
                new[] { 1ul, 2ul, 101ul, 102ul },
                CopyTicks(records));
        }

        [Test]
        public void DispatchCommands_MaxWavesReached_ThrowsAndClearsPendingCommands()
        {
            // Arrange
            CommandDispatcher dispatcher = new CommandDispatcher(maxCommandWaves: 1);
            List<CommandRecord> records = new List<CommandRecord>();

            dispatcher.RegisterCommandHandler(
                new RecordingCommandHandler<TestCommand>(
                    records,
                    "parent",
                    (data, command) => dispatcher.EnqueueCommand(
                        CommandData.Internal(data.Tick + 1, CommandType.LifeCycle),
                        new SecondaryTestCommand(command.Value))));
            dispatcher.RegisterCommandHandler(
                new RecordingCommandHandler<SecondaryTestCommand>(
                    records,
                    "child"));

            dispatcher.EnqueueCommand(
                CommandData.Internal(1, CommandType.Gameplay),
                new TestCommand(10));

            // Act / Assert
            Assert.Throws<InvalidOperationException>(
                () => dispatcher.DispatchCommands());
            CollectionAssert.AreEqual(new[] { "parent" }, CopyLabels(records));

            records.Clear();
            Assert.AreEqual(0, dispatcher.DispatchCommands());

            dispatcher.EnqueueCommand(
                CommandData.Internal(3, CommandType.LifeCycle),
                new SecondaryTestCommand(30));
            Assert.AreEqual(1, dispatcher.DispatchCommands());
            CollectionAssert.AreEqual(new[] { "child" }, CopyLabels(records));
            CollectionAssert.AreEqual(new[] { 30 }, CopyCommandValues(records));
        }

        [Test]
        public void DispatchCommands_HandlerThrows_ClearsRemainingCommands()
        {
            // Arrange
            CommandDispatcher dispatcher = new CommandDispatcher();
            List<CommandRecord> records = new List<CommandRecord>();

            dispatcher.RegisterCommandHandler(
                new ThrowingCommandHandler<TestCommand>());
            dispatcher.RegisterCommandHandler(
                new RecordingCommandHandler<SecondaryTestCommand>(
                    records,
                    "secondary"));

            dispatcher.EnqueueCommand(
                CommandData.Internal(1, CommandType.Gameplay),
                new TestCommand(10));
            dispatcher.EnqueueCommand(
                CommandData.Internal(2, CommandType.Gameplay),
                new SecondaryTestCommand(20));

            // Act / Assert
            Assert.Throws<InvalidOperationException>(
                () => dispatcher.DispatchCommands());
            Assert.AreEqual(0, records.Count);

            dispatcher.EnqueueCommand(
                CommandData.Internal(3, CommandType.Gameplay),
                new SecondaryTestCommand(30));
            Assert.AreEqual(1, dispatcher.DispatchCommands());
            CollectionAssert.AreEqual(new[] { 30 }, CopyCommandValues(records));
        }

        [Test]
        public void SameOperationSequence_OnDifferentDispatchers_ProducesSameDispatchLog()
        {
            // Act
            string[] firstRun = RunDeterministicDispatchScenario();
            string[] secondRun = RunDeterministicDispatchScenario();

            // Assert
            CollectionAssert.AreEqual(firstRun, secondRun);
        }

        private static string[] RunDeterministicDispatchScenario()
        {
            CommandDispatcher dispatcher = new CommandDispatcher();
            List<string> log = new List<string>();

            dispatcher.RegisterCommandHandler(
                new RecordingCommandHandler<TestCommand>(
                    new List<CommandRecord>(),
                    "primary",
                    (data, command) =>
                    {
                        log.Add($"primary:{data.Tick}:{command.Value}");
                        dispatcher.EnqueueCommand(
                            CommandData.Internal(data.Tick + 10, CommandType.LifeCycle),
                            new SecondaryTestCommand(command.Value + 100));
                    }));
            dispatcher.RegisterCommandHandler(
                new RecordingCommandHandler<SecondaryTestCommand>(
                    new List<CommandRecord>(),
                    "secondary",
                    (data, command) => log.Add(
                        $"secondary:{data.Tick}:{command.Value}")));

            dispatcher.EnqueueCommand(
                CommandData.Internal(1, CommandType.Gameplay),
                new TestCommand(10));
            dispatcher.EnqueueCommand(
                CommandData.External(2, CommandType.Physics),
                new SecondaryTestCommand(20));
            dispatcher.EnqueueCommand(
                CommandData.Internal(3, CommandType.Gameplay),
                new TestCommand(30));

            dispatcher.DispatchCommands();

            return log.ToArray();
        }

        private static string[] CopyLabels(List<CommandRecord> records)
        {
            string[] labels = new string[records.Count];
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = records[i].Label;
            }

            return labels;
        }

        private static ulong[] CopyTicks(List<CommandRecord> records)
        {
            ulong[] ticks = new ulong[records.Count];
            for (int i = 0; i < ticks.Length; i++)
            {
                ticks[i] = records[i].Data.Tick;
            }

            return ticks;
        }

        private static int[] CopyTestCommandValues(List<CommandRecord> records)
        {
            int[] values = new int[records.Count];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = ((TestCommand)records[i].CommandInstance).Value;
            }

            return values;
        }

        private static int[] CopyCommandValues(List<CommandRecord> records)
        {
            int[] values = new int[records.Count];
            for (int i = 0; i < values.Length; i++)
            {
                if (records[i].CommandInstance is TestCommand testCommand)
                {
                    values[i] = testCommand.Value;
                    continue;
                }

                values[i] = ((SecondaryTestCommand)records[i].CommandInstance).Value;
            }

            return values;
        }
    }
}
