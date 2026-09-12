using System;
using System.Collections.Generic;
using System.Text;
using Arena.Integration;
using Module.Verification.RuntimeControl;

namespace Arena.Composition
{
    internal sealed class ArenaQueuedOperation
    {
        public ArenaQueuedOperation(OperationHandle handle, ulong targetTick, string payload, ArenaTraceMetadata metadata)
        {
            Handle = handle;
            TargetTick = targetTick;
            Payload = payload;
            Metadata = metadata;
        }

        public OperationHandle Handle { get; }
        public ulong TargetTick { get; }
        public string Payload { get; }
        public ArenaTraceMetadata Metadata { get; }
    }

    internal sealed class ArenaQueuedOperationComparer : IComparer<ArenaQueuedOperation>
    {
        public int Compare(ArenaQueuedOperation left, ArenaQueuedOperation right)
        {
            return left.Handle.Sequence.CompareTo(right.Handle.Sequence);
        }
    }

    /// <summary>Admits and retains operation state; execution still enters ArenaApplication through the simulation intent handler.</summary>
    internal sealed class ArenaControlAdapter
    {
        private readonly ArenaDefinition definition;
        private readonly ArenaLimits limits;
        private readonly OperationRegistry<ArenaOperationResult> registry;
        private readonly List<ArenaRecordedInput> inputs = new List<ArenaRecordedInput>();
        private readonly List<ArenaOperationResult> resultHistory = new List<ArenaOperationResult>();
        private readonly SortedDictionary<ulong, List<ArenaQueuedOperation>> pending = new SortedDictionary<ulong, List<ArenaQueuedOperation>>();
        private long totalPayloadBytes;

        internal ArenaControlAdapter(ArenaDefinition definition, ArenaLimits limits, string sessionId, long epoch, int scenarioPayloadBytes)
        {
            this.definition = definition;
            this.limits = limits;
            registry = new OperationRegistry<ArenaOperationResult>(sessionId, epoch, limits.MaxInputs, limits.MaxInputs);
            totalPayloadBytes = scenarioPayloadBytes;
        }

        internal IReadOnlyList<ArenaRecordedInput> Inputs
        {
            get { return inputs.AsReadOnly(); }
        }

        internal OperationAdmission Submit(ulong currentTick, ulong targetTick, ArenaInput input)
        {
            if (input == null) return OperationAdmission.Invalid("input.invalid");
            if (targetTick <= currentTick || targetTick > (ulong)limits.MaxTicks) return OperationAdmission.Invalid("tick.out_of_range");
            if (inputs.Count >= limits.MaxInputs) return OperationAdmission.Invalid("input.capacity");
            string payload;
            ArenaInput independent;
            try
            {
                payload = definition.EncodeInput(input);
                limits.CheckPayload(payload);
                independent = definition.DecodeInput(payload);
            }
            catch (ArgumentException)
            {
                return OperationAdmission.Invalid("input.invalid");
            }
            int payloadBytes = Encoding.UTF8.GetByteCount(payload);
            if (totalPayloadBytes + payloadBytes > limits.MaxTotalPayloadBytes) return OperationAdmission.Invalid("input.payload_budget");
            ArenaTraceMetadata metadata = definition.DescribeInput(independent);
            OperationDescriptor descriptor = new OperationDescriptor(metadata.Type);
            OperationAdmission admission = registry.Admit(descriptor);
            if (!admission.IsAdmitted) return admission;
            ArenaQueuedOperation operation = new ArenaQueuedOperation(admission.Handle, targetTick, payload, metadata);
            List<ArenaQueuedOperation> batch;
            if (!pending.TryGetValue(targetTick, out batch))
            {
                batch = new List<ArenaQueuedOperation>();
                pending.Add(targetTick, batch);
            }
            batch.Add(operation);
            inputs.Add(new ArenaRecordedInput(admission.Handle.Sequence, targetTick, payload));
            totalPayloadBytes += payloadBytes;
            return admission;
        }

        internal IReadOnlyList<ArenaQueuedOperation> TakeBatch(ulong tick)
        {
            List<ArenaQueuedOperation> batch;
            if (!pending.TryGetValue(tick, out batch)) return Array.Empty<ArenaQueuedOperation>();
            pending.Remove(tick);
            batch.Sort(new ArenaQueuedOperationComparer());
            return batch.AsReadOnly();
        }

        internal ArenaOperationResult Complete(ArenaQueuedOperation operation, ArenaOperationResult outcome, string observationBarrier)
        {
            ArenaOperationResult result = new ArenaOperationResult(operation.Handle.Sequence, operation.TargetTick, outcome.State, outcome.Code, observationBarrier);
            OperationCompletion<ArenaOperationResult> completion = new OperationCompletion<ArenaOperationResult>(result.State, result.Code, result, observationBarrier);
            bool completed = registry.TryComplete(operation.Handle, completion);
            if (!completed) throw new InvalidOperationException("Arena operation could not transition to a terminal state.");
            resultHistory.Add(result);
            return result;
        }

        internal void MarkRunning(ArenaQueuedOperation operation)
        {
            if (!registry.TryMarkRunning(operation.Handle)) throw new InvalidOperationException("Arena operation could not enter Running.");
        }

        internal OperationRead<ArenaOperationResult> Find(OperationHandle handle)
        {
            return registry.Read(handle);
        }

        internal ArenaOperationResultPage Read(int afterIndex, int maxItems)
        {
            if (afterIndex < 0 || afterIndex > resultHistory.Count) throw new ArgumentOutOfRangeException(nameof(afterIndex));
            if (maxItems < 1 || maxItems > 1024) throw new ArgumentOutOfRangeException(nameof(maxItems));
            int count = Math.Min(maxItems, resultHistory.Count - afterIndex);
            List<ArenaOperationResult> page = resultHistory.GetRange(afterIndex, count);
            return new ArenaOperationResultPage(page, afterIndex + count, afterIndex + count < resultHistory.Count);
        }

        internal void CancelPending(string code)
        {
            foreach (KeyValuePair<ulong, List<ArenaQueuedOperation>> scheduled in pending)
            {
                foreach (ArenaQueuedOperation operation in scheduled.Value)
                {
                    ArenaOperationResult outcome = new ArenaOperationResult(operation.Handle.Sequence, operation.TargetTick, OperationState.Cancelled, code, null);
                    Complete(operation, outcome, null);
                }
            }
            pending.Clear();
        }
    }
}
