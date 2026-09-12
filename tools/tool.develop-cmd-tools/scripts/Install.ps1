param(
    [string]$ProjectRoot,
    [string]$Destination,
    [string]$Version,
    [string]$PackagePath,
    [string]$Repository = 'crafty-racoon/tool.develop-cmd-tools'
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Destination)) {
    if ([string]::IsNullOrWhiteSpace($ProjectRoot)) { throw 'ProjectRoot or Destination is required.' }
    [string]$project = (Resolve-Path -LiteralPath $ProjectRoot).Path
    $Destination = Join-Path $project 'tools/tool.develop-cmd-tools'
}
[string]$destination = [IO.Path]::GetFullPath($Destination).TrimEnd('\', '/')
[string]$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ("develop-cmd-tools-install-{0}" -f [guid]::NewGuid().ToString('N'))

try {
    New-Item -ItemType Directory -Path $temporaryRoot -Force | Out-Null
    [string]$archive = Join-Path $temporaryRoot 'package.zip'
    [string]$source = ''
    if ([string]::IsNullOrWhiteSpace($PackagePath)) {
        [string]$reference = if ([string]::IsNullOrWhiteSpace($Version)) { 'refs/heads/main' } else { "refs/tags/v$Version" }
        [string]$url = "https://github.com/$Repository/archive/$reference.zip"
        Write-Host "Downloading $url"
        try {
            Invoke-WebRequest -Uri $url -OutFile $archive -UseBasicParsing
        } catch {
            if ($null -eq (Get-Command gh -ErrorAction SilentlyContinue)) {
                throw 'Anonymous download failed. Install and authenticate GitHub CLI to access a private repository.'
            }
            if (-not [string]::IsNullOrWhiteSpace($Version)) {
                Write-Host "Using authenticated GitHub Release download for v$Version"
                & gh release download "v$Version" --repo $Repository --pattern 'tool.develop-cmd-tools-*.zip' --dir $temporaryRoot
                if ($LASTEXITCODE -ne 0) { throw "Could not download release v$Version from $Repository." }
                [string]$downloadedArchive = @(Get-ChildItem -LiteralPath $temporaryRoot -Filter 'tool.develop-cmd-tools-*.zip')[0].FullName
                Move-Item -LiteralPath $downloadedArchive -Destination $archive
            } else {
                Write-Host 'Using authenticated Git clone for the main branch'
                [string]$cloneRoot = Join-Path $temporaryRoot 'repository'
                & git clone --depth 1 "https://github.com/$Repository.git" $cloneRoot
                if ($LASTEXITCODE -ne 0) { throw "Could not clone $Repository." }
                $source = $cloneRoot
            }
        }
    } else {
        Copy-Item -LiteralPath (Resolve-Path -LiteralPath $PackagePath).Path -Destination $archive
    }

    if ([string]::IsNullOrWhiteSpace($source)) {
        [string]$expanded = Join-Path $temporaryRoot 'expanded'
        Expand-Archive -LiteralPath $archive -DestinationPath $expanded -Force
        $source = @(
            Get-ChildItem -LiteralPath $expanded -Directory -Recurse |
                Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'scripts/version.json') -PathType Leaf }
        )[0].FullName
    }
    if ([string]::IsNullOrWhiteSpace($source)) { throw 'The package does not contain tool.develop-cmd-tools.' }

    [string]$savedConfig = Join-Path $temporaryRoot 'tool-config.json'
    [string]$currentConfig = Join-Path $destination 'tool-config.json'
    if (Test-Path -LiteralPath $currentConfig -PathType Leaf) { Copy-Item -LiteralPath $currentConfig -Destination $savedConfig }
    if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination -Recurse -Force }
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Recurse
    if (Test-Path -LiteralPath $savedConfig -PathType Leaf) {
        Copy-Item -LiteralPath $savedConfig -Destination (Join-Path $destination 'tool-config.json')
    } else {
        Copy-Item -LiteralPath (Join-Path $destination 'scripts/develop-cmd-tools.json') `
            -Destination (Join-Path $destination 'tool-config.json')
    }
    [object]$installed = Get-Content -LiteralPath (Join-Path $destination 'scripts/version.json') -Raw | ConvertFrom-Json
    Write-Host "Installed $($installed.name) $($installed.version) to $destination"
} finally {
    if (Test-Path -LiteralPath $temporaryRoot) { Remove-Item -LiteralPath $temporaryRoot -Recurse -Force }
}
