using System;
using InvariantChecks;
using TraceBuffering;
using System.Collections.Generic;
using CharacterCombat;
using CharacterMovement.Application;
using CharacterMovement.Domain;
using CharacterMovement.Integration;
using DeterministicSimulation;
using DeterministicSimulation.Framework;
using SimulationObjects;
using SimulationObjects.Contract;
using Testability;
using MovementAggregate = CharacterMovement.Domain.CharacterMovement;
using SeededRandom;

namespace GameplaySimulation
{
    /// <summary>Single-threaded project composition. Only Step advances authoritative state.</summary>
    public sealed partial class GameplaySession : ITestSession<GameplayScenario>, IGameplayControl,
        IIntentHandler<GameplaySession.RequestIntent>, IInternalCommandHandler<GameplaySession.ExecuteAction>,
        IDomainEventHandler<GameplaySession.ActorDied>, IInternalCommandHandler<GameplaySession.SpawnEnemy>, IPrePhysicsParticipant, IStructuralCommitParticipant
    {
        private sealed class Actor
        {
            internal MovementAggregate Movement;
            internal Combatant Combat;
            internal SimulationObjectRecord Identity;
        }
        internal readonly struct RequestIntent : IIntent
        {
            internal RequestIntent(GameplayRequest request) { Request = request; }
            internal GameplayRequest Request { get; }
        }
        internal readonly struct ExecuteAction : IInternalCommand
        {
            internal ExecuteAction(GameplayRequest request) { Request = request; }
            internal GameplayRequest Request { get; }
        }
        internal readonly struct ActorDamaged : IDomainEvent
        {
            internal ActorDamaged(ulong sequence, ulong target, int damage) { Sequence = sequence; Target = target; Damage = damage; }
            internal ulong Sequence { get; }
            internal ulong Target { get; }
            internal int Damage { get; }
        }
        internal readonly struct ActorDied : IDomainEvent
        {
            internal ActorDied(ulong sequence, ulong target) { Sequence = sequence; Target = target; }
            internal ulong Sequence { get; }
            internal ulong Target { get; }
        }
        internal readonly struct SpawnEnemy : IInternalCommand { }
        private SplitMix64Random enemyRandom;
        private SplitMix64Random respawnRandom;
        private readonly List<ulong> pendingRespawnTicks = new List<ulong>();
        private int enemiesSpawned;
        private int pendingEnemySpawns;
        private string currentStage;
        private string cancellationReason;
        public ulong LastCompletedTick { get; private set; }
        public LifecycleSnapshot ObserveLifecycle()
        {
            if (stepping || objects == null) throw new InvalidOperationException("Observe lifecycle between initialized ticks.");
            return new LifecycleSnapshot(objects.GetActiveOrdered().Count, movements.GetActiveOrdered().Count,
                actors.Count, enemiesSpawned, pendingEnemySpawns + pendingRespawnTicks.Count);
        }

        private GameplayScenario scenario;
        private SimulationPipeline pipeline;
        private SimulationRunner runner;
        private SimulationObjectRegistry objects;
        private CharacterMovementRepository movements;
        private MovementApplication movementApplication;
        private readonly SortedDictionary<ulong, Actor> actors = new SortedDictionary<ulong, Actor>();
        private readonly SortedDictionary<ulong, SortedDictionary<ulong, GameplayRequest>> pending = new SortedDictionary<ulong, SortedDictionary<ulong, GameplayRequest>>();
        private readonly HashSet<ulong> sequences = new HashSet<ulong>();
        private readonly List<GameplayRequest> history = new List<GameplayRequest>();
        private readonly List<ActionResult> resultHistory = new List<ActionResult>();
        private readonly List<HashCheckpoint> hashHistory = new List<HashCheckpoint>();
        private readonly List<ActionResult> tickResults = new List<ActionResult>();
        private readonly List<Func<IInvariant<GameplayObservation>>> extraInvariants = new List<Func<IInvariant<GameplayObservation>>>();
        private InvariantRegistry<GameplayObservation> invariants;
        private TraceRecorder trace;
        private ulong executingSequence;
        private bool stepping;
        private InvariantReport invariantReport = new InvariantReport(false, 0, 0, Array.Empty<InvariantViolation>());

        private readonly string policyRevision;
        public GameplaySession(SimulationDriveMode driveMode = SimulationDriveMode.Manual, string policyRevision = "v1")
        {
            if (!Enum.IsDefined(typeof(SimulationDriveMode), driveMode)) throw new ArgumentOutOfRangeException(nameof(driveMode));
            if (string.IsNullOrWhiteSpace(policyRevision)) throw new ArgumentException("Policy revision is required.", nameof(policyRevision));
            this.policyRevision = policyRevision;
            DriveMode = driveMode;
            Diagnostics = new DiagnosticsPort(this);
            Gameplay = new GameplayPort(this); Simulation = new SimulationPort(this);
            Admin = new AdminPort(this); Results = new ResultsPort(this); Capabilities = new CapabilitiesPort(this);
        }
        public IDiagnosticReader<GameplayObservation> Diagnostics { get; }

        public string Id { get; private set; } = string.Empty;
        public SessionState State { get; private set; } = SessionState.Created;
        public ulong CurrentTick => runner == null ? 0 : runner.TickNumber;
        public FailureArtifact Failure { get; private set; }
        public string DiagnosticPolicy { get; private set; }
        public IReadOnlyList<TraceEntry> ReadTrace() => trace == null ? Array.Empty<TraceEntry>() : trace.Snapshot();
        public IReadOnlyList<GameplayRequest> ActionHistory => new List<GameplayRequest>(history).AsReadOnly();
        public IReadOnlyList<HashCheckpoint> HashHistory => new List<HashCheckpoint>(hashHistory).AsReadOnly();

        // Administrative composition only; sealed by Start, not exposed via IGameplayControl.
        public void RegisterInvariant(Func<IInvariant<GameplayObservation>> factory)
        {
            if (State != SessionState.Created) throw new InvalidOperationException("Register invariants before Start.");
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            extraInvariants.Add(factory);
        }

        public void Start(GameplayScenario initial)
        {
            if (State != SessionState.Created) throw new InvalidOperationException("Start is valid only for a new session.");
            Initialize(initial);
        }
        public void Reset(GameplayScenario initial)
        {
            if (stepping) throw new InvalidOperationException("Cannot reset during a tick.");
            Initialize(initial);
        }
        public void Stop()
        {
            if (stepping) throw new InvalidOperationException("Cannot stop during a tick.");
            pending.Clear();
            if (State != SessionState.Faulted) cancellationReason = "session.stopped";
            if (State != SessionState.Faulted) State = SessionState.Stopped;
        }

        private void Initialize(GameplayScenario initial)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            initial.Validate();
            InvariantRegistry<GameplayObservation> nextChecks = new InvariantRegistry<GameplayObservation>();
            nextChecks.Register(new GameplayInvariant());
            List<string> policyCodes = new List<string> { "gameplay.valid_state" };
            foreach (Func<IInvariant<GameplayObservation>> factory in extraInvariants)
            {
                IInvariant<GameplayObservation> check = factory();
                nextChecks.Register(check);
                policyCodes.Add(check.Code);
            }
            nextChecks.Seal();
            policyCodes.Sort(StringComparer.Ordinal);
            DiagnosticPolicy = policyRevision + (initial.RandomRespawnDelay ? "/lifecycle-v3" : initial.ExtendedLifecycle ? "/lifecycle-v2" : "") + ":" + string.Join("|", policyCodes);
            invariants = nextChecks;
            invariantReport = new InvariantReport(false, 0, invariants.Count, Array.Empty<InvariantViolation>());
            scenario = initial;
            Id = Guid.NewGuid().ToString("N"); // Diagnostic/session identity only; excluded from state hash.
            actors.Clear(); pending.Clear(); sequences.Clear(); history.Clear(); resultHistory.Clear(); hashHistory.Clear(); tickResults.Clear();
            Failure = null; executingSequence = 0;
            LastCompletedTick = 0; currentStage = "Initialize"; cancellationReason = null;
            enemiesSpawned = 0; pendingEnemySpawns = 0;
            enemyRandom = SplitMix64Random.FromStream(initial.Seed, 1);
            respawnRandom = SplitMix64Random.FromStream(initial.Seed, 2);
            pendingRespawnTicks.Clear();
            trace = new TraceRecorder(initial.TraceCapacity);
            objects = new SimulationObjectRegistry();
            movements = new CharacterMovementRepository();
            movementApplication = new MovementApplication(movements);
            Spawn(default, false);
            if (initial.IncludeEnemy) Spawn(new MovementPosition(1, 0), true);
            objects.Commit();
            pipeline = new SimulationPipeline(onDispatch: RecordDispatch, onPhase: RecordPhase);
            pipeline.RegisterIntentHandler<RequestIntent>(this);
            pipeline.RegisterInternalCommandHandler<ExecuteAction>(this);
            pipeline.RegisterInternalCommandHandler<SpawnEnemy>(this);
            pipeline.RegisterDomainEventHandler<ActorDied>(this);
            pipeline.RegisterPrePhysicsParticipant(this);
            pipeline.RegisterStructuralCommitParticipant(this);
            pipeline.Seal();
            runner = new SimulationRunner(pipeline, initial.TickDelta);
            State = SessionState.Running;
            hashHistory.Add(new HashCheckpoint(0, GameplayStateHasher.Compute(Observe(), scenario)));
        }

