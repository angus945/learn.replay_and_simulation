$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$definitions = @{}
foreach ($file in (& rg --files (Join-Path $projectRoot 'Assets') -g '*.asmdef')) {
    $definition = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
    if ($definitions.ContainsKey($definition.name)) { throw "Duplicate assembly: $($definition.name)" }
    $definitions[$definition.name] = $definition
}
$visited = @{}
$active = @{}
function Visit-Assembly([string] $name) {
    if ($active[$name]) { throw "Assembly dependency cycle at $name" }
    if ($visited[$name]) { return }
    $active[$name] = $true
    foreach ($reference in $definitions[$name].references) {
        if ($definitions.ContainsKey($reference)) {
            Visit-Assembly $reference
            continue
        }
        if ($reference -notlike 'Unity.*') { throw "Unknown assembly reference: $name -> $reference" }
    }
    $active.Remove($name)
    $visited[$name] = $true
}
foreach ($name in $definitions.Keys) {
    $definition = $definitions[$name]
    Visit-Assembly $name
    if ($name -like '*.Tests') { continue }
    if ($name -like 'Module.*') {
        foreach ($reference in $definition.references) {
            if ($reference -notlike 'Module.*') { throw "Module depends outside modules: $name -> $reference" }
        }
    }
    if ($name -like 'Framework.*') {
        foreach ($reference in $definition.references) {
            if ($reference -like 'Game.*') { throw "Framework depends on game: $name -> $reference" }
        }
    }
    if ($name -like '*.Domain') {
        foreach ($reference in $definition.references) {
            if ($reference -like 'Framework.*' -or $reference -like 'Unity*' -or $reference -like '*.Application') {
                throw "Domain points outward: $name -> $reference"
            }
        }
    }
    if ($name -notlike '*.Unity' -and $name -notlike '*.Editor' -and $definition.noEngineReferences -ne $true) {
        throw "Pure assembly must disable engine references: $name"
    }
}
$assetGuids = @{}
foreach ($file in (& rg --files (Join-Path $projectRoot 'Assets') -g '*.meta')) {
    $guidLine = (Get-Content -LiteralPath $file | Select-String '^guid:').Line
    if ($guidLine -notmatch '^guid: ([0-9a-f]{32})$') { throw "Malformed asset GUID: $file" }
    $guid = $Matches[1]
    if ($assetGuids.ContainsKey($guid)) { throw "Duplicate asset GUID: $file and $($assetGuids[$guid])" }
    $assetGuids[$guid] = $file
}
Write-Output "PASS: $($definitions.Count) assembly definitions; acyclic, no Module->Framework/Game, no Framework->Game, pure assemblies disable engine references; $($assetGuids.Count) valid unique asset GUIDs."

# These APIs were retired after baseline 22f6966. Historical documents and fixtures are intentionally excluded.
$retiredApi = '\b(GameplaySession|GameplayRequest|TickReport|HashCheckpoint|FailureArtifact|ReplayArtifact|ReplayPlayback|ReplayPlaybackState|ReplayFile|ScenarioRerun|FailureRerun|RerunReport|RerunDifference|GameplayStateHasher|IGameplayControl|ISimulationControl|IRealtimeTickDriver|IActionResultReader|IGameplayCapabilities|GameplayCapabilities|ActionDescriptor|ActionLookupState|ActionLookup|ActionResultPage|SimulationDriveMode|IReplayPlayback|ITestSession|IStateObserver|Old_Simulation|ReplayableSimulationDefinition|TestableSimulationSession|TemplateReplay|TemplateRecording|TemplateRecordingIO|ITemplateGameplay|ITemplateSimulation|ITemplateAdmin|ITemplateResults|IDiagnosticReader|DiagnosticSnapshot|InvariantReport|TestRun|TestabilityRuntime|TestabilityBuilder)\b'
$activeSources = @(& rg --files (Join-Path $projectRoot 'Assets') (Join-Path $projectRoot 'tools') -g '*.cs' -g '*.csproj' -g '*.asmdef' -g '!**/obj/**' -g '!**/bin/**')
foreach ($source in $activeSources) {
    if ((Get-Content -LiteralPath $source -Raw) -match $retiredApi) { throw "Active source references retired API: $source ($($Matches[0]))" }
}
Write-Output "PASS: $($activeSources.Count) active source/project/assembly files contain no retired gameplay API or Old_Simulation reference."

