using System;
using System.Threading.Tasks;
using GameplaySimulation;
using NUnit.Framework;

namespace GameplayProtocol.Game.Tests
{
    public sealed class GameplayProtocolTests
    {
        private static ProtocolResponse Send(GameplayProtocolAdapter adapter, ProtocolClient client, string id, string session, string op, string payload = "{}")
        {
            // Exercise envelope serialization as well as project payload mapping.
            ProtocolRequest request = ProtocolJson.Read<ProtocolRequest>(ProtocolJson.Write(new ProtocolRequest(1, id, session, op, payload)));
            Task<ProtocolResponse> response = adapter.Endpoint.Enqueue(client, request); adapter.Endpoint.Drain(1);
            return ProtocolJson.Read<ProtocolResponse>(ProtocolJson.Write(response.Result));
        }
        private static GameplaySession Session(SimulationDriveMode mode = SimulationDriveMode.Manual)
        { GameplaySession session = new GameplaySession(mode); session.Start(new GameplayScenario(tickDelta: .25f)); return session; }
        private static ProtocolClient Controller() => new ProtocolClient("test", ProtocolPermission.Observe | ProtocolPermission.Act | ProtocolPermission.Drive | ProtocolPermission.Admin);

        [Test]
        public void JsonProtocolMatchesDirectGameplayAndStepRetryDoesNotAdvance()
        {
            GameplaySession direct = Session(), target = Session();
            GameplayProtocolAdapter adapter = new GameplayProtocolAdapter(target); ProtocolClient client = Controller();
            Assert.That(Send(adapter, client, "claim", target.Id, "control.acquire").Success, Is.True);
            for (int tick = 1; tick <= 8; tick++)
            {
                string payload = ProtocolJson.Write(new ActionDto { Sequence = tick.ToString(), TargetTick = tick.ToString(), Kind = "Move", Actor = "1", X = 1 });
                Assert.That(Send(adapter, client, "act" + tick, target.Id, "action.submit", payload).Success, Is.True);
                direct.Submit(new GameplayRequest(direct.Id, (ulong)tick, (ulong)tick, GameplayActionKind.Move, 1, x: 1));
                string expected = direct.Step().StateHash;
                ProtocolResponse response = Send(adapter, client, "step" + tick, target.Id, "simulation.step");
                Assert.That(ProtocolJson.Read<StepDto>(response.PayloadJson).StateHash, Is.EqualTo(expected));
                Assert.That(Send(adapter, client, "step" + tick, target.Id, "simulation.step").PayloadJson, Is.EqualTo(response.PayloadJson));
                Assert.That(target.CurrentTick, Is.EqualTo(tick));
            }
        }
        [Test]
        public void ReaderCannotClaimOrMutateButCanDiscoverAndObserve()
        {
            GameplaySession target = Session(); GameplayProtocolAdapter adapter = new GameplayProtocolAdapter(target);
            ProtocolClient reader = new ProtocolClient("overlay", ProtocolPermission.Observe);
            Assert.That(Send(adapter, reader, "1", "", "capabilities.read").Success, Is.True);
            Assert.That(Send(adapter, reader, "2", target.Id, "control.acquire").Code, Is.EqualTo("permission.denied"));
            Assert.That(Send(adapter, reader, "3", target.Id, "simulation.step").Code, Is.EqualTo("permission.denied"));
            Assert.That(Send(adapter, reader, "4", target.Id, "observation.read").Success, Is.True);
            Assert.That(target.CurrentTick, Is.Zero);
        }
        [Test]
        public void ResetRetryReturnsOriginalNewIdentityAndRequiresNewLease()
        {
            GameplaySession target = Session(); GameplayProtocolAdapter adapter = new GameplayProtocolAdapter(target); ProtocolClient client = Controller();
            string old = target.Id; Send(adapter, client, "claim", old, "control.acquire");
            ProtocolResponse reset = Send(adapter, client, "reset", old, "session.reset");
            Assert.That(reset.SessionId, Is.Not.EqualTo(old));
            Assert.That(Send(adapter, client, "reset", old, "session.reset").SessionId, Is.EqualTo(reset.SessionId));
            Assert.That(target.Id, Is.EqualTo(reset.SessionId));
            Assert.That(Send(adapter, client, "stale", old, "simulation.step").Code, Is.EqualTo("session.stale"));
            Assert.That(Send(adapter, client, "step", target.Id, "simulation.step").Code, Is.EqualTo("control.required"));
        }
        [Test]
        public void RealtimeSessionIsReadOnlyThroughAdapter()
        {
            GameplaySession target = Session(SimulationDriveMode.Realtime); GameplayProtocolAdapter adapter = new GameplayProtocolAdapter(target); ProtocolClient client = Controller();
            Assert.That(Send(adapter, client, "1", target.Id, "control.acquire").Code, Is.EqualTo("session.realtime"));
            Assert.That(Send(adapter, client, "2", target.Id, "observation.read").Success, Is.True);
            Assert.That(target.CurrentTick, Is.Zero);
        }
        [Test]
        public void ResultsDiagnosticsAndTraceAreMappedToIndependentDtos()
        {
            GameplaySession target = Session(); GameplayProtocolAdapter adapter = new GameplayProtocolAdapter(target); ProtocolClient client = Controller();
            Send(adapter, client, "claim", target.Id, "control.acquire");
            Send(adapter, client, "act", target.Id, "action.submit", ProtocolJson.Write(new ActionDto { Sequence = "9007199254740993", TargetTick = "1", Kind = "Attack", Actor = "1", Target = "2" }));
            Send(adapter, client, "step", target.Id, "simulation.step");
            ResultPageDto results = ProtocolJson.Read<ResultPageDto>(Send(adapter, client, "results", target.Id, "results.read", ProtocolJson.Write(new PageDto { AfterIndex = 0, MaxItems = 10 })).PayloadJson);
            Assert.That(results.Items[0].Sequence, Is.EqualTo("9007199254740993"));
            DiagnosticsDto diagnostics = ProtocolJson.Read<DiagnosticsDto>(Send(adapter, client, "diag", target.Id, "diagnostics.read").PayloadJson);
            Assert.That(diagnostics.Observation.Actors[1].Health, Is.EqualTo(20));
            Assert.That(diagnostics.Evaluated, Is.True);
            TracePageDto trace = ProtocolJson.Read<TracePageDto>(Send(adapter, client, "trace", target.Id, "trace.read",
                ProtocolJson.Write(new TraceQueryDto { StreamId = Guid.Empty.ToString("D"), AfterSequence = "0", MaxItems = 1 })).PayloadJson);
            Assert.That(trace.Items.Length, Is.EqualTo(1)); Assert.That(trace.HasMore, Is.True);
            Assert.That(target.CurrentTick, Is.EqualTo(1));
        }
        [Test]
        public void BadPayloadAndCursorAreStructuredErrors()
        {
            GameplaySession target = Session(); GameplayProtocolAdapter adapter = new GameplayProtocolAdapter(target); ProtocolClient client = Controller();
            Send(adapter, client, "claim", target.Id, "control.acquire");
            Assert.That(Send(adapter, client, "1", target.Id, "action.submit", "{}").Code, Is.EqualTo("payload.invalid"));
            Assert.That(Send(adapter, client, "2", target.Id, "results.read", "{\"AfterIndex\":-1,\"MaxItems\":1}").Code, Is.EqualTo("cursor.invalid"));
            Assert.That(Send(adapter, client, "3", target.Id, "trace.read", "{\"StreamId\":\"bad\",\"AfterSequence\":\"0\",\"MaxItems\":1}").Code, Is.EqualTo("cursor.invalid"));
        }
    }
}
