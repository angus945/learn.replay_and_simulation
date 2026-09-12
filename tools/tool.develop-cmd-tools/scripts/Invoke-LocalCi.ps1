#Requires -Version 7.2
[CmdletBinding(DefaultParameterSetName = 'Run')]
param(
    [string]$ProjectRoot,
    [Parameter(ParameterSetName = 'Run')][string]$ConfigFile,
    [Parameter(ParameterSetName = 'Run')][switch]$NoUpload,
    [Parameter(ParameterSetName = 'Run')][switch]$Plan,
    [Parameter(ParameterSetName = 'Run')][string[]]$SkipPhase = @(),
    [Parameter(ParameterSetName = 'Run')][string[]]$OnlyPhase = @(),
    [Parameter(ParameterSetName = 'Run')][Parameter(ParameterSetName = 'Upload')][switch]$UploadPartial,
    [Parameter(Mandatory, ParameterSetName = 'Upload')][switch]$UploadOnly,
    [Parameter(Mandatory, ParameterSetName = 'Upload')][string]$RunDirectory
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'LocalCi.Common.ps1')
. (Join-Path $PSScriptRoot 'LocalCi.Report.ps1')
. (Join-Path $PSScriptRoot 'LocalCi.Upload.ps1')

try {
    if ($UploadOnly) {
        $RunDirectory = (Resolve-Path -LiteralPath $RunDirectory).Path
        Publish-CiDraft $RunDirectory $UploadPartial
        [hashtable]$uploadedRun = Read-CiJson (Join-Path $RunDirectory 'upload-manifest.json')
        if ($uploadedRun.status -ne 'passed') { exit 1 }
        exit 0
    }
    if (-not $ProjectRoot -and $env:DEVELOP_CMD_PROJECT_ROOT) { $ProjectRoot = $env:DEVELOP_CMD_PROJECT_ROOT }
    [bool]$explicitRoot = -not [string]::IsNullOrWhiteSpace($ProjectRoot)
    if (-not $ProjectRoot) { $ProjectRoot = (Get-Location).Path }
    if (-not $ConfigFile) {
        $ConfigFile = Join-Path (Split-Path -Parent $PSScriptRoot) 'tool-config.json'
        if (-not (Test-Path -LiteralPath $ConfigFile -PathType Leaf)) { $ConfigFile = Join-Path $PSScriptRoot 'develop-cmd-tools.json' }
    }
    $ConfigFile = (Resolve-Path -LiteralPath $ConfigFile).Path
    [hashtable]$execution = Get-CiPlan $ProjectRoot $ConfigFile $explicitRoot
    [string]$root = $execution.root
    $RunDirectory = $execution.variables.runDirectory
    [string[]]$configuredPhaseIds = @($execution.phases | ForEach-Object { [string]$_.id })
    [string[]]$requestedSkipIds = @($SkipPhase | ForEach-Object { $_ -split ',' } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.Trim() })
    [string[]]$requestedOnlyIds = @($OnlyPhase | ForEach-Object { $_ -split ',' } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.Trim() })
    if ($requestedSkipIds.Count -gt 0 -and $requestedOnlyIds.Count -gt 0) { throw '-SkipPhase and -OnlyPhase cannot be used together.' }
    [string[]]$requestedIds = @($requestedSkipIds + $requestedOnlyIds | Select-Object -Unique)
    foreach ($requestedId in $requestedIds) {
        if ($requestedId -notin $configuredPhaseIds) { throw "Unknown CI phase '$requestedId'. Available phases: $($configuredPhaseIds -join ', ')." }
    }
    [bool]$partialRun = $requestedIds.Count -gt 0
    foreach ($phase in $execution.phases) {
        [bool]$selected = if ($requestedOnlyIds.Count -gt 0) { $phase.id -in $requestedOnlyIds } else { $phase.id -notin $requestedSkipIds }
        if (-not $selected) { $phase.status = 'skipped' }
    }
    Write-Host "[local-ci] Project: $root"
    Write-Host "[local-ci] Output:  $RunDirectory"
    if ($partialRun -and $UploadPartial) { Write-Host '[local-ci] Partial run: draft upload is enabled; ci/local status remains disabled.' }
    if ($partialRun -and -not $UploadPartial) { Write-Host '[local-ci] Partial run: upload and ci/local status are disabled.' }
    if ($Plan) {
        foreach ($phase in $execution.phases) {
            [string]$planStatus = if ($phase.status -eq 'skipped') { ' [skipped]' } else { '' }
            Write-Host "$($phase.id)$($planStatus): $($phase.script) $($phase.arguments -join ' ')"
        }
        Write-Host "Upload draft: $(-not $NoUpload -and (-not $partialRun -or $UploadPartial))"
        exit 0
    }
    if ($execution.source.changes.Count -gt 0) { throw 'Local CI requires a clean Git working tree, including submodules and untracked files. Commit your intended changes first.' }
    [string]$ignoreProbe = Join-Path $execution.outputRoot '.local-ci-output-probe'
    try { $null = Invoke-CiGit $root @('check-ignore', '--quiet', $ignoreProbe) } catch { throw "The configured outputRoot must be Git-ignored: $($execution.outputRoot)" }
    [string]$repository = Get-CiRepository $root ([string]$execution.config.localCi.remote)
    if (-not $NoUpload -and (-not $partialRun -or $UploadPartial)) {
        [string]$remoteCommit = Invoke-CiGh @('api', "repos/$repository/commits/$($execution.source.commit)", '--jq', '.sha')
        if ($remoteCommit.Trim() -cne $execution.source.commit) { throw 'Push the tested commit before running CI with upload enabled.' }
    }
    [hashtable]$versions = @{ powershell = $PSVersionTable.PSVersion.ToString(); os = [Environment]::OSVersion.VersionString }
    foreach ($command in $execution.config.localCi.versionCommands) {
        $null = Get-Command $command -ErrorAction Stop
        [string[]]$version = @(& $command --version 2>&1)
        if ($LASTEXITCODE -ne 0) { throw "Could not read $command version." }
        $versions[$command] = $version -join [Environment]::NewLine
    }
    New-Item -ItemType Directory -Path $execution.outputRoot -Force | Out-Null
    [string]$lockPath = Join-Path $execution.outputRoot '.local-ci.lock'
    [IO.FileStream]$lock = $null
    try { $lock = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None) } catch { throw 'Another local CI run holds this output directory. Wait for it to finish.' }
    try {
        [bool]$shutdownDotnetBuildServers = $execution.config.localCi.ContainsKey('shutdownDotnetBuildServers') -and [bool]$execution.config.localCi.shutdownDotnetBuildServers
        if ($shutdownDotnetBuildServers) {
            Write-Host '[local-ci] Shutting down existing .NET build servers.'
            & dotnet build-server shutdown
            if ($LASTEXITCODE -ne 0) { throw "dotnet build-server shutdown failed with exit code $LASTEXITCODE." }
        }
        [hashtable]$initial = Get-CiSource $root
        if ($initial.commit -ne $execution.source.commit -or $initial.changes.Count -gt 0) { throw 'Source changed during preflight. Run CI again on a clean commit.' }
        [string]$evidence = Join-Path $RunDirectory 'evidence'
        [string]$logs = Join-Path $evidence 'logs'
        [string]$toolEvidence = Join-Path $evidence 'tool'
        New-Item -ItemType Directory -Path $logs, $toolEvidence | Out-Null
        Copy-Item -LiteralPath $ConfigFile -Destination (Join-Path $evidence 'config.json')
        foreach ($file in @('Invoke-LocalCi.ps1', 'LocalCi.Common.ps1', 'LocalCi.Report.ps1', 'LocalCi.Upload.ps1', 'version.json')) {
            Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $toolEvidence
        }
        [hashtable]$run = @{ schemaVersion = 1; id = $execution.variables.runId; projectName = $execution.config.projectName; projectRoot = $root; repository = $repository; commit = $initial.commit; source = $initial; toolchain = $versions; packageVersion = $execution.variables.packageVersion; startedUtc = [datetime]::UtcNow.ToString('o'); phases = $execution.phases; status = 'running'; artifacts = @(); sourceUnchanged = $false; complete = -not $partialRun }
        Write-CiJson $run (Join-Path $RunDirectory 'run.json')
        [string]$resultsRoot = Resolve-CiPath $root ([string]$execution.config.testResultsRoot)
        try {
            [bool]$allPassed = $true
            foreach ($phase in $run.phases) {
                if ($phase.status -eq 'skipped') { Write-Host "[local-ci] SKIPPED $($phase.id)"; continue }
                if ($phase.requiresSuccess -and -not $allPassed) { $phase.status = 'blocked'; continue }
                [hashtable]$before = Get-CiResultSnapshot $resultsRoot
                $phase.startedUtc = [datetime]::UtcNow.ToString('o')
                $phase.status = 'running'
                Write-CiJson $run (Join-Path $RunDirectory 'run.json')
                Write-Host "[local-ci] START $($phase.id)"
                try {
                    [string]$logPath = Resolve-CiPath $evidence $phase.log
                    $phase.exitCode = Invoke-CiPhase $root $phase $logPath
                    $phase.status = if ($phase.exitCode -eq 0) { 'passed' } else { 'failed' }
                } catch {
                    $phase.status = 'failed'
                    $phase.error = $_.Exception.Message
                    $run.error = "Phase $($phase.id): $($phase.error)"
                } finally {
                    $phase.finishedUtc = [datetime]::UtcNow.ToString('o')
                    [string]$phaseResults = Join-Path $evidence ('test-results/' + $phase.id)
                    Copy-CiResults $resultsRoot $phaseResults $before
                    Write-CiJson $run (Join-Path $RunDirectory 'run.json')
                }
                if ($phase.status -ne 'passed') { $allPassed = $false }
                Write-Host "[local-ci] $($phase.status.ToUpperInvariant()) $($phase.id)"
            }
            $run.status = if ($allPassed) { 'passed' } else { 'failed' }
            if ($allPassed) {
                foreach ($artifact in $execution.config.localCi.artifacts) {
                    [string]$relative = Expand-CiValue $artifact $execution.variables
                    [string]$path = Resolve-CiPath $RunDirectory $relative
                    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
                        if ($run.complete) { throw "Missing CI artifact: $path" }
                        continue
                    }
                    $run.artifacts += $path
                }
            }
        } catch { $run.status = 'failed'; $run.error = $_.Exception.Message }
        [hashtable]$finalSource = Get-CiSource $root
        $run.sourceUnchanged = $finalSource.commit -eq $run.commit -and $finalSource.changes.Count -eq 0
        $run.finalSource = $finalSource
        if (-not $run.sourceUnchanged) { $run.status = 'failed'; $run.error = 'Source changed during CI; upload is disabled for this run.' }
        $run.finishedUtc = [datetime]::UtcNow.ToString('o')
        Write-CiReport $RunDirectory $run
        if (-not $NoUpload -and (-not $partialRun -or $UploadPartial)) { Publish-CiDraft $RunDirectory $UploadPartial }
        if ($run.status -ne 'passed') { exit 1 }
    } finally { if ($lock) { $lock.Dispose() } }
} catch {
    Write-Host "[local-ci] ERROR: $($_.Exception.Message)" -ForegroundColor Red
    if ($RunDirectory -and (Test-Path -LiteralPath (Join-Path $RunDirectory 'upload-manifest.json'))) {
        Write-Host 'The report is preserved. Retry only the upload with:'
        Write-Host ('pwsh -NoProfile -File "' + $PSCommandPath + '" -UploadOnly -RunDirectory "' + $RunDirectory + '"')
    }
    exit 1
}
