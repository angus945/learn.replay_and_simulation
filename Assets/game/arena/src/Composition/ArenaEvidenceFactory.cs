using System;
using Arena.Integration;
using TestabilityEvidence;

namespace Arena.Composition
{
    public static class ArenaEvidenceFactory
    {
        public static EvidenceBundle Build(string runId, string caseId, ArenaRecording recording, ArenaDiagnosticSnapshot snapshot)
        {
            if (recording == null) throw new ArgumentNullException(nameof(recording));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            EvidenceManifest manifest = new EvidenceManifest(runId, caseId, recording.Runtime, recording.Scenario);
            EvidenceBuilder builder = new EvidenceBuilder(manifest, recording.Limits.MaxTotalPayloadBytes, recording.Limits.MaxInputs + recording.Limits.MaxTicks + recording.Limits.TraceCapacity + 4);
            AddOperations(builder, recording);
            AddTicks(builder, recording);
            EvidenceReference observationReference = new EvidenceReference("arena-observation", snapshot.ObservationReference.ChannelId.ToString("N") + ":" + snapshot.ObservationReference.CaptureId);
            builder.TryAdd(new EvidenceEntry(EvidenceKind.Observation, "latest-observation", observationReference, 128));
            EvidenceReference traceReference = new EvidenceReference("arena-trace", snapshot.SessionId + ":" + recording.Trace.Count);
            builder.TryAdd(new EvidenceEntry(EvidenceKind.Trace, "bounded-trace", traceReference, recording.Trace.Count * 128L, recording.DroppedTraceEntries > 0));
            if (snapshot.FaultCode != null) builder.RecordFailure(snapshot.FaultCode);
            return builder.Build();
        }

        private static void AddOperations(EvidenceBuilder builder, ArenaRecording recording)
        {
            foreach (ArenaRecordedInput input in recording.Inputs)
            {
                EvidenceReference reference = new EvidenceReference("arena-operation", input.Sequence.ToString());
                builder.TryAdd(new EvidenceEntry(EvidenceKind.Operation, "operation-" + input.Sequence, reference, input.Payload.Length * 2L));
            }
        }

        private static void AddTicks(EvidenceBuilder builder, ArenaRecording recording)
        {
            foreach (ArenaRecordedTick tick in recording.Ticks)
            {
                EvidenceReference observation = new EvidenceReference("arena-state-digest", tick.Tick + ":" + tick.Digest);
                builder.TryAdd(new EvidenceEntry(EvidenceKind.Observation, "tick-" + tick.Tick, observation, 96));
                EvidenceReference evaluation = new EvidenceReference("arena-evaluation", tick.Tick + ":" + (tick.Failure == null ? "passed" : tick.Failure.Code));
                builder.TryAdd(new EvidenceEntry(EvidenceKind.Evaluation, "evaluation-" + tick.Tick, evaluation, 96));
            }
        }
    }
}
