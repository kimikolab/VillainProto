param(
    [string]$Godot = 'C:\tools\Godot_v4.7.2-stable_mono_win64\Godot_console.exe'
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Godot)) { throw "Godotのパスを指定してください: -Godot <実行ファイル>" }
if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'PolyBlocks\NatureBlocks'))) {
    throw '先にPrepareAssetPreview.ps1で購入素材を用意してください。'
}
dotnet build (Join-Path $PSScriptRoot 'DemoApp.csproj') -v minimal
if ($LASTEXITCODE -ne 0) { throw 'ビルドに失敗しました。' }
& $Godot --headless --editor --path $PSScriptRoot --import
if ($LASTEXITCODE -ne 0) { throw '素材のインポートに失敗しました。' }
& $Godot --path $PSScriptRoot --windowed --resolution 1440x900 res://AssetPreview.tscn
