function Assert-CiUpload([string]$RunDirectory, [hashtable]$Manifest, [bool]$AllowPartial = $false) {
    if ($Manifest.schemaVersion -ne 1 -or $Manifest.repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$' -or $Manifest.commit -notmatch '^[0-9a-f]{40}$') { throw 'Invalid upload manifest.' }
    if ($Manifest.id -notmatch '^[0-9]{8}-[0-9]{6}-[0-9a-f]{7}-[0-9a-f]{8}$' -or $Manifest.tag -cne ('local-ci-' + $Manifest.id)) { throw 'Invalid local CI run identity.' }
    if ($Manifest.ContainsKey('complete') -and $Manifest.complete -ne $true -and -not $AllowPartial) { throw 'Partial local CI runs require -UploadPartial before their draft and artifacts can be uploaded.' }
    if (-not $Manifest.sourceUnchanged) { throw 'The source changed during CI. The local report is preserved; run CI again on a clean commit before uploading.' }
    if ($Manifest.status -notin @('passed', 'failed')) { throw 'Only completed CI results can be uploaded.' }
    [System.Collections.Generic.HashSet[string]]$names = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($asset in $Manifest.assets) {
        [string]$path = Resolve-CiPath $RunDirectory $asset.path
        [hashtable]$actual = New-CiAsset $RunDirectory $path
        if ($asset.name -cne $actual.name -or -not $names.Add($asset.name) -or $asset.size -ne $actual.size -or $asset.sha256 -cne $actual.sha256) { throw "Asset changed or duplicated: $($asset.path)" }
    }
    foreach ($required in @('SUMMARY.md', 'local-ci-evidence.zip', 'local-ci-evidence.zip.sha256')) {
        if (-not $names.Contains($required)) { throw "Missing required upload asset: $required" }
    }
}

function Assert-CiRemoteAsset([hashtable]$Remote, [hashtable]$Local) {
    if ($Remote.state -ne 'uploaded' -or $Remote.size -ne $Local.size -or $Remote.digest -cne ('sha256:' + $Local.sha256)) {
        throw "Draft asset '$($Local.name)' differs from this run or is incomplete. Existing assets were preserved."
    }
}

function Get-CiCommitStatus([string]$Repository, [string]$Commit) {
    [string]$json = Invoke-CiGh @('api', "repos/$Repository/commits/$Commit/statuses?per_page=100")
    [object[]]$statuses = @($json | ConvertFrom-Json -AsHashtable)
    return $statuses | Where-Object { $_.context -eq 'ci/local' } | Select-Object -First 1
}

function Publish-CiDraft([string]$RunDirectory, [bool]$AllowPartial = $false) {
    [hashtable]$manifest = Read-CiJson (Join-Path $RunDirectory 'upload-manifest.json')
    Assert-CiUpload $RunDirectory $manifest $AllowPartial
    [bool]$complete = -not $manifest.ContainsKey('complete') -or $manifest.complete -eq $true
    [string]$repository = $manifest.repository
    [string]$commit = $manifest.commit
    [string]$remoteCommit = Invoke-CiGh @('api', "repos/$repository/commits/$commit", '--jq', '.sha')
    if ($remoteCommit.Trim() -cne $commit) { throw 'The tested commit must exist in the GitHub repository before uploading.' }
    [string]$summaryPath = Join-Path $RunDirectory 'SUMMARY.md'
    [string]$summary = Get-Content -LiteralPath $summaryPath -Raw
    [string]$releaseJson = Invoke-CiGh @('api', "repos/$repository/releases?per_page=100", '--paginate', '--slurp')
    [object[]]$pages = $releaseJson | ConvertFrom-Json -AsHashtable -NoEnumerate
    [System.Collections.Generic.List[object]]$matches = [System.Collections.Generic.List[object]]::new()
    foreach ($page in $pages) {
        foreach ($release in $page) { if ($release.tag_name -ceq $manifest.tag) { $matches.Add($release) } }
    }
    if ($matches.Count -gt 1) { throw 'Multiple releases match the run identity.' }
    [hashtable]$draft = if ($matches.Count -eq 1) { $matches[0] } else { $null }
    if ($draft) {
        [string]$remoteNotes = ([string]$draft.body).Replace("`r`n", "`n").TrimEnd()
        [string]$localNotes = $summary.Replace("`r`n", "`n").TrimEnd()
        if (-not $draft.draft -or $draft.target_commitish -cne $commit -or $remoteNotes -cne $localNotes) { throw 'Existing release is published or does not match this run; it was preserved.' }
    } else {
        [string]$scope = if ($complete) { $manifest.status } else { "partial $($manifest.status)" }
        [hashtable]$body = @{ tag_name = $manifest.tag; target_commitish = $commit; name = "Local CI $($manifest.id) ($scope)"; body = $summary; draft = $true; make_latest = 'false' }
        [string]$requestPath = Join-Path $RunDirectory 'release-request.json'
        Write-CiJson $body $requestPath
        $releaseJson = Invoke-CiGh @('api', "repos/$repository/releases", '--method', 'POST', '--input', $requestPath)
        $draft = $releaseJson | ConvertFrom-Json -AsHashtable
    }
    [System.Collections.Generic.List[string]]$missing = [System.Collections.Generic.List[string]]::new()
    foreach ($asset in $manifest.assets) {
        [object[]]$existing = @($draft.assets | Where-Object { $_.name -ceq $asset.name })
        if ($existing.Count -gt 1) { throw "Duplicate remote asset: $($asset.name)" }
        if ($existing.Count -eq 1 -and $existing[0].state -eq 'starter' -and $existing[0].size -eq 0) {
            [long]$incompleteId = $existing[0].id
            if ($incompleteId -le 0) { throw 'Invalid incomplete upload identity.' }
            $null = Invoke-CiGh @('api', "repos/$repository/releases/assets/$incompleteId", '--method', 'DELETE')
            $existing = @()
        }
        if ($existing.Count -eq 1) { Assert-CiRemoteAsset $existing[0] $asset } else { $missing.Add((Resolve-CiPath $RunDirectory $asset.path)) }
    }
    if ($missing.Count -gt 0) {
        Write-Host "[local-ci] Uploading $($missing.Count) assets to draft $($manifest.tag)"
        [string[]]$arguments = @('release', 'upload', $manifest.tag, '--repo', $repository) + $missing.ToArray()
        $null = Invoke-CiGh $arguments
    }
    $releaseJson = Invoke-CiGh @('api', "repos/$repository/releases/$($draft.id)")
    $draft = $releaseJson | ConvertFrom-Json -AsHashtable
    if (-not $draft.draft -or $draft.target_commitish -cne $commit) { throw 'Uploaded release is not the expected draft.' }
    foreach ($asset in $manifest.assets) {
        [object[]]$existing = @($draft.assets | Where-Object { $_.name -ceq $asset.name })
        if ($existing.Count -ne 1) { throw "Missing uploaded asset: $($asset.name)" }
        Assert-CiRemoteAsset $existing[0] $asset
    }
    Write-CiJson $draft (Join-Path $RunDirectory 'uploaded-release.json')
    if (-not $complete) {
        Write-Host "[local-ci] Partial draft: $($draft.html_url)"
        Write-Host '[local-ci] ci/local status was not created for this partial run.'
        return
    }
    [string]$state = if ($manifest.status -eq 'passed') { 'success' } else { 'failure' }
    [hashtable]$status = @{ state = $state; context = 'ci/local'; target_url = $draft.html_url; description = "Local CI $($manifest.status); report and artifacts verified in draft release" }
    [string]$statusPath = Join-Path $RunDirectory 'status-request.json'
    Write-CiJson $status $statusPath
    [object]$latest = Get-CiCommitStatus $repository $commit
    if (-not $latest -or $latest.state -ne $state -or $latest.target_url -cne $draft.html_url) {
        $null = Invoke-CiGh @('api', "repos/$repository/statuses/$commit", '--method', 'POST', '--input', $statusPath)
        $latest = Get-CiCommitStatus $repository $commit
    }
    if (-not $latest -or $latest.state -ne $state -or $latest.target_url -cne $draft.html_url) { throw 'Commit status verification failed; retry with -UploadOnly.' }
    Write-CiJson $latest (Join-Path $RunDirectory 'uploaded-status.json')
    Write-Host "[local-ci] Draft: $($draft.html_url)"
    Write-Host "[local-ci] $commit ci/local = $state"
}
