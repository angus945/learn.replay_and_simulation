# Shared by the runner, report writer and focused offline tests. Requires PowerShell 7.
function Write-CiJson([object]$Value, [string]$Path) {
    ConvertTo-Json -InputObject $Value -Depth 20 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Read-CiJson([string]$Path) {
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -AsHashtable
}

function Resolve-CiPath([string]$Root, [string]$RelativePath) {
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [IO.Path]::IsPathRooted($RelativePath)) {
        throw "Expected a relative path: $RelativePath"
    }
    [string]$prefix = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    [string]$path = [IO.Path]::GetFullPath((Join-Path $Root $RelativePath))
    if (-not $path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escapes its root: $RelativePath"
    }
    return $path
}

function Invoke-CiGit([string]$Root, [string[]]$Arguments) {
    [string[]]$output = @(& git -C $Root @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments[0]) failed: $($output -join [Environment]::NewLine)" }
    return $output
}

function Get-CiSource([string]$Root) {
    [string]$commit = Invoke-CiGit $Root @('rev-parse', 'HEAD')
    [string[]]$changes = @(Invoke-CiGit $Root @('status', '--porcelain', '--untracked-files=all', '--ignore-submodules=none'))
    [string[]]$submodules = @(Invoke-CiGit $Root @('submodule', 'status', '--recursive'))
    return @{ commit = $commit.Trim(); changes = $changes; submodules = $submodules }
}

function Get-CiRepository([string]$Root, [string]$Remote) {
    [string]$url = Invoke-CiGit $Root @('remote', 'get-url', $Remote)
    if ($url -notmatch '^(?:https://github\.com/|git@github\.com:|ssh://git@github\.com/)([A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+?)(?:\.git)?/?$') {
        throw "Remote '$Remote' must identify a github.com repository."
    }
    return $Matches[1]
}

function Invoke-CiGh([string[]]$Arguments) {
    [string[]]$output = @(& gh @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "gh $($Arguments[0]) failed: $($output -join [Environment]::NewLine)" }
    return $output -join [Environment]::NewLine
}

function Expand-CiValue([string]$Value, [hashtable]$Variables) {
    foreach ($key in $Variables.Keys) { $Value = $Value.Replace('{' + $key + '}', [string]$Variables[$key]) }
    if ($Value -match '\{[^{}]+\}') { throw "Unknown local CI placeholder: $Value" }
    return $Value
}

function Get-CiPlan([string]$ProjectRoot, [string]$ConfigFile, [bool]$ExplicitRoot) {
    [hashtable]$config = Read-CiJson $ConfigFile
    if (-not $config.localCi -or -not $config.localCi.phases) { throw "Configure localCi in $ConfigFile; see scripts/develop-cmd-tools.json." }
    [string]$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
    [string]$anchor = [string]$config.testScript
    while (-not (Test-Path -LiteralPath (Resolve-CiPath $root $anchor) -PathType Leaf)) {
        [string]$parent = Split-Path -Parent $root
        if ($ExplicitRoot -or -not $parent -or $parent -eq $root) { throw "Cannot find '$anchor'. Set -ProjectRoot or DEVELOP_CMD_PROJECT_ROOT." }
        $root = $parent
    }
    [string]$gitRoot = Invoke-CiGit $root @('rev-parse', '--show-toplevel')
    if ([IO.Path]::GetFullPath($gitRoot) -ne [IO.Path]::GetFullPath($root)) { throw 'ProjectRoot must be the Git repository root.' }
    [hashtable]$source = Get-CiSource $root
    [string]$nonce = [guid]::NewGuid().ToString('N').Substring(0, 8)
    [string]$shortCommit = $source.commit.Substring(0, 7)
    [string]$runId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + $shortCommit + '-' + $nonce
    [string]$outputRoot = Resolve-CiPath $root ([string]$config.localCi.outputRoot)
    [string]$runDirectory = Resolve-CiPath $outputRoot $runId
    [hashtable]$variables = @{ projectRoot = $root; runDirectory = $runDirectory; runId = $runId; configuration = $config.configuration; shortCommit = $shortCommit; nonce = $nonce }
    $variables.packageVersion = Expand-CiValue ([string]$config.localCi.packageVersion) $variables
    if ($variables.packageVersion -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$' -or $variables.packageVersion.Contains('..')) { throw 'Invalid packageVersion.' }
    [System.Collections.Generic.List[object]]$phases = [System.Collections.Generic.List[object]]::new()
    [System.Collections.Generic.HashSet[string]]$ids = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($definition in $config.localCi.phases) {
        [string]$id = [string]$definition.id
        if ($id -notmatch '^[a-z0-9][a-z0-9-]*$' -or -not $ids.Add($id)) { throw "Invalid or duplicate phase id: $id" }
        [string]$scriptPath = Resolve-CiPath $root ([string]$definition.script)
        if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) { throw "Missing CI script: $scriptPath" }
        [string[]]$arguments = @(foreach ($argument in $definition.arguments) { Expand-CiValue ([string]$argument) $variables })
        [bool]$requiresSuccess = $definition.ContainsKey('requiresSuccess') -and [bool]$definition.requiresSuccess
        $phases.Add(@{ id = $id; script = $definition.script; arguments = $arguments; requiresSuccess = $requiresSuccess; status = 'pending'; exitCode = $null; log = "logs/$id.log" })
    }
    return @{ root = $root; config = $config; configFile = $ConfigFile; source = $source; variables = $variables; outputRoot = $outputRoot; phases = $phases.ToArray() }
}

