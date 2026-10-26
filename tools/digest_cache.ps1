# 第327期: 過去の器具の照合（台本の指紋）の保存と使い回し（design/MEASURE_LIGHT.md §2）。
#
#   powershell -File tools/digest_cache.ps1 list   [-Units tou,kata]   # 照合する器具の一覧だけ（戦闘0回）。-Units を付けると軽い期の絞り込み
#   powershell -File tools/digest_cache.ps1 save   [-Commit <sha>]    # いまのビルドで全本を回して .tmp/digest_cache/<sha>/ に保存（既定は HEAD の短い sha）
#   powershell -File tools/digest_cache.ps1 light  -Units tou,kata    # 軽い期: 変えた駒が出る器具だけを新しいビルドで回し、HEAD の保存と比べる
#   powershell -File tools/digest_cache.ps1 full                       # 重い期: 全本を新しいビルドで回し、HEAD の保存と比べる
#   powershell -File tools/digest_cache.ps1 recheck                    # work/ をもう一度 HEAD の保存と比べるだけ（回し直さない）
#   powershell -File tools/digest_cache.ps1 promote                    # 期の最後（コミットの後）: HEAD の保存 ＝ 直前の保存 ＋ この期に回した分（work/）で上書き
#
# 置き場所: .tmp/digest_cache/<sha>/（指紋 1 本 1 ファイル ＋ manifest.txt）・.tmp/digest_cache/work/（この期に新しいビルドで回した分）。
# **HEAD のビルド（別の worktree）を建て直さない**——HEAD の短い sha の保存があればそれと比べる。無いとき（Codex のコミットが挟まったなど）だけ
# HEAD を別の worktree で建てて `save -Commit <HEAD>` を回す（worktree には `.tmp/pNNN/predict.md` を写すこと・R397 の門で止まる）。
#
# 軽い期の絞り込み（器具 → 駒の対応表は自動で引く）: `Program.cs` の振り分けから器具の Diag のクラスを引き、そのクラスのファイル
# （＋ `using X = YDiag;` で引いている Diag のファイル・1段だけ）に、駒が名前で出てくる（`UnitCatalog.<駒>…` か `"<id>"`）器具を選ぶ。
# `compare` の行や試遊の台に出るだけの器具は選ばない（夜間の `sweep nightly` が拾う）。`derive check` はソースの行番号を出すので、差は「ずれ」と数えない。
#
# 文字コード: dotnet の標準出力は `chcp 65001` の cmd の中でファイルへ直に落とす（PowerShell の変数に取ると cp932 で化ける・第250期）。
param(
    [Parameter(Position = 0)][ValidateSet('list', 'save', 'light', 'full', 'recheck', 'promote')][string]$Mode = 'list',
    [string[]]$Units = @(),
    [string]$Commit = '',
    [string]$Dll = '',
    [string]$Root = '',
    [string]$Cache = ''
)
if ($Root -eq '') { $Root = if ($PSScriptRoot) { Split-Path $PSScriptRoot -Parent } else { (Get-Location).Path } }
$ErrorActionPreference = "Continue"
Set-Location $Root
$utf8 = New-Object System.Text.UTF8Encoding($false)
$cacheRoot = if ($Cache -ne '') { $Cache } else { Join-Path $Root ".tmp\digest_cache" }
$work = Join-Path $cacheRoot "work"

