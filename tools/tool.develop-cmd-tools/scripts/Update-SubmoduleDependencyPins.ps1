param([Parameter(Mandatory = $true)][string]$ProjectRoot)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path

function Get-RepositoryUrlKey([string] $url) {
    if ([string]::IsNullOrWhiteSpace($url)) {
        throw 'A dependency repository URL cannot be empty.'
    }

    return $url.Trim().TrimEnd('/') -replace '\.git$', ''
}

function Get-SubmodulePaths {
    [string[]]$paths = @(& git -C $root submodule foreach --recursive --quiet 'pwd -W')
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not enumerate submodules.'
    }

    return @($paths | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Get-ManifestPaths([string[]] $repositoryPaths) {
    [System.Collections.Generic.List[string]]$paths = [System.Collections.Generic.List[string]]::new()
    foreach ($repositoryPath in $repositoryPaths) {
        [string[]]$trackedManifests = @(& git -C $repositoryPath ls-files -- ':(glob)**/dependencies.json' ':(glob)**/dependency-lock.json')
        if ($LASTEXITCODE -ne 0) {
            throw "Could not enumerate dependency manifests in repository: $repositoryPath"
        }

        foreach ($trackedManifest in $trackedManifests) {
            if ([string]::IsNullOrWhiteSpace($trackedManifest)) {
                continue
            }

            $paths.Add((Join-Path $repositoryPath $trackedManifest))
        }
    }

    return $paths.ToArray()
}

function Get-DependencyPinEntries([object] $value) {
    if ($null -eq $value -or $value -is [string]) {
        return
    }

    if ($value -is [System.Collections.IEnumerable]) {
        foreach ($item in $value) {
            Get-DependencyPinEntries $item
        }

        return
    }

    if ($value -isnot [pscustomobject]) {
        return
    }

    [System.Management.Automation.PSPropertyInfo]$repositoryUrlProperty = $value.PSObject.Properties['repositoryUrl']
    if ($null -ne $repositoryUrlProperty) {
        foreach ($pinName in @('commit', 'reference')) {
            [System.Management.Automation.PSPropertyInfo]$pinProperty = $value.PSObject.Properties[$pinName]
            if ($null -ne $pinProperty) {
                [pscustomobject]@{
                    RepositoryUrl = [string]$repositoryUrlProperty.Value
                    PinName = $pinName
                    PinProperty = $pinProperty
                }
            }
        }
    }

    foreach ($property in $value.PSObject.Properties) {
        Get-DependencyPinEntries $property.Value
    }
}

[System.Collections.Generic.Dictionary[string, string]]$checkoutCommits =
    [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::OrdinalIgnoreCase)

[string[]]$submodulePaths = @(Get-SubmodulePaths)
foreach ($submodulePath in $submodulePaths) {
    [string]$repositoryUrl = (& git -C $submodulePath remote get-url origin).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "Could not read the origin URL for submodule: $submodulePath"
    }

    [string]$currentCommit = (& git -C $submodulePath rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $currentCommit -notmatch '^[0-9a-f]{40}$') {
        throw "Could not resolve the current commit for submodule: $submodulePath"
    }

    [string]$urlKey = Get-RepositoryUrlKey $repositoryUrl
    if ($checkoutCommits.ContainsKey($urlKey) -and $checkoutCommits[$urlKey] -cne $currentCommit) {
        throw "The same repository URL is checked out at different commits: $repositoryUrl"
    }

    $checkoutCommits[$urlKey] = $currentCommit
}

[string[]]$repositoryPaths = @($root) + $submodulePaths
[string[]]$manifestPaths = @(Get-ManifestPaths $repositoryPaths)

[int]$updatedManifestCount = 0
[int]$updatedPinCount = 0
foreach ($manifestPath in $manifestPaths) {
    [byte[]]$manifestBytes = [System.IO.File]::ReadAllBytes($manifestPath)
    [bool]$hasUtf8Bom = $manifestBytes.Length -ge 3 -and
        $manifestBytes[0] -eq 0xEF -and
        $manifestBytes[1] -eq 0xBB -and
        $manifestBytes[2] -eq 0xBF
    [string]$manifestText = Get-Content -LiteralPath $manifestPath -Raw
    [object]$manifest = $manifestText | ConvertFrom-Json
    [int]$manifestPinUpdates = 0

    foreach ($entry in @(Get-DependencyPinEntries $manifest)) {
        [string]$existingPin = [string]$entry.PinProperty.Value
        if ($existingPin -notmatch '^[0-9a-f]{40}$') {
            throw "Dependency pin '$($entry.PinName)' in $manifestPath must be a 40-character commit hash."
        }

        [string]$urlKey = Get-RepositoryUrlKey $entry.RepositoryUrl
        if (-not $checkoutCommits.ContainsKey($urlKey)) {
            throw "No checked-out submodule matches dependency URL '$($entry.RepositoryUrl)' in $manifestPath."
        }

        [string]$currentCommit = $checkoutCommits[$urlKey]
        if ($existingPin -ceq $currentCommit) {
            continue
        }

        [string]$escapedRepositoryUrl = [regex]::Escape($entry.RepositoryUrl)
        [string]$escapedPinName = [regex]::Escape($entry.PinName)
        [string]$escapedExistingPin = [regex]::Escape($existingPin)
        [string]$pattern = '(?s)("repositoryUrl"\s*:\s*"' + $escapedRepositoryUrl + '"' +
            '(?:(?!"repositoryUrl").)*?"' + $escapedPinName + '"\s*:\s*")' +
            $escapedExistingPin + '("\s*[,}])'
        [System.Text.RegularExpressions.MatchCollection]$pinMatches = [regex]::Matches($manifestText, $pattern)
        if ($pinMatches.Count -ne 1) {
            throw "Could not uniquely locate dependency pin '$($entry.PinName)' for '$($entry.RepositoryUrl)' in $manifestPath."
        }

        [string]$replacement = '${1}' + $currentCommit + '${2}'
        $manifestText = [regex]::Replace($manifestText, $pattern, $replacement, 1)
        Write-Host "[Dependencies] ${manifestPath}: $($entry.PinName) $existingPin -> $currentCommit"
        $manifestPinUpdates++
    }

    if ($manifestPinUpdates -eq 0) {
        continue
    }

    [System.Text.UTF8Encoding]$utf8Encoding = [System.Text.UTF8Encoding]::new($hasUtf8Bom)
    [System.IO.File]::WriteAllText($manifestPath, $manifestText, $utf8Encoding)
    $updatedManifestCount++
    $updatedPinCount += $manifestPinUpdates
}

if ($updatedPinCount -eq 0) {
    Write-Host '[Dependencies] All checked-out dependency pins are already current.'
    exit 0
}

Write-Host "[Dependencies] Updated $updatedPinCount dependency pin(s) in $updatedManifestCount manifest(s)."