function Get-CiResultSnapshot([string]$Root) {
    [hashtable]$snapshot = @{}
    if (Test-Path -LiteralPath $Root -PathType Container) {
        foreach ($file in Get-ChildItem -LiteralPath $Root -Recurse -File) {
            if ($file.Extension -eq '.trx' -or $file.Name -eq 'coverage.cobertura.xml') {
                $snapshot[$file.FullName] = "$($file.LastWriteTimeUtc.Ticks):$($file.Length)"
            }
        }
    }
    return $snapshot
}

function Copy-CiResults([string]$Root, [string]$Destination, [hashtable]$Before) {
    [hashtable]$after = Get-CiResultSnapshot $Root
    foreach ($path in $after.Keys) {
        if ($Before.ContainsKey($path) -and $Before[$path] -eq $after[$path]) { continue }
        [string]$relative = [IO.Path]::GetRelativePath($Root, $path)
        [string]$target = Resolve-CiPath $Destination $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        Copy-Item -LiteralPath $path -Destination $target
    }
}

function Invoke-CiPhase([string]$Root, [hashtable]$Phase, [string]$LogPath) {
    [Diagnostics.ProcessStartInfo]$start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = (Get-Command pwsh -ErrorAction Stop).Source
    $start.WorkingDirectory = $Root
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.Environment['DOTNET_CLI_UI_LANGUAGE'] = 'en-US'
    $start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'
    $start.Environment['DOTNET_NOLOGO'] = '1'
    [string]$scriptPath = Resolve-CiPath $Root $Phase.script
    foreach ($argument in @('-NoLogo', '-NoProfile', '-File', $scriptPath) + $Phase.arguments) { $start.ArgumentList.Add([string]$argument) }
    [Diagnostics.Process]$process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    [IO.StreamWriter]$writer = [IO.StreamWriter]::new($LogPath, $false, [Text.UTF8Encoding]::new($false))
    $writer.AutoFlush = $true
    [bool]$started = $false
    try {
        $started = $process.Start()
        [hashtable]$reads = @{ stdout = $process.StandardOutput.ReadLineAsync(); stderr = $process.StandardError.ReadLineAsync() }
        while ($reads.Count -gt 0) {
            foreach ($stream in @($reads.Keys)) {
                if (-not $reads[$stream].IsCompleted) { continue }
                [object]$line = $reads[$stream].GetAwaiter().GetResult()
                if ($null -eq $line) { $reads.Remove($stream); continue }
                $writer.WriteLine($line)
                Write-Host $line
                $reads[$stream] = if ($stream -eq 'stdout') { $process.StandardOutput.ReadLineAsync() } else { $process.StandardError.ReadLineAsync() }
            }
            if ($reads.Count -gt 0) {
                [Threading.Tasks.Task[]]$pending = @($reads.Values)
                $null = [Threading.Tasks.Task]::WaitAny($pending, 100)
            }
        }
        $process.WaitForExit()
        return $process.ExitCode
    } finally {
        if ($started -and -not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        $writer.Dispose()
        $process.Dispose()
    }
}