# 照合する器具（第326期の 140 本・`.tmp/p326/digests.ps1` から移した）。新しい器具の check はここに足す。
$Cmds = @(
    "shockdigest k0", "shockdigest aka", "shockdigest t216", "shockdigest w217", "shockdigest m218", "shockdigest m219 cat", "shockdigest m219 m5",
    "shockdigest e223", "shockdigest k226", "shockdigest l227", "shockdigest h228", "shockdigest g229", "shockdigest s232", "shockdigest a231",
    "shockdigest w230", "shockdigest r225", "shockdigest f224", "shockdigest d222",
    "borgfront digest", "fireward digest", "firescale digest", "fireburst digest", "enemyfire digest", "firecycle digest", "firetri digest",
    "firefinish digest", "fireatk digest", "firekindle digest", "som276 digest",
    "tome281 check", "tou323 p0", "tou323 boards", "tou323 compare", "tou323 check", "tou323 memo", "tome282 check", "boss283 check", "elite check",
    "spread", "cross check", "derive check", "misa285 check", "shock289 check", "shock290 check", "shock293 check", "kugu292 check", "guard294 check",
    "markheal295 check", "rally296 check", "doha297 check", "feather298 check", "zan299 check", "round300 check", "hisa301 check", "hisa302 check",
    "hush303 check", "hush304 check", "hush305 check", "hush306 check",
    "som307 p0", "som307 compare", "som307 boards", "som307 check", "tou279 check", "shock287 check",
    "som308 p0", "som308 compare", "som308 boards", "som308 check", "som309 p0", "som309 playtest", "som309 check",
    "som310 p0", "som310 boards", "som310 compare", "som310 swap", "som310 check",
    "som311 p0", "som311 boards", "som311 compare", "som311 swap", "som311 kubi", "som311 check",
    "som312 beni", "som312 playtest", "som312 check", "som314 p0", "som314 boards", "som314 compare", "som314 swap", "som314 hole", "som314 check",
    "doha313 p0", "doha313 boards", "doha313 compare", "doha313 check", "doha315 strikers", "doha315 p0", "doha315 boards", "doha315 compare", "doha315 check",
    "shiga316 dboards", "shiga316 dcompare", "shiga316 p0", "shiga316 boards", "shiga316 compare", "shiga316 check",
    "som317 p0", "som317 boards", "som317 compare", "som317 check", "som318 p0", "som318 boards", "som318 compare", "som318 check",
    "som319 p0", "som319 boards", "som319 compare", "som319 check", "som320 boards", "som320 guide", "som320 census", "som320 check",
    "shiga321 check", "hisa321 check", "shiga321 memo", "hisa321 memo",
    "shiga322 p0", "shiga322 boards", "shiga322 compare", "shiga322 check", "shiga322 memo",
    "tou324 p0", "tou324 boards", "tou324 compare", "tou324 check", "tou324 memo",
    "tou325 boards", "tou325 guide", "tou325 check",
    # 第327期に足した（第326期の器具）
    "dense326 p0s", "dense326 p0", "dense326 check",
    # 第327期に足した（この期の器具・保存 772f48c から入っている）
    "dust327 p0", "dust327 check",
    # 第328期に足した（この期の器具）
    "settle328 p0", "settle328 check",
    # 第329期に足した（この期の器具）
    "fuse329 p0", "fuse329 check",
    # 第330期に足した（この期の器具）
    "settle330 check",
    # 第331期に足した（この期の器具）
    "stun331 p0", "stun331 check",
    # 第332期に足した（この期の器具）
    "veil332 check",
    # 第333期に足した（この期の器具）
    "settle333 check",
    # 第334期に足した（この期の器具）
    "zan334 check",
    # 第335期に足した（この期の器具）
    "doha335 check",
    # 第336期に足した（この期の器具）
    "settle336 check",
    # 第337期に足した（この期の器具）
    "mark337 check",
    # 第338期に足した（この期の器具）
    "settle338 check",
    # 第339期に足した（この期の器具）
    "grow339 check",
    # 第340期に足した（この期の器具）
    "settle340 check",
    # 第341期に足した（この期の器具）
    "credit341 check",
    # 第342期に足した（この期の器具）
    "kata342 check",
    # 第343期に足した（この期の器具）
    "golm343 check",
    # 第344期に足した（この期の器具）
    "sw344 check",
    # 第345期に足した（この期の器具）
    "rp345 check",
    # 第347期に足した（この期の器具）
    "flow347 check",
    # 第348期に足した（この期の器具）
    "inv348 check",
    # 第349期に足した（この期の器具）
    "burn349 check",
    # 第351期に足した（この期の器具）
    "db351 check",
    # 第358期に足した（この期の器具）
    "sh358 check",
    # 第359期に足した（この期の器具）
    "sh359 check",
    # 第360期に足した（この期の器具）
    "sh360 check",
    # 第361期に足した（この期の器具）
    "sh361 check",
    # 第362期に足した（この期の器具）
    "sh362 check",
    # 第363期に足した（この期の器具）
    "sh363 check",
    # 第364期に足した（この期の器具）
    "sh364 check",
    # 第365期に足した（この期の器具）
    "sh365 check",
    # 第366期に足した（この期の器具）
    "sh366 check",
    # 第367期に足した（この期の器具）
    "sh367 check",
    # 第368期に足した（この期の器具）
    "sh368 check",
    # 第369期に足した（この期の器具）
    "sh369 check",
    # 第370期に足した（この期の器具）
    "sh370 check",
    # 第371期に足した（この期の器具）
    "sh371 check",
    # 第372期に足した（この期の器具）
    "sh372 check",
    # 第373期に足した（この期の器具）
    "sh373 check",
    # 第374期に足した（この期の器具）
    "sh374 check",
    # 第375期に足した（この期の器具）
    "sh375 check",
    # 第376期に足した（この期の器具）
    "sh376 check",
    # 第377期に足した（この期の器具）
    "sh377 check",
    # 第378期に足した（この期の器具）
    "sh378 check",
    # 第379期に足した（この期の器具）
    "sh379 check",
    # 第380期に足した（この期の器具）
    "sh380 check",
    # 第381期に足した（この期の器具）
    "sh381 check",
    # 第382期に足した（この期の器具）
    "sh382 check",
    # 第383期に足した（この期の器具）
    "sh383 check",
    # 第384期に足した（この期の器具）
    "sh384 check",
    # 第385期に足した（この期の器具）
    "sh385 check",
    # 第386期に足した（この期の器具）
    "sh386 check",
    # 第387期に足した（この期の器具）
    "sh387 check",
    # 第388期に足した（この期の器具）
    "sh388 check",
    # 第389期に足した（この期の器具）
    "sh389 check",
    # 第390期に足した（この期の器具）
    "sh390 check",
    # 第391期に足した（この期の器具）
    "sh391 check",
    # 第392期に足した（この期の器具）
    "sh392 check",
    # 第393期に足した（この期の器具）
    "sh393 check",
    # 第394期に足した（この期の器具）
    "sh394 check",
    # 第395期に足した（この期の器具）
    "sh395 check",
    # 第396期に足した（この期の器具）
    "sh396 check",
    # 第397期に足した（この期の器具）
    "sh397 check",
    # 第398期に足した（この期の器具）
    "sh398 check",
    # 第399期に足した（この期の器具）
    "sh399 check",
    # 第400期に足した（この期の器具）
    "sh400 check",
    # 第401期に足した（この期の器具）
    "sh401 check",
    # 第402期に足した（この期の器具）
    "sh402 check",
    # 第403期に足した（この期の器具）
    "sh403 check",
    # 第404期に足した（この期の器具）
    "sh404 check"
)
# ソースの行番号などを出す（差は「ずれ」と数えない）
$Volatile = @("derive check")

