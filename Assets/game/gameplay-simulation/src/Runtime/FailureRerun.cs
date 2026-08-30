using System;
using System.Collections.Generic;
using Testability;

namespace GameplaySimulation
{
    public static class FailureRerun
    {
        /// <summary>Build/runtime differences warn; schema/policy/data differences fail. Does not load historical binaries.</summary>
        public static RerunReport Compare(FailureArtifact artifact, GameplaySession freshSession = null, string currentBuild = null)
        {
            List<RerunDifference> differences = new List<RerunDifference>();
            List<string> warnings = new List<string>();
            if (artifact == null || artifact.SchemaVersion != 1)
            {
                differences.Add(new RerunDifference("schema", null, "1", artifact == null ? "null artifact" : artifact.SchemaVersion.ToString()));
                return new RerunReport(false, differences, warnings);
            }
            GameplaySession session = freshSession ?? new GameplaySession();
            if (session.State != SessionState.Created || session.DriveMode != SimulationDriveMode.Manual)
            {
                differences.Add(new RerunDifference("session", null, "fresh manual session", session.State + "/" + session.DriveMode));
                return new RerunReport(false, differences, warnings);
            }
            try
            {
                if (artifact.Scenario == null) throw new ArgumentException("Missing scenario.");
                artifact.Scenario.Validate();
                if (artifact.FailureTick == 0 || artifact.FailureTick > (ulong)artifact.Scenario.MaxTicks)
                    throw new ArgumentException("Failure tick exceeds scenario bounds.");
                if (artifact.Actions.Count > artifact.Scenario.MaxActions || artifact.Results.Count > artifact.Scenario.MaxActions ||
                    artifact.Hashes.Count > (long)artifact.Scenario.MaxTicks + 1)
                    throw new ArgumentException("Artifact history exceeds scenario bounds.");
                if (currentBuild == null) warnings.Add("build.unverified: caller did not identify the executing build.");
                else if (currentBuild != artifact.Scenario.Build) warnings.Add("build.mismatch: recorded=" + artifact.Scenario.Build + "; current=" + currentBuild);
                string runtime = Environment.Version + " / " + Environment.OSVersion;
                if (runtime != artifact.Runtime) warnings.Add("runtime.mismatch: recorded=" + artifact.Runtime + "; current=" + runtime);
                ScenarioRerun.Run(artifact.Scenario, artifact.Actions, checked((int)artifact.FailureTick), session);
                if (artifact.DiagnosticPolicy == null) warnings.Add("policy.unverified: legacy artifact has no diagnostic policy identity.");
                else Add(differences, "policy", null, artifact.DiagnosticPolicy, session.DiagnosticPolicy);
                FailureArtifact actual = session.Failure;
                ulong failureTick = actual == null ? artifact.FailureTick : Math.Min(artifact.FailureTick, actual.FailureTick);
                Add(differences, "failure.code", failureTick, artifact.Code, actual?.Code);
                Add(differences, "failure.exception_type", failureTick, artifact.ExceptionType, actual?.ExceptionType);
                Add(differences, "failure.tick", failureTick, artifact.FailureTick.ToString(), actual?.FailureTick.ToString());
                Add(differences, "failure.action", failureTick, artifact.ActionSequence.ToString(), actual?.ActionSequence.ToString());
                if (artifact.FailureStage != null)
                {
                    Add(differences, "failure.stage", failureTick, artifact.FailureStage, actual?.FailureStage);
                    Add(differences, "failure.last_completed_tick", failureTick, artifact.LastCompletedTick.ToString(), actual?.LastCompletedTick.ToString());
                }
                ActionResultPage results = session.Results.Read(session.Id, 0, 1024);
                List<ActionResult> all = new List<ActionResult>(results.Items);
                while (results.HasMore)
                {
                    results = session.Results.Read(session.Id, results.NextIndex, 1024);
                    all.AddRange(results.Items);
                }
                int resultCount = Math.Max(artifact.Results.Count, all.Count);
                for (int index = 0; index < resultCount; index++)
                {
                    ActionResult expected = index < artifact.Results.Count ? artifact.Results[index] : null;
                    ActionResult observed = index < all.Count ? all[index] : null;
                    Add(differences, "action_result", Earlier(expected?.Tick, observed?.Tick), Format(expected), Format(observed));
                }
                IReadOnlyList<HashCheckpoint> hashes = session.HashHistory;
                int hashCount = Math.Max(artifact.Hashes.Count, hashes.Count);
                for (int index = 0; index < hashCount; index++)
                {
                    HashCheckpoint expected = index < artifact.Hashes.Count ? artifact.Hashes[index] : null;
                    HashCheckpoint observed = index < hashes.Count ? hashes[index] : null;
                    Add(differences, "state_hash", Earlier(expected?.Tick, observed?.Tick),
                        expected == null ? null : expected.Tick + ":" + expected.Hash,
                        observed == null ? null : observed.Tick + ":" + observed.Hash);
                }
                return new RerunReport(true, differences, warnings);
            }
            catch (Exception exception)
            {
                differences.Add(new RerunDifference("rerun.error", null, "valid reproducible artifact", exception.GetType().FullName + ": " + exception.Message));
                return new RerunReport(false, differences, warnings);
            }
        }

        private static ulong? Earlier(ulong? left, ulong? right) => !left.HasValue ? right : !right.HasValue ? left : Math.Min(left.Value, right.Value);
        private static string Format(ActionResult result) => result == null ? null : result.Tick + ":" + result.Sequence + ":" + result.Status + ":" + result.Code;
        private static void Add(List<RerunDifference> differences, string category, ulong? tick, string expected, string actual)
        {
            if (expected != actual) differences.Add(new RerunDifference(category, tick, expected, actual));
        }
    }
}
