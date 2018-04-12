<#
  .Synopsis
  Script for running commands (restore, build, test, pack, ...) in repositories utilizing dotnet/arcade

  .Parameter configuration
  Set the MSBuild Configuration

  .Parameter verbosity
  Set the MSBuild Verbosity

  .Parameter help
  Print this help and exit

  .Parameter projects
  List of projects to build

  .Parameter log
  Produce an MSBuild binary log

  .Parameter restore
  Restore dependencies

  .Parameter build
  Build Solution

  .Parameter rebuild
  Rebuild Solution

  .Parameter deploy
  Deploy build VSIXes

  .Parameter deployDeps
  Deploy dependencies (Roslyn VSIXes for integration tests)

  .Parameter test
  Run all unit tests in the solution

  .Parameter integrationTest
  Run all integration tests in the solution

  .Parameter sign
  Sign build outputs

  .Parameter pack
  Package build outputs into nuget packages

  .Parameter ci
  Set when running on CI machine

  .Parameter prepareMachine
  Prepare machine for CI run

  .Notes
  Additional arguments are passed through to MSBuild
#>
[CmdletBinding(PositionalBinding=$false)]
Param(
  [ValidateSet("Debug", "release")]
  [string] $configuration = "Debug",
  [string] $projects = "",
  [ValidateSet("quiet", "minimal", "normal", "detailed", "diagnostic")]
  [string] $verbosity = "minimal",
  [switch] $help,
  [switch] $log,
  [switch] $restore,
  [switch] $deployDeps,
  [switch] $build,
  [switch] $rebuild,
  [switch] $deploy,
  [switch] $test,
  [switch] $integrationTest,
  [switch] $sign,
  [switch] $pack,
  [switch] $ci,
  [switch] $prepareMachine,
  [Parameter(ValueFromRemainingArguments=$true)][String[]]$AdditionalArguments
)

if ($Help) {
  Get-Help $MyInvocation.MyCommand.Definition
  exit 0
}

set-strictmode -version 2.0
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Definition
Import-Module $here/helpers.psm1 3>$null 4>$null -Force

# This is a temporary workaround for https://github.com/Microsoft/msbuild/issues/2095 and
# https://github.com/dotnet/cli/issues/6589
# Currently, SDK's always get resolved to the global location, but we want our packages
# to all be installed into a local folder (prevent machine contamination from global state).
# 
# We are restoring all of our packages locally and setting NuGetPackageRoot to reference the
# local location, but this breaks Custom SDK's which are expecting the SDK to be available
# from the global user folder.
function MakeGlobalSdkAvailableLocal {
  $RepoToolsetSource = Join-Path $DefaultNuGetPackageRoot "roslyntools.repotoolset\$ToolsetVersion\"
  $RepoToolsetDestination = Join-Path $NuGetPackageRoot "roslyntools.repotoolset\$ToolsetVersion\"
  if (!(Test-Path $RepoToolsetDestination)) {
    Copy-Item $RepoToolsetSource -Destination $RepoToolsetDestination -Recurse
  }
}
function InstallToolset {
  param(
    [ScriptBlock] $msbuild
  )

  if (!(Test-Path $ToolsetBuildProj)) {
    New-Directory $TempDir

    $proj = Join-Path $TempDir "_restore.proj"
    '<Project Sdk="RoslynTools.RepoToolset"><Target Name="NoOp"/></Project>' | Set-Content $proj
    & $msbuild $proj /t:NoOp /clp:None /p:__ExcludeSdkImports=true
  }
}

function Build {
  param(
    [ScriptBlock] $msbuild
  )

  if ($env:OfficialBuildId) {
    MakeGlobalSdkAvailableLocal
  }

  $buildArgs = @(
    "/clp:Summary",
    "/p:Configuration=$configuration",
    "/p:RepoRoot=$RepoRoot",
    "/p:Projects=$projects",
    "/p:Restore=$restore",
    "/p:DeployDeps=$deployDeps",
    "/p:Build=$build",
    "/p:Rebuild=$rebuild",
    "/p:Deploy=$deploy",
    "/p:Test=$test",
    "/p:IntegrationTest=$integrationTest",
    "/p:Sign=$sign",
    "/p:Pack=$pack",
    "/p:CIBuild=$ci"
  )

  if ($ci -or $log) {
    New-Directory $LogDir
    $buildArgs += "/bl:$(Join-Path $LogDir "Build.binlog")"
  }

  & $msbuild $ToolsetBuildProj @buildArgs @AdditionalArguments
}

function Stop-Processes() {
  Write-Host "Killing running build processes..."
  Get-Process -Name "msbuild" -ErrorAction SilentlyContinue | Stop-Process
  Get-Process -Name "vbcscompiler" -ErrorAction SilentlyContinue | Stop-Process
}

try {
  $GlobalJsonFile = Get-FileAbove $here "global.json"
  $RepoRoot = Split-Path -Parent $GlobalJsonFile
  $GlobalJson = Get-Content $GlobalJsonFile | ConvertFrom-Json
  $DotNetRoot = Join-Path $RepoRoot ".\.dotnet"
  $ArtifactsDir = Join-Path $RepoRoot "artifacts"
  $LogDir = Join-Path (Join-Path $ArtifactsDir $configuration) "log"
  $TempDir = Join-Path (Join-Path $ArtifactsDir $configuration) "tmp"

  $env:DOTNET_MULTILEVEL_LOOKUP = 0
  $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "true"

  if ($projects -eq "") {
    $projects = Join-Path $RepoRoot "**\*.sln"
  }

  if ($env:NUGET_PACKAGES -ne $null) {
    $NuGetPackageRoot = $env:NUGET_PACKAGES.TrimEnd("\") + "\"
    $DefaultNuGetPackageRoot = $NuGetPackageRoot
  } else {
    if ($env:OfficialBuildId) {
      $NuGetPackageRoot = Join-Path $RepoRoot "packages\"
    } else {
      $NuGetPackageRoot = Join-Path $env:UserProfile ".nuget\packages\"
    }
    $DefaultNuGetPackageRoot = Join-Path $env:UserProfile ".nuget\packages\"
  }

  $ToolsetVersion = $GlobalJson.'msbuild-sdks'.'RoslynTools.RepoToolset'
  $ToolsetBuildProj = Join-Path $NuGetPackageRoot "roslyntools.repotoolset\$ToolsetVersion\tools\Build.proj"

  if ($ci) {
    New-Directory $TempDir
    $env:TEMP = $TempDir
    $env:TMP = $TempDir
  }

  $cliArgs = @{
    Version=$GlobalJson.sdk.version;
    Path=$DotNetRoot
  }
  if ($ci) {
    $cliArgs["Force"] = $true
  }
  Get-DotNetCli @cliArgs
  $dotnet = (Get-Command dotnet.exe).Source

  $msbuildArgs = @(
    "/m",
    "/nologo",
    "/warnaserror",
    "/v:$Verbosity",
    "/p:NuGetPackageRoot=$NuGetPackageRoot",
    "/p:RestorePackagesPath=$NuGetPackageRoot"
  )

  $msbuild = {
    Write-Verbose "$dotnet msbuild $msbuildArgs $Args"
    & $dotnet msbuild @msbuildArgs @Args
  }


  if ($restore) {
    InstallToolset $msbuild
  }

  Build $msbuild
  exit $lastExitCode
}
catch {
  Write-Host $_
  Write-Host $_.Exception
  Write-Host $_.ScriptStackTrace
  exit 1
}
finally {
  Pop-Location
  if ($ci -and $prepareMachine) {
    Stop-Processes
  }
}

