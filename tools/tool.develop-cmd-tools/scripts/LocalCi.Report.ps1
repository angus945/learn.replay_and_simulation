function New-CiAsset([string]$RunDirectory, [string]$Path) {
    [IO.FileInfo]$file = Get-Item -LiteralPath $Path -ErrorAction Stop
    [string]$relative = [IO.Path]::GetRelativePath($RunDirectory, $file.FullName)
    $null = Resolve-CiPath $RunDirectory $relative
    [string]$hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    return @{ path = $relative; name = $file.Name; size = $file.Length; sha256 = $hash }
}

function Write-CiReport([string]$RunDirectory, [hashtable]$Run) {
    [string]$evidence = Join-Path $RunDirectory 'evidence'
    [System.Collections.Generic.List[object]]$suites = [System.Collections.Generic.List[object]]::new()
    [hashtable]$totals = @{ passed = 0; failed = 0; skipped = 0; total = 0; backendPassed = 0 }
    [string]$pattern = '(?:Passed!|Failed!)\s*-\s*Failed:\s*(\d+),\s*Passed:\s*(\d+),\s*Skipped:\s*(\d+),\s*Total:\s*(\d+),\s*Duration:\s*(.*?)\s+-\s*(.+)'
    foreach ($phase in $Run.phases) {
        [string]$logPath = Resolve-CiPath $evidence $phase.log
        if (-not (Test-Path -LiteralPath $logPath -PathType Leaf)) { continue }
        [string]$log = Get-Content -LiteralPath $logPath -Raw
        foreach ($match in [regex]::Matches($log, $pattern)) {
            [hashtable]$suite = @{ phase = $phase.id; name = $match.Groups[6].Value.Trim(); failed = [int]$match.Groups[1].Value; passed = [int]$match.Groups[2].Value; skipped = [int]$match.Groups[3].Value; total = [int]$match.Groups[4].Value }
            $suites.Add($suite)
            foreach ($key in @('passed', 'failed', 'skipped', 'total')) { $totals[$key] += $suite[$key] }
        }
        $totals.backendPassed += [regex]::Matches($log, '(?m)^ok - ').Count
    }
    if ($totals.failed -gt 0) { $Run.status = 'failed' }
    $Run.totals = $totals
    Write-CiJson $Run (Join-Path $RunDirectory 'run.json')
    Write-CiJson $Run (Join-Path $evidence 'run.json')
    Write-CiJson $suites.ToArray() (Join-Path $evidence 'test-summaries.json')
    [System.Collections.Generic.List[string]]$lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("# $($Run.projectName) local CI")
    $lines.Add('')
    $lines.Add("Status: **$($Run.status.ToUpperInvariant())**")
    $lines.Add('')
    $lines.Add('Commit: `' + $Run.commit + '`; run: `' + $Run.id + '`.')
    $lines.Add('')
    if ($Run.complete) {
        $lines.Add('Executed locally with the configured CI scripts. This report is separate from GitHub Actions and uses the `ci/local` commit status. Uploaded releases remain drafts.')
    } else {
        $lines.Add('This is a partial local run. Skipped phases are listed below; upload and the `ci/local` commit status are disabled.')
    }
    $lines.Add('')
    $lines.Add("Started: $($Run.startedUtc); finished: $($Run.finishedUtc) (UTC).")
    $lines.Add('')
    $lines.Add('| Phase | Status | Exit code |')
    $lines.Add('| --- | --- | ---: |')
    foreach ($phase in $Run.phases) { $lines.Add("| $($phase.id) | $($phase.status) | $($phase.exitCode) |") }
    $lines.Add('')
    $lines.Add("Managed test executions: **$($totals.passed) passed, $($totals.failed) failed, $($totals.skipped) skipped, $($totals.total) total**.")
    $lines.Add('')
    $lines.Add("Backend checks reported as 'ok -': **$($totals.backendPassed) passed** (separate from the managed total).")
    $lines.Add('')
    $lines.Add('Counts use the final test-runner summaries, including actual skips. Debug and Release are separate executions. Phase exit codes determine success even if a failing process produces no test results. Consult the logs for benchmark skip reasons, warnings, and other script checks.')
    $lines.Add('')
    $lines.Add('| Phase | Suite | Passed | Failed | Skipped | Total |')
    $lines.Add('| --- | --- | ---: | ---: | ---: | ---: |')
    foreach ($suite in $suites) { $lines.Add("| $($suite.phase) | $($suite.name) | $($suite.passed) | $($suite.failed) | $($suite.skipped) | $($suite.total) |") }
    if ($Run.ContainsKey('error') -and -not [string]::IsNullOrWhiteSpace([string]$Run.error)) { $lines.Add(''); $lines.Add('Run error: ' + $Run.error) }
    $lines.Add('')
    $lines.Add('Evidence contains the selected configuration, tool scripts, source/toolchain metadata, logs and per-phase TRX/coverage files created or updated during this run. Earlier workspace results are excluded. Package artifacts and SHA-256 checksums are separate assets.')
    [string]$summary = Join-Path $RunDirectory 'SUMMARY.md'
    $lines | Set-Content -LiteralPath $summary -Encoding utf8
    Copy-Item -LiteralPath $summary -Destination $evidence
    [string]$archive = Join-Path $RunDirectory 'local-ci-evidence.zip'
    [IO.Compression.ZipFile]::CreateFromDirectory($evidence, $archive)
    [hashtable]$archiveAsset = New-CiAsset $RunDirectory $archive
    [string]$checksumPath = $archive + '.sha256'
    Set-Content -LiteralPath $checksumPath -Value ($archiveAsset.sha256 + '  ' + $archiveAsset.name) -Encoding ascii
    [System.Collections.Generic.List[object]]$assets = [System.Collections.Generic.List[object]]::new()
    $assets.Add((New-CiAsset $RunDirectory $summary))
    $assets.Add($archiveAsset)
    $assets.Add((New-CiAsset $RunDirectory $checksumPath))
    foreach ($path in $Run.artifacts) { $assets.Add((New-CiAsset $RunDirectory $path)) }
    [hashtable]$manifest = @{ schemaVersion = 1; id = $Run.id; repository = $Run.repository; commit = $Run.commit; status = $Run.status; sourceUnchanged = $Run.sourceUnchanged; complete = $Run.complete; tag = 'local-ci-' + $Run.id; assets = $assets.ToArray() }
    Write-CiJson $manifest (Join-Path $RunDirectory 'upload-manifest.json')
    Write-Host "[local-ci] $($Run.status.ToUpperInvariant()): $summary"
}
