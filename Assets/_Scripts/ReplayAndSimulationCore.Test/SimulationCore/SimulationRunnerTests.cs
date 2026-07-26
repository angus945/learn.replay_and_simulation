using System.Collections.Generic;
using NUnit.Framework;
using SimulationCore.Contracts;
using SimulationCore.Infrastructure;
using CoreSimulationRunner = SimulationCore.SimulationRunner;

namespace ReplayAndSimulationCore.Test.SimulationCore
{
    public sealed class SimulationRunnerTests
    {
        [Test]
        public void AdvanceTick_ExecutesPipelineInExpectedOrder()
        {
            List<string> trace = new();
            RecordingCommandSystem commandSystem = new(trace);
            RecordingExternalCommands externalCommands = new(trace);
            RecordingWorld world = new(trace);
            RecordingActor actor = new(trace);
            RecordingPhysics physics = new(trace);
            RecordingPresentation presentation = new(trace);
            CoreSimulationRunner runner = new(
                commandSystem,
                externalCommands,
                world,
                actor,
                physics,
                presentation,
                tickDelta: 0.25f);

            runner.AdvanceTick();

            CollectionAssert.AreEqual(
                new[]
                {
                    "external.AcquireCommands tick=1 delta=0.25",
                    "commands.DispatchAll",
                    "world.PrePhysicsTick tick=1 delta=0.25",
                    "commands.DispatchAll",
                    "actor.ReconcileBeforePhysics",
                    "physics.ApplyPrePhysicsState",
                    "physics.Simulate delta=0.25",
                    "physics.CapturePostPhysicsState",
                    "physics.PublishPhysicsEvents tick=1",
                    "commands.DispatchAll",
                    "world.PostPhysicsTick tick=1 delta=0.25",
                    "commands.DispatchAll",
                    "world.CommitStructuralChanges",
                    "actor.ReconcileAfterStructuralCommit",
                    "presentation.CaptureTickState tick=1"
                },
                trace);
            Assert.AreEqual(1ul, runner.Tick);
            Assert.AreEqual(0f, runner.Accumulator);
        }

        [Test]
        public void AdvanceTime_WhenAccumulatedTimeCrossesTickDelta_RunsWholeTicksAndKeepsRemainder()
        {
            List<string> trace = new();
            RecordingExternalCommands externalCommands = new(trace);
            CoreSimulationRunner runner = new(
                new RecordingCommandSystem(trace),
                externalCommands,
                new RecordingWorld(trace),
                new RecordingActor(trace),
                new RecordingPhysics(trace),
                new RecordingPresentation(trace),
                tickDelta: 0.25f);

            runner.AdvanceTime(0.1f);

            Assert.AreEqual(0ul, runner.Tick);
            Assert.AreEqual(0.1f, runner.Accumulator, 0.0001f);
            Assert.AreEqual(0, trace.Count);

            runner.AdvanceTime(0.45f);

            CollectionAssert.AreEqual(new[] { 1ul, 2ul }, externalCommands.AcquiredTicks);
            Assert.AreEqual(2ul, runner.Tick);
            Assert.AreEqual(0.05f, runner.Accumulator, 0.0001f);
        }

        [Test]
        public void UpdatePresentation_RendersWithoutAdvancingTick()
        {
            List<string> trace = new();
            RecordingPresentation presentation = new(trace);
            CoreSimulationRunner runner = new(
                new RecordingCommandSystem(trace),
                new RecordingExternalCommands(trace),
                new RecordingWorld(trace),
                new RecordingActor(trace),
                new RecordingPhysics(trace),
                presentation,
                tickDelta: 0.25f);

            runner.UpdatePresentation();

            CollectionAssert.AreEqual(new[] { "presentation.Render" }, trace);
            Assert.AreEqual(0ul, runner.Tick);
        }

        [Test]
        public void AdvanceTick_WithNullInfrastructureAndOmittedLogger_DoesNotThrow()
        {
            CoreSimulationRunner runner = new(
                new NullSimulationCommandSystem(),
                new NullSimulationExternalCommands(),
                new NullSimulationWorld(),
                new NullSimulationActor(),
                new NullSimulationPhysics(),
                new NullSimulationPresentation(),
                tickDelta: 0.25f);

            Assert.DoesNotThrow(() => runner.AdvanceTick());
            Assert.AreEqual(1ul, runner.Tick);
        }

        private sealed class RecordingCommandSystem : ISimulationCommandSystem
        {
            private readonly List<string> trace;

            internal RecordingCommandSystem(List<string> trace)
            {
                this.trace = trace;
            }

            public void DispatchAll()
            {
                trace.Add("commands.DispatchAll");
            }
        }

        private sealed class RecordingExternalCommands : ISimulationExternalCommands
        {
            private readonly List<string> trace;

            internal readonly List<ulong> AcquiredTicks = new();

            internal RecordingExternalCommands(List<string> trace)
            {
                this.trace = trace;
            }

            public void AcquireCommands(ulong tick, float delta)
            {
                AcquiredTicks.Add(tick);
                trace.Add($"external.AcquireCommands tick={tick} delta={delta}");
            }
        }

        private sealed class RecordingWorld : ISimulationWorld
        {
            private readonly List<string> trace;

            internal RecordingWorld(List<string> trace)
            {
                this.trace = trace;
            }

            public void PrePhysicsTick(ulong tick, float delta)
            {
                trace.Add($"world.PrePhysicsTick tick={tick} delta={delta}");
            }

            public void PostPhysicsTick(ulong tick, float delta)
            {
                trace.Add($"world.PostPhysicsTick tick={tick} delta={delta}");
            }

            public void CommitStructuralChanges()
            {
                trace.Add("world.CommitStructuralChanges");
            }
        }

        private sealed class RecordingActor : ISimulationActor
        {
            private readonly List<string> trace;

            internal RecordingActor(List<string> trace)
            {
                this.trace = trace;
            }

            public void ReconcileBeforePhysics()
            {
                trace.Add("actor.ReconcileBeforePhysics");
            }

            public void ReconcileAfterStructuralCommit()
            {
                trace.Add("actor.ReconcileAfterStructuralCommit");
            }
        }

        private sealed class RecordingPhysics : ISimulationPhysics
        {
            private readonly List<string> trace;

            internal RecordingPhysics(List<string> trace)
            {
                this.trace = trace;
            }

            public void ApplyPrePhysicsState()
            {
                trace.Add("physics.ApplyPrePhysicsState");
            }

            public void Simulate(float deltaTime)
            {
                trace.Add($"physics.Simulate delta={deltaTime}");
            }

            public void CapturePostPhysicsState()
            {
                trace.Add("physics.CapturePostPhysicsState");
            }

            public void PublishPhysicsEvents(ulong tick)
            {
                trace.Add($"physics.PublishPhysicsEvents tick={tick}");
            }
        }

        private sealed class RecordingPresentation : ISimulationPresentation
        {
            private readonly List<string> trace;

            internal RecordingPresentation(List<string> trace)
            {
                this.trace = trace;
            }

            public void CaptureTickState(ulong tick)
            {
                trace.Add($"presentation.CaptureTickState tick={tick}");
            }

            public void Render(float interpolationAlpha)
            {
                trace.Add("presentation.Render");
            }

        }
    }
}
