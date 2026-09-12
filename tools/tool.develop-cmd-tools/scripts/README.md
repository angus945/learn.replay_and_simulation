# Develop CMD Tools

The parent directory is one portable development-tool bundle. Copy that entire folder
to another repository; none of its PowerShell scripts resolve the project from
their own installation path.

## Entry points

- `Git Operations.cmd [project-root]` operates on a Git repository and all of
  its recursive submodules. If omitted, `project-root` is the current directory.
- `Run Tests.cmd` runs the interactive commit, dependency, and suite workflow.
- `Build And Test.cmd` runs the same workflow and builds first.
- `Local CI.cmd` runs the configured full local CI and uploads its report and
  artifacts to a GitHub draft release. With no command-line arguments it opens
  a phase/upload checklist: Up/Down navigates, Space toggles, Enter runs, and Esc
  cancels. Supplying arguments bypasses the menu for automation.

For either test entry point, the current directory and its ancestors are searched
for the configured test script. This also supports double-clicking the CMD inside
a tool folder within the repository. A wrapper may instead set
`DEVELOP_CMD_PROJECT_ROOT` to an explicit project root, which is validated without
searching parent directories. The selected root is printed before the menus.
Both test entry points pause on failure so the error remains visible, then return
the original exit code after a key is pressed.

## Create an initialized worktree

Run `Create Worktree.cmd` from inside the source repository (including by
double-clicking the CMD in the tool folder). It offers the Codex worktree directory
(selected by default) or a custom destination. Use Up/Down Arrow to navigate,
Enter to confirm, or Esc to cancel. It then prompts for a branch,
creates the worktree, and runs `git submodule update --init --recursive`.
The Codex option uses `$CODEX_HOME/worktrees/<unique-id>/<repository-name>`,
falling back to `%USERPROFILE%/.codex/worktrees/<unique-id>/<repository-name>`.
This creates a Git worktree on disk; it does not create a Codex task.
Existing local branches are checked out; new branches start at `HEAD` by default.
Git rejects branches already checked out elsewhere. The destination must not exist.
Relative destinations resolve from the current directory.

```powershell
& './tools/tool.develop-cmd-tools/Create Worktree.cmd' -Path '../NarrativeEditor-feature' -Branch 'codex/feature'
& './tools/tool.develop-cmd-tools/Create Worktree.cmd' -UseCodexDirectory -Branch 'codex/feature'
# Optionally specify -ProjectRoot and -StartPoint (for a new branch).
```

`DEVELOP_CMD_PROJECT_ROOT` also selects the source repository. Committed contents
and pinned submodule revisions are used; uncommitted changes are not copied.
If submodule initialization fails, the worktree remains available for retry and
the command returns a failure code. Set `DEVELOP_CMD_NO_PAUSE=1` to skip the CMD's
pause on failure.

## Project configuration

Git operations are project-neutral. After copying, edit
`scripts/develop-cmd-tools.json` to define the project name, build and test scripts,
result directory, suite labels/arguments, and optional dependency-preparation
command. Optional dependency-pin updates scan tracked `dependencies.json` and
`dependency-lock.json` files in the project repository and all recursive Git
submodules, then align existing repository URL pins with current submodule commits.

Requirements: Git, Windows PowerShell, and the target project's own build/test
runtime (for NarrativeEditor, the pinned .NET SDK).

## Local CI and draft upload

`Local CI.cmd` additionally requires **PowerShell 7.2+ (`pwsh`)**, GitHub CLI
(`gh`) for uploads, and the configured project runtimes. Authenticate `gh` with
access to create releases and commit statuses in the project's GitHub repository.
The tested commit must be pushed before running with upload enabled. The tool
requires a clean working tree, including submodules and untracked files; it does
not commit or push source changes.

From the project root:

```powershell
& './tools/tool.develop-cmd-tools/Local CI.cmd'

# Inspect the configured commands without building, testing or uploading.
& './tools/tool.develop-cmd-tools/Local CI.cmd' -Plan

# Run the same CI and save the report locally, without GitHub access.
& './tools/tool.develop-cmd-tools/Local CI.cmd' -NoUpload

# Run a subset locally. Comma-separated phase ids also work from CMD.
& './tools/tool.develop-cmd-tools/Local CI.cmd' -SkipPhase debug-mode,release-preview
& './tools/tool.develop-cmd-tools/Local CI.cmd' -OnlyPhase release-tests

# Build/package selected phases and upload their artifacts to a partial draft.
& './tools/tool.develop-cmd-tools/Local CI.cmd' -OnlyPhase package,release-preview -UploadPartial

# Upload a completed run later, or retry an interrupted upload without rerunning CI.
& './tools/tool.develop-cmd-tools/Local CI.cmd' -UploadOnly -RunDirectory 'artifacts/local-ci/<run-id>'
```

