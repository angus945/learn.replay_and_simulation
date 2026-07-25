using System;
using System.Collections.Generic;
using NUnit.Framework;
using SimulationCore.CommandSystem.Application;
using SimulationCore.Contracts;

namespace ReplayAndSimulationCore.Test.CommandSystem.Application
{
    public sealed class CommandServicesTests
    {
        [Test]
        public void Constructor_WhenMaxCommandWavesIsZero_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CommandServices(0));
        }

        [Test]
        public void RegisterCommandHandler_WhenHandlerIsNull_Throws()
        {
            CommandServices services = new();

            Assert.Throws<ArgumentNullException>(
                () => services.RegisterCommandHandler<TraceCommand>(null));
        }

        [Test]
        public void RegisterCommandHandler_WhenSameCommandTypeIsRegisteredTwice_Throws()
        {
            CommandServices services = new();
            services.RegisterCommandHandler(new RecordingHandler<TraceCommand>(_ => { }));

            Assert.Throws<InvalidOperationException>(
                () => services.RegisterCommandHandler(new RecordingHandler<TraceCommand>(_ => { })));
        }

        [Test]
        public void EnqueueCommand_WhenCommandIsNull_Throws()
        {
            CommandServices services = new();

            Assert.Throws<ArgumentNullException>(
                () => services.EnqueueCommand(Metadata(), null));
        }

        [Test]
        public void DispatchCommands_WhenNoCommandsArePending_DoesNotInvokeHandlers()
        {
            CommandServices services = new();
            int calls = 0;
            services.RegisterCommandHandler(new RecordingHandler<TraceCommand>(_ => calls++));

            services.DispatchCommands();

            Assert.AreEqual(0, calls);
        }

        [Test]
        public void DispatchCommands_WhenCommandHasRegisteredHandler_DispatchesIt()
        {
            CommandServices services = new();
            List<string> trace = new();
            services.RegisterCommandHandler(
                new RecordingHandler<TraceCommand>(command => trace.Add(command.Label)));

            services.EnqueueCommand(Metadata(), new TraceCommand("first"));
            services.EnqueueCommand(Metadata(), new TraceCommand("second"));
            services.DispatchCommands();

            CollectionAssert.AreEqual(new[] { "first", "second" }, trace);
        }

        [Test]
        public void DispatchCommands_WhenCommandHasNoRegisteredHandler_ThrowsAndClearsBufferedCommands()
        {
            CommandServices services = new();
            services.EnqueueCommand(Metadata(), new UnhandledCommand());

            Assert.Throws<InvalidOperationException>(() => services.DispatchCommands());

            int calls = 0;
            services.RegisterCommandHandler(new RecordingHandler<UnhandledCommand>(_ => calls++));
            services.DispatchCommands();

            Assert.AreEqual(0, calls);
        }

        [Test]
        public void DispatchCommands_WhenHandlerThrows_ClearsBufferedCommands()
        {
            CommandServices services = new();
            List<string> trace = new();
            bool throwOnNextCommand = true;
            services.RegisterCommandHandler(
                new RecordingHandler<TraceCommand>(
                    command =>
                    {
                        trace.Add(command.Label);
                        if (throwOnNextCommand)
                        {
                            throwOnNextCommand = false;
                            throw new InvalidOperationException("handler failed");
                        }
                    }));

            services.EnqueueCommand(Metadata(), new TraceCommand("first"));
            services.EnqueueCommand(Metadata(), new TraceCommand("second"));

            Assert.Throws<InvalidOperationException>(() => services.DispatchCommands());
            services.DispatchCommands();

            CollectionAssert.AreEqual(new[] { "first" }, trace);
        }

        [Test]
        public void DispatchCommands_WhenHandlersEnqueueMoreCommands_ProcessesThemAfterCurrentWave()
        {
            CommandServices services = new();
            List<string> trace = new();

            services.RegisterCommandHandler(
                new RecordingHandler<TraceCommand>(
                    command =>
                    {
                        trace.Add($"trace:{command.Label}");
                        services.EnqueueCommand(Metadata(), new FollowUpCommand($"{command.Label}:a"));
                        services.EnqueueCommand(Metadata(), new FollowUpCommand($"{command.Label}:b"));
                    }));
            services.RegisterCommandHandler(
                new RecordingHandler<FollowUpCommand>(
                    command => trace.Add($"follow:{command.Label}")));

            services.EnqueueCommand(Metadata(), new TraceCommand("first"));
            services.EnqueueCommand(Metadata(), new TraceCommand("second"));
            services.DispatchCommands();

            CollectionAssert.AreEqual(
                new[]
                {
                    "trace:first",
                    "trace:second",
                    "follow:first:a",
                    "follow:first:b",
                    "follow:second:a",
                    "follow:second:b"
                },
                trace);
        }

        [Test]
        public void DispatchCommands_ReplayingSameCommandSequence_ProducesSameHandlerTrace()
        {
            string[] expected =
            {
                "trace:first",
                "trace:second",
                "trace:third",
                "follow:first:a",
                "follow:first:b",
                "follow:second:a",
                "follow:second:b",
                "summary:summary",
                "follow:third:a",
                "follow:third:b"
            };

            for (int i = 0; i < 20; i++)
            {
                CollectionAssert.AreEqual(expected, RunDeterminismScenario());
            }
        }

        [Test]
        public void DispatchCommands_WhenMaxCommandWavesIsReached_ThrowsAndClearsBufferedCommands()
        {
            CommandServices services = new(maxCommandWaves: 2);
            int calls = 0;

            services.RegisterCommandHandler(
                new RecordingHandler<RecursiveCommand>(
                    command =>
                    {
                        calls++;
                        services.EnqueueCommand(Metadata(), new RecursiveCommand(command.Depth + 1));
                    }));

            services.EnqueueCommand(Metadata(), new RecursiveCommand(0));

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() => services.DispatchCommands());

            StringAssert.Contains("Max command dispatch waves reached", exception.Message);
            Assert.AreEqual(2, calls);

            services.DispatchCommands();

            Assert.AreEqual(2, calls);
        }

        private static List<string> RunDeterminismScenario()
        {
            CommandServices services = new();
            List<string> trace = new();

            services.RegisterCommandHandler(
                new RecordingHandler<TraceCommand>(
                    command =>
                    {
                        trace.Add($"trace:{command.Label}");
                        services.EnqueueCommand(Metadata(), new FollowUpCommand($"{command.Label}:a"));
                        services.EnqueueCommand(Metadata(), new FollowUpCommand($"{command.Label}:b"));

                        if (command.Label == "second")
                        {
                            services.EnqueueCommand(Metadata(), new SummaryCommand("summary"));
                        }
                    }));
            services.RegisterCommandHandler(
                new RecordingHandler<FollowUpCommand>(
                    command => trace.Add($"follow:{command.Label}")));
            services.RegisterCommandHandler(
                new RecordingHandler<SummaryCommand>(
                    command => trace.Add($"summary:{command.Label}")));

            services.EnqueueCommand(Metadata(), new TraceCommand("first"));
            services.EnqueueCommand(Metadata(), new TraceCommand("second"));
            services.EnqueueCommand(Metadata(), new TraceCommand("third"));
            services.DispatchCommands();

            return trace;
        }

        private static CommandMetadata Metadata()
        {
            return CommandMetadata.Internal(42, CommandType.Gameplay);
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

        private sealed class SummaryCommand : ICommand
        {
            internal readonly string Label;

            internal SummaryCommand(string label)
            {
                Label = label;
            }
        }

        private sealed class RecursiveCommand : ICommand
        {
            internal readonly int Depth;

            internal RecursiveCommand(int depth)
            {
                Depth = depth;
            }
        }

        private sealed class UnhandledCommand : ICommand
        {
        }
    }
}