        private void Spawn(MovementPosition position, bool enemy)
        {
            SimulationObjectRecord identity = objects.RequestSpawn();
            MovementAggregate movement = new MovementAggregate(new CharacterId(identity.Id.Value), position, scenario.Speed);
            movements.Add(movement);
            int health = enemy && scenario.RandomEnemyHealth ? enemyRandom.NextInt(scenario.EnemyHealthMin, scenario.EnemyHealthMax + 1) : scenario.Health;
            if (enemy) enemiesSpawned++;
            actors.Add(identity.Id.Value, new Actor { Identity = identity, Movement = movement, Combat = new Combatant(health) });
        }

        public SubmissionResult Submit(GameplayRequest request)
        {
            if (State != SessionState.Running) return new SubmissionResult(false, "session.not_running");
            if (stepping) return new SubmissionResult(false, "session.busy");
            if (request == null) return new SubmissionResult(false, "request.null");
            if (request.SessionId != Id) return new SubmissionResult(false, "session.stale");
            if (request.Sequence == 0) return new SubmissionResult(false, "sequence.invalid");
            if (sequences.Contains(request.Sequence)) return new SubmissionResult(false, "sequence.duplicate");
            if (request.TargetTick <= CurrentTick || request.TargetTick > (ulong)scenario.MaxTicks)
                return new SubmissionResult(false, "tick.out_of_range");
            if (history.Count >= scenario.MaxActions) return new SubmissionResult(false, "action.capacity");
            if (!pending.TryGetValue(request.TargetTick, out SortedDictionary<ulong, GameplayRequest> tick))
            {
                tick = new SortedDictionary<ulong, GameplayRequest>();
                pending.Add(request.TargetTick, tick);
            }
            tick.Add(request.Sequence, request);
            sequences.Add(request.Sequence);
            history.Add(request); // Complete admitted history, bounded; never silently truncate reproduction data.
            trace.Record(new TraceEntry(Id, CurrentTick, request.Sequence, "Queued", request.Kind.ToString(), "queue.accepted", actor: request.Actor, target: request.Target));
            return new SubmissionResult(true, "queue.accepted");
        }

