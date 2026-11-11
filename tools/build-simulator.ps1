# Builds the frozen VB Simulator into tools/SimulatorBin (not into Simulator's Publish folder).
# The reference data of Dynamics.Tests is generated from this build by tools/ReferenceExport.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$simulator = Join-Path (Split-Path -Parent $root) 'Simulator\Simulator\Simulator.vbproj'
$out = Join-Path $PSScriptRoot 'SimulatorBin\'

dotnet build $simulator -c Release "-p:OutputPath=$out" -nologo -v q
