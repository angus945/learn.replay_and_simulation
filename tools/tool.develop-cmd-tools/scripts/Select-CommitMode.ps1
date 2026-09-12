$ErrorActionPreference = 'Stop'

[string[]]$options = @(
    'Commit all Git changes before testing',
    'Keep uncommitted changes and continue'
)
[int]$selectedIndex = 0

function Write-Menu {
    Clear-Host
    Write-Host 'Git changes'
    Write-Host ''
    for ([int]$index = 0; $index -lt $options.Count; $index++) {
        [string]$prefix = if ($index -eq $selectedIndex) { '>' } else { ' ' }
        Write-Host "$prefix $($options[$index])"
    }

    Write-Host ''
    Write-Host 'Use Up/Down Arrow and Enter to continue. Esc cancels.'
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

            exit 0
        }
        27 {
            exit 1
        }
    }
}