        public TickReport Step()
        {
            if (DriveMode != SimulationDriveMode.Manual) throw new InvalidOperationException("Realtime driver owns this session clock.");
            return StepCore();
        }

        private TickReport StepCore()
        {
            if (State != SessionState.Running || stepping) throw new InvalidOperationException("Session cannot step.");
            if (CurrentTick >= (ulong)scenario.MaxTicks) { Stop(); cancellationReason = "tick.budget"; throw new InvalidOperationException("Tick budget exhausted."); }
            stepping = true;
            tickResults.Clear(); executingSequence = 0;
            ulong nextTick = CurrentTick + 1;
            string hash = string.Empty;
            IReadOnlyList<InvariantViolation> failures = Array.Empty<InvariantViolation>();
            List<GameplayRequest> executingBatch = new List<GameplayRequest>();
            try
            {
                if (pending.TryGetValue(nextTick, out SortedDictionary<ulong, GameplayRequest> tick))
                {
                    foreach (GameplayRequest request in tick.Values)
                    {
                        executingBatch.Add(request);
                        pipeline.EnqueueIntent(new RequestIntent(request));
                    }
                    pending.Remove(nextTick);
                }
                runner.AdvanceTick();
                executingSequence = 0; // Tick-level failures must not be falsely attributed to the last action.
                GameplayObservation observation = Observe();
                currentStage = "StateHash";
                hash = GameplayStateHasher.Compute(observation, scenario);
                hashHistory.Add(new HashCheckpoint(CurrentTick, hash));
                trace.Record(new TraceEntry(Id, CurrentTick, 0, "StateHash", "Gameplay", hash));
                currentStage = "Invariant";
                ValidateLifecycle();
                failures = invariants.Evaluate(observation);
                invariantReport = new InvariantReport(true, CurrentTick, invariants.Count, failures);
                foreach (InvariantViolation failure in failures)
                    trace.Record(new TraceEntry(Id, CurrentTick, 0, "Invariant", failure.Code, failure.Detail));
                if (failures.Count > 0) CaptureFailure(failures[0].Code, null);
                else LastCompletedTick = CurrentTick;
            }
            catch (Exception exception)
            {
                if (executingSequence != 0 && !tickResults.Exists(result => result.Sequence == executingSequence))
                    Complete(executingSequence, ActionStatus.Failed, "simulation.exception");
                foreach (GameplayRequest request in executingBatch)
                    if (!tickResults.Exists(result => result.Sequence == request.Sequence))
                        Complete(request.Sequence, ActionStatus.Failed, "tick.aborted");
                trace.Record(new TraceEntry(Id, CurrentTick, executingSequence, "Exception", exception.GetType().FullName, exception.Message));
                CaptureFailure("simulation.exception", exception);
            }
            finally { stepping = false; }
            return new TickReport(CurrentTick, tickResults, hash, failures);
        }

