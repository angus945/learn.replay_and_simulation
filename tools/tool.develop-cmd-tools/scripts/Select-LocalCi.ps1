#Requires -Version 7.2
[CmdletBinding()]
param([string]$ProjectRoot)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Show-LocalCiMenu([System.Collections.Generic.List[object]]$Options, [int]$SelectedIndex) {
    Clear-Host
    Write-Host 'Local CI'
    Write-Host 'Use Up/Down Arrow to navigate, Space to select, Enter to run. Esc cancels.'
    Write-Host ''
    for ([int]$index = 0; $index -lt $Options.Count; $index++) {
        [string]$cursor = if ($index -eq $SelectedIndex) { '>' } else { ' ' }
        [string]$checked = if ($Options[$index].Selected) { '[x]' } else { '[ ]' }
        Write-Host "$cursor $checked $($Options[$index].Label)"
    }
}

try {
    [string]$toolRoot = Split-Path -Parent $PSScriptRoot
    [string]$configPath = Join-Path $toolRoot 'tool-config.json'
    if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) { $configPath = Join-Path $PSScriptRoot 'develop-cmd-tools.json' }
    [hashtable]$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json -AsHashtable
    [System.Collections.Generic.List[object]]$options = [System.Collections.Generic.List[object]]::new()
    foreach ($phase in $config.localCi.phases) {
        $options.Add([pscustomobject]@{ Id = [string]$phase.id; Label = "Phase: $($phase.id)"; Kind = 'phase'; Selected = $true })
    }
    $options.Add([pscustomobject]@{ Id = 'upload'; Label = 'Upload GitHub draft'; Kind = 'upload'; Selected = $true })
    [int]$selectedIndex = 0
    while ($true) {
        Show-LocalCiMenu $options $selectedIndex
        $key = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
        if ($key.VirtualKeyCode -eq 38) { $selectedIndex = ($selectedIndex + $options.Count - 1) % $options.Count; continue }
        if ($key.VirtualKeyCode -eq 40) { $selectedIndex = ($selectedIndex + 1) % $options.Count; continue }
        if ($key.VirtualKeyCode -eq 32) { $options[$selectedIndex].Selected = -not $options[$selectedIndex].Selected; continue }
        if ($key.VirtualKeyCode -eq 27) { Write-Host 'Local CI cancelled.'; exit 0 }
        if ($key.VirtualKeyCode -ne 13) { continue }
        [object[]]$phaseOptions = @($options | Where-Object { $_.Kind -eq 'phase' })
        [string[]]$selectedPhaseIds = @($phaseOptions | Where-Object { $_.Selected } | ForEach-Object { [string]$_.Id })
        if ($selectedPhaseIds.Count -eq 0) { Write-Host 'Select at least one CI phase.'; Start-Sleep -Seconds 1; continue }
        [object]$uploadOption = $options | Where-Object { $_.Kind -eq 'upload' }
        [bool]$upload = [bool]$uploadOption.Selected
        [hashtable]$runnerParameters = @{}
        if (-not [string]::IsNullOrWhiteSpace($ProjectRoot)) { $runnerParameters.ProjectRoot = $ProjectRoot }
        if ($selectedPhaseIds.Count -ne $phaseOptions.Count) {
            $runnerParameters.OnlyPhase = $selectedPhaseIds
            if ($upload) { $runnerParameters.UploadPartial = $true }
        }
        if (-not $upload) { $runnerParameters.NoUpload = $true }
        & (Join-Path $PSScriptRoot 'Invoke-LocalCi.ps1') @runnerParameters
        exit $LASTEXITCODE
    }
} catch {
    Write-Host "[local-ci] ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
