using System.Collections.Generic;
using DeterministicSimulation;
using NUnit.Framework;

namespace DeterministicSimulation.Framework.Tests
{
    public sealed class SimulationPipelineTests
    {
        [Test]
        public void Tick_DispatchesIntentThenInternalCommandThenDomainEvent()
        {
            List<string> trace = new List<string>();
            SimulationPipeline pipeline = new SimulationPipeline();

            pipeline.RegisterIntentHandler(new StartIntentHandler(trace, pipeline));
            pipeline.RegisterInternalCommandHandler(new MoveCommandHandler(trace, pipeline));
            pipeline.RegisterDomainEventHandler(new MovedEventHandler(trace));
            pipeline.Seal();

            pipeline.EnqueueIntent(new StartIntent());
            new SimulationRunner(pipeline).AdvanceTick();

            CollectionAssert.AreEqual(
                new[] { "intent", "internal-command", "domain-event" },
                trace);
        }

        [Test]
        public void Tick_ExecutesFrameworkPhasesInRegistrationOrder()
        {
            List<string> trace = new List<string>();
            SimulationPipeline pipeline = new SimulationPipeline();
            RecordingParticipant participant = new RecordingParticipant(trace);

            pipeline.RegisterIntentSource(participant);
            pipeline.RegisterPrePhysicsParticipant(participant);
            pipeline.RegisterPhysicsParticipant(participant);
            pipeline.RegisterPostPhysicsParticipant(participant);
            pipeline.RegisterStructuralCommitParticipant(participant);
            pipeline.RegisterPresentationParticipant(participant);
            pipeline.Seal();

            SimulationRunner runner = new SimulationRunner(pipeline, 0.1f);
            runner.AdvanceTick();
            runner.UpdatePresentation();

            CollectionAssert.AreEqual(
                new[] { "acquire:1", "pre:1", "physics:1", "post:1", "commit:1", "capture:1", "render:1" },
                trace);
        }

        [Test]
        public void AdvanceTime_UsesFixedTicksAndExposesInterpolationAlpha()
        {
            SimulationPipeline pipeline = new SimulationPipeline();
            pipeline.Seal();
            SimulationRunner runner = new SimulationRunner(pipeline, 0.1f);

            runner.AdvanceTime(0.25f);

            Assert.That(runner.TickNumber, Is.EqualTo(2));
            Assert.That(runner.PresentationAlpha, Is.EqualTo(0.5f).Within(0.0001f));
        }

        private readonly struct StartIntent : IIntent { }
        private readonly struct MoveCommand : IInternalCommand { }
        private readonly struct MovedEvent : IDomainEvent { }

        private sealed class StartIntentHandler : IIntentHandler<StartIntent>
        {
            private readonly List<string> trace;
            private readonly IInternalCommandSink commandSink;

            internal StartIntentHandler(List<string> trace, IInternalCommandSink commandSink)
            {
                this.trace = trace;
                this.commandSink = commandSink;
            }

            public void Handle(StartIntent intent)
            {
                trace.Add("intent");
                commandSink.EnqueueInternalCommand(new MoveCommand());
            }
        }

        private sealed class MoveCommandHandler : IInternalCommandHandler<MoveCommand>
        {
            private readonly List<string> trace;
            private readonly IDomainEventSink eventSink;

            internal MoveCommandHandler(List<string> trace, IDomainEventSink eventSink)
            {
                this.trace = trace;
                this.eventSink = eventSink;
            }

            public void Handle(MoveCommand command)
            {
                trace.Add("internal-command");
                eventSink.PublishDomainEvent(new MovedEvent());
            }
        }

        private sealed class MovedEventHandler : IDomainEventHandler<MovedEvent>
        {
            private readonly List<string> trace;

            internal MovedEventHandler(List<string> trace)
            {
                this.trace = trace;
            }

            public void Handle(MovedEvent domainEvent)
            {
                trace.Add("domain-event");
            }
        }

        private sealed class RecordingParticipant :
            IIntentSource,
            IPrePhysicsParticipant,
            IPhysicsParticipant,
            IPostPhysicsParticipant,
            IStructuralCommitParticipant,
            IPresentationParticipant
        {
            private readonly List<string> trace;

            internal RecordingParticipant(List<string> trace)
            {
                this.trace = trace;
            }

            public void AcquireIntents(SimulationContext context, IIntentSink sink) =>
                trace.Add($"acquire:{context.Tick.Number}");

            void IPrePhysicsParticipant.Tick(SimulationContext context) =>
                trace.Add($"pre:{context.Tick.Number}");

            public void Simulate(SimulationContext context) =>
                trace.Add($"physics:{context.Tick.Number}");

            void IPostPhysicsParticipant.Tick(SimulationContext context) =>
                trace.Add($"post:{context.Tick.Number}");

            public void Commit(SimulationContext context) =>
                trace.Add($"commit:{context.Tick.Number}");

            public void CaptureTickState(SimulationContext context) =>
                trace.Add($"capture:{context.Tick.Number}");

            public void Render(SimulationContext context, float interpolationAlpha) =>
                trace.Add($"render:{context.Tick.Number}");
        }
    }
}
