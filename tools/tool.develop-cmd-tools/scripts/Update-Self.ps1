param(
    [Parameter(Mandatory = $true)][string]$ToolRoot,
    [string]$Version,
    [string]$Repository = 'crafty-racoon/tool.develop-cmd-tools'
)

$ErrorActionPreference = 'Stop'
[string]$tool = [IO.Path]::GetFullPath($ToolRoot).TrimEnd('\', '/')
[string]$selectionFile = Join-Path ([IO.Path]::GetTempPath()) ("develop-cmd-tools-version-{0}.txt" -f [guid]::NewGuid().ToString('N'))
[string]$installerCopy = Join-Path ([IO.Path]::GetTempPath()) ("develop-cmd-tools-installer-{0}.ps1" -f [guid]::NewGuid().ToString('N'))
try {
    if ([string]::IsNullOrWhiteSpace($Version)) {
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $tool 'scripts/Select-Version.ps1') `
            -OutputFile $selectionFile -Repository $Repository
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $Version = (Get-Content -LiteralPath $selectionFile -Raw).Trim()
    }
    Copy-Item -LiteralPath (Join-Path $tool 'scripts/Install.ps1') -Destination $installerCopy
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installerCopy `
        -Destination $tool -Version $Version -Repository $Repository
    exit $LASTEXITCODE
} finally {
    Remove-Item -LiteralPath $selectionFile, $installerCopy -Force -ErrorAction SilentlyContinue
}
