using System;
using System.Collections.Generic;
using InvariantChecks;
using RuntimeControl;
using TestabilityOracles;

namespace Arena.Integration
{
    public sealed class ArenaInvariantOracle : ITestOracle<ArenaObservation>
    {
        private readonly IInvariant<ArenaObservation> invariant;

        public ArenaInvariantOracle(IInvariant<ArenaObservation> invariant)
        {
            this.invariant = invariant ?? throw new ArgumentNullException(nameof(invariant));
        }

        public string Id
        {
            get { return invariant.Code; }
        }

        public OracleResult Evaluate(ArenaObservation context)
        {
            InvariantViolation violation = invariant.Evaluate(context);
            if (violation == null) return new OracleResult(TestVerdict.Passed, invariant.Code + ".passed");
            return new OracleResult(TestVerdict.Failed, violation.Code, violation.Detail);
        }
    }

    public sealed class ArenaDeterminismContext
    {
        public ArenaDeterminismContext(ArenaTickEvidence expected, ArenaTickEvidence actual)
        {
            Expected = expected ?? throw new ArgumentNullException(nameof(expected));
            Actual = actual ?? throw new ArgumentNullException(nameof(actual));
        }

        public ArenaTickEvidence Expected { get; }
        public ArenaTickEvidence Actual { get; }
    }

    public sealed class ArenaDeterminismOracle : ITestOracle<ArenaDeterminismContext>
    {
        public string Id
        {
            get { return "arena.determinism"; }
        }

        public OracleResult Evaluate(ArenaDeterminismContext context)
        {
            string difference = Compare(context.Expected, context.Actual);
            if (difference == null) return new OracleResult(TestVerdict.Passed, "arena.determinism.passed");
            return new OracleResult(TestVerdict.Failed, "arena.determinism.diverged", difference);
        }

        public static string Compare(ArenaTickEvidence expected, ArenaTickEvidence actual)
        {
            if (expected.Tick != actual.Tick) return "tick";
            if (!string.Equals(expected.Digest, actual.Digest, StringComparison.Ordinal)) return "state_digest";
            if (expected.Results.Count != actual.Results.Count) return "result_count";
            for (int index = 0; index < expected.Results.Count; index++)
            {
                ArenaOperationResult left = expected.Results[index];
                ArenaOperationResult right = actual.Results[index];
                bool same = left.Sequence == right.Sequence && left.Tick == right.Tick && left.State == right.State && string.Equals(left.Code, right.Code, StringComparison.Ordinal);
                if (!same) return "operation_result";
            }
            string expectedFailure = expected.Failure == null ? null : expected.Failure.Fingerprint;
            string actualFailure = actual.Failure == null ? null : actual.Failure.Fingerprint;
            if (!string.Equals(expectedFailure, actualFailure, StringComparison.Ordinal)) return "failure";
            return null;
        }
    }
}
