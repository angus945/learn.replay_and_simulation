[CmdletBinding()]
param(
    [string]$Path,
    [string]$Branch,
    [string]$StartPoint = 'HEAD',
    [string]$ProjectRoot = $env:DEVELOP_CMD_PROJECT_ROOT,
    [switch]$UseCodexDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Select-WorktreeDirectory() {
    [string[]]$options = @('Codex worktree directory (default)', 'Custom directory')
    [int]$selectedIndex = 0
    while ($true) {
        Clear-Host
        Write-Host 'Select worktree directory'
        Write-Host 'Use Up/Down Arrow and Enter to continue. Esc cancels.'
        Write-Host ''
        for ([int]$index = 0; $index -lt $options.Count; $index++) {
            [string]$prefix = if ($index -eq $selectedIndex) { '>' } else { ' ' }
            [string]$label = $options[$index]
            Write-Host "$prefix $label"
        }
        while ($true) {
            $key = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
            if ($key.VirtualKeyCode -eq 13) {
                return $selectedIndex
            }
            if ($key.VirtualKeyCode -eq 27) {
                throw 'Worktree creation cancelled.'
            }
            if ($key.VirtualKeyCode -eq 38) {
                $selectedIndex = ($selectedIndex + $options.Count - 1) % $options.Count
                break
            }
            if ($key.VirtualKeyCode -eq 40) {
                $selectedIndex = ($selectedIndex + 1) % $options.Count
                break
            }
        }
    }
}

function Invoke-WorktreeGit([string]$Repository, [string[]]$Arguments) {
    & git -C $Repository @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Git failed (exit $LASTEXITCODE): git $($Arguments -join ' ')"
    }
}

try {
    if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
        $ProjectRoot = (Get-Location).Path
    }

    $rootOutput = & git -C $ProjectRoot rev-parse --show-toplevel
    if ($LASTEXITCODE -ne 0) {
        throw 'Run from inside a Git repository, or specify -ProjectRoot.'
    }
    $repository = [string]$rootOutput

    if ($UseCodexDirectory -and -not [string]::IsNullOrWhiteSpace($Path)) {
        throw 'Specify either -Path or -UseCodexDirectory, not both.'
    }
    if (-not $UseCodexDirectory -and [string]::IsNullOrWhiteSpace($Path)) {
        [int]$directoryChoice = Select-WorktreeDirectory
        if ($directoryChoice -eq 0) {
            $UseCodexDirectory = $true
        }
        else {
            $Path = Read-Host 'New worktree directory'
        }
    }
    if ($UseCodexDirectory) {
        $codexDirectory = $env:CODEX_HOME
        if ([string]::IsNullOrWhiteSpace($codexDirectory)) {
            $userDirectory = [Environment]::GetFolderPath('UserProfile')
            $codexDirectory = Join-Path $userDirectory '.codex'
        }
        $worktreesDirectory = Join-Path $codexDirectory 'worktrees'
        $worktreeId = [guid]::NewGuid().ToString('N')
        $worktreeDirectory = Join-Path $worktreesDirectory $worktreeId
        $repositoryName = Split-Path -Leaf $repository
        $Path = Join-Path $worktreeDirectory $repositoryName
    }
    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw 'A worktree directory is required.'
    }
    $destination = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
    if (Test-Path -LiteralPath $destination) {
        throw "Destination already exists: $destination. Choose a new directory."
    }
    Write-Host "Worktree directory: $destination"

    if ([string]::IsNullOrWhiteSpace($Branch)) {
        $Branch = Read-Host 'Branch name (existing or new)'
    }
    if ([string]::IsNullOrWhiteSpace($Branch)) {
        throw 'A branch name is required.'
    }
    Invoke-WorktreeGit $repository @('check-ref-format', '--branch', $Branch)
    & git -C $repository show-ref --verify --quiet "refs/heads/$Branch"
    $branchStatus = $LASTEXITCODE
    if ($branchStatus -eq 0) {
        Invoke-WorktreeGit $repository @('worktree', 'add', '--', $destination, $Branch)
    }
    elseif ($branchStatus -eq 1) {
        Invoke-WorktreeGit $repository @('worktree', 'add', '-b', $Branch, '--', $destination, $StartPoint)
    }
    else {
        throw "Could not inspect branch: $Branch"
    }

    Write-Host "Initializing recursive submodules in $destination"
    try {
        Invoke-WorktreeGit $destination @('submodule', 'update', '--init', '--recursive')
    }
    catch {
        Write-Host "Worktree retained at: $destination"
        Write-Host 'After resolving the error, retry: git -C <worktree-directory> submodule update --init --recursive'
        throw
    }
    Write-Host "Worktree ready: $destination"
    exit 0
}
catch {
    Write-Error $_ -ErrorAction Continue
    exit 1
}
