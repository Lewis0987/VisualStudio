<#
    CSV Demo generator - convenience wrapper.

    Rebuilds and runs tools\CsvDemoGenerator, which calls the production
    CsvLogger.Append(TestResult) directly. Because the tool has a ProjectReference
    to DX01_Common, any edit to DX01_Common\Services\CsvLogger.cs is recompiled
    automatically - the demo CSV always reflects the current format.

    Usage:
        powershell -ExecutionPolicy Bypass -File .\tools\GenerateCsvDemo.ps1
        powershell -ExecutionPolicy Bypass -File .\tools\GenerateCsvDemo.ps1 -Reset
        powershell -ExecutionPolicy Bypass -File .\tools\GenerateCsvDemo.ps1 -OutDir 'D:\somewhere\Logs'

    -Reset   delete today's demo CSV first, so the run produces a single fresh row
    -OutDir  override the output folder (default: <repo>\Demo\Logs)

    Does not launch DX01_ShortCircuitTester.exe and never touches the production
    Logs folder (DX01_ShortCircuitTester\bin\Debug\net48\Logs).
#>
[CmdletBinding()]
param(
    [switch]$Reset,
    [string]$OutDir
)

$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'CsvDemoGenerator'

$toolArgs = @()
if ($Reset)                             { $toolArgs += '--reset' }
if (-not [string]::IsNullOrEmpty($OutDir)) { $toolArgs += $OutDir }

& dotnet run --project $project -v quiet --nologo -- @toolArgs
exit $LASTEXITCODE