function FileName([string]$c) { ($c -replace '[ |]', '_') + ".txt" }

# 器具 → その器具が名前で引く駒の Id（自動）
$prog = [System.IO.File]::ReadAllText((Join-Path $Root "BattleSim\Program.cs"))
$srcFiles = Get-ChildItem -Recurse -Filter *.cs (Join-Path $Root "BattleSim") | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }
$srcText = @{}
foreach ($f in $srcFiles) { $srcText[$f.FullName] = [System.IO.File]::ReadAllText($f.FullName) }
function FilesOfClass([string]$cls) {
    $srcText.Keys | Where-Object { $srcText[$_] -match "class\s+$cls\b" }
}
$unitNames = @{ tou = "Tou"; kata = "Kata"; kugu = "Kugu"; som = "Som"; shiga = "Shiga"; hisa = "Hisa"; doha = "Doha"; beni = "Beni"; tome = "Tome"; zan = "Zan"; golm = "Golm"; borg = "Borg"; hota = "Hota"; hiyo = "Hiyo"; wata = "Wata"; uke = "Uke"; utsu = "Utsu"; nel = "Nel"; hibi = "Hibi"; gare = "Gare"; tsugi = "Tsugi"; sasa = "Sasa"; kado = "Kado"; susu = "Susu"; guza = "Guza"; vio = "Vio"; sid = "Sid"; rica = "Rica"; zoto = "Zoto"; mug = "Mug"; vel = "Vel"; ere = "Ere"; hagi = "Hagi"; tomo = "Tomo"; dolga = "Dolga"; gan = "Gan"; sekki = "Sekki" }   # 第396期: sekki（セッキの転生の版）を足した・第394期: 手番の軸の3枚（tomo ／ dolga ／ gan）を足した（第393期の -Units tomo,dolga,gan,hagi は hagi だけが当たっていた）・第388期: hagi（ハギの版）を足した・第385期: ere（哀歌のエレ）を足した・第377期: 死の4枚（rica ／ zoto ／ mug ／ vel）を足した・第368期: sid を足した・第367期: guza ／ vio を足した・第363期: susu（アカ）を足した・第362期: kado を足した・第335期: golm を足した・第349期: 燃焼の3枚を足した・第351期: デバフの4枚を足した・第358期: 破片の3枚を足した
function UnitsOfMode([string]$mode) {
    $m = [regex]::Match($prog, 'focusId == "' + [regex]::Escape($mode) + '"\)[^;]*?(\w+Diag)\.Run', 'Singleline')
    if (-not $m.Success) { return @("?") }
    $cls = $m.Groups[1].Value
    $files = @(FilesOfClass $cls)
    $more = @()
    foreach ($f in $files) { foreach ($a in [regex]::Matches($srcText[$f], 'using\s+\w+\s*=\s*(\w+Diag)\s*;')) { $more += FilesOfClass $a.Groups[1].Value } }
    $all = ($files + $more) | Select-Object -Unique
    $text = ($all | ForEach-Object { $srcText[$_] }) -join "`n"
    $hit = @()
    foreach ($id in $unitNames.Keys) {
        if ($text -match ('UnitCatalog\.' + $unitNames[$id] + '\w*') -or $text -match ('"' + $id + '"')) { $hit += $id }
    }
    return $hit
}

