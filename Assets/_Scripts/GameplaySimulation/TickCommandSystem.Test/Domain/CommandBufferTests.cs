using NUnit.Framework;
using TickCommandSystem.Contract;
using TickCommandSystem.Domain;
using TickCommandSystem.Test.Fixtures;

namespace TickCommandSystem.Test.Domain
{
    [TestFixture]
    public sealed class CommandBufferTests
    {
        [Test]
        public void Add_BeforeBeginNextWave_SetsPendingWithoutChangingCurrent()
        {
            // Arrange
            CommandBuffer buffer = new CommandBuffer();

            // Act
            buffer.Add(
                CommandData.Internal(1, CommandType.Gameplay),
                new TestCommand(10));

            // Assert
            Assert.IsTrue(buffer.HasPending);
            Assert.AreEqual(0, buffer.Current.Count);
        }

        [Test]
        public void BeginNextWave_MovesPendingCommandsToCurrentInEnqueueOrder()
        {
            // Arrange
            CommandBuffer buffer = new CommandBuffer();
            CommandData firstData = CommandData.Internal(1, CommandType.Gameplay);
            CommandData secondData = CommandData.External(2, CommandType.Physics);

            buffer.Add(firstData, new TestCommand(10));
            buffer.Add(secondData, new TestCommand(20));

            // Act
            buffer.BeginNextWave();

            // Assert
            Assert.IsFalse(buffer.HasPending);
            Assert.AreEqual(2, buffer.Current.Count);
            Assert.AreEqual(firstData.Tick, buffer.Current[0].Data.Tick);
            Assert.AreEqual(10, ((TestCommand)buffer.Current[0].CommandInstance).Value);
            Assert.AreEqual(secondData.Tick, buffer.Current[1].Data.Tick);
            Assert.AreEqual(20, ((TestCommand)buffer.Current[1].CommandInstance).Value);
        }

        [Test]
        public void BeginNextWave_ReleasesPreviousCurrentBeforeMovingNextPendingWave()
        {
            // Arrange
            CommandBuffer buffer = new CommandBuffer();
            buffer.Add(
                CommandData.Internal(1, CommandType.Gameplay),
                new TestCommand(10));
            buffer.BeginNextWave();

            buffer.Add(
                CommandData.Internal(2, CommandType.Gameplay),
                new TestCommand(20));

            // Act
            buffer.BeginNextWave();

            // Assert
            Assert.IsFalse(buffer.HasPending);
            Assert.AreEqual(1, buffer.Current.Count);
            Assert.AreEqual(20, ((TestCommand)buffer.Current[0].CommandInstance).Value);
        }

        [Test]
        public void ClearAll_RemovesCurrentAndPendingCommandsAndLeavesBufferReusable()
        {
            // Arrange
            CommandBuffer buffer = new CommandBuffer();
            buffer.Add(
                CommandData.Internal(1, CommandType.Gameplay),
                new TestCommand(10));
            buffer.BeginNextWave();
            buffer.Add(
                CommandData.Internal(2, CommandType.Gameplay),
                new TestCommand(20));

            // Act
            buffer.ClearAll();

            // Assert
            Assert.IsFalse(buffer.HasPending);
            Assert.AreEqual(0, buffer.Current.Count);

            buffer.Add(
                CommandData.Internal(3, CommandType.Gameplay),
                new TestCommand(30));
            buffer.BeginNextWave();

            Assert.AreEqual(1, buffer.Current.Count);
            Assert.AreEqual(30, ((TestCommand)buffer.Current[0].CommandInstance).Value);
        }
    }
}
