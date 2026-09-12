param(
    [string]$CommitSubject,
    [Parameter(Mandatory = $true)]
    [string]$ProjectRoot
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($CommitSubject)) {
    $CommitSubject = Read-Host 'Commit subject'
}

if ([string]::IsNullOrWhiteSpace($CommitSubject)) {
    throw 'A commit subject is required.'
}

$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
[string[]]$submodulePaths = @(& git -C $root submodule foreach --recursive --quiet 'echo "$sm_path"')
if ($LASTEXITCODE -ne 0) {
    throw 'Could not enumerate the repository submodules.'
}

[System.Collections.Generic.HashSet[string]]$repositories =
    [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
[void]$repositories.Add($root)
foreach ($submodulePath in $submodulePaths) {
    if (-not [string]::IsNullOrWhiteSpace($submodulePath)) {
        [void]$repositories.Add((Join-Path $root $submodulePath))
    }
}

[string[]]$orderedRepositories = @($repositories | Sort-Object { $_.Length } -Descending)
[int]$commitCount = 0
foreach ($repository in $orderedRepositories) {
    [string[]]$changes = @(& git -C $repository status --porcelain --untracked-files=all)
    if ($LASTEXITCODE -ne 0) {
        throw "Could not inspect Git changes in $repository."
    }

    if ($changes.Count -eq 0) {
        continue
    }

    Write-Host "[Commit] Staging all changes in $repository"
    & git -C $repository add --all
    if ($LASTEXITCODE -ne 0) {
        throw "Could not stage changes in $repository."
    }

    Write-Host "[Commit] Creating commit in $repository"
    & git -C $repository commit --message $CommitSubject
    if ($LASTEXITCODE -ne 0) {
        throw "Could not create a commit in $repository."
    }

    $commitCount++
}

if ($commitCount -eq 0) {
    Write-Host '[Commit] No changes to commit.'
} else {
    Write-Host "[Commit] Created $commitCount commit(s)."
}