function Select-Cmds([string[]]$want) {
    if ($want.Count -eq 0) { return $Cmds }
    $memo = @{}
    $Cmds | Where-Object {
        $mode = ($_ -split ' ')[0]
        if (-not $memo.ContainsKey($mode)) { $memo[$mode] = @(UnitsOfMode $mode) }
        $u = $memo[$mode]
        ($u -contains "?") -or @($u | Where-Object { $want -contains $_ }).Count -gt 0
    }
}

function HeadSha { (git rev-parse --short HEAD).Trim() }

# ソースの指紋（BattleCore ／ BattleSim の作業ツリーの *.cs と *.csproj の git の blob の並び・R422）。light ／ full がビルドした時点の値を work/ に控え、promote でいまの値と比べる。
function SourcePrint {
    $files = git ls-files --cached --others --exclude-standard BattleCore BattleSim | Sort-Object | Where-Object { $_ -match '\.(cs|csproj)$' }
    $blobs = git hash-object @files   # 引数で渡す（PowerShell 5.1 のパイプは git の標準入力に BOM を付ける）
    $sha = [System.Security.Cryptography.SHA1]::Create()
    $bytes = [System.Text.Encoding]::UTF8.GetBytes((($files | ForEach-Object { $_ }) -join "`n") + "`n" + ($blobs -join "`n"))
    return ([BitConverter]::ToString($sha.ComputeHash($bytes)) -replace '-', '').Substring(0, 16).ToLower()
}

function Build-New {
    if ($Dll -ne '') { return $Dll }
    $out = Join-Path $cacheRoot "build_$Mode"
    cmd /c "dotnet build BattleSim -c Release -nologo -v q -o ""$out"" > nul 2>&1"
    if ($LASTEXITCODE -ne 0) { Write-Output "ビルドに失敗した"; exit 1 }
    return (Join-Path $out "BattleSim.dll")
}

function Run-Cmds([string[]]$cmds, [string]$dll, [string]$outdir) {
    New-Item -ItemType Directory -Force $outdir | Out-Null
    foreach ($c in $cmds) {
        $path = Join-Path $outdir (FileName $c)
        $sw = [Diagnostics.Stopwatch]::StartNew()
        cmd /c "chcp 65001 >nul & dotnet ""$dll"" 0 $c > ""$path"" 2>&1"
        Write-Output "$($c -replace ' ', '_') $([int]$sw.Elapsed.TotalSeconds)s exit=$LASTEXITCODE"
    }
}

# 所要時間の行（「（12 秒）」「所要 6.8 秒。」など）は回すたびに違うので比べない
function Norm([string]$path) {
    $lines = [System.IO.File]::ReadAllLines($path, $utf8) | Where-Object { $_ -notmatch '[0-9.]+ ?秒' }
    return ($lines -join "`n")
}

function Compare-To([string[]]$cmds, [string]$cache) {
    $same = 0; $diff = @(); $missing = @()
    foreach ($c in $cmds) {
        $a = Join-Path $cache (FileName $c); $b = Join-Path $work (FileName $c)
        if (-not (Test-Path $a)) { $missing += $c; continue }
        if ((Norm $a) -eq (Norm $b)) { $same++ }
        elseif ($Volatile -contains $c) { $same++; Write-Output "  （差あり・数えない）$c" }
        else { $diff += $c }
    }
    Write-Output "照合 $($cmds.Count) 本: 一致 $same ／ 差 $($diff.Count) ／ HEAD の保存に無い $($missing.Count)"
    foreach ($c in $diff) { Write-Output "  差: $c" }
    foreach ($c in $missing) { Write-Output "  保存に無い: $c" }
}

