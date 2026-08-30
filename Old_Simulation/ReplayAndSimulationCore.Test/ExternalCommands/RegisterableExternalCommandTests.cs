using System.Collections.Generic;
using NUnit.Framework;
using SimulationCore.ExternalCommands;
using SimulationCore.ExternalCommands.Contract;

namespace ReplayAndSimulationCore.Test.ExternalCommands
{
    [TestFixture]
    public sealed class RegisterableExternalCommandTests
    {
        [Test]
        public void AcquireCommands_InvokesRegisteredProvidersInRegistrationOrder()
        {
            List<string> calls = new List<string>();
            RegisterableExternalCommand externalCommands = new RegisterableExternalCommand();
            externalCommands.RegisterExternalCommandProvider(new RecordingProvider("first", calls));
            externalCommands.RegisterExternalCommandProvider(new RecordingProvider("second", calls));

            externalCommands.AcquireCommands(12);

            CollectionAssert.AreEqual(
                new[] { "first:12", "second:12" },
                calls);
        }

        [Test]
        public void AcquireCommands_WithDelta_DelegatesTheTickToRegisteredProviders()
        {
            List<string> calls = new List<string>();
            RegisterableExternalCommand externalCommands = new RegisterableExternalCommand();
            externalCommands.RegisterExternalCommandProvider(new RecordingProvider("input", calls));

            externalCommands.AcquireCommands(27, 0.016f);

            CollectionAssert.AreEqual(new[] { "input:27" }, calls);
        }

        [Test]
        public void RegisterExternalCommandProvider_WhenSameProviderRegisteredTwice_Throws()
        {
            RegisterableExternalCommand externalCommands = new RegisterableExternalCommand();
            RecordingProvider provider = new RecordingProvider("input", new List<string>());
            externalCommands.RegisterExternalCommandProvider(provider);

            Assert.Throws<System.Exception>(
                () => externalCommands.RegisterExternalCommandProvider(provider));
        }

        [Test]
        public void AcquireCommands_WhenSameScriptRunsTwice_ProducesSameCallSequence()
        {
            CollectionAssert.AreEqual(
                RunProviderScript(),
                RunProviderScript());
        }

        private static string[] RunProviderScript()
        {
            List<string> calls = new List<string>();
            RegisterableExternalCommand externalCommands = new RegisterableExternalCommand();
            externalCommands.RegisterExternalCommandProvider(new RecordingProvider("input", calls));
            externalCommands.RegisterExternalCommandProvider(new RecordingProvider("debug", calls));

            externalCommands.AcquireCommands(0);
            externalCommands.AcquireCommands(5, 0.1f);
            externalCommands.AcquireCommands(5);

            return calls.ToArray();
        }

        private sealed class RecordingProvider : IExternalCommandProvider
        {
            private readonly string name;
            private readonly List<string> calls;

            public RecordingProvider(string name, List<string> calls)
            {
                this.name = name;
                this.calls = calls;
            }

            public void EnqueueCommands(ulong tick)
            {
                calls.Add($"{name}:{tick}");
            }
        }
    }
}