# The reference application has real compiler boundaries, not only folder names.
$arenaAllowed = @{
    'Game.Arena.Domain' = @()
    'Game.Arena.Application' = @('Game.Arena.Domain')
    'Game.Arena.Infrastructure' = @('Game.Arena.Domain','Game.Arena.Application','Module.SeededRandom','Module.SimulationObjectRegistry')
    'Game.Arena.Integration' = @('Game.Arena.Domain','Game.Arena.Application','Game.Arena.Infrastructure','Framework.DeterministicSimulation','Module.SimulationPrimitives','Module.Verification.Invariant','Module.Verification.TraceBuffer','Module.Verification.Diagnostics','Module.Verification.RuntimeControl','Module.Verification.StateSnapshot','Module.Verification.Oracle','Module.Verification.SystemFact','Module.Verification.SystemFact.Observability')
    'Game.Arena.Composition' = @('Game.Arena.Domain','Game.Arena.Application','Game.Arena.Integration','Framework.DeterministicSimulation','framework.deterministic-playback','Module.SimulationPrimitives','Module.Verification.Invariant','Module.Verification.TraceBuffer','Module.TickInputBuffer','Module.Verification.Diagnostics','Module.Verification.RuntimeControl','Module.Verification.StateSnapshot','Module.Verification.Oracle','Module.Verification.Evidence','Module.Verification.SystemFact','Module.Verification.SystemFact.Observability')
}
foreach ($arenaName in $arenaAllowed.Keys) {
    if (-not $definitions.ContainsKey($arenaName)) { throw "Missing Arena assembly: $arenaName" }
    foreach ($arenaReference in $definitions[$arenaName].references) {
        if ($arenaReference -notin $arenaAllowed[$arenaName]) { throw "Arena dependency points outward: $arenaName -> $arenaReference" }
    }
    $arenaProject = Join-Path $projectRoot "tools/arena-build/$arenaName/$arenaName.csproj"
    [xml]$arenaProjectXml = Get-Content -LiteralPath $arenaProject -Raw
    $arenaProjectDirectory = Split-Path -Parent $arenaProject
    $arenaProjectRefs = @($arenaProjectXml.Project.ItemGroup.ProjectReference | Where-Object { $_ -and $_.Include } | ForEach-Object {
        $referenceProject = Join-Path $arenaProjectDirectory $_.Include
        [xml]$referenceProjectXml = Get-Content -LiteralPath $referenceProject -Raw
        [string]$referenceAssemblyName = @($referenceProjectXml.Project.PropertyGroup.AssemblyName | Where-Object { $_ })[0]
        if ([string]::IsNullOrWhiteSpace($referenceAssemblyName)) { [IO.Path]::GetFileNameWithoutExtension($_.Include) } else { $referenceAssemblyName }
    })
    $arenaAssemblyRefs = @($definitions[$arenaName].references | Where-Object { $_ })
    if (($arenaProjectRefs | Sort-Object) -join ',' -cne (($arenaAssemblyRefs | Sort-Object) -join ',')) { throw "Headless/Unity dependency mismatch: $arenaName" }
}
foreach ($arenaFile in (& rg --files (Join-Path $projectRoot 'Assets/game/arena') (Join-Path $projectRoot 'tools/arena-checks') -g '*.cs' -g '!**/obj/**' -g '!**/bin/**')) {
    if ((Get-Content -LiteralPath $arenaFile -Raw) -match '\bvar\s+\w+\s*(=|in\b)') { throw "Arena C# requires explicit variable types: $arenaFile" }
}
Write-Output 'PASS: Arena inner-layer allowlist, matching Unity/headless project references, explicit C# variable types.'

