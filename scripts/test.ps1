param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
Push-Location (Join-Path $PSScriptRoot '..')
try {
    dotnet restore Cutsy.slnx --locked-mode
    dotnet run --project tests/Cutsy.Core.Tests -c $Configuration --no-restore
} finally {
    Pop-Location
}