        private void CaptureFailure(string code, Exception exception)
        {
            if (Failure != null) return;
            State = SessionState.Faulted;
            cancellationReason = "session.faulted";
            pending.Clear();
            // No rollback is claimed. Retain partial state and prohibit further stepping until Reset.
            Failure = new FailureArtifact(Id, scenario, CurrentTick, executingSequence, code, exception?.ToString(),
                history, resultHistory, hashHistory, trace.Snapshot(), trace.DroppedCount, Observe(), exception?.GetType().FullName, DiagnosticPolicy, currentStage, LastCompletedTick);
        }

        public GameplayObservation Observe()
        {
            List<ActorObservation> snapshot = new List<ActorObservation>();
            foreach (KeyValuePair<ulong, Actor> pair in actors)
            {
                Actor actor = pair.Value;
                bool active = objects.TryGet(actor.Identity.Handle, out SimulationObjectRecord record) && record.IsActive;
                snapshot.Add(new ActorObservation(pair.Key, actor.Movement.Position.X, actor.Movement.Position.Y,
                    actor.Movement.DesiredDirection.X, actor.Movement.DesiredDirection.Y, actor.Movement.Speed,
                    actor.Combat.Health, actor.Combat.MaxHealth, active));
            }
            return new GameplayObservation(CurrentTick, snapshot, enemyRandom == null ? 0 : enemyRandom.CaptureState().Value, enemiesSpawned,
                respawnRandom == null ? 0 : respawnRandom.CaptureState().Value, pendingRespawnTicks);
        }

        void IIntentHandler<RequestIntent>.Handle(RequestIntent intent)
            => pipeline.EnqueueInternalCommand(new ExecuteAction(intent.Request));

