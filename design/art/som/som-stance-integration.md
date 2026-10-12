# ソム：寝返った獣の臨戦態勢（2026-10-11）

`CODEX_BRIEF_SOM_STANCE.md` に基づく、DemoAppの演出・素材の変更。前回の群れ・緊急下僕の実装を引き継ぐ。戦闘規則・台本の対象・席・光の数・HPは変更しない。

## 動き

走りの後に獣が敵陣で向きを反転し、こちらへ牙を剥く。小型猫と大型下僕の専用差分を使い、逆立つ毛・低い構え・短い体の震えで威嚇を示す。胴体の雷が膨らみ、首輪の宝石が明滅した後、胴体の中央から暴発する。首輪が壊れる描画は加えていない。

通常の構えは0.34秒（反転0.10＋威嚇0.08＋帯電0.16）。全匹の `BeastBurst.Amount` が1の一団だけ0.15秒へ圧縮（0.05＋0.03＋0.07）。群れは同時に構え、同時に暴発する。

`SomBeast3D.TakeStance`／`BattlefieldView3D.TakeSomStance` と `BurstSomBeasts` を分離。臨戦態勢は単独では自動消滅せず、帯電した状態で待ち続ける。将来の「弾けずに残る」規則の分岐は追加していない。

すべての `Spark 降る` の粒が、ソムの立ち絵に描かれた魔導書のページへ集まる。ページに短い金の光を出し、0.08秒の回収の拍を挟んでから味方へ放つ。緊急の1体への集光も同じ経路。ベニ対策では魔導書から出た光が指定相手へ寄り、紫の輪で散って全員へ降る。止められた光は魔導書へ届く前に消える。

## 素材

威嚇PNG2枚の保存先・生成方法・プロンプトは [som-stance-v1-prompts.md](som-stance-v1-prompts.md)。既存の立ち絵は置換していない。体表の雷の薄い縁は `DemoApp/assets/fx/som_beast_charge.svg`。

購入済みEffectBlocks v4の `energy/electric_sparks_1.tscn`／`electric_sparks_2.tscn`／`lightning_ball.tscn` の実ファイルを確認。1の細い火花を帯電の胴体に採用（6粒・寿命0.18秒・低い速度）。2は瞬間的な飛散、lightning_ballは大きな球が主役になるため、構えの常駐には不採用。暴発は前回採用した1の火花と既存の電弧を継続。

発動ごとに処理材を複製し、購入原本と共有素材は変更しない。購入素材本体はGitに追加しない。未配置ならSVGの雷・電弧・宝石の明滅で描く。素材を外した実行は未検証。

## 検証

- 通常のDemoAppビルド成功。
- `SomCheck` の7ケースを初戦・再戦の2回ずつ完走。全獣が暴発前に臨戦態勢へ入り、席が塞がった一団だけ短縮。降る光のすべてが魔導書を経由し、回復・衣・放電・最終HPは元の台本と一致。
- ボス戦の群れ（最大5匹）、大隊戦の緊急、粛で消える光、ベニ入りの集光／散光、ソム不在を確認。
- 左右両陣営・1倍／4倍で威嚇・帯電・宝石・魔導書を描画して確認。走行中と臨戦態勢の一時停止、途中の倍速切替、帯電したまま保持、途中中断と終了時の消去を確認。
- 一時停止の再開時に完了済みTweenを再生しようとする問題を検出し、完了通知で再開対象から外すよう修正。
- `compare`／`dump` はそれぞれ `docs/balance.md`／`docs/units.md` と差分0行。`audit` はずれているファイル0件。生成物は変更しない。

キャプチャはGit除外の `design/art/som/game-check/stance/`、実行ログは `output/som-stance-*.log`。合格の目印は `SOM_STANCE_VISUAL_OK`（左右×速度4件）、実台本の `SOM_REPLAY_OK`、`SOM_CHECK_COMPLETE`。ヘッドレス終了時には従来のFont／CanvasItem解放警告が残る。

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
& 'C:\tools\Godot_v4.7.2-stable_mono_win64\Godot_console.exe' --headless --path DemoApp res://SomCheck.tscn -- --verify-only
& 'C:\tools\Godot_v4.7.2-stable_mono_win64\Godot_console.exe' --path DemoApp --windowed --resolution 1440x900 res://SomCheck.tscn -- --visual-only --swarm-only
```
