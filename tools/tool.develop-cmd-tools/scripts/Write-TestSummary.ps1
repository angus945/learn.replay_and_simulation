param(
    [Parameter(Mandatory = $true)]
    [string]$MarkerFile,
    [Parameter(Mandatory = $true)]
    [string]$ProjectRoot,
    [Parameter(Mandatory = $true)]
    [string]$ConfigFile
)

$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
[object]$config = Get-Content -LiteralPath $ConfigFile -Raw | ConvertFrom-Json
$testResultsRoot = Join-Path $root ([string]$config.testResultsRoot)
if (-not (Test-Path -LiteralPath $testResultsRoot -PathType Container)) {
    Write-Host '[Tests] No test results were produced; no summary is available.'
    return
}
$startedAt = (Get-Item -LiteralPath $MarkerFile).LastWriteTimeUtc
$trxFiles = @(Get-ChildItem -LiteralPath $testResultsRoot -Filter '*.trx' -File -Recurse |
    Where-Object { $_.LastWriteTimeUtc -ge $startedAt })
if ($trxFiles.Count -eq 0) {
    Write-Host '[Tests] No new test results were produced; no summary is available.'
    return
}

[int]$total = 0
[int]$passed = 0
[int]$failed = 0
[int]$skipped = 0
[System.Collections.Generic.List[string]]$suiteSummaries = [System.Collections.Generic.List[string]]::new()
foreach ($trxFile in $trxFiles) {
    [xml]$trx = Get-Content -LiteralPath $trxFile.FullName -Raw
    $counters = $trx.SelectSingleNode(
        "/*[local-name()='TestRun']/*[local-name()='ResultSummary']/*[local-name()='Counters']")
    if ($null -eq $counters) {
        continue
    }

    [int]$suiteTotal = [int]$counters.GetAttribute('total')
    [int]$suitePassed = [int]$counters.GetAttribute('passed')
    [int]$suiteFailed = [int]$counters.GetAttribute('failed')
    [int]$suiteSkipped = [int]$counters.GetAttribute('notExecuted')
    $total += $suiteTotal
    $passed += $suitePassed
    $failed += $suiteFailed
    $skipped += $suiteSkipped
    $suiteName = Split-Path -Leaf (Split-Path -Parent $trxFile.DirectoryName)
    $suiteSummaries.Add("- ${suiteName}: $suitePassed passed, $suiteFailed failed, $suiteSkipped skipped ($suiteTotal total)")
}

[string]$status = if ($failed -eq 0) { 'PASSED' } else { 'FAILED' }
[string[]]$lines = @(
    "$($config.projectName) test summary",
    "Status: $status",
    "Passed: $passed | Failed: $failed | Skipped: $skipped | Total: $total",
    '',
    'Suites:'
) + $suiteSummaries
[string]$summary = $lines -join [Environment]::NewLine

$summaryPath = Join-Path $testResultsRoot 'latest-summary.txt'
Set-Content -LiteralPath $summaryPath -Value $summary -Encoding utf8
Write-Host $summary
Write-Host "`nSaved: $summaryPath"

try {
    Set-Clipboard -Value $summary
    Write-Host 'The summary has been copied to the clipboard.'
} catch {
    Write-Warning 'The summary could not be copied to the clipboard.'
}