$want = @($Units | ForEach-Object { $_ -split ',' } | Where-Object { $_ -ne '' })
switch ($Mode) {
    'list' {
        $sel = @(Select-Cmds $want)
        Write-Output "照合する器具 $($sel.Count) 本（全 $($Cmds.Count) 本・絞り込み: $(if ($want.Count) { $want -join ',' } else { 'なし' })）"
        foreach ($c in $sel) { Write-Output "  $c  [$((UnitsOfMode (($c -split ' ')[0])) -join ',')]" }
    }
    'save' {
        $sha = if ($Commit -ne '') { $Commit } else { HeadSha }
        $dir = Join-Path $cacheRoot $sha
        $dll = Build-New
        Run-Cmds $Cmds $dll $dir
        [System.IO.File]::WriteAllText((Join-Path $dir "manifest.txt"), "commit $sha`nsaved $(Get-Date -Format s)`nfull $($Cmds.Count)`n", $utf8)
        Write-Output "DONE $dir"
    }
    { $_ -in 'light', 'full' } {
        $sha = HeadSha
        $cache = Join-Path $cacheRoot $sha
        if (-not (Test-Path (Join-Path $cache "manifest.txt"))) {
            # HEAD の保存が無いとき: BattleCore ／ BattleSim が HEAD と同じコミットの保存があればそれを使う（DemoApp だけのコミットが挟まった場合）
            $same = Get-ChildItem $cacheRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName "manifest.txt") } | Sort-Object LastWriteTime -Descending |
                Where-Object { git diff --quiet $_.Name HEAD -- BattleCore BattleSim 2>$null; $LASTEXITCODE -eq 0 } | Select-Object -First 1
            if ($null -eq $same) {
                Write-Output "HEAD（$sha）の保存が無い——HEAD を別の worktree で建てて ``save -Commit $sha`` を回すこと（.tmp/pNNN/predict.md を写す）。"
                exit 2
            }
            Write-Output "HEAD（$sha）の保存は無いが、保存 $($same.Name) と BattleCore ／ BattleSim が同じなのでそれと比べる。"
            $sha = $same.Name
            $cache = $same.FullName
        }
        $sel = if ($Mode -eq 'full') { $Cmds } else { @(Select-Cmds $want) }
        $print = SourcePrint
        $dll = Build-New
        if (Test-Path $work) { Remove-Item -Recurse -Force $work }
        Run-Cmds $sel $dll $work
        [System.IO.File]::WriteAllText((Join-Path $work "manifest.txt"), "base $sha`nmode $Mode`nunits $($want -join ',')`nran $($sel.Count)`nsource $print`n", $utf8)
        Compare-To $sel $cache
    }
    'recheck' {
        # work/ をもう一度 HEAD の保存と比べるだけ（回し直さない）
        $man = Join-Path $work "manifest.txt"
        if (-not (Test-Path $man)) { Write-Output "work/ に manifest が無い"; exit 2 }
        $base = ((Get-Content $man -Encoding UTF8 | Where-Object { $_ -like 'base *' }) -replace '^base ', '').Trim()
        $ran = @(Get-ChildItem (Join-Path $work "*.txt") | Where-Object { $_.Name -ne 'manifest.txt' } | ForEach-Object { $_.BaseName })
        $cmds = $Cmds | Where-Object { $ran -contains (($_ -replace '[ |]', '_')) }
        Compare-To $cmds (Join-Path $cacheRoot $base)
    }
    'promote' {
        $sha = HeadSha
        $man = Join-Path $work "manifest.txt"
        if (-not (Test-Path $man)) { Write-Output "work/ に manifest が無い（この期に light ／ full を回していない）"; exit 2 }
        $base = ((Get-Content $man -Encoding UTF8 | Where-Object { $_ -like 'base *' }) -replace '^base ', '').Trim()
        $ranSrc = ((Get-Content $man -Encoding UTF8 | Where-Object { $_ -like 'source *' }) -replace '^source ', '').Trim()
        if ($ranSrc -ne (SourcePrint) -or (git status --porcelain -- BattleCore BattleSim)) {
            Write-Output "work/ はいまの BattleCore ／ BattleSim と違うソースで回した（または未コミットの変更がある）——コミットしたソースで light ／ full を回し直してから promote すること（R422）。"
            exit 2
        }
        $dir = Join-Path $cacheRoot $sha
        New-Item -ItemType Directory -Force $dir | Out-Null
        Copy-Item (Join-Path $cacheRoot "$base\*.txt") $dir -Force
        Copy-Item (Join-Path $work "*.txt") $dir -Force
        $ran = @(Get-ChildItem (Join-Path $work "*.txt") | Where-Object { $_.Name -ne 'manifest.txt' }).Count
        [System.IO.File]::WriteAllText((Join-Path $dir "manifest.txt"), "commit $sha`nsaved $(Get-Date -Format s)`nfrom $base（この期に回した $ran 本で上書き・残りは $base の保存を引き継いだ）`n", $utf8)
        Write-Output "DONE $dir（$base ＋ $ran 本）"
    }
}
