param([string]$ProjectRoot = (Get-Location).Path)

[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()

$root = (& git -C $ProjectRoot rev-parse --show-toplevel 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($root)) {
    Write-Host 'Error: This tool must be run inside a Git repository.' -ForegroundColor Red
    exit 1
}

Set-Location -LiteralPath $root

function Get-SubmodulePaths {
    $paths = @(& git -C $root submodule foreach --recursive --quiet 'pwd -W')
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not enumerate the repository submodules.'
    }

    return @($paths | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Attach-SubmoduleHead([string] $path) {
    & git -C $path symbolic-ref --quiet HEAD *> $null
    if ($LASTEXITCODE -eq 0) {
        return $true
    }

    Write-Host "--- $path (detached HEAD detected)" -ForegroundColor Yellow
    $localBranches = @(& git -C $path for-each-ref '--format=%(refname:short)' --points-at HEAD refs/heads)
    $branch = $localBranches |
        Sort-Object @{ Expression = { if ($_ -eq 'main') { 0 } elseif ($_ -eq 'master') { 1 } else { 2 } } }, @{ Expression = { $_ } } |
        Select-Object -First 1

    if ($branch) {
        & git -C $path switch $branch *> $null
        return ($LASTEXITCODE -eq 0)
    }

    $remoteHead = & git -C $path symbolic-ref --quiet --short refs/remotes/origin/HEAD 2>$null
    if ($LASTEXITCODE -eq 0 -and $remoteHead) {
        $currentCommit = & git -C $path rev-parse HEAD
        $remoteCommit = & git -C $path rev-parse $remoteHead
        if ($currentCommit -eq $remoteCommit) {
            $branch = $remoteHead.Substring($remoteHead.IndexOf('/') + 1)
            & git -C $path show-ref --verify --quiet ("refs/heads/$branch")
            if ($LASTEXITCODE -eq 0) {
                & git -C $path merge-base --is-ancestor $branch $remoteHead
                if ($LASTEXITCODE -ne 0) {
                    Write-Error "Local branch $branch has diverged in $path"
                    return $false
                }

                & git -C $path branch -f $branch $remoteHead
                if ($LASTEXITCODE -ne 0) {
                    return $false
                }

                & git -C $path switch $branch *> $null
                return ($LASTEXITCODE -eq 0)
            }

            & git -C $path switch --track -c $branch $remoteHead *> $null
            return ($LASTEXITCODE -eq 0)
        }
    }

    Write-Error "Cannot safely choose a branch for detached HEAD in $path."
    return $false
}

function Invoke-CommitAllSubmodules {
    $message = Read-Host 'Commit message for all changed submodules'
    if ([string]::IsNullOrWhiteSpace($message)) {
        Write-Host 'Commit message cannot be empty.' -ForegroundColor Yellow
        return
    }

    $exitCode = 0
    $paths = @(Get-SubmodulePaths | Sort-Object { ($_ -split '[/\\]').Count } -Descending)
    foreach ($path in $paths) {
        if (-not (Attach-SubmoduleHead $path)) {
            $exitCode = 1
            continue
        }

        $changes = @(& git -C $path status --porcelain)
        if ($LASTEXITCODE -ne 0) {
            $exitCode = $LASTEXITCODE
            continue
        }

        if ($changes.Count -eq 0) {
            Write-Host "--- $path (clean, skipped)"
            continue
        }

        Write-Host "--- $path"
        & git -C $path add -A
        if ($LASTEXITCODE -ne 0) {
            $exitCode = $LASTEXITCODE
            continue
        }

        & git -C $path commit -m $message
        if ($LASTEXITCODE -ne 0) {
            $exitCode = $LASTEXITCODE
        }
    }

    if ($exitCode -eq 0) {
        Write-Host 'Finished committing all changed submodules.' -ForegroundColor Green
        Write-Host 'The parent repository was not committed; its submodule pointers may now be modified.'
    } else {
        Write-Host "One or more submodule commits failed. Exit code: $exitCode" -ForegroundColor Red
    }
}

function Invoke-PushAll {
    & git -C $root push
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Root repository push failed. Exit code: $LASTEXITCODE" -ForegroundColor Red
        return
    }

    $exitCode = 0
    foreach ($path in Get-SubmodulePaths) {
        Write-Host "--- $path"
        & git -C $path push
        if ($LASTEXITCODE -ne 0) {
            $exitCode = $LASTEXITCODE
        }
    }

    if ($exitCode -eq 0) {
        Write-Host 'Finished pushing the root repository and all submodules.' -ForegroundColor Green
    } else {
        Write-Host "One or more submodule pushes failed. Exit code: $exitCode" -ForegroundColor Red
    }
}

function Invoke-PullAllSubmodules {
    & git -C $root pull --ff-only
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Root repository pull failed. Exit code: $LASTEXITCODE" -ForegroundColor Red
        return
    }

    & git -C $root submodule sync --recursive
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Submodule synchronization failed. Exit code: $LASTEXITCODE" -ForegroundColor Red
        return
    }

    & git -C $root submodule update --init --recursive
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Submodule initialization failed. Exit code: $LASTEXITCODE" -ForegroundColor Red
        return
    }

    $exitCode = 0
    foreach ($path in Get-SubmodulePaths) {
        if (-not (Attach-SubmoduleHead $path)) {
            $exitCode = 1
            continue
        }

        Write-Host "--- $path"
        & git -C $path pull --ff-only
        if ($LASTEXITCODE -ne 0) {
            $exitCode = $LASTEXITCODE
        }
    }

    if ($exitCode -eq 0) {
        Write-Host 'Finished pulling the root repository and all submodules.' -ForegroundColor Green
    } else {
        Write-Host "One or more submodule pulls failed. Exit code: $exitCode" -ForegroundColor Red
    }
}

