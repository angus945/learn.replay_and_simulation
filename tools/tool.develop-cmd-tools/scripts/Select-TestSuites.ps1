param(
    [Parameter(Mandatory = $true)]
    [string]$OutputFile,
    [Parameter(Mandatory = $true)]
    [string]$ConfigFile
)

$ErrorActionPreference = 'Stop'

[object]$config = Get-Content -LiteralPath $ConfigFile -Raw | ConvertFrom-Json
[object[]]$options = @($config.suites | ForEach-Object {
    [pscustomobject]@{ Id = [string]$_.id; Label = [string]$_.label; Selected = $true }
})
[int]$selectedIndex = 0

function Write-Menu {
    Clear-Host
    Write-Host 'Test suites'
    Write-Host ''
    for ([int]$index = 0; $index -lt $options.Count; $index++) {
        [string]$cursor = if ($index -eq $selectedIndex) { '>' } else { ' ' }
        [string]$checked = if ($options[$index].Selected) { '[x]' } else { '[ ]' }
        Write-Host "$cursor $checked $($options[$index].Label)"
    }

    Write-Host ''
    Write-Host 'Use Up/Down to navigate, Space to toggle, Enter to run, Esc to cancel.'
}

Write-Menu
while ($true) {
    $key = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
    switch ($key.VirtualKeyCode) {
        38 {
            $selectedIndex = ($selectedIndex + $options.Count - 1) % $options.Count
            Write-Menu
            continue
        }
        40 {
            $selectedIndex = ($selectedIndex + 1) % $options.Count
            Write-Menu
            continue
        }
        32 {
            $options[$selectedIndex].Selected = -not $options[$selectedIndex].Selected
            Write-Menu
            continue
        }
        13 {
            [string[]]$selectedIds = @($options | Where-Object { $_.Selected } | ForEach-Object { $_.Id })
            if ($selectedIds.Count -eq 0) {
                Write-Host ''
                Write-Host 'Select at least one test suite.'
                continue
            }

            Set-Content -LiteralPath $OutputFile -Value $selectedIds -Encoding utf8
            exit 0
        }
        27 {
            exit 1
        }
    }
}
