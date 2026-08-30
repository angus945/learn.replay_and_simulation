using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeterministicSimulation.Framework;
using GameplaySimulation;
using Testability;
using Testability.Templates;
using ModernSession = Testability.Templates.TestableSimulationSession<GameplaySimulation.GameplayWorld, GameplaySimulation.GameplayScenario, GameplaySimulation.GameplayInput, GameplaySimulation.GameplayObservation>;

namespace GameplayProtocol.Game.Tests
{
    /// <summary>JSON-boundary contract checks shared by NUnit and the headless CLI; no engine or NUnit dependency.</summary>
    public static class GameplayProtocolContractChecks
    {
        private const string VersionedEmpty = "{\"Version\":2}";

        public static void RunAll()
        {
            JsonProtocolMatchesDirectGameplayAndStepRetryDoesNotAdvance();
            ReaderCannotClaimOrMutateButCanDiscoverAndObserve();
            ResetRetryReturnsOriginalNewIdentityAndRequiresNewLease();
            RealtimeSessionIsReadOnlyThroughAdapter();
            ResultsDiagnosticsAndTraceAreMappedToIndependentDtos();
            BadPayloadAndCursorAreStructuredErrors();
            PayloadVersionIsExplicitAndOldClientsCannotMutate();
            CapabilitiesReportActualLimitsAndModernPolicy();
            ModernAdmissionCodesAndExecutionResultsRemainDistinct();
            RuntimeDriveOwnershipChangesAreCheckedAtExecution();
        }

        public static void JsonProtocolMatchesDirectGameplayAndStepRetryDoesNotAdvance()
        {
            GameplayDefinition definition = new GameplayDefinition(null, "protocol-contract/custom-policy");
            GameplayScenario scenario = new GameplayScenario(tickDelta: .25f);
            using (ModernSession direct = definition.CreateTestSession(scenario))
            using (ModernSession target = definition.CreateTestSession(scenario))
            {
                GameplayProtocolAdapter adapter = new GameplayProtocolAdapter(target, () => scenario);
                ProtocolClient client = Controller();
                Require(Send(adapter, client, "claim", target.Id, "control.acquire").Success, "claim");
                for (int tick = 1; tick <= 8; tick++)
                {
                    ActionDto action = Move((ulong)tick, (ulong)tick);
                    AdmissionDto admission = Admit(adapter, client, target.Id, "act" + tick, action);
                    Require(admission.Queued && admission.Code == "queue.accepted", "admit");
                    direct.Gameplay.Submit(direct.Id, (ulong)tick, (ulong)tick, new GameplayInput(GameplayActionKind.Move, 1, x: 1));
                    TemplateTick expected = direct.Simulation.Step();
                    ProtocolResponse response = Send(adapter, client, "step" + tick, target.Id, "simulation.step");
                    StepDto step = Read<StepDto>(response);
                    Require(step.Version == 2 && step.Policy == definition.PolicyId, "modern hash policy");
                    Require(step.ModernHash == expected.Hash && step.ModernHash.Length == 64, "exact modern canonical hash");
                    Require(step.Results.Length == 1 && step.Results[0].Code == "move.applied", "results");
                    Require(Send(adapter, client, "step" + tick, target.Id, "simulation.step").PayloadJson == response.PayloadJson, "step retry response");
                    Require(target.CurrentTick == (ulong)tick, "step retry must not advance");
                }
            }
        }

        public static void ReaderCannotClaimOrMutateButCanDiscoverAndObserve()
        {
            using (ModernSession target = Session())
            {
                GameplayProtocolAdapter adapter = Adapter(target);
                ProtocolClient reader = new ProtocolClient("overlay", ProtocolPermission.Observe);
                Require(Send(adapter, reader, "1", "", "capabilities.read", "{}").Success, "reader discovery");
                Require(Send(adapter, reader, "2", target.Id, "control.acquire").Code == "permission.denied", "reader claim");
                Require(Send(adapter, reader, "3", target.Id, "simulation.step").Code == "permission.denied", "reader step");
                Require(Send(adapter, reader, "4", target.Id, "observation.read").Success, "reader observation");
                Require(target.CurrentTick == 0, "reader must not advance");
            }
        }