function Invoke-MergeAllSubmoduleBranchesIntoMain {
    $submodules = @()
    $paths = @(Get-SubmodulePaths | Sort-Object { ($_ -split '[/\\]').Count })
    foreach ($path in $paths) {
        $branch = & git -C $path symbolic-ref --quiet --short HEAD 2>$null
        $source = $branch
        $sourceDescription = $branch
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($branch)) {
            $source = & git -C $path rev-parse HEAD 2>$null
            if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($source)) {
                Write-Host "Could not resolve the detached HEAD in $path." -ForegroundColor Red
                return
            }

            $shortCommit = & git -C $path rev-parse --short HEAD
            $sourceDescription = "detached HEAD $shortCommit"
        }

        $changes = @(& git -C $path status --porcelain)
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Could not inspect the working tree in $path." -ForegroundColor Red
            return
        }

        if ($changes.Count -ne 0) {
            Write-Host "Cannot merge $path because it has uncommitted changes." -ForegroundColor Red
            return
        }

        $mainStart = $null
        & git -C $path show-ref --verify --quiet refs/heads/main
        if ($LASTEXITCODE -ne 0) {
            & git -C $path show-ref --verify --quiet refs/remotes/origin/main
            if ($LASTEXITCODE -eq 0) {
                $mainStart = 'origin/main'
            } else {
                $mainStart = $source
            }
        }

        $submodules += [PSCustomObject]@{
            Path = $path
            Branch = $branch
            Source = $source
            SourceDescription = $sourceDescription
            MainStart = $mainStart
        }
    }

    foreach ($submodule in $submodules) {
        $path = $submodule.Path
        $branch = $submodule.Branch
        $source = $submodule.Source
        $sourceDescription = $submodule.SourceDescription
        $mainStart = $submodule.MainStart
        if ($branch -eq 'main') {
            Write-Host "--- $path (already on main, skipped)"
            continue
        }

        Write-Host "--- $path ($sourceDescription -> main)"
        if ($null -eq $mainStart) {
            & git -C $path switch main
        } else {
            & git -C $path switch -c main $mainStart
        }

        if ($LASTEXITCODE -ne 0) {
            Write-Host "Could not switch $path to main." -ForegroundColor Red
            return
        }

        if ($mainStart -eq $source) {
            & git -C $path remote get-url origin *> $null
            if ($LASTEXITCODE -eq 0) {
                & git -C $path config branch.main.remote origin
                & git -C $path config branch.main.merge refs/heads/main
            }

            Write-Host "Created main at $sourceDescription because no local or origin/main branch exists." -ForegroundColor Yellow
            continue
        }

        & git -C $path merge --no-edit $source
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Merge failed in $path. Resolve or abort the merge there before retrying." -ForegroundColor Red
            return
        }
    }

    Write-Host 'Finished merging every submodule current branch into main.' -ForegroundColor Green
    Write-Host 'The merges were not pushed. The parent repository submodule pointers may now be modified.'
}