        void IInternalCommandHandler<ExecuteAction>.Handle(ExecuteAction command)
        {
            GameplayRequest request = command.Request;
            executingSequence = request.Sequence;
            if (!Enum.IsDefined(typeof(GameplayActionKind), request.Kind)) { Complete(request.Sequence, ActionStatus.InvalidRequest, "action.unknown"); return; }
            if (request.Actor == 0 || !GameplayScenario.Finite(request.X) || !GameplayScenario.Finite(request.Y))
            { Complete(request.Sequence, ActionStatus.InvalidRequest, "parameters.invalid"); return; }
            if (!actors.TryGetValue(request.Actor, out Actor actor)) { Complete(request.Sequence, ActionStatus.Rejected, "actor.unknown"); return; }
            if (actor.Combat.IsDead) { Complete(request.Sequence, ActionStatus.Rejected, "actor.dead"); return; }
            if (request.Kind == GameplayActionKind.Move)
            {
                PlayerMoveIntent move = new PlayerMoveIntent(actor.Movement.Id, MovementDirection.FromAxes(request.X, request.Y));
                movementApplication.TrySetDirection(move.Character, move.Direction);
                Complete(request.Sequence, ActionStatus.Accepted, "move.applied");
                return;
            }
            if (request.Target == request.Actor) { Complete(request.Sequence, ActionStatus.Rejected, "target.self"); return; }
            if (!actors.TryGetValue(request.Target, out Actor target)) { Complete(request.Sequence, ActionStatus.Rejected, "target.unknown"); return; }
            if (target.Combat.IsDead) { Complete(request.Sequence, ActionStatus.Rejected, "target.dead"); return; }
            double dx = (double)actor.Movement.Position.X - target.Movement.Position.X;
            double dy = (double)actor.Movement.Position.Y - target.Movement.Position.Y;
            if (dx * dx + dy * dy > (double)scenario.AttackRange * scenario.AttackRange)
            { Complete(request.Sequence, ActionStatus.Rejected, "target.out_of_range"); return; }
            int applied = target.Combat.TakeDamage(scenario.Damage);
            pipeline.PublishDomainEvent(new ActorDamaged(request.Sequence, request.Target, applied));
            if (target.Combat.IsDead)
            {
                target.Movement.SetDesiredDirection(default);
                pipeline.PublishDomainEvent(new ActorDied(request.Sequence, request.Target));
            }
            Complete(request.Sequence, ActionStatus.Accepted, "attack.applied");
        }

        private void Complete(ulong sequence, ActionStatus status, string code)
        {
            ActionResult result = new ActionResult(sequence, CurrentTick, status, code);
            tickResults.Add(result); resultHistory.Add(result);
            trace.Record(new TraceEntry(Id, CurrentTick, sequence, status.ToString(), "ActionResult", code));
        }

        void IDomainEventHandler<ActorDied>.Handle(ActorDied death)
        {
            bool requested = objects.RequestDestroy(actors[death.Target].Identity.Handle);
            if (requested && death.Target != 1 && scenario.RespawnEnemies) pipeline.EnqueueInternalCommand(new SpawnEnemy());
        }
        void IInternalCommandHandler<SpawnEnemy>.Handle(SpawnEnemy command) { pendingEnemySpawns++; }

        void IPrePhysicsParticipant.Tick(SimulationContext context)
        {
            executingSequence = 0;
            foreach (Actor actor in actors.Values)
                if (!actor.Combat.IsDead) actor.Movement.Advance(context.Tick.DeltaTime);
        }

