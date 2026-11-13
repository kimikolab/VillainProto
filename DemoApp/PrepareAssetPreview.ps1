param(
    [string]$AssetRoot = 'D:\Assets\GodotAssets'
)
$ErrorActionPreference = 'Stop'
# Folders = 原本から写すフォルダ。NatureBlocks の tools は道・川・湖・崖・山・ばらまきの道具（3D の作戦マップが使う）。
$packages = @(
    @{ Source = 'PolyBlocks-NatureBlocks-[v1]\PolyBlocks\NatureBlocks'; Name = 'NatureBlocks'; Folders = @('assets', 'source_files', 'tools') },
    @{ Source = 'EffectBlocks v4\PolyBlocks\EffectBlocks'; Name = 'EffectBlocks'; Folders = @('assets', 'source_files') }
)
# 購入済み原本からコピーする。旧版やPixelRendererを同じres://へ混ぜない。
foreach ($package in $packages) {
    $source = Join-Path $AssetRoot $package.Source
    if (-not (Test-Path -LiteralPath $source -PathType Container)) { throw "素材がありません: $source" }
}
# 既にあるフォルダは上書きしない（足りないフォルダだけ足す。何度走らせてもよい）。
foreach ($package in $packages) {
    $source = Join-Path $AssetRoot $package.Source
    $destination = Join-Path $PSScriptRoot "PolyBlocks\$($package.Name)"
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    foreach ($folder in $package.Folders) {
        $target = Join-Path $destination $folder
        if (Test-Path -LiteralPath $target) { Write-Output "そのまま: $target"; continue }
        Copy-Item -LiteralPath (Join-Path $source $folder) -Destination $destination -Recurse
        Write-Output "導入: $target"
    }
}
