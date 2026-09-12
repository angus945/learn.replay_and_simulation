param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [string]$Version,
    [string]$Repository = 'crafty-racoon/tool.develop-cmd-tools'
)

$ErrorActionPreference = 'Stop'
[string]$selectionFile = Join-Path ([IO.Path]::GetTempPath()) ("develop-cmd-tools-version-{0}.txt" -f [guid]::NewGuid().ToString('N'))
try {
    if ([string]::IsNullOrWhiteSpace($Version)) {
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Select-Version.ps1') `
            -OutputFile $selectionFile -Repository $Repository
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $Version = (Get-Content -LiteralPath $selectionFile -Raw).Trim()
    }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Install.ps1') `
        -ProjectRoot $ProjectRoot -Version $Version -Repository $Repository
    exit $LASTEXITCODE
} finally {
    Remove-Item -LiteralPath $selectionFile -Force -ErrorAction SilentlyContinue
}
