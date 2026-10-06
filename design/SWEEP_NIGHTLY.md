# sweep の夜間 full（第280期）

description: 現役網（`design/SWEEP_ROSTER.md`）の `sweep full` を夜に無人で回し、朝にサマリ1枚を読む運用の手順。**スケジューラへの登録はポンの環境操作**なので、ここには登録コマンドそのものまでを書く。

## 1. 作法（`CLAUDE.md` の (G17) と同じ文）

**日中は `sweep fast`・full は夜間、朝にサマリを確認。差分があれば翌朝の最初の仕事は原因特定（`git bisect`）。**

## 2. 何が走るか

`tools/sweep_nightly.ps1`（引数なし）:

1. `dotnet build BattleSim -c Release`（**作業ツリーのまま**——夜に回したい状態はコミットしておく）
2. `dotnet BattleSim.dll 0 sweep nightly` ＝ 現役網の full・**指紋照合なし**（毎晩全部走らせる）・直列・上限 150 秒（`roster audit` は 600 秒）。サマリ本体はこれが書く
3. DemoApp のビルドと頭なしの門 `--map11-phase0` ／ `phase171` ／ `phase172` ／ `phase173`（`*_COMPLETE ok=True` の行で読む）。結果をサマリの末尾に足す

所要は現役網で約 12 分（第280期の実測は報告書）＋ 門で 1 分弱。

## 3. 朝のサマリ（`.sweep/nightly/<日付>.md`・`.gitignore` 済み）

| 欄 | 中身 |
|---|---|
| 1行目 | `# sweep 夜間サマリ <日付>（<HEAD の sha>）`。作業ツリーに BattleCore ／ BattleSim の未コミットの変更があれば sha に `+作業中` |
| 判定 | **異常なし**（指紋の差分 0・異常終了 0・打ち切り 0）か **要確認** |
| 本数と所要 | 現役網の本数・所要（分）・前回のサマリとの差 |
| 表 | 指紋の差分 ／ 異常終了 ／ 打ち切り ／ 前回の指紋が無い本 の本数 |
| 指紋の差分があった本 | 本の名と（前回 → 今回）の指紋 |
| 異常終了 ／ 打ち切り | 本の名・exit・最後の例外行 |
| DemoApp の門 | 4 本の `*_COMPLETE` の行 |
| 末尾 | 機械が読む1行 `<!-- nightly minutes=… changed=… bad=… timeouts=… -->`（次の夜の「前回との差」が読む） |

- **指紋の比較の相手は「この回を走らせる前のキャッシュ（`.sweep/cache.tsv`）の指紋」**——前の夜の結果か、日中に回した `sweep fast` の結果。日中に盤面を変えて `fast` を回していれば、その差は日中の `fast` に吸われて夜には出ない（日中の変更は日中の `fast` で見る）
- 標準出力の指紋は所要（秒・分）を書いた行を落として取る（第278期 `OutFingerprint`）。**生成物や文書を読む本**（`audit` は `CLAUDE.md` の行数とバイト、`derive rules` は文書の言及を数える）は文書を書き換えた日に指紋が動く——その日の変更と突き合わせて読む
- ビルドに失敗した夜は `sweep nightly` がサマリを書かないので、スクリプトが「書かなかった」とだけ書いた1枚を置く（`<日付>.log` を見る）

## 4. 登録（Windows のタスク スケジューラ・毎日 3:00）

PowerShell（管理者でなくてよい・ログオン中に走る）:

```powershell
$a = New-ScheduledTaskAction -Execute "powershell.exe" -Argument '-NoProfile -ExecutionPolicy Bypass -File "C:\works\VillainProto\tools\sweep_nightly.ps1"' -WorkingDirectory "C:\works\VillainProto"
$t = New-ScheduledTaskTrigger -Daily -At 3:00
$s = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 2) -DontStopIfGoingOnBatteries -AllowStartIfOnBatteries
Register-ScheduledTask -TaskName "VillainProto sweep nightly" -Action $a -Trigger $t -Settings $s -Description "第280期: 現役網の sweep full と DemoApp の門 → .sweep/nightly/<日付>.md"
```

cmd の1行版（同じ中身・設定は既定）:

```
schtasks /Create /TN "VillainProto sweep nightly" /SC DAILY /ST 03:00 /TR "powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\works\VillainProto\tools\sweep_nightly.ps1"
```

- 試しに今すぐ1回: `Start-ScheduledTask -TaskName "VillainProto sweep nightly"`（または `schtasks /Run /TN "VillainProto sweep nightly"`）
- 外す: `Unregister-ScheduledTask -TaskName "VillainProto sweep nightly" -Confirm:$false`
- **夜間は他の重い計算を走らせない**——上限 150 秒は壁時計なので、重い負荷と重なると打ち切りが出る（第278期 R370）。`-StartWhenAvailable` は、3:00 にスリープしていた夜は起きたときに走らせる
- Godot の場所が違う機械では、スクリプトの `-Godot` 引数を `-Argument` に足す

## 5. 手で回す

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools\sweep_nightly.ps1
```

BattleSim だけなら `dotnet run --project BattleSim -c Release 0 sweep nightly`（`chcp 65001` の端末で）。
