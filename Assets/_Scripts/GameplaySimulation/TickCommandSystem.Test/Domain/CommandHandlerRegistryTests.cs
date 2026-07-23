using System;
using System.Collections.Generic;
using NUnit.Framework;
using TickCommandSystem.Contract;
using TickCommandSystem.Domain;
using TickCommandSystem.Test.Fixtures;

namespace TickCommandSystem.Test.Domain
{
    [TestFixture]
    public sealed class CommandHandlerRegistryTests
    {
        [Test]
        public void RegisterHandler_NullHandler_Throws()
        {
            // Arrange
            CommandHandlerRegistry registry = new CommandHandlerRegistry();

            // Act / Assert
            Assert.Throws<ArgumentNullException>(
                () => registry.RegisterHandler<TestCommand>(null));
        }

        [Test]
        public void RegisterHandler_DuplicateCommandType_Throws()
        {
            // Arrange
            CommandHandlerRegistry registry = new CommandHandlerRegistry();
            List<CommandRecord> records = new List<CommandRecord>();
            registry.RegisterHandler(
                new RecordingCommandHandler<TestCommand>(records, "test"));

            // Act / Assert
            Assert.Throws<InvalidOperationException>(
                () => registry.RegisterHandler(
                    new RecordingCommandHandler<TestCommand>(records, "test")));
        }

        [Test]
        public void Dispatch_RegisteredHandler_ReceivesDataAndCommand()
        {
            // Arrange
            CommandHandlerRegistry registry = new CommandHandlerRegistry();
            List<CommandRecord> records = new List<CommandRecord>();
            CommandData data = CommandData.External(7, CommandType.LifeCycle);

            registry.RegisterHandler(
                new RecordingCommandHandler<TestCommand>(records, "test"));

            // Act
            registry.Dispatch(data, new TestCommand(42));

            // Assert
            Assert.AreEqual(1, records.Count);
            Assert.AreEqual("test", records[0].Label);
            Assert.AreEqual(data.Tick, records[0].Data.Tick);
            Assert.AreEqual(42, ((TestCommand)records[0].CommandInstance).Value);
        }

        [Test]
        public void Dispatch_UnregisteredCommandType_Throws()
        {
            // Arrange
            CommandHandlerRegistry registry = new CommandHandlerRegistry();

            // Act / Assert
            Assert.Throws<InvalidOperationException>(
                () => registry.Dispatch(
                    CommandData.Internal(1, CommandType.Gameplay),
                    new TestCommand(1)));
        }

        [Test]
        public void Dispatch_NullCommand_Throws()
        {
            // Arrange
            CommandHandlerRegistry registry = new CommandHandlerRegistry();

            // Act / Assert
            Assert.Throws<ArgumentNullException>(
                () => registry.Dispatch(
                    CommandData.Internal(1, CommandType.Gameplay),
                    null));
        }
    }
}
