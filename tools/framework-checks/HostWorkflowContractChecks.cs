using System;
using System.Collections.Generic;
using Module.Verification.RuntimeControl;
using Module.Verification.StateSnapshot;
using Module.Verification.Evidence;
using Module.Verification.Oracle;

internal static class HostWorkflowContractChecks
{
    private sealed class DocumentWorkspaceState
    {
        public DocumentWorkspaceState(string text, int revision)
        {
            Text = text;
            Revision = revision;
        }

        public string Text { get; private set; }
        public int Revision { get; private set; }

        public void Replace(string text)
        {
            Text = text;
            Revision++;
        }
    }

    private sealed class DocumentObservation
    {
        public DocumentObservation(string text, int revision)
        {
            Text = text;
            Revision = revision;
        }

        public string Text { get; }
        public int Revision { get; }
    }

    private sealed class DocumentOutcome
    {
        public DocumentOutcome(int revision)
        {
            Revision = revision;
        }

        public int Revision { get; }
    }

    private sealed class ExpectedDocumentOracle : ITestOracle<DocumentObservation>
    {
        private readonly string expectedText;
        private readonly int expectedRevision;

        public ExpectedDocumentOracle(string expectedText, int expectedRevision)
        {
            this.expectedText = expectedText;
            this.expectedRevision = expectedRevision;
        }

        public string Id
        {
            get { return "document.expected-state"; }
        }

        public OracleResult Evaluate(DocumentObservation context)
        {
            bool matches = context != null && context.Text == expectedText && context.Revision == expectedRevision;
            if (matches) return new OracleResult(TestVerdict.Passed, "document.matches");
            return new OracleResult(TestVerdict.Failed, "document.differs");
        }
    }

    private sealed class ImportRequest
    {
        public ImportRequest(OperationHandle handle, string value)
        {
            Handle = handle;
            Value = value;
        }

        public OperationHandle Handle { get; }
        public string Value { get; }
    }

    private sealed class ImportQueueHost
    {
        private readonly DocumentWorkspaceState workspace = new DocumentWorkspaceState(string.Empty, 0);
        private readonly StateSnapshotChannel<DocumentObservation> observations = new StateSnapshotChannel<DocumentObservation>();
        private readonly OperationRegistry<DocumentOutcome> operations = new OperationRegistry<DocumentOutcome>("import-workspace", 1);
        private readonly Queue<ImportRequest> pending = new Queue<ImportRequest>();

        public ImportQueueHost()
        {
            Publish();
        }

        public IStateSnapshotReader<DocumentObservation> Observations
        {
            get { return observations.ReaderPort; }
        }

        public OperationAdmission Submit(string value)
        {
            OperationAdmission admission = operations.Admit(new OperationDescriptor("document.import"));
            if (admission.IsAdmitted) pending.Enqueue(new ImportRequest(admission.Handle, value));
            return admission;
        }

        public OperationRead<DocumentOutcome> Read(OperationHandle handle)
        {
            return operations.Read(handle);
        }

        public StateSnapshotReference ProgressOne()
        {
            if (pending.Count == 0) throw new InvalidOperationException("No import is pending.");
            ImportRequest request = pending.Dequeue();
            if (!operations.TryMarkRunning(request.Handle)) throw new InvalidOperationException("Import did not enter running state.");
            workspace.Replace(request.Value);
            StateSnapshotReference barrier = Publish();
            DocumentOutcome outcome = new DocumentOutcome(workspace.Revision);
            OperationCompletion<DocumentOutcome> completion = new OperationCompletion<DocumentOutcome>(OperationState.Succeeded, "import.completed", outcome, FormatBarrier(barrier));
            if (!operations.TryComplete(request.Handle, completion)) throw new InvalidOperationException("Import did not complete.");
            return barrier;
        }

        private StateSnapshotReference Publish()
        {
            DocumentObservation observation = new DocumentObservation(workspace.Text, workspace.Revision);
            StateSnapshotCaptureMetadata metadata = new StateSnapshotCaptureMetadata("import-host", "import-workspace", workspace.Revision);
            return observations.PublisherPort.Publish(observation, metadata);
        }
    }

