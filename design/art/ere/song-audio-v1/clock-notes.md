# エレ：演奏から時計の解放まで音楽を続ける

2026-10-29。ユーザーの追加要望により、3秒の演奏Cの後も曲を続ける比較版を作成。
比較後にユーザーが両曲を採用。現在の本番は発動ごとにA→Bを繰り返す（末尾の採用記録参照）。

- [A：Ah.mp3を時計の終わりまで](ere-clock-song-a.mp4)
- [B：Ah (1).mp3を時計の終わりまで](ere-clock-song-b.mp4)

両方とも味方→敵の順、341コマ／30fps／約11.37秒の実録音付き動画。
今回の1体へのギフトでは、演奏開始から時計の余韻が消えるまで約5.3秒間のフレーズになる。
演奏Cの3秒や受け渡し・時計の演出時間は延ばさない。

## 音源と寿命

提供曲を3秒に切らず、Aは0.08秒以降6.6935秒、Bは1.10秒以降5.66秒を保持。
原本・最初の3秒版・以前の動画は維持。音量は両方RMS約-18 dBFS、再生時-8 dB。
ピッチとテンポは変えず、ループなし。

開始の光は以前と同じ3.45秒で消えるが、音楽は別の `GatherCast3D` で継続する。
同じ演奏者の最初の追加手番の時計が動き始めると、`DirgeAura3D.ReleaseDuration` 後を曲の終了時刻にする。
最後の0.35秒をフェードし、針が進んだ後の光の余韻が消えるところで止める。
別の演奏者の時計では止めない。原曲の末尾が先なら自然終了し、引き延ばさない。
一時停止と速度は既存の共通時計に従い、倍速でも音高を変えない。
途中中断・演奏者の死亡・次の手番・再戦の終了処理では音も消す。

購入済み素材のチェンバロ候補と既存のギフトSEは保持。
今回の音楽はユーザー提供曲を使い、受け渡し・追加手番の既存SEも重ねて比較する。
原本の場所とSHA256は `clips-clock.json`、素材の配置先は `DemoApp/assets/audio/se/SOURCES.md`。
最終採用した2曲のWAVとimport情報はGit管理する。購入音源と不採用の3秒比較音源はGit対象外。

## 再作成

```powershell
D:/comfyui/venv/Scripts/python.exe design/art/ere/song-audio-v1/prepare.py --through-clock
dotnet build DemoApp/DemoApp.csproj --no-restore
Godot_console.exe --headless --editor --path DemoApp --import
Godot_console.exe --path DemoApp --fixed-fps 30 --write-movie C:/works/VillainProto/.tmp/ere-clock-song-a-game.avi res://EreGiftCheck.tscn -- --movie --ere-audio=clock-a
Godot_console.exe --path DemoApp --fixed-fps 30 --write-movie C:/works/VillainProto/.tmp/ere-clock-song-b-game.avi res://EreGiftCheck.tscn -- --movie --ere-audio=clock-b
D:/comfyui/venv/Scripts/python.exe design/art/ere/song-start-v1/export_audio_preview.py --source C:/works/VillainProto/.tmp/ere-clock-song-a-game.avi --target C:/works/VillainProto/design/art/ere/song-audio-v1/ere-clock-song-a.mp4 --sound "A: Ah.mp3 / through clock / -8 dB" --width 1152
D:/comfyui/venv/Scripts/python.exe design/art/ere/song-start-v1/export_audio_preview.py --source C:/works/VillainProto/.tmp/ere-clock-song-b-game.avi --target C:/works/VillainProto/design/art/ere/song-audio-v1/ere-clock-song-b.mp4 --sound "B: Ah (1).mp3 / through clock / -8 dB" --width 1152
```

## 確認

DemoAppビルドは警告・エラー0。
`EreGiftCheck -- --ere-audio=clock-a` / `clock-b` が両方合格。
3秒のモーションと光が終了した後も曲が残ること、時計の解放でフェードして終了すること、
フェード中の停止、原音ピッチでの再開・倍速、途中中断・死亡・再戦用の全消去を追加検査。
既存の敵味方・左右・実台本12戦・対象と件数・最終HP・再生2回の検査も合格。
Godot終了時の既存のRID／ObjectDB警告は残る。

Forward+で両曲とも描画・実録音に成功。動画の音を後から合成せず、そのままAACへ変換した。
音割れなし。実録音のピークはA=0.2922、B=0.2806。
時計表示中の実フレームを確認し、顔・HP・陣営リングの視認性と従来の時計演出を維持。
Git除外と差分の空白検査も確認。BattleCore／BattleSim／生成物は編集しない。

## A/B交互再生を採用

同日のユーザー指示により、通常の `ShowDirgePerformance` はA→B→A→Bの順で選択する。
順序はBattlefieldの戦闘ごとに持ち、演奏1回につき1つ進む。複数の受け渡し先や時計では選択し直さない。
再戦・再生し直しでは `ResetGatherPresentation` からAへ戻す。
演奏中断で音を消す処理と、次の曲の順序の初期化を分け、戦闘の途中でAへ戻らないようにする。
比較用の曲の明示指定は引き続き可能。片方がないときはある方、両方ないときは以前のチェンバロへ戻す。

[A→Bの実録音付き動画](ere-alternate.mp4)（味方へのギフトでA、続く敵へのギフトでB）。
同じ戦闘のまま2回演奏し、時計の余韻まで音を続けて切り替える。341コマ／30fps／約11.37秒。

```powershell
Godot_console.exe --path DemoApp --fixed-fps 30 --write-movie C:/works/VillainProto/.tmp/ere-alternate-game.avi res://EreGiftCheck.tscn -- --movie
D:/comfyui/venv/Scripts/python.exe design/art/ere/song-start-v1/export_audio_preview.py --source C:/works/VillainProto/.tmp/ere-alternate-game.avi --target C:/works/VillainProto/design/art/ere/song-audio-v1/ere-alternate.mp4 --sound "A then B / through clock / -8 dB" --width 1152
```

DemoAppビルドは警告・エラー0。無指定の `EreGiftCheck` はA→B→A→B→Aの実プレイヤーへの接続を検査し、
次がBになる状態から再戦してAへ戻ることも確認。停止・倍速・時計までの継続・中断・死亡・全消去の検査も合格。
`EreMotionCheck --installed` は本番Mainの同じ戦闘で2回演奏する台本を流し、最初A・2回目Bを確認して合格。
演奏C、開始の光、2体への受け渡し、時計、追加行動の接続と後片付けも確認した。

コミット前の確認では `audit` はずれ0件。ほかの作業の差分を含めず、ステージ済みのDemoApp／BattleCoreソース423ファイルを別ディレクトリへ展開して単独ビルドも成功した（未変更BattleCoreの既存2警告・エラー0）。採用A/BのWAVとimport設定も含め、購入素材の原本と不採用モーションの連番は含めない。