        public static void ResetRetryReturnsOriginalNewIdentityAndRequiresNewLease()
        {
            using (ModernSession target = Session())
            {
                int scenarioRequests = 0;
                GameplayScenario trusted = new GameplayScenario(tickDelta: .5f, health: 45, maxTicks: 17, maxActions: 19);
                GameplayProtocolAdapter adapter = new GameplayProtocolAdapter(target, () => { scenarioRequests++; return trusted; });
                ProtocolClient client = Controller();
                string old = target.Id;
                Require(Send(adapter, client, "claim", old, "control.acquire").Success, "claim");
                // Unknown JSON fields cannot inject a scenario; only the trusted factory supplies it.
                string resetPayload = "{\"Version\":2,\"Scenario\":{\"Health\":999}}";
                ProtocolResponse reset = Send(adapter, client, "reset", old, "session.reset", resetPayload);
                Require(reset.Success && reset.SessionId != old, "reset new identity");
                Require(target.Gameplay.Observe().Actors[0].Health == 45 && target.TickDelta == .5f, "trusted reset scenario");
                trusted = new GameplayScenario(tickDelta: .125f, health: 60, maxTicks: 23, maxActions: 29);
                ProtocolResponse retry = Send(adapter, client, "reset", old, "session.reset", resetPayload);
                Require(retry.SessionId == reset.SessionId && target.Id == reset.SessionId && scenarioRequests == 1, "reset retry uses cached identity, not factory");
                Require(Send(adapter, client, "stale", old, "simulation.step").Code == "session.stale", "old identity");
                Require(Send(adapter, client, "step", target.Id, "simulation.step").Code == "control.required", "new lease required");
                Require(Send(adapter, client, "reclaim", target.Id, "control.acquire").Success, "new lease");
                string secondOld = target.Id;
                Require(Send(adapter, client, "reset-again", secondOld, "session.reset").Success, "new reset");
                Require(target.Id != secondOld && scenarioRequests == 2 && target.TickDelta == .125f, "factory queried for new reset");
                Require(target.Gameplay.Observe().Actors[0].Health == 60 && target.Limits.MaxTicks == 23, "new host scenario and default budgets");
            }
        }

        public static void RealtimeSessionIsReadOnlyThroughAdapter()
        {
            using (ModernSession target = Session())
            using (RealtimeSimulationRunner runner = target.CreateRealtimeRunner())
            {
                GameplayProtocolAdapter adapter = Adapter(target);
                ProtocolClient client = Controller();
                Require(Send(adapter, client, "1", target.Id, "control.acquire").Code == "session.realtime", "actual runner owns clock");
                Require(Send(adapter, client, "2", target.Id, "observation.read").Success, "realtime observation");
                Require(Read<CapabilitiesDto>(Send(adapter, client, "3", "", "capabilities.read", "{}")).HasRealtimeDriver, "realtime capability");
                Require(target.CurrentTick == 0, "protocol did not advance realtime session");
            }
        }

        public static void ResultsDiagnosticsAndTraceAreMappedToIndependentDtos()
        {
            using (ModernSession target = Session())
            {
                GameplayProtocolAdapter adapter = Adapter(target);
                ProtocolClient client = Controller();
                Send(adapter, client, "claim", target.Id, "control.acquire");
                AdmissionDto admission = Admit(adapter, client, target.Id, "act", new ActionDto
                    { Version = 2, Sequence = "9007199254740993", TargetTick = "1", Kind = "Attack", Actor = "1", Target = "2" });
                Require(admission.Queued, "large sequence admitted");
                Send(adapter, client, "step", target.Id, "simulation.step");
                ResultPageDto results = Read<ResultPageDto>(Send(adapter, client, "results", target.Id, "results.read",
                    ProtocolJson.Write(new PageDto { Version = 2, AfterIndex = 0, MaxItems = 10 })));
                Require(results.Version == 2 && results.Items.Length == 1 && results.Items[0].Sequence == "9007199254740993", "ulong JSON precision");
                DiagnosticsDto diagnostics = Read<DiagnosticsDto>(Send(adapter, client, "diag", target.Id, "diagnostics.read"));
                Require(diagnostics.Observation.Actors[1].Health == 20 && diagnostics.Evaluated, "diagnostics");
                Require(diagnostics.Tick == "1" && diagnostics.ObservationTick == "1" && diagnostics.LastCompletedTick == "1", "diagnostic tick meanings");
                Require(diagnostics.Observation.PlayerId == "1" && diagnostics.Observation.EnemyRandomState == Number(target.Observe().EnemyRandomState), "modern observation metadata");
                diagnostics.Observation.Actors[1].Health = -1;
                Require(target.Observe().Actors[1].Health == 20, "DTO cannot mutate session");
                TracePageDto trace = Read<TracePageDto>(Send(adapter, client, "trace", target.Id, "trace.read", FirstTracePage(1)));
                Require(trace.Version == 2 && trace.Items.Length == 1 && trace.HasMore, "trace cursor mapping");
                Require(target.CurrentTick == 1, "reads do not tick");
            }
        }

