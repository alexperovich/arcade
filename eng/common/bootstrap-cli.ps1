$here = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoRoot = Resolve-Path (Join-Path $here ..\..\)
$globalJson = Get-Content "$repoRoot\global.json" | ConvertFrom-Json

$Path = Join-Path $repoRoot .dotnet
$Version = $globalJson.sdk.version

if ((-not $Force) -and (Get-Command dotnet -ErrorAction SilentlyContinue) -and ($(dotnet --version) -eq $Version)) {
  Write-Verbose "Global dotnet cli version $Version found"
  return
}

New-Item -ItemType Container $Path | Out-Null
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
