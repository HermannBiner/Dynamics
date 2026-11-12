# Builds the frozen VB Simulator into tools/SimulatorBin (not into Simulator's Publish folder) and copies the
# data files the reference export needs (Data/LSystemsEN.json) next to it.
# The reference data of Dynamics.Tests is generated from this build by tools/ReferenceExport.
#
# The Simulator repository is found by, in this order:
#   -SimulatorRepo <path>
#   the environment variable SIMULATOR_REPO
#   ..\..\Simulator 8.0 GitHubRep\Simulator relative to the Dynamics repository (the layout of the author)

param([string]$SimulatorRepo)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $SimulatorRepo) { $SimulatorRepo = $env:SIMULATOR_REPO }
if (-not $SimulatorRepo) { $SimulatorRepo = Join-Path $root '..\..\Simulator 8.0 GitHubRep\Simulator' }

$project = Join-Path $SimulatorRepo 'Simulator\Simulator.vbproj'
if (-not (Test-Path $project)) {
    throw "Simulator not found: $project. Pass -SimulatorRepo <path of the Simulator repository> or set SIMULATOR_REPO."
}

# Without a trailing backslash: in Windows PowerShell 5.1 it would escape the closing quote of the argument
$out = Join-Path $PSScriptRoot 'SimulatorBin'
dotnet build $project -c Release "-p:OutputPath=$out" -nologo -v q

$data = Join-Path $out 'Data'
New-Item -ItemType Directory -Force $data | Out-Null
Copy-Item (Join-Path $SimulatorRepo 'Simulator\Data\LSystemsEN.json') $data -Force
