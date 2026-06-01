$ErrorActionPreference = "Stop"

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoDir = Split-Path -Parent (Split-Path -Parent $projectDir)
$outputDir = Join-Path $projectDir "bin"
$outputPath = Join-Path $outputDir "DepthLayer Studio.exe"
$csc = "C:\Program Files\dotnet\sdk\6.0.135\Roslyn\bincore\csc.dll"
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
$framework = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"

if (-not (Test-Path $csc)) {
    throw "Could not find the Roslyn compiler at $csc. Install the .NET SDK, then run this again."
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$sources = Get-ChildItem $projectDir -Filter "*.cs" | ForEach-Object { $_.FullName }

& $dotnet exec $csc `
    -nologo `
    -target:winexe `
    -langversion:10.0 `
    -nullable:enable `
    -out:$outputPath `
    -r:"$framework\mscorlib.dll" `
    -r:"$framework\System.dll" `
    -r:"$framework\System.Core.dll" `
    -r:"$framework\System.Drawing.dll" `
    -r:"$framework\System.Xml.dll" `
    -r:"$framework\System.Windows.Forms.dll" `
    $sources

if ($LASTEXITCODE -ne 0) {
    throw "Build failed."
}

Write-Host "Built $outputPath"