        public static void BadPayloadAndCursorAreStructuredErrors()
        {
            using (ModernSession target = Session())
            {
                GameplayProtocolAdapter adapter = Adapter(target);
                ProtocolClient client = Controller();
                Send(adapter, client, "claim", target.Id, "control.acquire");
                Require(Send(adapter, client, "1", target.Id, "action.submit", VersionedEmpty).Code == "payload.invalid", "missing action fields");
                Require(Send(adapter, client, "2", target.Id, "results.read", "{\"Version\":2,\"AfterIndex\":-1,\"MaxItems\":1}").Code == "cursor.invalid", "invalid results cursor");
                Require(Send(adapter, client, "3", target.Id, "trace.read", "{\"Version\":2,\"StreamId\":\"bad\",\"AfterSequence\":\"0\",\"MaxItems\":1}").Code == "cursor.invalid", "invalid trace cursor");
                ActionDto unknown = Move(1, 1);
                unknown.Kind = "Teleport";
                Require(Send(adapter, client, "unknown", target.Id, "action.submit", ProtocolJson.Write(unknown)).Code == "action.unknown", "unknown action");
                unknown.Kind = "Move"; unknown.Actor = "-1";
                Require(Send(adapter, client, "bad-actor", target.Id, "action.submit", ProtocolJson.Write(unknown)).Code == "payload.invalid", "unsigned ID required");
                Require(target.CaptureRecording().Inputs.Count == 0, "bad payload not admitted");
            }
        }

        public static void PayloadVersionIsExplicitAndOldClientsCannotMutate()
        {
            using (ModernSession target = Session())
            {
                GameplayProtocolAdapter adapter = Adapter(target);
                ProtocolClient client = Controller();
                Require(Read<CapabilitiesDto>(Send(adapter, client, "discover", "", "capabilities.read", "{}")).Version == 2, "discovery v2");
                Require(Send(adapter, client, "envelope-v2", target.Id, "control.acquire", VersionedEmpty, 2).Code == "version.unsupported", "envelope stays v1");
                Require(Send(adapter, client, "missing-claim", target.Id, "control.acquire", "{}").Code == "payload.invalid", "missing version cannot claim");
                Require(Send(adapter, client, "old-claim", target.Id, "control.acquire", "{\"Version\":1}").Code == "payload.version.unsupported", "old version cannot claim");
                Require(Send(adapter, client, "unclaimed", target.Id, "simulation.step").Code == "control.required", "rejected claim has no lease");
                Require(Send(adapter, client, "claim", target.Id, "control.acquire").Success, "v2 claim");
                ActionDto action = Move(1, 1); action.Version = 1;
                Require(Send(adapter, client, "old-action", target.Id, "action.submit", ProtocolJson.Write(action)).Code == "payload.version.unsupported", "old action");
                action.Version = 3;
                Require(Send(adapter, client, "future-action", target.Id, "action.submit", ProtocolJson.Write(action)).Code == "payload.version.unsupported", "unknown future version");
                Require(Send(adapter, client, "missing-action", target.Id, "action.submit",
                    "{\"Sequence\":\"1\",\"TargetTick\":\"1\",\"Kind\":\"Move\",\"Actor\":\"1\"}").Code == "payload.invalid", "missing version cannot default to two");
                string identity = target.Id;
                foreach (string operation in new[] { "simulation.step", "session.stop", "session.reset" })
                    Require(Send(adapter, client, "old-" + operation, identity, operation, "{\"Version\":1}").Code == "payload.version.unsupported", "reject " + operation);
                Require(target.CurrentTick == 0 && target.Id == identity && target.State == SessionState.Running && target.CaptureRecording().Inputs.Count == 0, "version failure has no side effects");
                Require(Admit(adapter, client, identity, "new-action", Move(1, 1)).Queued, "rejected old request does not reserve sequence");
            }
        }

