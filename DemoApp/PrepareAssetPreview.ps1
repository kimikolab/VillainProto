param(
    [string]$AssetRoot = 'D:\Assets\GodotAssets'
)
$ErrorActionPreference = 'Stop'
$packages = @(
    @{ Source = 'PolyBlocks-NatureBlocks-[v1]\PolyBlocks\NatureBlocks'; Name = 'NatureBlocks' },
    @{ Source = 'EffectBlocks v4\PolyBlocks\EffectBlocks'; Name = 'EffectBlocks' }
)
# 購入済み原本からコピーする。旧版やPixelRendererを同じres://へ混ぜない。
foreach ($package in $packages) {
    $source = Join-Path $AssetRoot $package.Source
    if (-not (Test-Path -LiteralPath $source -PathType Container)) { throw "素材がありません: $source" }
    $destination = Join-Path $PSScriptRoot "PolyBlocks\$($package.Name)"
    if (Test-Path -LiteralPath $destination) { throw "既存の素材は上書きしません: $destination" }
}
foreach ($package in $packages) {
    $source = Join-Path $AssetRoot $package.Source
    $destination = Join-Path $PSScriptRoot "PolyBlocks\$($package.Name)"
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    foreach ($folder in @('assets', 'source_files')) {
        Copy-Item -LiteralPath (Join-Path $source $folder) -Destination $destination -Recurse
    }
    Write-Output "導入: $destination"
}