function Set-AllSubmoduleDefaultBranchesToMain {
    $ghCommand = Get-Command gh -ErrorAction SilentlyContinue
    if ($null -eq $ghCommand) {
        Write-Host 'GitHub CLI (gh) is required but was not found.' -ForegroundColor Red
        return
    }

    & gh auth status
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'GitHub CLI is not authenticated. Run gh auth login and retry.' -ForegroundColor Red
        return
    }

    $submodules = @()
    foreach ($path in Get-SubmodulePaths) {
        $remoteUrl = & git -C $path remote get-url origin 2>$null
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($remoteUrl)) {
            Write-Host "Cannot find the origin remote for $path." -ForegroundColor Red
            return
        }

        & git -C $path ls-remote --exit-code --heads origin main *> $null
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Cannot update $path because origin/main does not exist or cannot be accessed." -ForegroundColor Red
            return
        }

        $submodules += [PSCustomObject]@{
            Path = $path
            RemoteUrl = $remoteUrl
        }
    }

    $exitCode = 0
    foreach ($submodule in $submodules) {
        Write-Host "--- $($submodule.Path)"
        & gh repo edit $submodule.RemoteUrl --default-branch main
        if ($LASTEXITCODE -ne 0) {
            $exitCode = $LASTEXITCODE
        }
    }

    if ($exitCode -eq 0) {
        Write-Host 'Finished setting every submodule GitHub default branch to main.' -ForegroundColor Green
    } else {
        Write-Host "One or more default-branch updates failed. Exit code: $exitCode" -ForegroundColor Red
    }
}

function Remove-AllMergedSubmoduleBranches {
    $submodules = @()
    Write-Host 'Checking every submodule before deleting branches...' -ForegroundColor Cyan
    foreach ($path in Get-SubmodulePaths) {
        Write-Host ''
        Write-Host "--- $path"
        Write-Host 'Checking working tree...'
        $changes = @(& git -C $path status --porcelain)
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Could not inspect the working tree in $path." -ForegroundColor Red
            return
        }

        if ($changes.Count -ne 0) {
            Write-Host "Cannot delete branches because $path has uncommitted changes." -ForegroundColor Red
            return
        }

        Write-Host 'Working tree is clean.' -ForegroundColor Green
        Write-Host 'Checking local main branch...'
        & git -C $path show-ref --verify --quiet refs/heads/main
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Cannot delete branches because $path does not have a local main branch." -ForegroundColor Red
            return
        }

        Write-Host 'Local main branch exists.' -ForegroundColor Green
        Write-Host 'Fetching and pruning origin...'
        & git -C $path fetch origin --prune
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Could not fetch origin for $path." -ForegroundColor Red
            return
        }

        $localBranches = @(& git -C $path for-each-ref '--format=%(refname:short)' refs/heads | Where-Object { $_ -ne 'main' })
        $remoteBranchNames = @(& git -C $path for-each-ref '--format=%(refname:lstrip=3)' refs/remotes/origin | Where-Object { $_ -ne 'main' -and $_ -ne 'HEAD' })
        $branchesToCheck = @($localBranches)
        $branchesToCheck += @($remoteBranchNames | ForEach-Object { "origin/$_" })
        if ($branchesToCheck.Count -eq 0) {
            Write-Host 'No branches other than main were found.' -ForegroundColor Green
        }

        foreach ($branch in $branchesToCheck) {
            Write-Host "Checking whether $branch is merged into main..."
            & git -C $path merge-base --is-ancestor $branch main
            if ($LASTEXITCODE -ne 0) {
                Write-Host "Cannot delete branches because $branch in $path has not been merged into main." -ForegroundColor Red
                return
            }

            Write-Host "$branch is fully merged." -ForegroundColor Green
        }

        $submodules += [PSCustomObject]@{
            Path = $path
            LocalBranches = $localBranches
            RemoteBranches = $remoteBranchNames
        }
    }

    $localBranchCount = 0
    $remoteBranchCount = 0
    foreach ($submodule in $submodules) {
        $localBranchCount += $submodule.LocalBranches.Count
        $remoteBranchCount += $submodule.RemoteBranches.Count
    }

    $branchCount = $localBranchCount + $remoteBranchCount
    Write-Host ''
    Write-Host 'Branch deletion summary' -ForegroundColor Cyan
    Write-Host "Submodules checked: $($submodules.Count)"
    Write-Host "Local branches to delete: $localBranchCount"
    Write-Host "Remote branches to delete: $remoteBranchCount"
    foreach ($submodule in $submodules) {
        if ($submodule.LocalBranches.Count -eq 0 -and $submodule.RemoteBranches.Count -eq 0) {
            continue
        }

        Write-Host ''
        Write-Host $submodule.Path
        foreach ($branch in $submodule.LocalBranches) {
            Write-Host "  local:  $branch"
        }

        foreach ($branch in $submodule.RemoteBranches) {
            Write-Host "  remote: origin/$branch"
        }
    }

    Write-Host ''
    if ($branchCount -eq 0) {
        Write-Host 'No merged submodule branches need to be deleted.' -ForegroundColor Green
        return
    }

    Write-Host "All checks passed. $branchCount local or remote branch references will be deleted." -ForegroundColor Yellow
    $confirmation = Read-Host 'Type DELETE to continue'
    if ($confirmation -cne 'DELETE') {
        Write-Host 'Branch deletion cancelled.' -ForegroundColor Yellow
        return
    }

    $exitCode = 0
    foreach ($submodule in $submodules) {
        $path = $submodule.Path
        Write-Host "--- $path"
        & git -C $path switch main
        if ($LASTEXITCODE -ne 0) {
            $exitCode = $LASTEXITCODE
            Write-Host "Could not switch $path to main; its branches were preserved." -ForegroundColor Red
            continue
        }

        if ($submodule.RemoteBranches.Count -ne 0) {
            & git -C $path push origin --delete $submodule.RemoteBranches
            if ($LASTEXITCODE -ne 0) {
                $exitCode = $LASTEXITCODE
                Write-Host "Remote branch deletion failed in $path; its local branches were preserved." -ForegroundColor Red
                continue
            }
        }

        foreach ($branch in $submodule.LocalBranches) {
            & git -C $path branch -d $branch
            if ($LASTEXITCODE -ne 0) {
                $exitCode = $LASTEXITCODE
            }
        }
    }

    if ($exitCode -eq 0) {
        Write-Host 'Finished deleting all fully merged submodule branches.' -ForegroundColor Green
    } else {
        Write-Host "One or more branch deletions failed. Exit code: $exitCode" -ForegroundColor Red
    }
}