        public static void CapabilitiesReportActualLimitsAndModernPolicy()
        {
            GameplayScenario scenario = new GameplayScenario(tickDelta: .25f, maxTicks: 500, maxActions: 600, traceCapacity: 512);
            TemplateLimits limits = new TemplateLimits(maxTicks: 7, maxInputs: 2, traceCapacity: 31, maxPayloadBytes: 4096, maxTotalPayloadBytes: 8192);
            GameplayDefinition definition = new GameplayDefinition(null, "protocol-limits/custom-policy");
            using (ModernSession target = definition.CreateTestSession(scenario, limits))
            {
                GameplayProtocolAdapter adapter = new GameplayProtocolAdapter(target, () => scenario);
                CapabilitiesDto capabilities = Read<CapabilitiesDto>(Send(adapter, Controller(), "caps", "", "capabilities.read", "{}"));
                Require(capabilities.Version == 2 && capabilities.Policy == definition.PolicyId && capabilities.HashKind == "modernHash", "modern policy contract");
                Require(capabilities.MaxTicks == 7 && capabilities.MaxInputs == 2 && capabilities.TraceCapacity == 31 &&
                    capabilities.MaxPayloadBytes == 4096 && capabilities.MaxTotalPayloadBytes == 8192, "actual limits, not scenario defaults");
                Require(!capabilities.HasRealtimeDriver && capabilities.TickDelta == target.TickDelta, "actual drive and step size");
                Require(capabilities.AdmissionCodes.Contains("sequence.invalid_or_duplicate") && capabilities.AdmissionCodes.Contains("input.payload_budget"), "modern admission catalog");
                Require(capabilities.Actions.Length == 2 && capabilities.Actions.Single(a => a.Kind == "Move").NormalizesAxes &&
                    capabilities.Actions.Single(a => a.Kind == "Attack").RequiresTarget, "game action catalog");
            }
        }

