[CmdletBinding()]
param(
    [string]$InnoSetupCompiler
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectDirectory = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $projectDirectory 'WinUtil.csproj'
$testsPath = Join-Path $projectDirectory 'WinUtil.Tests\WinUtil.Tests.csproj'
$publishDirectory = Join-Path $projectDirectory 'artifacts\publish\win-x64'
$installerDirectory = Join-Path $projectDirectory 'artifacts\installer'
$installerScript = Join-Path $projectDirectory 'packaging\WinUtil.iss'

if ($InnoSetupCompiler) {
    $compilerPath = (Resolve-Path -LiteralPath $InnoSetupCompiler).Path
} else {
    $compilerCommand = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    $compilerCandidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7 (64-bit)\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7 (32-bit)\ISCC.exe')
    )
    $compilerPath = if ($compilerCommand) {
        $compilerCommand.Source
    } else {
        $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    }
}

if (-not $compilerPath) {
    throw 'Install Inno Setup 6 or 7, or pass -InnoSetupCompiler with the full path to ISCC.exe.'
}

& dotnet run --project $testsPath --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw "Tests failed with exit code $LASTEXITCODE."
}

& dotnet publish $projectPath --configuration Release -p:PublishProfile=win-x64 --source https://api.nuget.org/v3/index.json
if ($LASTEXITCODE -ne 0) {
    throw "Publishing failed with exit code $LASTEXITCODE."
}

$executablePath = Join-Path $publishDirectory 'WinUtil.exe'
$appVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($executablePath).ProductVersion.Split('+')[0]

& $compilerPath "/DAppVersion=$appVersion" "/DPublishDirectory=$publishDirectory" "/DInstallerDirectory=$installerDirectory" $installerScript
if ($LASTEXITCODE -ne 0) {
    throw "Installer compilation failed with exit code $LASTEXITCODE."
}

Get-Item -LiteralPath (Join-Path $installerDirectory "WinUtil-Setup-$appVersion-win-x64.exe")