For automation, invoke `scripts/Invoke-LocalCi.ps1` with `pwsh -NoProfile -File`
and the same arguments. It returns 0 only when the selected operation succeeds
and CI passed; failed CI or upload returns 1. The CMD wrapper pauses on failure;
set `DEVELOP_CMD_NO_PAUSE=1` to suppress that pause. `-ProjectRoot` and
`DEVELOP_CMD_PROJECT_ROOT` select an explicit repository. Otherwise, the tool
searches the current directory and its ancestors, independently of where the
bundle is installed. `-ConfigFile` selects an alternative configuration.
`-SkipPhase` excludes one or more configured phase ids, while `-OnlyPhase` runs
only the listed ids. The two options cannot be combined. Such runs are partial:
they still produce local evidence and do not set `ci/local`. Add `-UploadPartial`
to upload the available report and artifacts to a clearly labelled partial draft.
The same switch can accompany `-UploadOnly` when retrying that partial upload.

The bundled NarrativeEditor profile runs these phases **sequentially**:

1. Debug mode checks, including the Debug build.
2. Complete Release tests, with coverage and continue-on-failure reporting.
3. Windows packaging and package smoke tests, after both test phases pass.
4. Release Preview installation/update and package file parity checks.

Sequential execution avoids multiple coverage collectors modifying the same
local build outputs. The output directory is locked while the run is active.
Release Preview uses the project's existing script and therefore updates
`build/release` while verifying settings preservation.

Each run gets a unique directory under `localCi.outputRoot` (which must be
Git-ignored). It contains `SUMMARY.md`, source/configuration/toolchain metadata,
phase logs, per-phase TRX/coverage snapshots, an evidence ZIP and SHA-256 file,
and configured package artifacts. Old workspace results are excluded, including
when a later phase overwrites a previous phase's result filename. Counts include
actual skips reported by the test runner. Failures without TRX still fail the run.

The uploader uses the configured GitHub remote, pins the draft to the tested
commit, verifies every uploaded asset's size and SHA-256 digest, and then posts
`ci/local` as success or failure with a link to the draft. Drafts are named
`local-ci-<run-id>` and remain unpublished; this does not dispatch GitHub Actions
or publish a version tag. View them under the repository's **Releases**, using an
account with write access. Reports from failed CI can also be uploaded. If source
changes during CI, the report is retained locally and upload is blocked because
it cannot be attributed to the recorded commit.

`-UploadOnly` uses the completed run's `upload-manifest.json`. It verifies local
files before contacting GitHub, reuses the same draft and uploads only missing
assets. It refuses to overwrite conflicting assets or modify a published release.
An empty GitHub `starter` asset left by an interrupted transfer is removed and
retried; completed assets and an already matching commit status are reused.
If CI itself was interrupted before producing a complete report, start a new run.

### Reuse in another project

Copy the whole bundle and edit the **`localCi` section of `tool-config.json`**.
The bundled `scripts/develop-cmd-tools.json` is the template used by fresh
installations. Updates preserve existing `tool-config.json` files; when upgrading
an older installation, copy the template's `localCi` section into that file and
adapt its paths before using this command.

- `outputRoot`: repository-relative, Git-ignored run directory.
- `remote`: Git remote pointing to a `github.com` repository, normally `origin`.
- `shutdownDotnetBuildServers`: when `true`, runs `dotnet build-server shutdown`
  after acquiring the local CI lock and before starting any selected phase. This
  prevents stale MSBuild/compiler nodes from retaining project output locks.
- `versionCommands`: installed commands queried with `--version` for the report.
- `packageVersion`: a template for a short package version. The default avoids
  embedding the entire timestamp in deeply nested Windows package paths.
- `phases`: ordered objects with unique `id`, repository-relative PowerShell
  `script`, and an `arguments` array. `requiresSuccess: true` blocks that phase if
  any earlier phase failed. Independent test phases can still run after a failure.
- `artifacts`: exact file paths relative to the run directory, attached only after
  all phases pass. Every listed file must exist; wildcards are not expanded.

Arguments and artifact paths support `{projectRoot}`, `{runDirectory}`, `{runId}`,
`{configuration}`, `{shortCommit}`, `{nonce}`, and `{packageVersion}`. Configuration
is trusted executable input; arguments are passed as separate process arguments,
including paths with spaces. Scripts run with the project as their working
directory. The shared `projectName`, `testScript`, `testResultsRoot`, and
`configuration` settings also apply to local CI.

Focused offline verification for this tool:

```powershell
pwsh -NoProfile -File tools/tool.develop-cmd-tools/scripts/tests/Test-LocalCi.ps1
```

These tests use temporary Git repositories, small fixture scripts, and a mocked
GitHub boundary. They do not run NarrativeEditor's full CI or upload anything.

## Package and install

Run `Pack.cmd` to create a versioned ZIP and SHA-256 file. Run
`Install.cmd C:\path\to\project` to choose a release with an Up/Down menu and
install it into that project. Run `Update.cmd` inside an existing installation
to choose a release and overwrite that tool directory in place.

The first installation creates `tool-config.json` beside the CMD entry points. Edit that
file for the target project; later updates preserve it.