        public static void ModernAdmissionCodesAndExecutionResultsRemainDistinct()
        {
            GameplayScenario scenario = new GameplayScenario(tickDelta: .25f);
            using (ModernSession target = new GameplayDefinition().CreateTestSession(scenario, new TemplateLimits(maxTicks: 2, maxInputs: 2)))
            {
                GameplayProtocolAdapter adapter = new GameplayProtocolAdapter(target, () => scenario);
                ProtocolClient client = Controller();
                Send(adapter, client, "claim", target.Id, "control.acquire");
                CheckAdmission(adapter, client, target, "zero", Move(0, 1), false, "sequence.invalid_or_duplicate");
                CheckAdmission(adapter, client, target, "past", Move(1, 0), false, "tick.out_of_range");
                CheckAdmission(adapter, client, target, "future", Move(1, 3), false, "tick.out_of_range");
                ActionDto unknownActor = Move(1, 1); unknownActor.Actor = "77";
                CheckAdmission(adapter, client, target, "valid-envelope", unknownActor, true, "queue.accepted");
                CheckAdmission(adapter, client, target, "duplicate", Move(1, 2), false, "sequence.invalid_or_duplicate");
                StepDto step = Read<StepDto>(Send(adapter, client, "step1", target.Id, "simulation.step"));
                Require(step.Results[0].Status == "Rejected" && step.Results[0].Code == "actor.unknown", "domain rejection happens at tick");
                ActionDto invalidActor = Move(2, 2); invalidActor.Actor = "0";
                CheckAdmission(adapter, client, target, "invalid-domain", invalidActor, true, "queue.accepted");
                CheckAdmission(adapter, client, target, "capacity", Move(3, 2), false, "input.capacity");
                StepDto second = Read<StepDto>(Send(adapter, client, "step2", target.Id, "simulation.step"));
                Require(second.Results[0].Status == "InvalidRequest" && second.Results[0].Code == "parameters.invalid", "invalid domain input is completion result");
                Require(Send(adapter, client, "budget", target.Id, "simulation.step").Code == "tick.budget" && target.State == SessionState.Stopped, "tick budget stops");
                CheckAdmission(adapter, client, target, "stopped", Move(3, 3), false, "session.not_running");
            }
            // Scenario bytes count against the same lifetime payload budget as frozen input payloads.
            using (ModernSession target = new GameplayDefinition().CreateTestSession(scenario,
                new TemplateLimits(maxTicks: 50, maxInputs: 50, maxPayloadBytes: 1024, maxTotalPayloadBytes: 1024)))
            {
                GameplayProtocolAdapter adapter = new GameplayProtocolAdapter(target, () => scenario);
                ProtocolClient client = Controller();
                Send(adapter, client, "claim", target.Id, "control.acquire");
                bool reachedPayloadBudget = false;
                for (ulong sequence = 1; sequence <= 50; sequence++)
                {
                    AdmissionDto admission = Admit(adapter, client, target.Id, "payload-" + sequence, Move(sequence, 1));
                    if (!admission.Queued)
                    {
                        Require(admission.Code == "input.payload_budget", "modern aggregate payload budget code");
                        reachedPayloadBudget = true;
                        break;
                    }
                }
                Require(reachedPayloadBudget, "bounded payload fixture must exhaust budget");
                TemplateRecording recording = target.CaptureRecording();
                long bytes = Encoding.UTF8.GetByteCount(recording.Scenario);
                foreach (RecordedInput input in recording.Inputs) bytes += Encoding.UTF8.GetByteCount(input.Payload);
                Require(bytes <= target.Limits.MaxTotalPayloadBytes, "rejected payload not recorded");
            }
        }

        public static void RuntimeDriveOwnershipChangesAreCheckedAtExecution()
        {
            using (ModernSession target = Session())
            {
                int resetCalls = 0;
                GameplayProtocolAdapter adapter = new GameplayProtocolAdapter(target, () => { resetCalls++; return new GameplayScenario(); });
                ProtocolClient client = Controller();
                Require(Send(adapter, client, "claim", target.Id, "control.acquire").Success, "manual lease");
                Task<ProtocolResponse> queuedBeforeOwnership = adapter.Endpoint.Enqueue(client,
                    new ProtocolRequest(1, "queued-action", target.Id, "action.submit", ProtocolJson.Write(Move(1, 1))));
                using (RealtimeSimulationRunner runner = target.CreateRealtimeRunner())
                {
                    adapter.Endpoint.Drain(1);
                    Require(queuedBeforeOwnership.Result.Code == "session.realtime", "ownership checked at drain");
                    Require(Send(adapter, client, "reclaim", target.Id, "control.acquire").Code == "session.realtime", "held lease does not override driver");
                    foreach (string operation in new[] { "simulation.step", "session.reset", "session.stop" })
                        Require(Send(adapter, client, "during-" + operation, target.Id, operation).Code == "session.realtime", "held lease cannot " + operation);
                    Require(Send(adapter, client, "during-action", target.Id, "action.submit", ProtocolJson.Write(Move(1, 1))).Code == "session.realtime", "held lease cannot submit");
                    runner.Pause();
                    Require(Send(adapter, client, "paused-step", target.Id, "simulation.step").Code == "session.realtime", "pause retains drive ownership");
                    Require(Read<CapabilitiesDto>(Send(adapter, client, "caps", "", "capabilities.read", "{}")).HasRealtimeDriver, "capabilities follows live ownership");
                    Require(Send(adapter, client, "observe", target.Id, "observation.read").Success, "observe during realtime");
                    Require(Send(adapter, client, "diagnostics", target.Id, "diagnostics.read").Success, "diagnostics during realtime");
                    Require(Send(adapter, client, "results", target.Id, "results.read", ProtocolJson.Write(new PageDto { Version = 2, AfterIndex = 0, MaxItems = 1 })).Success, "results during realtime");
                    Require(Send(adapter, client, "trace", target.Id, "trace.read", FirstTracePage(1)).Success, "trace during realtime");
                    Require(target.CurrentTick == 0 && resetCalls == 0 && target.CaptureRecording().Inputs.Count == 0, "ownership rejections have no mutation");
                    runner.Resume();
                    Require(runner.AdvanceTime(.25f) == 1, "host remains able to drive");
                    Require(Read<ObservationDto>(Send(adapter, client, "after-host-tick", target.Id, "observation.read")).Tick == "1", "observe host tick");
                    Require(Send(adapter, client, "release", target.Id, "control.release").Success, "lease can be released during realtime");
                }
                Require(!Read<CapabilitiesDto>(Send(adapter, client, "manual-again", "", "capabilities.read", "{}")).HasRealtimeDriver, "dispose releases actual ownership");
                Require(Send(adapter, client, "new-claim", target.Id, "control.acquire").Success, "can acquire after driver release");
                Require(Admit(adapter, client, target.Id, "new-action", Move(1, 2)).Queued, "blocked request did not reserve sequence");
                Require(Read<StepDto>(Send(adapter, client, "new-step", target.Id, "simulation.step")).Tick == "2", "manual control restored");
            }
        }

