param(
    [Parameter(Mandatory = $true)][string]$OutputFile,
    [string]$Repository = 'crafty-racoon/tool.develop-cmd-tools'
)

$ErrorActionPreference = 'Stop'
if ($null -ne (Get-Command gh -ErrorAction SilentlyContinue)) {
    [object[]]$releases = @(& gh release list --repo $Repository --limit 100 `
        --json tagName,publishedAt,isLatest | ConvertFrom-Json)
    if ($LASTEXITCODE -ne 0) { throw "Could not list releases from $Repository." }
} else {
    [object[]]$releases = @(Invoke-RestMethod -Uri "https://api.github.com/repos/$Repository/releases?per_page=100")
}
[object[]]$options = @($releases | Sort-Object {
    if ($_.publishedAt) { [datetime]$_.publishedAt } else { [datetime]$_.published_at }
} -Descending | ForEach-Object {
    [string]$tag = if ($_.tagName) { $_.tagName } else { $_.tag_name }
    [pscustomobject]@{ Tag = $tag; Version = $tag -replace '^v', '' }
})
if ($options.Count -eq 0) { throw "No releases were found in $Repository." }

[int]$selectedIndex = 0
while ($true) {
    Clear-Host
    Write-Host 'Select tool version' -ForegroundColor Cyan
    Write-Host 'Use Up/Down and Enter. Esc cancels.'
    Write-Host ''
    for ([int]$index = 0; $index -lt $options.Count; $index++) {
        [string]$prefix = if ($index -eq $selectedIndex) { '>' } else { ' ' }
        Write-Host "$prefix $($options[$index].Tag)"
    }
    [ConsoleKey]$key = [Console]::ReadKey($true).Key
    switch ($key) {
        ([ConsoleKey]::UpArrow) { $selectedIndex = ($selectedIndex + $options.Count - 1) % $options.Count }
        ([ConsoleKey]::DownArrow) { $selectedIndex = ($selectedIndex + 1) % $options.Count }
        ([ConsoleKey]::Enter) {
            Set-Content -LiteralPath $OutputFile -Value $options[$selectedIndex].Version -Encoding ascii
            exit 0
        }
        ([ConsoleKey]::Escape) { exit 1 }
    }
}
