using System;
using System.Globalization;
using System.Linq;
using TraceBuffering;
using GameplaySimulation;
using Testability;
using Testability.Templates;

namespace GameplayProtocol.Game
{
    /// <summary>Game payload v2 over protocol envelope v1. The host owns the session, reset scenario and pump.</summary>
    public sealed class GameplayProtocolAdapter
    {
        public const int PayloadVersion = 2;
        public const string HashKind = "modernHash";
        private readonly TestableSimulationSession<GameplayWorld, GameplayScenario, GameplayInput, GameplayObservation> session;
        private readonly Func<GameplayScenario> resetScenarioFactory;
        public ProtocolEndpoint Endpoint { get; }

        public GameplayProtocolAdapter(
            TestableSimulationSession<GameplayWorld, GameplayScenario, GameplayInput, GameplayObservation> session,
            Func<GameplayScenario> resetScenarioFactory, ProtocolLimits limits = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.resetScenarioFactory = resetScenarioFactory ?? throw new ArgumentNullException(nameof(resetScenarioFactory));
            session.Gameplay.Observe(); // Validate owner-thread/idle/lifetime before attaching a pump.
            Endpoint = new ProtocolEndpoint(() => session.Id, limits);
            Route("capabilities.read", ProtocolPermission.Observe, false, false, false, (client, request) => Describe(client), true);
            Route("control.acquire", ProtocolPermission.None, true, false, true, (client, request) =>
            { Endpoint.AcquireControl(client, request.SessionId); return Acknowledge(); });
            // Releasing an existing lease is safe even after the host starts a realtime driver.
            Route("control.release", ProtocolPermission.None, true, true, false, (client, request) =>
            { Endpoint.ReleaseControl(client); return Acknowledge(); });
            Route("observation.read", ProtocolPermission.Observe, true, false, false, (client, request) => ProtocolJson.Write(Map(session.Gameplay.Observe())));
            Route("action.submit", ProtocolPermission.Act, true, true, true, Submit);
            Route("simulation.step", ProtocolPermission.Drive, true, true, true, (client, request) =>
            {
                if (session.State != SessionState.Running) throw new ProtocolFault("session.not_running");
                TemplateTick report;
                try { report = session.Simulation.Step(); }
                catch (InvalidOperationException) when (session.State == SessionState.Stopped && session.CurrentTick >= (ulong)session.Limits.MaxTicks)
                { throw new ProtocolFault("tick.budget"); }
                return ProtocolJson.Write(new StepDto { Policy = session.Policy, ModernHash = report.Hash,
                    Tick = Number(report.Tick), Results = report.Results.Select(Map).ToArray() });
            });
            Route("results.read", ProtocolPermission.Observe, true, false, false, (client, request) =>
            {
                PageDto query = ProtocolJson.Read<PageDto>(request.PayloadJson);
                try
                {
                    TemplateActionResultPage page = session.Results.Read(request.SessionId, query.AfterIndex, query.MaxItems);
                    return ProtocolJson.Write(new ResultPageDto { Items = page.Items.Select(Map).ToArray(), NextIndex = page.NextIndex, HasMore = page.HasMore });
                }
                catch (ArgumentException) { throw new ProtocolFault("cursor.invalid"); }
            });
            Route("diagnostics.read", ProtocolPermission.Observe, true, false, false, (client, request) =>
            {
                DiagnosticSnapshot<GameplayObservation> snapshot = session.Diagnostics.ObserveDiagnostics();
                return ProtocolJson.Write(new DiagnosticsDto { Observation = Map(snapshot.Observation), State = snapshot.State.ToString(),
                    Tick = Number(snapshot.Tick), ObservationTick = Number(snapshot.ObservationTick), LastCompletedTick = Number(session.LastCompletedTick),
                    Evaluated = snapshot.Invariants.Evaluated, InvariantTick = Number(snapshot.Invariants.Tick), CheckCount = snapshot.Invariants.CheckCount,
                    Violations = snapshot.Invariants.Violations.Select(v => new ViolationDto { Code = v.Code, Detail = v.Detail }).ToArray(), FaultCode = snapshot.FaultCode });
            });
            Route("trace.read", ProtocolPermission.Observe, true, false, false, ReadTrace);
            Route("session.reset", ProtocolPermission.Admin, true, true, true, (client, request) =>
            {
                GameplayScenario scenario = this.resetScenarioFactory();
                if (scenario == null) throw new ProtocolFault("reset.scenario_unavailable");
                session.Admin.Reset(scenario);
                return Acknowledge();
            });
            Route("session.stop", ProtocolPermission.Admin, true, true, true, (client, request) =>
            { session.Admin.Stop(); return Acknowledge(); });
            Endpoint.Seal();
        }

        private void Route(string name, ProtocolPermission permission, bool sessionRequired, bool control,
            bool requiresManualDriver, ProtocolHandler handler, bool allowDiscovery = false)
        {
            Endpoint.Register(new ProtocolOperation(name, permission, sessionRequired, control), (client, request) =>
            {
                if (!allowDiscovery || request.PayloadJson.Trim() != "{}")
                {
                    VersionDto header = ProtocolJson.Read<VersionDto>(request.PayloadJson);
                    if (header.Version != PayloadVersion) throw new ProtocolFault("payload.version.unsupported");
                }
                // Check at execution, not construction/acquire/enqueue: ownership may change between requests.
                if (requiresManualDriver && session.HasRealtimeDriver) throw new ProtocolFault("session.realtime");
                return handler(client, request);
            });
        }

