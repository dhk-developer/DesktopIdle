$ErrorActionPreference = 'Stop'

$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$OutputDir = Join-Path $ProjectDir 'publish\win-x64'

Write-Host 'Publishing DesktopIdle as a self-contained Windows x64 executable...' -ForegroundColor Cyan
Write-Host "Project: $ProjectDir"
Write-Host "Output:  $OutputDir"

Push-Location $ProjectDir
try {
    dotnet restore
    dotnet publish .\DesktopIdle.csproj `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:EnableCompressionInSingleFile=false `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $OutputDir
}
finally {
    Pop-Location
}

$ExePath = Join-Path $OutputDir 'DesktopIdle.exe'
if (Test-Path $ExePath) {
    Write-Host ''
    Write-Host 'Done. Your executable is here:' -ForegroundColor Green
    Write-Host $ExePath -ForegroundColor Green
} else {
    throw 'Publish completed, but DesktopIdle.exe was not found in the expected output folder.'
}
