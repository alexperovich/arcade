set-strictmode -version 2.0
$ErrorActionPreference = "Stop"

<#
  .Synopsis
  Searches the file system from $SearchPath up until a file named $FileName is found

  .Parameter SearchPath
  The directory to start the search at

  .Parameter FileName
  The file name to search for
#>
function Get-FileAbove {
  param(
    [Parameter(Mandatory=$true)]
    [ValidateScript({Test-Path -PathType Container $_})]
    $SearchPath,
    [Parameter(Mandatory=$true)]
    $FileName
  )

  Write-Verbose "Get-FileAbove: Starting search for $FileName"
  [IO.DirectoryInfo]$lookIn = Get-Item $SearchPath
  Do
  {
    Write-Verbose "Get-FileAbove: Searching $($lookIn.FullName)"
    $possibleFile = Join-Path $lookIn.FullName $FileName
    if (Test-Path -PathType Leaf $possibleFile) {
      return $possibleFile
    } else {
      $lookIn = $lookIn.Parent
    }
  } While ($lookIn)
}

<#
  .Synopsis
  Creates one or more Directories
#>
function New-Directory {
  [CmdletBinding()]
  param(
    [string[]]
    $Path
  )
  foreach ($p in $Path) {
    if (!(Test-Path $p -PathType Container)) {
      New-Item $p -Force -ItemType Container | Out-Null
    }
  }
}

<#
  .Synopsis
  Acquires dotnet cli and makes it avaliable on the path

  .Notes
  This will use a globally installed dotnet cli if possible and -Force is not specified

  .Parameter Version
  The Version of dotnet cli to download

  .Parameter Path
  The Path to place dotnet cli

  .Parameter Force
  Force dotnet cli to be downloaded to the local path
#>
function Get-DotNetCli {
  [CmdletBinding()]
  param(
    [string] $Version,
    [string] $Path,
    [switch] $Force
  )

  if ((-not $Force) -and (Get-Command dotnet -ErrorAction SilentlyContinue) -and ($(dotnet --version) -eq $Version)) {
    Write-Verbose "Global dotnet cli version $Version found"
    return
  }

  New-Directory $Path
  $installScriptUri = "https://dot.net/v1/dotnet-install.ps1"
  $installScriptPath = Join-Path $Path dotnet-install.ps1

  if (!(Test-Path -PathType Leaf $installScriptPath)) {
    Invoke-WebRequest $installScriptUri -UseBasicParsing -OutFile $installScriptPath
  }

  Write-Verbose "Installing dotnet cli"
  & $installScriptPath -Version $Version -InstallDir $Path
  if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to install dotnet cli (exit code $LASTEXITCODE)";
  }
}
