# 第280期: sweep の夜間 full（現役網）＋ DemoApp の門 → 朝のサマリ `.sweep/nightly/<日付>.md`。
# 手順書は design/SWEEP_NIGHTLY.md。OS のスケジューラから無人で呼ぶ（引数なし）。
#
#   1. BattleSim を Release でビルド（作業ツリーのまま）
#   2. `sweep nightly`（現役網・指紋照合なし・直列）——サマリ本体は BattleSim が書く
#   3. DemoApp をビルドして頭なしの門（map11 phase0 ／ 171 ／ 172 ／ 173）を回し、結果をサマリの末尾に足す
#
# 文字コード: dotnet の標準出力は `chcp 65001` の cmd の中でファイルへ直に落とす（PowerShell の変数に取ると cp932 で化ける・第250期）。
param(
    [string]$Root = (Split-Path $PSScriptRoot -Parent),
    [string]$Godot = "C:\tools\Godot_v4.7.2-stable_mono_win64\Godot_console.exe"
)
$ErrorActionPreference = "Continue"
Set-Location $Root
$dir = Join-Path $Root ".sweep\nightly"
New-Item -ItemType Directory -Force $dir | Out-Null
$date = Get-Date -Format "yyyy-MM-dd"
$log = Join-Path $dir "$date.log"
$utf8 = New-Object System.Text.UTF8Encoding($false)

cmd /c "dotnet build BattleSim -c Release -nologo -v q > ""$log"" 2>&1"
cmd /c "chcp 65001 >nul & dotnet BattleSim\bin\Release\net8.0\BattleSim.dll 0 sweep nightly >> ""$log"" 2>&1"

$summary = Join-Path $dir "$date.md"
if (-not (Test-Path $summary)) {
    [System.IO.File]::WriteAllText($summary, "# sweep 夜間サマリ $date`n`n**sweep nightly がサマリを書かなかった**（ビルドの失敗など）——``$date.log`` を見ること。`n", $utf8)
}

$gates = @()
if (Test-Path $Godot) {
    cmd /c "dotnet build DemoApp/DemoApp.csproj -c Debug -nologo -v q >> ""$log"" 2>&1"
    foreach ($g in "phase0", "phase171", "phase172", "phase173") {
        $o = & $Godot --path DemoApp --headless -- "--map11-$g" 2>&1 | Out-String
        $line = ($o -split "`n" | Where-Object { $_ -match "_COMPLETE" } | Select-Object -First 1)
        if (-not $line) { $line = "（COMPLETE の行が無い）" }
        $gates += "- ``--map11-$g``: $($line.Trim())"
    }
} else {
    $gates += "- Godot が見つからない（$Godot）——門は回していない"
}
$bad = @($gates | Where-Object { $_ -notmatch "ok=True" }).Count
$text = "`n## DemoApp の門$(if ($bad -gt 0) { "（**要確認 $bad**）" } else { "（すべて ok）" })`n`n" + ($gates -join "`n") + "`n"
[System.IO.File]::AppendAllText($summary, $text, $utf8)
