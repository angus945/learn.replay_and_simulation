$ErrorActionPreference = 'Stop'

[string[]]$options = @(
    'Verify dependency revisions',
    'Skip dependency revision verification',
    'Update all checked-out dependency pins to current commits, then verify'
)
[int]$selectedIndex = 0

function Write-Menu {
    Clear-Host
    Write-Host 'Dependency preparation'
    Write-Host ''
    for ([int]$index = 0; $index -lt $options.Count; $index++) {
        [string]$prefix = if ($index -eq $selectedIndex) { '>' } else { ' ' }
        Write-Host "$prefix $($options[$index])"
    }

    Write-Host ''
    Write-Host 'Use Up/Down Arrow and Enter to continue.'
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
        13 {
            if ($selectedIndex -eq 1) {
                exit 10
            }

            if ($selectedIndex -eq 2) {
                exit 11
            }

            exit 0
        }
    }
}
