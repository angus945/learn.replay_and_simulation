param(
    [string]$ProjectRoot = (Get-Location).Path,
    [switch]$BuildFirst
)

$ErrorActionPreference = 'Stop'
[string]$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
[string]$toolRoot = $PSScriptRoot
[string]$localConfigFile = Join-Path (Split-Path -Parent $toolRoot) 'tool-config.json'
[string]$configFile = if (Test-Path -LiteralPath $localConfigFile -PathType Leaf) {
    $localConfigFile
} else {
    Join-Path $toolRoot 'develop-cmd-tools.json'
}
[object]$config = Get-Content -LiteralPath $configFile -Raw | ConvertFrom-Json

if (-not $PSBoundParameters.ContainsKey('ProjectRoot')) {
    while (-not (Test-Path -LiteralPath (Join-Path $root ([string]$config.testScript)) -PathType Leaf)) {
        [string]$parent = Split-Path -Parent $root
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $root) {
            throw 'Could not locate the project from the current directory. Set DEVELOP_CMD_PROJECT_ROOT to the target repository.'
        }
        $root = $parent
    }
}
if (-not (Test-Path -LiteralPath (Join-Path $root ([string]$config.testScript)) -PathType Leaf)) {
    throw "The configured test script '$($config.testScript)' does not exist in project root '$root'."
}
Write-Host "[Tests] Project root: $root"

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $toolRoot 'Select-CommitMode.ps1')
[int]$exitCode = $LASTEXITCODE
if ($exitCode -eq 0) {
    Write-Host 'Enter one commit subject to commit all Git changes before testing.'
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $toolRoot 'Commit-AllChanges.ps1') `
        -ProjectRoot $root
    $exitCode = $LASTEXITCODE
}
if ($exitCode -ne 0 -and $exitCode -ne 10) { exit $exitCode }

[bool]$skipDependencyPreparation = -not [bool]$config.dependencyPreparation.enabled
if (-not $skipDependencyPreparation) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $toolRoot 'Select-DependencyPreparation.ps1')
    $exitCode = $LASTEXITCODE
    if ($exitCode -eq 10) {
        $skipDependencyPreparation = $true
        $exitCode = 0
    } elseif ($exitCode -eq 11) {
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File `
            (Join-Path $toolRoot 'Update-SubmoduleDependencyPins.ps1') -ProjectRoot $root
        $exitCode = $LASTEXITCODE
    } elseif ($exitCode -ne 0) {
        exit $exitCode
    }
    if ($exitCode -ne 0) { exit $exitCode }
}

if ($BuildFirst) {
    Write-Host 'Building Release configuration before testing.'
    [System.Collections.Generic.List[string]]$buildArguments = @('-Configuration', [string]$config.configuration)
    if ([bool]$config.dependencyPreparation.enabled -and $skipDependencyPreparation) {
        $buildArguments.Add('-SkipDependencyPreparation')
    }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root ([string]$config.buildScript)) @buildArguments
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

[string]$selectionFile = Join-Path ([IO.Path]::GetTempPath()) ("TestSelection-{0}.txt" -f [guid]::NewGuid().ToString('N'))
[string]$markerFile = Join-Path ([IO.Path]::GetTempPath()) ("TestMarker-{0}.marker" -f [guid]::NewGuid().ToString('N'))
try {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $toolRoot 'Select-TestSuites.ps1') `
        -OutputFile $selectionFile -ConfigFile $configFile
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    [IO.File]::WriteAllText($markerFile, '')
    [System.Collections.Generic.List[string]]$testArguments = @(
        '-SelectionFile', $selectionFile,
        '-ProjectRoot', $root)
    $testArguments.Add('-ConfigFile')
    $testArguments.Add($configFile)
    if ($skipDependencyPreparation) { $testArguments.Add('-SkipDependencyPreparation') }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File `
        (Join-Path $toolRoot 'Invoke-SelectedTests.ps1') @testArguments
    $exitCode = $LASTEXITCODE

    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $toolRoot 'Write-TestSummary.ps1') `
        -MarkerFile $markerFile -ProjectRoot $root -ConfigFile $configFile
    exit $exitCode
} finally {
    Remove-Item -LiteralPath $selectionFile, $markerFile -Force -ErrorAction SilentlyContinue
}
