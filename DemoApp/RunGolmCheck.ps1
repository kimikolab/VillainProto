param(
    [string]$Godot = 'C:\tools\Godot_v4.7.2-stable_mono_win64\Godot_console.exe',
    [switch]$Verify
)

$ErrorActionPreference = 'Stop'
dotnet build (Join-Path $PSScriptRoot 'DemoApp.csproj')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $Godot --headless --path $PSScriptRoot --editor --import
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if ($Verify) {
    & $Godot --headless --path $PSScriptRoot res://GolmCheck.tscn -- --verify
}
else {
    & $Godot --path $PSScriptRoot res://GolmCheck.tscn
}