    internal static void DocumentWorkspace()
    {
        DocumentWorkspaceState workspace = new DocumentWorkspaceState("before", 0);
        StateSnapshotChannel<DocumentObservation> observations = new StateSnapshotChannel<DocumentObservation>();
        OperationRegistry<DocumentOutcome> operations = new OperationRegistry<DocumentOutcome>("document-workspace", 1);
        StateSnapshotCaptureMetadata initialMetadata = new StateSnapshotCaptureMetadata("document-host", "document-workspace", workspace.Revision);
        observations.PublisherPort.Publish(new DocumentObservation(workspace.Text, workspace.Revision), initialMetadata);

        OperationAdmission admission = operations.Admit(new OperationDescriptor("document.replace", "replace-once"));
        Check(admission.IsAdmitted && operations.TryMarkRunning(admission.Handle), "Document operation did not enter the formal product path.");
        workspace.Replace("after");
        StateSnapshotCaptureMetadata afterMetadata = new StateSnapshotCaptureMetadata("document-host", "document-workspace", workspace.Revision);
        StateSnapshotReference barrier = observations.PublisherPort.Publish(new DocumentObservation(workspace.Text, workspace.Revision), afterMetadata);
        DocumentOutcome outcome = new DocumentOutcome(workspace.Revision);
        OperationCompletion<DocumentOutcome> completion = new OperationCompletion<DocumentOutcome>(OperationState.Succeeded, "replace.completed", outcome, FormatBarrier(barrier));
        Check(operations.TryComplete(admission.Handle, completion), "Document operation did not complete.");

        StateSnapshotRead<DocumentObservation> exact = observations.ReaderPort.Read(barrier);
        ITestOracle<DocumentObservation>[] oracleItems = new ITestOracle<DocumentObservation>[] { new ExpectedDocumentOracle("after", 1) };
        OracleSet<DocumentObservation> oracleSet = new OracleSet<DocumentObservation>("document-case", oracleItems);
        EvaluationReport evaluation = oracleSet.Evaluate("replace", exact.Snapshot);
        EvidenceBuilder evidence = new EvidenceBuilder(new EvidenceManifest("run", "replace"), 1024, 4);
        evidence.TryAdd(new EvidenceEntry(EvidenceKind.FactStream, "replace-operation-transition", new EvidenceReference("operation", admission.Handle.Sequence.ToString()), 64));
        evidence.TryAdd(new EvidenceEntry(EvidenceKind.StateSnapshot, "after", new EvidenceReference("state-snapshot", FormatBarrier(barrier)), 64));
        evidence.TryAdd(new EvidenceEntry(EvidenceKind.Evaluation, "oracle", new EvidenceReference("evaluation", evaluation.Verdict.ToString()), 64));
        EvidenceBundle bundle = evidence.Build();

        OperationRead<DocumentOutcome> operation = operations.Read(admission.Handle);
        Check(exact.State == StateSnapshotReadState.Available && exact.Snapshot.Text == "after", "Exact state snapshot barrier was not resolved.");
        Check(operation.State == OperationState.Succeeded && operation.Completion.Result.Revision == 1, "Document completion state is incorrect.");
        Check(evaluation.Verdict == TestVerdict.Passed && bundle.Entries.Count == 3, "Document oracle or evidence assembly failed.");
    }

    internal static void ImportQueue()
    {
        ImportQueueHost host = new ImportQueueHost();
        OperationAdmission admission = host.Submit("imported");
        OperationRead<DocumentOutcome> pending = host.Read(admission.Handle);
        StateSnapshotRead<DocumentObservation> before = host.Observations.ReadLatest();
        Check(pending.State == OperationState.Pending && before.Snapshot.Revision == 0, "Submit performed asynchronous work before host progress.");

        StateSnapshotReference barrier = host.ProgressOne();
        OperationRead<DocumentOutcome> completed = host.Read(admission.Handle);
        StateSnapshotRead<DocumentObservation> after = host.Observations.Read(barrier);
        Check(completed.State == OperationState.Succeeded, "Explicit host progress did not complete the import.");
        Check(completed.Completion.ObservationBarrier == FormatBarrier(barrier), "Completion did not preserve its exact observation barrier.");
        Check(after.State == StateSnapshotReadState.Available && after.Snapshot.Text == "imported", "Import state snapshot did not follow completion.");
    }

    private static string FormatBarrier(StateSnapshotReference reference)
    {
        return reference.ChannelId.ToString("N") + ":" + reference.CaptureId;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