        private static ModernSession Session() => new GameplayDefinition().CreateTestSession(new GameplayScenario(tickDelta: .25f));
        private static GameplayProtocolAdapter Adapter(ModernSession target) => new GameplayProtocolAdapter(target, () => new GameplayScenario(tickDelta: .25f));
        private static ProtocolClient Controller() => new ProtocolClient("test", ProtocolPermission.Observe | ProtocolPermission.Act | ProtocolPermission.Drive | ProtocolPermission.Admin);
        private static ActionDto Move(ulong sequence, ulong tick) => new ActionDto
            { Version = 2, Sequence = Number(sequence), TargetTick = Number(tick), Kind = "Move", Actor = "1", X = 1 };
        private static string FirstTracePage(int count) => ProtocolJson.Write(new TraceQueryDto
            { Version = 2, StreamId = Guid.Empty.ToString("D"), AfterSequence = "0", MaxItems = count });
        private static string Number(ulong value) => value.ToString(CultureInfo.InvariantCulture);
        private static AdmissionDto Admit(GameplayProtocolAdapter adapter, ProtocolClient client, string session, string id, ActionDto action)
            => Read<AdmissionDto>(Send(adapter, client, id, session, "action.submit", ProtocolJson.Write(action)));
        private static void CheckAdmission(GameplayProtocolAdapter adapter, ProtocolClient client, ModernSession session, string id, ActionDto action, bool queued, string code)
        {
            AdmissionDto admission = Admit(adapter, client, session.Id, id, action);
            Require(admission.Version == 2 && admission.Queued == queued && admission.Code == code, "admission " + id + ": " + admission.Code);
        }
        private static ProtocolResponse Send(GameplayProtocolAdapter adapter, ProtocolClient client, string id, string session, string operation,
            string payload = VersionedEmpty, int envelopeVersion = 1)
        {
            ProtocolRequest request = ProtocolJson.Read<ProtocolRequest>(ProtocolJson.Write(new ProtocolRequest(envelopeVersion, id, session, operation, payload)));
            Task<ProtocolResponse> response = adapter.Endpoint.Enqueue(client, request);
            adapter.Endpoint.Drain(1);
            return ProtocolJson.Read<ProtocolResponse>(ProtocolJson.Write(response.GetAwaiter().GetResult()));
        }
        private static T Read<T>(ProtocolResponse response)
        {
            Require(response.Success, "expected successful response; got " + response.Code);
            return ProtocolJson.Read<T>(response.PayloadJson);
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Gameplay protocol contract: " + message);
        }
    }
}
