[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ReleaseDirectory,

    [string]$SignToolPath,

    [string]$InstallerCompilerPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'ReleaseQualification.psm1') -Force

$parameters = @{
    ReleaseDirectory = $ReleaseDirectory
}
if ($PSBoundParameters.ContainsKey('SignToolPath')) {
    $parameters.SignToolPath = $SignToolPath
}
if ($PSBoundParameters.ContainsKey('InstallerCompilerPath')) {
    $parameters.InstallerCompilerPath = $InstallerCompilerPath
}

$report = Invoke-ReleaseQualification @parameters
$report

foreach ($limitation in $report.Limitations) {
    Write-Warning $limitation
}

