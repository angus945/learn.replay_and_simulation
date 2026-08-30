[CmdletBinding()]
param(
    [string] $RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$issues = [System.Collections.Generic.List[string]]::new()
$documents = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$statistics = @{ Links = 0; Tables = 0; CSharpBlocks = 0 }

function Add-RequiredDocument([string] $relativePath) {
    $absolutePath = Join-Path $projectRoot $relativePath
    if (Test-Path -LiteralPath $absolutePath -PathType Leaf) {
        [void] $documents.Add([System.IO.Path]::GetFullPath($absolutePath))
    }
    else {
        $issues.Add("${relativePath}: required active document is missing")
    }
}

function Test-LocalLink([string] $documentPath, [int] $lineNumber, [string] $destination) {
    $target = $destination.Trim('<', '>')
    # Remote URLs are not fetched. Fragment-only links and anchors are not validated.
    if ($target.StartsWith('#') -or $target.StartsWith('//') -or
        ($target -match '^[A-Za-z][A-Za-z0-9+.-]*:' -and $target -notmatch '^[A-Za-z]:[\\/]')) { return }
    $target = ($target -split '[?#]', 2)[0]
    if ([string]::IsNullOrWhiteSpace($target)) { return }
    $statistics.Links++
    try {
        $target = [System.Uri]::UnescapeDataString($target)
        $target = [regex]::Replace($target, '\\([\\`*_{}\[\]()#+\-.! ])', '$1')
        $resolved = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine(
            [System.IO.Path]::GetDirectoryName($documentPath), $target))
        # Existing directory links are valid navigation targets too.
        if (-not (Test-Path -LiteralPath $resolved)) {
            $issues.Add("${documentPath}:${lineNumber}: missing local target '$destination'")
        }
    }
    catch {
        $issues.Add("${documentPath}:${lineNumber}: invalid local target '$destination': $($_.Exception.Message)")
    }
}

function Test-CSharpBlock([string] $documentPath, [int] $firstLine, [string[]] $blockLines) {
    $statistics.CSharpBlocks++
    $code = $blockLines -join "`n"
    # Skip comments, ordinary/verbatim strings and character literals, then flag the var token.
    $tokens = '(?s:/\*.*?\*/)|//[^\r\n]*|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|''(?:\\.|[^''\\])*''|\bvar\b'
    foreach ($token in [regex]::Matches($code, $tokens)) {
        if ($token.Value -ne 'var') { continue }
        $offset = [regex]::Matches($code.Substring(0, $token.Index), "`n").Count
        $issues.Add("${documentPath}:$($firstLine + $offset): C# example must use an explicit type, not var")
    }
}

Add-RequiredDocument 'README.md'
Add-RequiredDocument 'tools/arena-checks/README.md'
Add-RequiredDocument 'tools/framework-checks/README.md'
Add-RequiredDocument 'Assets/game/arena/README.md'
Add-RequiredDocument 'docs/verification/arena-rebuild-2026-08-30.md'

$assetsPath = Join-Path $projectRoot 'Assets'
foreach ($document in Get-ChildItem -LiteralPath $assetsPath -Filter README.md -Recurse -File) {
    if ($document.FullName -match '[\\/](OldSimulation|Old_Simulation)[\\/]') { continue }
    [void] $documents.Add($document.FullName)
}
$guidePath = Join-Path $projectRoot 'docs/arena-guide'
if (Test-Path -LiteralPath $guidePath -PathType Container) {
    foreach ($document in Get-ChildItem -LiteralPath $guidePath -Filter '*.md' -File) {
        [void] $documents.Add($document.FullName)
    }
}
else { $issues.Add('docs/arena-guide: active guide directory is missing') }

