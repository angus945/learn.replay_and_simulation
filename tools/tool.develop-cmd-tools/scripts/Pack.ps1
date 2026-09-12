param(
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'artifacts/tool.develop-cmd-tools')
)

$ErrorActionPreference = 'Stop'
[string]$toolRoot = Split-Path -Parent $PSScriptRoot
[object]$metadata = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'version.json') -Raw | ConvertFrom-Json
[string]$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
[string]$archivePath = Join-Path $outputRoot ("{0}-{1}.zip" -f $metadata.name, $metadata.version)
[string]$checksumPath = "$archivePath.sha256"
[string]$stagingRoot = Join-Path ([IO.Path]::GetTempPath()) ("develop-cmd-tools-pack-{0}" -f [guid]::NewGuid().ToString('N'))

try {
    [string]$stagedTool = Join-Path $stagingRoot 'tool.develop-cmd-tools'
    New-Item -ItemType Directory -Path $stagedTool, $outputRoot -Force | Out-Null
    Get-ChildItem -LiteralPath $toolRoot -Force | Where-Object {
        $_.Name -notin @('artifacts', '.git')
    } | Copy-Item -Destination $stagedTool -Recurse -Force
    [string]$localConfig = Join-Path $stagedTool 'tool-config.json'
    if (Test-Path -LiteralPath $localConfig) { Remove-Item -LiteralPath $localConfig -Force }
    Compress-Archive -LiteralPath $stagedTool -DestinationPath $archivePath -CompressionLevel Optimal -Force
    [string]$hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath $checksumPath -Value "$hash  $([IO.Path]::GetFileName($archivePath))" -Encoding ascii
    Write-Host "Created: $archivePath"
    Write-Host "SHA256:  $hash"
} finally {
    if (Test-Path -LiteralPath $stagingRoot) { Remove-Item -LiteralPath $stagingRoot -Recurse -Force }
}
