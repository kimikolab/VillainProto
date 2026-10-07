# 原画は変更せず、アルファの検査と目視用コンタクトシートだけを出す。
Add-Type -AssemblyName System.Drawing
$artRoot = Split-Path $PSScriptRoot -Parent
$assetNames = @(
    @('tou/tou-powder-right-v1.png', 'トウ：散粉'),
    @('misa/misa-control-right-v1.png', 'ミサ：管制'),
    @('misa/misa-spray-right-v1.png', 'ミサ：乱射'),
    @('shiga/shiga-interrupt-right-v1.png', 'シガ：割り込み'),
    @('kata/kata-thunder-right-v1.png', 'カタ：落雷'),
    @('kata/kata-thundercloud-v1.png', '雷雲'),
    @('tou/tou-powder-puff-v1.png', '鱗粉')
)
$sheet = [System.Drawing.Bitmap]::new(1500, 950)
$graphics = [System.Drawing.Graphics]::FromImage($sheet)
$graphics.Clear([System.Drawing.Color]::FromArgb(36, 40, 49))
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$light = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(83, 89, 102))
$dark = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(62, 68, 80))
$font = [System.Drawing.Font]::new('Yu Gothic UI', 18)
$report = @()
for ($index = 0; $index -lt $assetNames.Count; $index++) {
    $item = $assetNames[$index]
    $asset = [System.Drawing.Bitmap]::new((Join-Path $artRoot $item[0]))
    if ($index -lt 5) { $x = $index * 300; $y = 0; $w = 300; $h = 520 }
    else { $x = ($index - 5) * 750; $y = 530; $w = 750; $h = 420 }
    for ($yy = $y + 40; $yy -lt $y + $h; $yy += 20) {
        for ($xx = $x; $xx -lt $x + $w; $xx += 20) {
            $brush = if (((($xx-$x)/20 + ($yy-$y-40)/20) % 2) -eq 0) { $light } else { $dark }
            $graphics.FillRectangle($brush, $xx, $yy, [Math]::Min(20, $x+$w-$xx), [Math]::Min(20, $y+$h-$yy))
        }
    }
    $graphics.DrawString($item[1], $font, [System.Drawing.Brushes]::White, [single]($x+12), [single]($y+6))
    $scale = [Math]::Min(($w-20)/$asset.Width, ($h-60)/$asset.Height)
    $rw = [int]($asset.Width*$scale); $rh = [int]($asset.Height*$scale)
    $graphics.DrawImage($asset, [int]($x+($w-$rw)/2), [int]($y+45+($h-50-$rh)/2), $rw, $rh)
    $minimum = 255; $maximum = 0; $clear = 0; $partial = 0; $samples = 0
    for ($py=0; $py -lt $asset.Height; $py+=4) {
        for ($px=0; $px -lt $asset.Width; $px+=4) {
            $alpha = $asset.GetPixel($px,$py).A
            $minimum = [Math]::Min($minimum,$alpha); $maximum = [Math]::Max($maximum,$alpha)
            if ($alpha -eq 0) { $clear++ }
            elseif ($alpha -lt 255) { $partial++ }
            $samples++
        }
    }
    $report += [pscustomobject]@{File=$item[0]; Width=$asset.Width; Height=$asset.Height; Format=$asset.PixelFormat.ToString(); MinAlpha=$minimum; MaxAlpha=$maximum; ClearSamplePct=[Math]::Round(100*$clear/$samples,2); PartialSamplePct=[Math]::Round(100*$partial/$samples,2)}
    $asset.Dispose()
}
$sheet.Save((Join-Path $PSScriptRoot 'assets-alpha-preview.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'alpha-inspection.json') -Encoding utf8
$report | Format-Table -AutoSize
$font.Dispose(); $light.Dispose(); $dark.Dispose(); $graphics.Dispose(); $sheet.Dispose()