# Inline links support angle-bracket paths, escaped characters, one nested path-parenthesis,
# and optional titles. Reference-style definitions are checked at their definition line.
$inlineLinks = '\]\(\s*(?<target><[^>\r\n]+>|(?:\\.|[^()\s])+(?:\((?:\\.|[^()\s])*\)(?:\\.|[^()\s])*)*)\s*(?:(?:"[^"]*"|''[^'']*''|\([^)]*\))\s*)?\)'
$referenceLink = '^\s{0,3}\[[^\]]+\]:\s*(?<target><[^>]+>|\S+)'
$arenaReadme = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'Assets/game/arena/README.md'))
$guidePrefix = [System.IO.Path]::GetFullPath($guidePath).TrimEnd([char[]] '\/') + [System.IO.Path]::DirectorySeparatorChar

foreach ($documentPath in $documents | Sort-Object) {
    $lines = @(Get-Content -LiteralPath $documentPath)
    $arenaDocument = $documentPath.Equals($arenaReadme, [System.StringComparison]::OrdinalIgnoreCase) -or
        $documentPath.StartsWith($guidePrefix, [System.StringComparison]::OrdinalIgnoreCase)
    $fence = $null
    $csharp = $false
    $blockStart = 0
    $blockLines = [System.Collections.Generic.List[string]]::new()
    $prose = [bool[]]::new($lines.Count)

    for ($index = 0; $index -lt $lines.Count; $index++) {
        $line = $lines[$index]
        if ($null -ne $fence) {
            if ($line -match ('^\s*' + [regex]::Escape($fence.Substring(0, 1)) + '{' + $fence.Length + ',}\s*$')) {
                if ($arenaDocument -and $csharp) { Test-CSharpBlock $documentPath $blockStart $blockLines.ToArray() }
                $fence = $null
                $blockLines.Clear()
            }
            elseif ($csharp) { $blockLines.Add($line) }
            continue
        }
        if ($line -match '^\s*(`{3,}|~{3,})\s*(.*)$') {
            $fence = $Matches[1]
            $csharp = $Matches[2].Trim() -match '^(csharp|cs|c#)(\s|$)'
            $blockStart = $index + 2
            continue
        }
        $prose[$index] = $true
        foreach ($link in [regex]::Matches($line, $inlineLinks)) {
            Test-LocalLink $documentPath ($index + 1) $link.Groups['target'].Value
        }
        if ($line -match $referenceLink) { Test-LocalLink $documentPath ($index + 1) $Matches['target'] }
    }
    if ($arenaDocument -and $csharp -and $null -ne $fence) {
        Test-CSharpBlock $documentPath $blockStart $blockLines.ToArray()
    }

    if (-not $arenaDocument) { continue }
    $checkedRows = [System.Collections.Generic.HashSet[int]]::new()
    for ($index = 1; $index -lt $lines.Count; $index++) {
        if (-not $prose[$index] -or -not $prose[$index - 1] -or $lines[$index] -notmatch '\|') { continue }
        $cells = $lines[$index].Trim().Trim('|') -split '(?<!\\)\|'
        if (@($cells | Where-Object { $_ -notmatch '^\s*:?-{3,}:?\s*$' }).Count -gt 0) { continue }
        if ($lines[$index - 1] -notmatch '\|') { continue }
        # A delimiter identifies a Markdown table, not a C# bitwise operator or prose pipe.
        for ($row = $index - 1; $row -lt $lines.Count -and $prose[$row] -and $lines[$row] -match '\|'; $row++) {
            if (-not $checkedRows.Add($row)) { continue }
            $statistics.Tables++
            if ($lines[$row].Length -gt 120) {
                $issues.Add("${documentPath}:$($row + 1): Arena Markdown table row is $($lines[$row].Length) characters (maximum 120)")
            }
        }
    }
}

if ($issues.Count -gt 0) {
    foreach ($issue in $issues) { [Console]::Error.WriteLine("FAIL: $issue") }
    [Console]::Error.WriteLine("FAIL: $($issues.Count) issue(s); $($documents.Count) documents, $($statistics.Links) local links, $($statistics.CSharpBlocks) Arena C# blocks checked.")
    exit 1
}
Write-Output "PASS: $($documents.Count) active documents; $($statistics.Links) local links; $($statistics.Tables) Arena table rows <=120 characters; $($statistics.CSharpBlocks) Arena C# blocks use explicit types. Remote URLs and anchors are not checked."
