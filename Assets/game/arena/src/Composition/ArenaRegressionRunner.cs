using System;
using System.Collections.Generic;
using Arena.Integration;
using RuntimeControl;
using TestabilityEvidence;

namespace Arena.Composition
{
    public sealed class ArenaScheduledInput
    {
        public ArenaScheduledInput(ulong tick, ArenaInput input)
        {
            Tick = tick;
            Input = input ?? throw new ArgumentNullException(nameof(input));
        }

        public ulong Tick { get; }
        public ArenaInput Input { get; }
    }

    public sealed class ArenaRegressionResult
    {
        public ArenaRegressionResult(ArenaRecording recording, EvidenceBundle evidence, ArenaFailure failure)
        {
            Recording = recording;
            Evidence = evidence;
            Failure = failure;
        }

        public ArenaRecording Recording { get; }
        public EvidenceBundle Evidence { get; }
        public ArenaFailure Failure { get; }
    }

    /// <summary>Application-owned orchestration; modules only perform the finite calls made here.</summary>
    public sealed class ArenaRegressionRunner
    {
        private readonly ArenaDefinition definition;

        public ArenaRegressionRunner(ArenaDefinition definition)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public ArenaRegressionResult Run(string runId, string caseId, ArenaScenario scenario, IEnumerable<ArenaScheduledInput> inputs, ulong endTick)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            using (ArenaSession session = definition.CreateSession(scenario))
            {
                ArenaObservation initial = session.Observe();
                if (initial == null) throw new InvalidOperationException("Arena initial observation was not published.");
                foreach (ArenaScheduledInput scheduled in inputs)
                {
                    OperationAdmission admission = session.Submit(scheduled.Input, scheduled.Tick);
                    if (!admission.IsAdmitted) throw new InvalidOperationException("Arena regression input was not admitted: " + admission.Code);
                }
                while (session.CurrentTick < endTick && session.State == ArenaSessionState.Running) session.Step();
                ArenaRecording recording = session.CaptureRecording();
                EvidenceBundle evidence = session.CaptureEvidence(runId, caseId);
                return new ArenaRegressionResult(recording, evidence, session.Failure);
            }
        }
    }
}
