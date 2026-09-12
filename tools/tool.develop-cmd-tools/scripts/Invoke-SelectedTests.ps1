param(
    [Parameter(Mandatory = $true)][string]$SelectionFile,
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [Parameter(Mandatory = $true)][string]$ConfigFile,
    [switch]$SkipDependencyPreparation
)

$ErrorActionPreference = 'Stop'
[string]$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
[object]$config = Get-Content -LiteralPath $ConfigFile -Raw | ConvertFrom-Json
[string]$configuration = [string]$config.configuration
[string]$testScript = Join-Path $root ([string]$config.testScript)
[string[]]$selections = @(Get-Content -LiteralPath $SelectionFile | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

if (-not $SkipDependencyPreparation -and [bool]$config.dependencyPreparation.enabled) {
    Write-Host '[Dependencies] Verifying dependency revisions before running selected tests.'
    [string]$dependencyScript = Join-Path $root ([string]$config.dependencyPreparation.script)
    [System.Collections.Generic.List[string]]$dependencyArguments = @()
    foreach ($argument in $config.dependencyPreparation.arguments) {
        [string]$value = [string]$argument
        if (-not $value.StartsWith('-')) { $value = Join-Path $root $value }
        $dependencyArguments.Add($value)
    }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $dependencyScript @dependencyArguments
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Dependency revision check failed. Choose the skip or update option and retry.'
        exit $LASTEXITCODE
    }
}

[System.Collections.Generic.List[object]]$failedSuites = @()
[int]$suiteIndex = 0
foreach ($selection in $selections) {
    $suiteIndex++
    [object]$suite = @($config.suites | Where-Object { [string]$_.id -eq $selection })[0]
    if ($null -eq $suite) { throw "Unknown selected test suite: $selection" }

    Write-Host "`n[Tests] Suite progress: $suiteIndex/$($selections.Count) - $($suite.label)"
    [System.Collections.Generic.List[string]]$arguments = @('-Configuration', $configuration)
    foreach ($argument in $config.commonTestArguments) { $arguments.Add([string]$argument) }
    foreach ($argument in $suite.arguments) { $arguments.Add([string]$argument) }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $testScript @arguments
    if ($LASTEXITCODE -ne 0) {
        $failedSuites.Add($suite)
        Write-Host "[Tests] Suite completed: $($suite.label) - FAILED" -ForegroundColor Red
    } else {
        Write-Host "[Tests] Suite completed: $($suite.label) - PASSED" -ForegroundColor Green
    }
}

Write-Host "`n[Tests] Selected suite summary"
if ($failedSuites.Count -eq 0) {
    Write-Host '[Tests] All selected suites passed.'
    exit 0
}
foreach ($suite in $failedSuites) { Write-Host "- $($suite.label) [$($suite.id)]" }
exit 1
