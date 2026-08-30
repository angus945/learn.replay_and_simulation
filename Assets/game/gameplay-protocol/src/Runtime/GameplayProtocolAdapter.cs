using System;
using System.Globalization;
using System.Linq;
using TraceBuffering;
using GameplaySimulation;
using Testability;

namespace GameplayProtocol.Game
{
    /// <summary>Composition provisions the session and trusted clients. No sockets or engine references.</summary>
    public sealed class GameplayProtocolAdapter
    {
        private readonly GameplaySession session;
        public ProtocolEndpoint Endpoint { get; }
        public GameplayProtocolAdapter(GameplaySession session, ProtocolLimits limits = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            if (session.State == SessionState.Created) throw new ArgumentException("Provision/start the session before attaching protocol.");
            Endpoint = new ProtocolEndpoint(() => session.Id, limits);
            Route("capabilities.read", ProtocolPermission.Observe, false, false, (client, request) => Describe(client));
            Route("control.acquire", ProtocolPermission.None, true, false, (client, request) =>
            {
                if (session.DriveMode != SimulationDriveMode.Manual) throw new ProtocolFault("session.realtime");
                Endpoint.AcquireControl(client, request.SessionId); return "{}";
            });
            Route("control.release", ProtocolPermission.None, true, true, (client, request) => { Endpoint.ReleaseControl(client); return "{}"; });
            Route("observation.read", ProtocolPermission.Observe, true, false, (client, request) => ProtocolJson.Write(Map(session.Gameplay.Observe())));
            Route("action.submit", ProtocolPermission.Act, true, true, Submit);
            Route("simulation.step", ProtocolPermission.Drive, true, true, (client, request) =>
            {
                if (session.State != SessionState.Running) throw new ProtocolFault("session.not_running");
                TickReport report = session.Simulation.Step();
                return ProtocolJson.Write(new StepDto { Tick = Number(report.Tick), StateHash = report.StateHash, Results = report.Results.Select(Map).ToArray() });
            });
            Route("results.read", ProtocolPermission.Observe, true, false, (client, request) =>
            {
                PageDto query = ProtocolJson.Read<PageDto>(request.PayloadJson);
                try
                {
                    ActionResultPage page = session.Results.Read(session.Id, query.AfterIndex, query.MaxItems);
                    return ProtocolJson.Write(new ResultPageDto { Items = page.Items.Select(Map).ToArray(), NextIndex = page.NextIndex, HasMore = page.HasMore });
                }
                catch (ArgumentException) { throw new ProtocolFault("cursor.invalid"); }
            });
            Route("diagnostics.read", ProtocolPermission.Observe, true, false, (client, request) =>
            {
                DiagnosticSnapshot<GameplayObservation> snapshot = session.Diagnostics.ObserveDiagnostics();
                return ProtocolJson.Write(new DiagnosticsDto { Observation = Map(snapshot.Observation), State = snapshot.State.ToString(),
                    Evaluated = snapshot.Invariants.Evaluated, InvariantTick = Number(snapshot.Invariants.Tick), CheckCount = snapshot.Invariants.CheckCount,
                    Violations = snapshot.Invariants.Violations.Select(v => new ViolationDto { Code = v.Code, Detail = v.Detail }).ToArray(), FaultCode = snapshot.FaultCode });
            });
            Route("trace.read", ProtocolPermission.Observe, true, false, ReadTrace);
            Route("session.reset", ProtocolPermission.Admin, true, true, (client, request) => { session.Admin.Reset(session.Capabilities.Describe().Scenario); return "{}"; });
            Route("session.stop", ProtocolPermission.Admin, true, true, (client, request) => { session.Admin.Stop(); return "{}"; });
            Endpoint.Seal();
        }
        private void Route(string name, ProtocolPermission permission, bool sessionRequired, bool control, ProtocolHandler handler)
            => Endpoint.Register(new ProtocolOperation(name, permission, sessionRequired, control), handler);
        private string Submit(ProtocolClient client, ProtocolRequest request)
        {
            ActionDto dto = ProtocolJson.Read<ActionDto>(request.PayloadJson);
            GameplayActionKind kind;
            if (dto.Kind == "Move") kind = GameplayActionKind.Move;
            else if (dto.Kind == "Attack") kind = GameplayActionKind.Attack;
            else throw new ProtocolFault("action.unknown");
            SubmissionResult result = session.Gameplay.Submit(new GameplayRequest(request.SessionId, Parse(dto.Sequence), Parse(dto.TargetTick), kind,
                Parse(dto.Actor), Parse(dto.Target ?? "0"), dto.X, dto.Y));
            return ProtocolJson.Write(new AdmissionDto { Queued = result.Queued, Code = result.Code });
        }
        private string Describe(ProtocolClient client)
        {
            GameplayCapabilities capabilities = session.Capabilities.Describe();
            return ProtocolJson.Write(new CapabilitiesDto { Version = 1, SessionId = session.Id, State = session.State.ToString(),
                DriveMode = session.DriveMode.ToString(), GrantedPermissions = client.Permissions.ToString(), Tick = Number(session.CurrentTick),
                TickDelta = capabilities.Scenario.TickDelta, MaxTicks = capabilities.Scenario.MaxTicks, MaxActions = capabilities.Scenario.MaxActions,
                ActionOrdering = capabilities.ActionOrdering, RequiresNonzeroUniqueSequence = true, RequiresFutureTargetTick = true,
                Operations = Endpoint.Describe().Select(o => new OperationDto { Name = o.Name, Permission = o.Permission.ToString(), RequiresSession = o.RequiresSession, RequiresControl = o.RequiresControl }).ToArray(),
                Actions = capabilities.Actions.Select(a => new ActionDescriptionDto { Kind = a.Kind.ToString(), RequiresTarget = a.RequiresTarget,
                    UsesAxes = a.UsesAxes, RequiresActor = a.RequiresActor, RequiresFiniteAxes = a.RequiresFiniteAxes, NormalizesAxes = a.NormalizesAxes,
                    SuccessCode = a.SuccessCode, RejectionCodes = a.RejectionCodes.ToArray() }).ToArray() });
        }
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
        private static ulong Parse(string value)
        {
            if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong result)) throw new ProtocolFault("payload.invalid");
            return result;
        }
        private static string Number(ulong value) => value.ToString(CultureInfo.InvariantCulture);
        private static ResultDto Map(ActionResult result) => new ResultDto { Sequence = Number(result.Sequence), Tick = Number(result.Tick), Status = result.Status.ToString(), Code = result.Code };
        private static ObservationDto Map(GameplayObservation observation) => new ObservationDto { Tick = Number(observation.Tick), Actors = observation.Actors.Select(a =>
            new ActorDto { Id = Number(a.Id), X = a.X, Y = a.Y, DirectionX = a.DirectionX, DirectionY = a.DirectionY, Speed = a.Speed,
                Health = a.Health, MaxHealth = a.MaxHealth, Active = a.Active }).ToArray() };
    }
}
