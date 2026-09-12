#Requires -Version 7.2
param()
$ErrorActionPreference = 'Stop'
[string]$scripts = Split-Path -Parent $PSScriptRoot
. (Join-Path $scripts 'LocalCi.Common.ps1')
. (Join-Path $scripts 'LocalCi.Report.ps1')
. (Join-Path $scripts 'LocalCi.Upload.ps1')
[string]$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('local-ci-tests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
[int]$script:assertions = 0

function Assert-Equal([object]$Expected, [object]$Actual, [string]$Message) {
    if ($Expected -cne $Actual) { throw "$Message -- expected '$Expected', got '$Actual'." }
    $script:assertions++
}

function Assert-Fails([scriptblock]$Action, [string]$Message) {
    [bool]$failed = $false
    try { & $Action } catch { $failed = $true }
    Assert-Equal $true $failed $Message
}

function New-Fixture([string]$Name, [string]$TestMode) {
    [string]$root = Join-Path $testRoot $Name
    New-Item -ItemType Directory -Path (Join-Path $root 'ci'), (Join-Path $root 'artifacts/results') -Force | Out-Null
    [string]$phaseScript = @'
param([string]$Mode, [string]$OutputRoot, [string]$Message)
$ErrorActionPreference = 'Stop'
if ($Message -cne 'space & $() literal') { throw 'Argument quoting was not preserved.' }
[Console]::Error.WriteLine('fixture stderr')
if ($Mode -eq 'fail') { [Console]::Error.WriteLine('fixture test host failure'); exit 7 }
if ($Mode -eq 'dirty') { Add-Content -LiteralPath 'tracked.txt' -Value 'source changed' }
if ($Mode -eq 'package') {
    [string]$archive = Join-Path $OutputRoot 'bundle.zip'
    Set-Content -LiteralPath $archive -Value 'fixture package'
    [string]$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath ($archive + '.sha256') -Value ($hash + '  bundle.zip')
    exit 0
}
if ($Mode -eq 'preview') { Write-Host 'fixture package parity passed'; exit 0 }
Set-Content -LiteralPath 'artifacts/results/sample.trx' -Value ('<TestRun phase="' + $Mode + '" />')
Set-Content -LiteralPath 'artifacts/results/coverage.cobertura.xml' -Value ('<coverage phase="' + $Mode + '" />')
if ($Mode -eq 'debug') { Write-Host 'Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1, Duration: 1 ms - Fixture.dll (net10.0)' }
else { Write-Host 'Passed! - Failed: 0, Passed: 2, Skipped: 1, Total: 3, Duration: 1 ms - Fixture.dll (net10.0)'; Write-Host 'ok - backend fixture' }
'@
    Set-Content -LiteralPath (Join-Path $root 'ci/test.ps1') -Value $phaseScript
    Set-Content -LiteralPath (Join-Path $root '.gitignore') -Value 'artifacts/'
    Set-Content -LiteralPath (Join-Path $root 'tracked.txt') -Value 'original'
    Set-Content -LiteralPath (Join-Path $root 'artifacts/results/old.trx') -Value '<OldResult />'
    [object[]]$phases = @(foreach ($mode in @('debug', $TestMode, 'package', 'preview')) {
        @{ id = $mode; script = 'ci/test.ps1'; arguments = @('-Mode', $mode, '-OutputRoot', '{runDirectory}', '-Message', 'space & $() literal'); requiresSuccess = $mode -in @('package', 'preview') }
    })
    [hashtable]$config = @{ projectName = 'Fixture'; configuration = 'Release'; testScript = 'ci/test.ps1'; testResultsRoot = 'artifacts/results'; localCi = @{ outputRoot = 'artifacts/local-ci'; remote = 'origin'; packageVersion = 'ci-{shortCommit}-{nonce}'; versionCommands = @('git'); phases = $phases; artifacts = @('bundle.zip', 'bundle.zip.sha256') } }
    Write-CiJson $config (Join-Path $root 'tool-config.json')
    $null = Invoke-CiGit $root @('init', '--quiet')
    $null = Invoke-CiGit $root @('remote', 'add', 'origin', 'https://github.com/example/fixture.git')
    $null = Invoke-CiGit $root @('add', '.')
    $null = Invoke-CiGit $root @('-c', 'user.name=Local CI Tests', '-c', 'user.email=local-ci@example.invalid', '-c', 'commit.gpgSign=false', 'commit', '--quiet', '-m', 'Fixture')
    return $root
}

function Invoke-Fixture([string]$Root, [int]$ExpectedExit, [string[]]$Arguments = @()) {
    [string]$log = Join-Path $Root 'artifacts/runner.log'
    & pwsh -NoProfile -File (Join-Path $scripts 'Invoke-LocalCi.ps1') -ProjectRoot $Root -ConfigFile (Join-Path $Root 'tool-config.json') -NoUpload @Arguments *> $log
    if ($LASTEXITCODE -ne $ExpectedExit) { Get-Content -LiteralPath $log | Write-Host }
    Assert-Equal $ExpectedExit $LASTEXITCODE 'Runner exit code'
    [IO.DirectoryInfo[]]$runs = @(Get-ChildItem -LiteralPath (Join-Path $Root 'artifacts/local-ci') -Directory)
    Assert-Equal 1 $runs.Count 'One isolated run directory'
    return $runs[0].FullName
}

# This mock implements the GitHub boundary only; uploads, files, hashing,
# process execution and report generation still use the real tool code.
function Invoke-CiGh([string[]]$Arguments) {
    [string]$verb = $Arguments[0]
    [string]$endpoint = $Arguments[1]
    if ($verb -eq 'release' -and $endpoint -eq 'upload') {
        $script:uploadCalls++
        $script:lastUploadCount = $Arguments.Length - 5
        foreach ($path in $Arguments[5..($Arguments.Length - 1)]) {
            [hashtable]$asset = New-CiAsset $script:uploadDirectory $path
            $script:draft.assets += @{ name = $asset.name; size = $asset.size; digest = 'sha256:' + $asset.sha256; state = 'uploaded' }
            if ($script:interruptUpload) { $script:interruptUpload = $false; throw 'Simulated network interruption after the first asset' }
        }
        return ''
    }
    if ($Arguments -contains '--jq') { return $script:uploadManifest.commit }
    if ($endpoint.EndsWith('/releases?per_page=100')) {
        [object[]]$page = if ($script:draft) { @($script:draft) } else { @() }
        [object[]]$pages = @(@(@{ tag_name = 'unrelated-release' }), $page)
        return ConvertTo-Json -InputObject $pages -Depth 15 -Compress
    }
    if ($endpoint.EndsWith('/releases') -and $Arguments -contains 'POST') {
        [int]$inputIndex = [Array]::IndexOf($Arguments, '--input') + 1
        $script:draft = Read-CiJson $Arguments[$inputIndex]
        Assert-Equal $true $script:draft.draft 'Release is created as draft'
        Assert-Equal 'false' $script:draft.make_latest 'Release is not marked latest'
        $script:draft.body = $script:draft.body.Replace("`r`n", "`n")
        $script:draft.id = 123
        $script:draft.html_url = 'https://github.com/example/fixture/releases/tag/untagged-fixture'
        $script:draft.assets = @()
        $script:createCalls++
        return ConvertTo-Json -InputObject $script:draft -Depth 15
    }
    if ($endpoint.EndsWith('/releases/123')) { return ConvertTo-Json -InputObject $script:draft -Depth 15 }
    if ($endpoint.EndsWith('/releases/assets/456') -and $Arguments -contains 'DELETE') {
        $script:draft.assets = @($script:draft.assets | Where-Object { $_.id -ne 456 })
        $script:deletedIncomplete++
        return ''
    }
    if ($endpoint.EndsWith('/statuses?per_page=100')) { return ConvertTo-Json -InputObject @($script:commitStatus) -Depth 10 }
    if ($endpoint -match '/statuses/[0-9a-f]{40}$' -and $Arguments -contains 'POST') {
        [int]$inputIndex = [Array]::IndexOf($Arguments, '--input') + 1
        $script:commitStatus = Read-CiJson $Arguments[$inputIndex]
        $script:statusCalls++
        return ConvertTo-Json -InputObject $script:commitStatus
    }
    throw "Unexpected gh invocation: $($Arguments -join ' ')"
}

try {
    [string]$passingRoot = New-Fixture 'project with spaces 測試' 'test'
    [string]$passingRun = Invoke-Fixture $passingRoot 0
    [hashtable]$run = Read-CiJson (Join-Path $passingRun 'run.json')
    Assert-Equal 'passed' $run.status 'Passing run'
    Assert-Equal 3 $run.totals.passed 'Summed counts include both configurations'
    Assert-Equal 1 $run.totals.skipped 'Actual runner skip count'
    Assert-Equal 4 $run.totals.total 'Total count'
    Assert-Equal 1 $run.totals.backendPassed 'Backend count'
    Assert-Equal $true $run.sourceUnchanged 'Source identity retained'
    [string]$debugLog = Get-Content -LiteralPath (Join-Path $passingRun 'evidence/logs/debug.log') -Raw
    Assert-Equal $true $debugLog.Contains('fixture stderr') 'Stderr captured without deadlock'
    [IO.Compression.ZipArchive]$archive = [IO.Compression.ZipFile]::OpenRead((Join-Path $passingRun 'local-ci-evidence.zip'))
    try {
        [object[]]$trx = @($archive.Entries | Where-Object { $_.Name.EndsWith('.trx') })
        Assert-Equal 2 $trx.Count 'Per-phase snapshots retain overwritten results and omit old TRX'
        Assert-Equal 0 @($trx | Where-Object { $_.Name -eq 'old.trx' }).Count 'Old evidence excluded'
    } finally { $archive.Dispose() }

    [hashtable]$plan = Get-CiPlan (Join-Path $passingRoot 'ci') (Join-Path $passingRoot 'tool-config.json') $false
    Assert-Equal $passingRoot $plan.root 'Project discovery uses current-directory ancestors'
    Assert-Fails { Resolve-CiPath $passingRoot '../outside' } 'Reject path traversal'
    Assert-Fails { Get-CiPlan (Join-Path $passingRoot 'ci') (Join-Path $passingRoot 'tool-config.json') $true } 'Explicit wrong project root rejected'

    [string]$partialRoot = New-Fixture 'partial project' 'test'
    [string]$partialRun = Invoke-Fixture $partialRoot 0 @('-OnlyPhase', 'test')
    [hashtable]$partial = Read-CiJson (Join-Path $partialRun 'run.json')
    Assert-Equal $false $partial.complete 'Selected phases produce a partial run'
    Assert-Equal 'skipped' $partial.phases[0].status 'Unselected phase is skipped'
    Assert-Equal 'passed' $partial.phases[1].status 'Selected phase runs'
    Assert-Equal 0 $partial.artifacts.Count 'Missing artifacts do not fail a partial test run'
    [hashtable]$partialManifest = Read-CiJson (Join-Path $partialRun 'upload-manifest.json')
    Assert-Fails { Assert-CiUpload $partialRun $partialManifest } 'Partial run cannot be uploaded'
    Assert-Equal $true ($null -eq (Assert-CiUpload $partialRun $partialManifest $true)) 'Explicit partial upload passes local validation'

    [string]$failingRoot = New-Fixture 'failing project' 'fail'
    [string]$failingRun = Invoke-Fixture $failingRoot 1
    [hashtable]$failedRun = Read-CiJson (Join-Path $failingRun 'run.json')
    Assert-Equal 'failed' $failedRun.status 'Process failure without TRX cannot report success'
    Assert-Equal 7 $failedRun.phases[1].exitCode 'Original failing phase exit code retained'
    Assert-Equal 'blocked' $failedRun.phases[2].status 'Package gated on successful tests'
    Assert-Equal 0 $failedRun.artifacts.Count 'No package attached after failure'
    Assert-Equal $true (Test-Path -LiteralPath (Join-Path $failingRun 'local-ci-evidence.zip')) 'Failure still produces evidence'

    [string]$dirtyRoot = New-Fixture 'changing project' 'dirty'
    [string]$dirtyRun = Invoke-Fixture $dirtyRoot 1
    [hashtable]$dirtyManifest = Read-CiJson (Join-Path $dirtyRun 'upload-manifest.json')
    Assert-Equal $false $dirtyManifest.sourceUnchanged 'Changes during execution invalidate commit attribution'
    Assert-Fails { Assert-CiUpload $dirtyRun $dirtyManifest } 'Changed source cannot be uploaded as commit CI'

    $script:uploadDirectory = $passingRun
    $script:uploadManifest = Read-CiJson (Join-Path $passingRun 'upload-manifest.json')
    $script:draft = $null
    $script:createCalls = 0
    $script:uploadCalls = 0
    $script:statusCalls = 0
    $script:commitStatus = $null
    $script:deletedIncomplete = 0
    $script:interruptUpload = $true
    Assert-Fails { Publish-CiDraft $passingRun } 'Interrupted upload preserves the draft'
    Assert-Equal 1 $script:createCalls 'One draft created before interruption'
    Assert-Equal 0 $script:statusCalls 'Commit status not posted before artifacts verified'
    $script:draft.assets += @{ id = 456; name = 'local-ci-evidence.zip'; size = 0; state = 'starter' }
    Publish-CiDraft $passingRun
    Assert-Equal 1 $script:createCalls 'Upload retry reuses existing draft'
    Assert-Equal 4 $script:lastUploadCount 'Retry uploads only the four missing assets'
    Assert-Equal 1 $script:deletedIncomplete 'Retry removes only an empty incomplete upload'
    Assert-Equal 5 $script:draft.assets.Count 'All expected assets uploaded'
    Assert-Equal 'success' $script:commitStatus.state 'Passing commit status'
    [int]$uploadsBeforeRetry = $script:uploadCalls
    Publish-CiDraft $passingRun
    Assert-Equal $uploadsBeforeRetry $script:uploadCalls 'Verified assets are not uploaded twice'
    Assert-Equal 1 $script:statusCalls 'Verified commit status is not posted twice'
    $script:draft.assets[0].digest = 'sha256:incorrect'
    Assert-Fails { Publish-CiDraft $passingRun } 'Conflicting remote asset is preserved'
    $script:draft.draft = $false
    Assert-Fails { Publish-CiDraft $passingRun } 'Published release is preserved'
    Add-Content -LiteralPath (Join-Path $passingRun 'bundle.zip') -Value 'tampered'
    Assert-Fails { Publish-CiDraft $passingRun } 'Local tampering blocks upload before GitHub access'

    $script:uploadDirectory = $partialRun
    $script:uploadManifest = $partialManifest
    $script:draft = $null
    $script:commitStatus = $null
    $script:statusCalls = 0
    Publish-CiDraft $partialRun $true
    Assert-Equal $true $script:draft.name.Contains('(partial passed)') 'Partial draft is clearly labelled'
    Assert-Equal 0 $script:statusCalls 'Partial upload does not publish ci/local status'

    $script:uploadDirectory = $failingRun
    $script:uploadManifest = Read-CiJson (Join-Path $failingRun 'upload-manifest.json')
    $script:draft = $null
    Publish-CiDraft $failingRun
    Assert-Equal 'failure' $script:commitStatus.state 'Failed CI uploads report and failure status'
    Assert-Equal 3 $script:draft.assets.Count 'Failed run uploads only report assets'
    Write-Host "[tests] Local CI tool passed $script:assertions assertions. No real CI, GitHub writes or uploads were performed."
} finally {
    [string]$temporaryPrefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    [string]$resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
    if (-not $resolvedTestRoot.StartsWith($temporaryPrefix, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolvedTestRoot) -notlike 'local-ci-tests-*') { throw 'Refusing to delete a test directory outside the verified temporary root.' }
    Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
}
