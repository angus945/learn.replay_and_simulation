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
                () => services.EnqueueCommand<ICommand>(Metadata(), null));
        }

        [Test]
        public void EnqueueCommand_WhenEventInstanceIsPassed_Throws()
        {
            CommandServices services = new();

            Assert.Throws<InvalidOperationException>(
                () => services.EnqueueCommand(Metadata(), new TraceEvent("event")));
        }

        [Test]
        public void EnqueueEvent_WhenEventIsNull_Throws()
        {
            CommandServices services = new();

            Assert.Throws<ArgumentNullException>(
                () => services.EnqueueEvent<TraceEvent>(Metadata(), null));
        }

        [Test]
        public void DispatchCommands_WhenNoCommandsArePending_DoesNotInvokeHandlers()
        {
            CommandServices services = new();
            int calls = 0;
            services.RegisterCommandHandler(new RecordingHandler<TraceCommand>(_ => calls++));

            services.DispatchAll();

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
            services.DispatchAll();

            CollectionAssert.AreEqual(new[] { "first", "second" }, trace);
        }

        [Test]
        public void DispatchCommands_WhenCommandHasNoRegisteredHandler_ThrowsAndClearsBufferedCommands()
        {
            CommandServices services = new();
            services.EnqueueCommand(Metadata(), new UnhandledCommand());

            Assert.Throws<InvalidOperationException>(() => services.DispatchAll());

            Assert.DoesNotThrow(() => services.DispatchAll());
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

            Assert.Throws<InvalidOperationException>(() => services.DispatchAll());
            services.DispatchAll();

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
            services.DispatchAll();

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
        public void DispatchAll_WhenEventHasNoSubscribers_DoesNotThrow()
        {
            CommandServices services = new();
            List<string> trace = new();
            services.RegisterCommandHandler(
                new RecordingHandler<TraceCommand>(command => trace.Add(command.Label)));

            services.EnqueueEvent(Metadata(), new TraceEvent("unobserved"));
            services.EnqueueCommand(Metadata(), new TraceCommand("command"));

            Assert.DoesNotThrow(() => services.DispatchAll());
            CollectionAssert.AreEqual(new[] { "command" }, trace);
        }

        [Test]
        public void DispatchAll_WhenEventHasMultipleSubscribers_DispatchesSubscribersInRegistrationOrder()
        {
            CommandServices services = new();
            List<string> trace = new();
            services.RegisterEventHandler(
                new RecordingEventHandler<TraceEvent>(@event => trace.Add($"first:{@event.Label}")));
            services.RegisterEventHandler(
                new RecordingEventHandler<TraceEvent>(@event => trace.Add($"second:{@event.Label}")));
            services.RegisterCommandHandler(
                new RecordingHandler<TraceCommand>(command => trace.Add($"command:{command.Label}")));

            services.EnqueueEvent(Metadata(), new TraceEvent("event"));
            services.EnqueueCommand(Metadata(), new TraceCommand("command"));
            services.DispatchAll();

            CollectionAssert.AreEqual(
                new[] { "first:event", "second:event", "command:command" },
                trace);
        }

        [Test]
        public void DispatchAll_ReplayingSameCommandAndEventSequence_ProducesSameTrace()
        {
            string[] expected =
            {
                "event:first:alpha",
                "event:second:alpha",
                "event:audit:alpha",
                "command:first",
                "command:second",
                "event:first:beta",
                "event:second:beta",
                "event:audit:beta",
                "command:third"
            };

            for (int i = 0; i < 20; i++)
            {
                CollectionAssert.AreEqual(expected, RunCommandAndEventDeterminismScenario());
            }
        }

        [Test]
        public void RegisterCommandHandler_WhenDispatchHasStarted_Throws()
        {
            CommandServices services = new();
            services.RegisterCommandHandler(new RecordingHandler<TraceCommand>(_ => { }));
            services.EnqueueCommand(Metadata(), new TraceCommand("start"));
            services.DispatchAll();

            Assert.Throws<InvalidOperationException>(
                () => services.RegisterCommandHandler(new RecordingHandler<FollowUpCommand>(_ => { })));
        }

        [Test]
        public void RegisterEventHandler_WhenDispatchHasStarted_Throws()
        {
            CommandServices services = new();
            services.RegisterCommandHandler(new RecordingHandler<TraceCommand>(_ => { }));
            services.EnqueueCommand(Metadata(), new TraceCommand("start"));
            services.DispatchAll();

            Assert.Throws<InvalidOperationException>(
                () => services.RegisterEventHandler(new RecordingEventHandler<TraceEvent>(_ => { })));
        }

        [Test]
        public void RegisterCommandHandler_WhenCalledDuringDispatch_Throws()
        {
            CommandServices services = new();
            services.RegisterCommandHandler(
                new RecordingHandler<TraceCommand>(
                    _ => Assert.Throws<InvalidOperationException>(
                        () => services.RegisterCommandHandler(
                            new RecordingHandler<FollowUpCommand>(__ => { })))));

            services.EnqueueCommand(Metadata(), new TraceCommand("start"));
            services.DispatchAll();
        }

        [Test]
        public void RegisterEventHandler_WhenCalledDuringDispatch_Throws()
        {
            CommandServices services = new();
            services.RegisterCommandHandler(
                new RecordingHandler<TraceCommand>(
                    _ => Assert.Throws<InvalidOperationException>(
                        () => services.RegisterEventHandler(
                            new RecordingEventHandler<TraceEvent>(__ => { })))));

            services.EnqueueCommand(Metadata(), new TraceCommand("start"));
            services.DispatchAll();
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
                Assert.Throws<InvalidOperationException>(() => services.DispatchAll());

            StringAssert.Contains("Max command dispatch waves reached", exception.Message);
            Assert.AreEqual(2, calls);

            services.DispatchAll();

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
            services.DispatchAll();

            return trace;
        }

        private static List<string> RunCommandAndEventDeterminismScenario()
        {
            CommandServices services = new();
            List<string> trace = new();

            services.RegisterEventHandler(
                new RecordingEventHandler<TraceEvent>(
                    @event => trace.Add($"event:first:{@event.Label}")));
            services.RegisterEventHandler(
                new RecordingEventHandler<TraceEvent>(
                    @event => trace.Add($"event:second:{@event.Label}")));
            services.RegisterEventHandler(
                new RecordingEventHandler<AuditEvent>(
                    @event => trace.Add($"event:audit:{@event.Label}")));
            services.RegisterCommandHandler(
                new RecordingHandler<TraceCommand>(
                    command => trace.Add($"command:{command.Label}")));

            services.EnqueueEvent(Metadata(), new TraceEvent("alpha"));
            services.EnqueueEvent(Metadata(), new AuditEvent("alpha"));
            services.EnqueueCommand(Metadata(), new TraceCommand("first"));
            services.EnqueueCommand(Metadata(), new TraceCommand("second"));
            services.DispatchAll();

            services.EnqueueEvent(Metadata(), new TraceEvent("beta"));
            services.EnqueueEvent(Metadata(), new AuditEvent("beta"));
            services.EnqueueCommand(Metadata(), new TraceCommand("third"));
            services.DispatchAll();

            return trace;
        }

        private static CommandMetadata Metadata()
        {
            return CommandMetadata.Internal(42, CommandSource.Gameplay);
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

        private sealed class RecordingEventHandler<TEvent> : IEventHandler<TEvent>
            where TEvent : IEvent
        {
            private readonly Action<TEvent> handle;

            internal RecordingEventHandler(Action<TEvent> handle)
            {
                this.handle = handle;
            }

            public void Handle(TEvent @event)
            {
                handle(@event);
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

        private sealed class TraceEvent : IEvent
        {
            internal readonly string Label;

            internal TraceEvent(string label)
            {
                Label = label;
            }
        }

        private sealed class AuditEvent : IEvent
        {
            internal readonly string Label;

            internal AuditEvent(string label)
            {
                Label = label;
            }
        }
    }
}