# Verification capability family contract freeze.
$verificationNames = @('Module.Verification.SystemFact','Module.Verification.SystemFact.Observability','Module.Verification.StateSnapshot','Module.Verification.RuntimeControl','Module.Verification.Oracle','Module.Verification.Invariant','Module.Verification.Diagnostics','Module.Verification.TraceBuffer','Module.Verification.Evidence')
$verificationRoot = Join-Path $projectRoot 'Assets/modules/module.verification'
$actualVerificationNames = @(Get-ChildItem -LiteralPath $verificationRoot -Directory | ForEach-Object { $_.Name })
if (($actualVerificationNames | Sort-Object) -join ',' -cne (($verificationNames | Sort-Object) -join ',')) {
    throw "Verification repository taxonomy differs from the frozen eight-capability set: $($actualVerificationNames -join ', ')"
}

$verificationAssemblies = @{
    'Module.Verification.SystemFact' = @()
    'Module.Verification.SystemFact.Observability' = @('Module.Verification.SystemFact')
    'Module.Verification.StateSnapshot' = @()
    'Module.Verification.RuntimeControl' = @()
    'Module.Verification.Oracle' = @()
    'Module.Verification.Invariant' = @()
    'Module.Verification.Diagnostics' = @()
    'Module.Verification.TraceBuffer' = @()
    'Module.Verification.Evidence' = @()
}
foreach ($verificationAssembly in $verificationAssemblies.Keys) {
    if (-not $definitions.ContainsKey($verificationAssembly)) { throw "Missing frozen verification assembly: $verificationAssembly" }
    $actualReferences = @($definitions[$verificationAssembly].references | Where-Object { $_ })
    $expectedReferences = @($verificationAssemblies[$verificationAssembly])
    if (($actualReferences | Sort-Object) -join ',' -cne (($expectedReferences | Sort-Object) -join ',')) { throw "Frozen verification module has unexpected dependencies: $verificationAssembly" }
}

$verificationSources = @(& rg --files $verificationRoot -g 'Module.Verification.*/**/*.cs' -g '!**/tests/**' -g '!**/obj/**' -g '!**/bin/**')
$legacyVerificationNamespace = '\b(Module\.SystemFacts|Module\.RuntimeObservation|Module\.RuntimeControl|Module\.TestabilityOracles|Module\.TestabilityEvidence|Module\.Diagnostics|Module\.InvariantChecks|Module\.TraceBuffering|InvariantChecks)\b'
$productContamination = '\b(Game\.Arena|UnityEngine|Framework\.DeterministicPlayback|Framework\.DeterministicSimulation|Narrative)\b'
$duplicateContract = '\b(FactRecord|FactEntry|FactEnvelope|FactEvent|FactMessage|StateSnapshotCaptureMetadata|TraceRecord|TraceBatch|EvidenceOperationState|EvidenceFact|EvidenceSnapshot|EvidenceDiagnostic|EvidenceOracleResult)\b'
foreach ($verificationSource in $verificationSources) {
    $verificationText = Get-Content -LiteralPath $verificationSource -Raw
    if ($verificationText -match $legacyVerificationNamespace) { throw "Verification source uses a retired namespace: $verificationSource ($($Matches[0]))" }
    if ($verificationText -match $productContamination) { throw "Verification base module contains product/framework contamination: $verificationSource ($($Matches[0]))" }
    if ($verificationText -match $duplicateContract) { throw "Verification source reintroduces a frozen duplicate contract: $verificationSource ($($Matches[0]))" }
}
Write-Output "PASS: verification taxonomy, namespaces, dependency boundaries, product neutrality and duplicate-contract freeze."
