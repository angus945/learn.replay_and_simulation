using System;
using RuntimeControl;
using RuntimeObservation;
using TestabilityEvidence;
using TestabilityOracles;

internal static class ModuleContractChecks
{
    private sealed class PassingOracle : ITestOracle<int>
    {
        public string Id
        {
            get { return "pass"; }
        }

        public OracleResult Evaluate(int context)
        {
            return new OracleResult(TestVerdict.Passed, "value.seen", context.ToString());
        }
    }

    private sealed class ThrowingOracle : ITestOracle<int>
    {
        public string Id
        {
            get { return "throw"; }
        }

        public OracleResult Evaluate(int context)
        {
            throw new InvalidOperationException("oracle failure");
        }
    }

    internal static void RuntimeObservation()
    {
        ObservationChannel<string> channel = new ObservationChannel<string>(1);
        CaptureMetadata firstMetadata = new CaptureMetadata("source", "scope", 1);
        ObservationReference first = channel.PublisherPort.Publish("first", firstMetadata);
        Check(channel.ReaderPort.Read(first).Observation == "first", "Exact observation lookup failed.");
        CaptureMetadata failureMetadata = new CaptureMetadata("source", "scope", 2);
        channel.PublisherPort.ReportCaptureFailure(new CaptureFailure(failureMetadata, "capture.failed", "expected"));
        Check(channel.ReaderPort.ReadLatest().State == ObservationReadState.CaptureFailed, "Capture failure was not retained independently.");
        Check(channel.ReaderPort.Read(first).State == ObservationReadState.Evicted, "Bounded observation eviction failed.");
        ObservationChannel<string> other = new ObservationChannel<string>();
        ObservationReference foreign = other.PublisherPort.Publish("other", firstMetadata);
        Check(channel.ReaderPort.Read(foreign).State == ObservationReadState.ForeignReference, "Foreign observation reference was accepted.");
    }

    internal static void RuntimeControl()
    {
        OperationRegistry<string> registry = new OperationRegistry<string>("scope", 3, 1, 1);
        OperationDescriptor descriptor = new OperationDescriptor("move", "same-command", "correlation");
        OperationAdmission first = registry.Admit(descriptor);
        OperationAdmission duplicate = registry.Admit(descriptor);
        Check(first.Status == OperationAdmissionStatus.Admitted && duplicate.Status == OperationAdmissionStatus.Duplicate, "Idempotent operation admission failed.");
        Check(first.Handle == duplicate.Handle, "Duplicate admission did not return the stable handle.");
        OperationAdmission conflict = registry.Admit(new OperationDescriptor("attack", "same-command", "correlation"));
        Check(conflict.Status == OperationAdmissionStatus.Invalid && conflict.Code == "operation.idempotency_conflict", "Conflicting idempotency metadata was silently aliased.");
        Check(registry.Admit(new OperationDescriptor("blocked")).Status == OperationAdmissionStatus.CapacityExceeded, "Pending-operation capacity was not enforced.");
        Check(registry.TryMarkRunning(first.Handle), "Pending operation did not enter running state.");
        OperationCompletion<string> completion = new OperationCompletion<string>(OperationState.Succeeded, "move.applied", "result", "observation:1");
        Check(registry.TryComplete(first.Handle, completion), "Running operation did not complete.");
        OperationAdmission second = registry.Admit(new OperationDescriptor("move-next"));
        registry.TryComplete(second.Handle, new OperationCompletion<string>(OperationState.Rejected, "move.rejected"));
        Check(registry.Read(first.Handle).ReadState == OperationReadState.Evicted, "Bounded operation result eviction failed.");
        Check(registry.Read(second.Handle).State == OperationState.Rejected, "Terminal operation state was not queryable.");
    }

    internal static void TestabilityOracles()
    {
        ITestOracle<int>[] ordered = new ITestOracle<int>[] { new PassingOracle(), new ThrowingOracle() };
        OracleSet<int> set = new OracleSet<int>("contract", ordered);
        EvaluationReport report = set.Evaluate("case", 7);
        Check(report.Results.Count == 1 && report.Results[0].Code == "value.seen", "Oracle results lost explicit order.");
        Check(report.Errors.Count == 1 && report.Errors[0].OracleId == "throw", "Oracle exception was not preserved as infrastructure evidence.");
        Check(report.Verdict == TestVerdict.InfrastructureError, "Oracle infrastructure failure did not dominate the aggregate verdict.");
    }

    internal static void TestabilityEvidence()
    {
        EvidenceManifest manifest = new EvidenceManifest("run", "case", "build", "fixture");
        EvidenceBuilder builder = new EvidenceBuilder(manifest, 8, 2);
        EvidenceEntry retained = new EvidenceEntry(EvidenceKind.Observation, "observation", new EvidenceReference("memory", "one"), 8);
        EvidenceEntry dropped = new EvidenceEntry(EvidenceKind.Trace, "trace", new EvidenceReference("memory", "two"), 1);
        Check(builder.TryAdd(retained), "Evidence within budget was rejected.");
        Check(!builder.TryAdd(dropped), "Evidence byte budget was not enforced.");
        builder.RecordFailure("first");
        builder.RecordFailure("second");
        builder.RecordCleanupError("cleanup");
        EvidenceBundle bundle = builder.Build();
        Check(bundle.Entries.Count == 1 && bundle.DroppedEntryCount == 1 && bundle.RetainedBytes == 8, "Evidence bundle bounds are incorrect.");
        Check(bundle.FirstFailure == "first" && bundle.CleanupErrors.Count == 1, "Failure or cleanup evidence was not retained.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
