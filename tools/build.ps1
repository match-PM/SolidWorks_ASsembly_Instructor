param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [switch]$Restore,
    [switch]$CoreOnly
)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$msbuildCommand = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
if ($msbuildCommand) {
    $msbuildPath = $msbuildCommand.Source
} else {
    $vswherePath = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    if (-not (Test-Path -LiteralPath $vswherePath)) { throw 'Install Visual Studio with MSBuild and the .NET Framework 4.8 targeting pack.' }
    $msbuildPath = & $vswherePath -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    if (-not $msbuildPath) { throw 'MSBuild was not found.' }
}
$target = if ($CoreOnly) { 'tests/Swasi.Core.Tests/Swasi.Core.Tests.csproj' } else { 'SolidWorks_ASsembly_Instructor.sln' }
$buildArguments = @((Join-Path $workspace $target), '/t:Build', "/p:Configuration=$Configuration", '/v:minimal', '/nologo')
if ($Restore) { $buildArguments += @('/restore', '/p:RestorePackagesConfig=true') }
& $msbuildPath @buildArguments
if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)." }
& (Join-Path $workspace "tests/Swasi.Core.Tests/bin/$Configuration/Swasi.Core.Tests.exe")
if ($LASTEXITCODE -ne 0) { throw 'Core regression tests failed.' }
& python -m unittest discover -s (Join-Path $workspace 'tests') -p 'test_*.py'
if ($LASTEXITCODE -ne 0) { throw 'Python regression tests failed.' }