        private string Submit(ProtocolClient client, ProtocolRequest request)
        {
            ActionDto dto = ProtocolJson.Read<ActionDto>(request.PayloadJson);
            GameplayActionKind kind;
            if (dto.Kind == "Move") kind = GameplayActionKind.Move;
            else if (dto.Kind == "Attack") kind = GameplayActionKind.Attack;
            else throw new ProtocolFault("action.unknown");
            GameplayInput input = new GameplayInput(kind, Parse(dto.Actor), Parse(dto.Target ?? "0"), dto.X, dto.Y);
            SubmissionResult result = session.Gameplay.Submit(request.SessionId, Parse(dto.Sequence), Parse(dto.TargetTick), input);
            return ProtocolJson.Write(new AdmissionDto { Queued = result.Queued, Code = result.Code });
        }

        private string Describe(ProtocolClient client)
        {
            TemplateLimits limits = session.Limits;
            return ProtocolJson.Write(new CapabilitiesDto { SessionId = session.Id, State = session.State.ToString(), Policy = session.Policy,
                HashKind = HashKind, HasRealtimeDriver = session.HasRealtimeDriver, GrantedPermissions = client.Permissions.ToString(),
                Tick = Number(session.CurrentTick), LastCompletedTick = Number(session.LastCompletedTick), TickDelta = session.TickDelta,
                MaxTicks = limits.MaxTicks, MaxInputs = limits.MaxInputs, TraceCapacity = limits.TraceCapacity,
                MaxPayloadBytes = limits.MaxPayloadBytes, MaxTotalPayloadBytes = limits.MaxTotalPayloadBytes,
                ActionOrdering = "target-tick-then-sequence", RequiresNonzeroUniqueSequence = true, RequiresFutureTargetTick = true,
                AdmissionCodes = new[] { "queue.accepted", "session.not_running", "session.stale", "sequence.invalid_or_duplicate",
                    "tick.out_of_range", "input.capacity", "input.payload_budget", "input.invalid" },
                Operations = Endpoint.Describe().Select(o => new OperationDto { Name = o.Name, Permission = o.Permission.ToString(),
                    RequiresSession = o.RequiresSession, RequiresControl = o.RequiresControl }).ToArray(),
                Actions = new[]
                {
                    DescribeAction("Move", false, true, "move.applied", new[] { "actor.unknown", "actor.dead" }),
                    DescribeAction("Attack", true, false, "attack.applied", new[] { "actor.unknown", "actor.dead", "target.self", "target.unknown", "target.dead", "target.out_of_range" })
                } });
        }

        private static ActionDescriptionDto DescribeAction(string kind, bool requiresTarget, bool usesAxes, string success, string[] rejections)
            => new ActionDescriptionDto { Kind = kind, RequiresTarget = requiresTarget, UsesAxes = usesAxes, RequiresActor = true,
                RequiresFiniteAxes = true, NormalizesAxes = usesAxes, SuccessCode = success, RejectionCodes = rejections,
                InvalidRequestCodes = new[] { "parameters.invalid" } };

        private string ReadTrace(ProtocolClient client, ProtocolRequest request)
        {
            TraceQueryDto query = ProtocolJson.Read<TraceQueryDto>(request.PayloadJson);
            if (!Guid.TryParse(query.StreamId, out Guid stream) || !long.TryParse(query.AfterSequence, NumberStyles.None, CultureInfo.InvariantCulture, out long after) || query.MaxItems < 1 || query.MaxItems > 256)
                throw new ProtocolFault("cursor.invalid");
            try
            {
                TraceBatch<TraceEntry> page = session.Diagnostics.ReadTrace(new TraceCursor(stream, after), query.MaxItems);
                return ProtocolJson.Write(new TracePageDto { StreamId = page.NextCursor.StreamId.ToString("D"), AfterSequence = page.NextCursor.AfterSequence.ToString(CultureInfo.InvariantCulture),
                    StreamChanged = page.StreamChanged, MissedCount = page.MissedCount.ToString(CultureInfo.InvariantCulture), HasMore = page.HasMore,
                    Items = page.Items.Select(item => new TraceRecordDto { RecordSequence = item.Sequence.ToString(CultureInfo.InvariantCulture), Tick = Number(item.Entry.Tick),
                        ActionSequence = Number(item.Entry.Sequence), Stage = item.Entry.Stage, Type = item.Entry.Type, Code = item.Entry.Code, Wave = item.Entry.Wave,
                        Actor = Number(item.Entry.Actor), Target = Number(item.Entry.Target) }).ToArray() });
            }
            catch (ArgumentException) { throw new ProtocolFault("cursor.invalid"); }
        }

        private static string Acknowledge() => ProtocolJson.Write(new VersionDto { Version = PayloadVersion });
        private static ulong Parse(string value)
        {
            if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong result)) throw new ProtocolFault("payload.invalid");
            return result;
        }
        private static string Number(ulong value) => value.ToString(CultureInfo.InvariantCulture);
        private static ResultDto Map(ActionResult result) => new ResultDto { Sequence = Number(result.Sequence), Tick = Number(result.Tick), Status = result.Status.ToString(), Code = result.Code };
        private static ObservationDto Map(GameplayObservation observation) => new ObservationDto { Tick = Number(observation.Tick), PlayerId = Number(observation.PlayerId),
            EnemyRandomState = Number(observation.EnemyRandomState), RespawnRandomState = Number(observation.RespawnRandomState),
            EnemiesSpawned = observation.EnemiesSpawned, PendingRespawnTicks = observation.PendingRespawnTicks.Select(Number).ToArray(),
            Actors = observation.Actors.Select(a => new ActorDto { Id = Number(a.Id), X = a.X, Y = a.Y, DirectionX = a.DirectionX, DirectionY = a.DirectionY,
                Speed = a.Speed, Health = a.Health, MaxHealth = a.MaxHealth, Active = a.Active }).ToArray() };
    }
}