        void IStructuralCommitParticipant.Commit(SimulationContext context)
        {
            StructuralCommitResult result = objects.Commit();
            foreach (SimulationObjectRecord destroyed in result.Destroyed)
            {
                movements.Remove(new CharacterId(destroyed.Id.Value));
                trace.Record(new TraceEntry(Id, CurrentTick, 0, "StructuralCommit", "Destroyed", destroyed.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
            while (pendingEnemySpawns > 0)
            {
                pendingEnemySpawns--;
                if (enemiesSpawned + pendingRespawnTicks.Count >= scenario.MaxEnemySpawns)
                { trace.Record(new TraceEntry(Id, CurrentTick, 0, "StructuralCommit", "SpawnSkipped", "spawn.budget")); continue; }
                if (scenario.RandomRespawnDelay)
                {
                    int minTicks = (int)Math.Ceiling(1d / scenario.TickDelta);
                    int maxTicks = (int)Math.Floor(3d / scenario.TickDelta);
                    ulong due = checked(CurrentTick + (ulong)respawnRandom.NextInt(minTicks, maxTicks + 1));
                    pendingRespawnTicks.Add(due);
                    pendingRespawnTicks.Sort();
                    trace.Record(new TraceEntry(Id, CurrentTick, 0, "StructuralCommit", "RespawnScheduled", due.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                }
                else Spawn(new MovementPosition(1, 0), true);
            }
            while (pendingRespawnTicks.Count > 0 && pendingRespawnTicks[0] <= CurrentTick)
            {
                pendingRespawnTicks.RemoveAt(0);
                Spawn(new MovementPosition(1, 0), true);
            }
            StructuralCommitResult created = objects.Commit();
            foreach (SimulationObjectRecord spawned in created.Spawned)
                trace.Record(new TraceEntry(Id, CurrentTick, 0, "StructuralCommit", "Spawned", spawned.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        private void ValidateLifecycle()
        {
            int active = 0;
            foreach (Actor actor in actors.Values)
            {
                bool registered = objects.TryGet(actor.Identity.Handle, out SimulationObjectRecord record) && record.IsActive;
                bool inRepository = movements.TryGet(actor.Movement.Id, out MovementAggregate ignored);
                if (registered != inRepository || registered == actor.Combat.IsDead) throw new InvalidOperationException("Registry/repository/domain lifetime disagreement.");
                if (registered) active++;
            }
            if (active != objects.GetActiveOrdered().Count) throw new InvalidOperationException("Unowned registry object.");
            if (active != movements.GetActiveOrdered().Count) throw new InvalidOperationException("Unowned movement repository object.");
        }

        private void RecordPhase(SimulationPhase phase, bool entering)
        {
            currentStage = phase.ToString(); executingSequence = 0;
            trace.Record(new TraceEntry(Id, CurrentTick, 0, "Phase", currentStage, entering ? "begin" : "end"));
        }

        private sealed class DiagnosticsPort : IDiagnosticReader<GameplayObservation>
        {
            private readonly GameplaySession owner;
            internal DiagnosticsPort(GameplaySession owner) { this.owner = owner; }
            public DiagnosticSnapshot<GameplayObservation> ObserveDiagnostics()
            {
                if (owner.stepping) throw new InvalidOperationException("Read diagnostics between ticks.");
                return new DiagnosticSnapshot<GameplayObservation>(owner.Id, owner.State, owner.CurrentTick,
                    owner.Observe(), owner.invariantReport, owner.Failure?.Code);
            }
            public TraceBatch<TraceEntry> ReadTrace(TraceCursor cursor, int maxItems)
            {
                if (owner.stepping) throw new InvalidOperationException("Read diagnostics between ticks.");
                if (owner.trace == null) throw new InvalidOperationException("Start the session before reading trace.");
                return owner.trace.Reader.Read(cursor, maxItems);
            }
        }

        private void RecordDispatch(MessageDispatch dispatch)
        {
            ulong sequence = 0;
            ulong actor = 0;
            ulong target = 0;
            string details = string.Empty;
            if (dispatch.Message is RequestIntent intent)
            { sequence = intent.Request.Sequence; actor = intent.Request.Actor; target = intent.Request.Target; }
            if (dispatch.Message is ExecuteAction command)
            { sequence = command.Request.Sequence; actor = command.Request.Actor; target = command.Request.Target; }
            if (dispatch.Message is ActorDied death) { sequence = death.Sequence; target = death.Target; }
            if (dispatch.Message is ActorDamaged damage)
            { sequence = damage.Sequence; target = damage.Target; details = "damage=" + damage.Damage.ToString(System.Globalization.CultureInfo.InvariantCulture); }
            executingSequence = sequence;
            trace.Record(new TraceEntry(Id, CurrentTick, sequence, dispatch.Category.ToString(), dispatch.Message.GetType().Name, details, dispatch.Wave, actor, target));
        }
    }
}
