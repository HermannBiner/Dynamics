# Builds the VB Simulator at its reference state (tag dynamics-reference = commit 200c300) into tools/SimulatorBin
# and copies the data files the reference export needs (Data/LSystemsEN.json) next to it.
# The reference data of Dynamics.Tests is generated from this build by tools/ReferenceExport.
#
# Simulator is developed further independently; the source of the tag is extracted with git archive into
# tools/SimulatorSource, so the working tree of the Simulator repository is neither used nor changed.
#
# The Simulator repository is found by, in this order:
#   -SimulatorRepo <path>
#   the environment variable SIMULATOR_REPO
#   ..\..\Simulator 8.0 GitHubRep\Simulator relative to the Dynamics repository (the layout of the author)

param(
    [string]$SimulatorRepo,
    [string]$Reference = 'dynamics-reference'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $SimulatorRepo) { $SimulatorRepo = $env:SIMULATOR_REPO }
if (-not $SimulatorRepo) { $SimulatorRepo = Join-Path $root '..\..\Simulator 8.0 GitHubRep\Simulator' }
if (-not (Test-Path (Join-Path $SimulatorRepo '.git'))) {
    throw "Simulator repository not found: $SimulatorRepo. Pass -SimulatorRepo <path> or set SIMULATOR_REPO."
}

git -C $SimulatorRepo rev-parse --verify --quiet "$Reference^{commit}" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Reference '$Reference' not found in $SimulatorRepo (git fetch --tags?)" }

# Source of the reference state
$source = Join-Path $PSScriptRoot 'SimulatorSource'
if (Test-Path $source) { Remove-Item -Recurse -Force $source }
New-Item -ItemType Directory $source | Out-Null
$zip = Join-Path $PSScriptRoot 'SimulatorSource.zip'
git -C $SimulatorRepo archive --format=zip -o $zip $Reference
if ($LASTEXITCODE -ne 0) { throw "git archive failed" }
Expand-Archive -Path $zip -DestinationPath $source
Remove-Item $zip

# Without a trailing backslash: in Windows PowerShell 5.1 it would escape the closing quote of the argument
$out = Join-Path $PSScriptRoot 'SimulatorBin'
dotnet build (Join-Path $source 'Simulator\Simulator.vbproj') -c Release "-p:OutputPath=$out" -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Build of Simulator failed" }

$data = Join-Path $out 'Data'
New-Item -ItemType Directory -Force $data | Out-Null
Copy-Item (Join-Path $source 'Simulator\Data\LSystemsEN.json') $data -Force
Write-Host "Simulator $Reference built into $out"
