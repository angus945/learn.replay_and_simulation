using NUnit.Framework;
using TickCommandSystem.Contract;

namespace TickCommandSystem.Test.Contract
{
    [TestFixture]
    public sealed class CommandDataTests
    {
        [Test]
        public void External_CreatesExternalCommandData()
        {
            // Act
            CommandData data = CommandData.External(10, CommandType.Gameplay);

            // Assert
            Assert.AreEqual(10ul, data.Tick);
            Assert.IsTrue(data.IsExternal);
            Assert.AreEqual(CommandType.Gameplay, data.Type);
        }

        [Test]
        public void Internal_CreatesInternalCommandData()
        {
            // Act
            CommandData data = CommandData.Internal(20, CommandType.Physics);

            // Assert
            Assert.AreEqual(20ul, data.Tick);
            Assert.IsFalse(data.IsExternal);
            Assert.AreEqual(CommandType.Physics, data.Type);
        }
    }
}