function Read-MenuSelection([string[]] $items) {
    $selected = 0
    while ($true) {
        Clear-Host
        Write-Host 'Submodule Git Operations' -ForegroundColor Cyan
        Write-Host 'Use Up/Down to choose, Enter to run, Esc to go back.'
        Write-Host ''

        for ($index = 0; $index -lt $items.Count; $index++) {
            if ($index -eq $selected) {
                Write-Host ("> " + $items[$index]) -ForegroundColor Black -BackgroundColor Cyan
            } else {
                Write-Host ("  " + $items[$index])
            }
        }

        $key = [Console]::ReadKey($true).Key
        switch ($key) {
            ([ConsoleKey]::UpArrow) { $selected = ($selected - 1 + $items.Count) % $items.Count }
            ([ConsoleKey]::DownArrow) { $selected = ($selected + 1) % $items.Count }
            ([ConsoleKey]::Enter) { return $selected }
            ([ConsoleKey]::Escape) { return -1 }
        }
    }
}

$items = @(
    'Commit all changed submodules',
    'Push root repository and all submodules',
    'Pull root repository and all submodules',
    'Merge every submodule current branch into main',
    'Set every submodule GitHub default branch to main',
    'Delete every fully merged submodule branch except main',
    'Exit'
)

while ($true) {
    $selection = Read-MenuSelection $items
    if ($selection -lt 0 -or $selection -eq 6) {
        exit 0
    }

    Clear-Host
    try {
        switch ($selection) {
            0 { Invoke-CommitAllSubmodules }
            1 { Invoke-PushAll }
            2 { Invoke-PullAllSubmodules }
            3 { Invoke-MergeAllSubmoduleBranchesIntoMain }
            4 { Set-AllSubmoduleDefaultBranchesToMain }
            5 { Remove-AllMergedSubmoduleBranches }
        }
    } catch {
        Write-Host $_.Exception.Message -ForegroundColor Red
    }

    Write-Host ''
    Write-Host 'Press any key to return to the menu. Esc also returns to the menu.' -ForegroundColor DarkGray
    [void][Console]::ReadKey($true)
}
