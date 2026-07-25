using System;
using System.Collections.Generic;
using NUnit.Framework;
using SimulationCore.Contracts;

namespace ReplayAndSimulationCore.Test.CommandSystem.Domain
{
    public sealed class CommandHandlerRegistryTests
    {
        [Test]
        public void RegisterHandler_WhenHandlerIsNull_Throws()
        {
            CommandHandlerRegistryAccessor registry = new();

            Assert.Throws<ArgumentNullException>(
                () => registry.RegisterHandler<TraceCommand>(null));
        }

        [Test]
        public void RegisterHandler_WhenSameCommandTypeIsRegisteredTwice_Throws()
        {
            CommandHandlerRegistryAccessor registry = new();
            registry.RegisterHandler(new RecordingHandler<TraceCommand>(_ => { }));

            Assert.Throws<InvalidOperationException>(
                () => registry.RegisterHandler(new RecordingHandler<TraceCommand>(_ => { })));
        }

        [Test]
        public void Dispatch_WhenCommandIsNull_Throws()
        {
            CommandHandlerRegistryAccessor registry = new();
            registry.RegisterHandler(new RecordingHandler<TraceCommand>(_ => { }));

            Assert.Throws<ArgumentNullException>(
                () => registry.Dispatch(Metadata(1), null));
        }

        [Test]
        public void Dispatch_WhenNoHandlerIsRegistered_Throws()
        {
            CommandHandlerRegistryAccessor registry = new();

            Assert.Throws<InvalidOperationException>(
                () => registry.Dispatch(Metadata(1), new TraceCommand("missing")));
        }

        [Test]
        public void Dispatch_WhenHandlerIsRegistered_InvokesMatchingHandler()
        {
            CommandHandlerRegistryAccessor registry = new();
            List<string> trace = new();
            registry.RegisterHandler(
                new RecordingHandler<TraceCommand>(command => trace.Add(command.Label)));

            registry.Dispatch(Metadata(1), new TraceCommand("first"));
            registry.Dispatch(Metadata(2), new TraceCommand("second"));

            CollectionAssert.AreEqual(new[] { "first", "second" }, trace);
        }

        [Test]
        public void Dispatch_WhenMultipleHandlersAreRegistered_UsesExactCommandType()
        {
            CommandHandlerRegistryAccessor registry = new();
            List<string> trace = new();
            registry.RegisterHandler(
                new RecordingHandler<TraceCommand>(command => trace.Add($"trace:{command.Label}")));
            registry.RegisterHandler(
                new RecordingHandler<FollowUpCommand>(command => trace.Add($"follow:{command.Label}")));

            registry.Dispatch(Metadata(1), new FollowUpCommand("one"));
            registry.Dispatch(Metadata(2), new TraceCommand("two"));
            registry.Dispatch(Metadata(3), new FollowUpCommand("three"));

            CollectionAssert.AreEqual(
                new[] { "follow:one", "trace:two", "follow:three" },
                trace);
        }

        [Test]
        public void ReplayingSameDispatchSequence_ProducesSameHandlerTrace()
        {
            string[] expected =
            {
                "trace:first",
                "follow:first:a",
                "trace:second",
                "follow:second:a",
                "follow:second:b",
                "trace:third"
            };

            for (int i = 0; i < 20; i++)
            {
                CollectionAssert.AreEqual(expected, RunDeterministicRegistryScenario());
            }
        }

        private static List<string> RunDeterministicRegistryScenario()
        {
            CommandHandlerRegistryAccessor registry = new();
            List<string> trace = new();

            registry.RegisterHandler(
                new RecordingHandler<TraceCommand>(command => trace.Add($"trace:{command.Label}")));
            registry.RegisterHandler(
                new RecordingHandler<FollowUpCommand>(command => trace.Add($"follow:{command.Label}")));

            registry.Dispatch(Metadata(1), new TraceCommand("first"));
            registry.Dispatch(Metadata(1), new FollowUpCommand("first:a"));
            registry.Dispatch(Metadata(2), new TraceCommand("second"));
            registry.Dispatch(Metadata(2), new FollowUpCommand("second:a"));
            registry.Dispatch(Metadata(2), new FollowUpCommand("second:b"));
            registry.Dispatch(Metadata(3), new TraceCommand("third"));

            return trace;
        }

        private static CommandMetadata Metadata(ulong tick)
        {
            return CommandMetadata.Internal(tick, CommandSource.Gameplay);
        }

        private sealed class RecordingHandler<TCommand> : ICommandHandler<TCommand>
            where TCommand : ICommand
        {
            private readonly Action<TCommand> handle;

            internal RecordingHandler(Action<TCommand> handle)
            {
                this.handle = handle;
            }

            public void Handle(TCommand command)
            {
                handle(command);
            }
        }

        private sealed class TraceCommand : ICommand
        {
            internal readonly string Label;

            internal TraceCommand(string label)
            {
                Label = label;
            }
        }

        private sealed class FollowUpCommand : ICommand
        {
            internal readonly string Label;

            internal FollowUpCommand(string label)
            {
                Label = label;
            }
        }
    }
}

