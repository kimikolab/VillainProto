# エレ：Cを維持した演奏開始の光と音符（2026-10-29）

ユーザーは、演奏モーションの比較では既存Cが自然と評価した。
戦闘画面ではCも新A／Bも通常の立ち絵との差が読み取りづらいため、演奏開始からエフェクトを出す方針を採用。

## 表示

- Cの48コマ／16fps／3秒を維持する。原画と48コマは `performance-v2/canonical-before.json` のSHA-256と一致。
- `Main.HoldDirgeSong` の開始時に、楽器の光と小さな光片を出す。
- エレの腰から楽器の周りを金色の五線譜と7つの音符が巡る。青白い音符を少量交ぜる。
- 足元に明るい金色の光だまりと薄い波紋、上へ立ち上がる扇状の光。顔・HP・陣営リングは読み取れる範囲に保つ。
- 既存の3秒の演奏と同時に進める。演奏後の0.45秒だけギフトの受け渡しと重ねて消し、待ち時間を追加しない。
- 対象の選び直し、BattleCoreの変更、テロップの追加はしない。

`BattlefieldView3D.ErePerformance.cs` が既存の `GatherCast3D` を使い、停止・倍速・終了・再戦を共有する。
`ere_song_light.gdshader` も同じ時計の値で描き、シェーダーの `TIME` は使わない。
描画の後片付けからCのモーションを切り替えないため、演奏の終端保持や受け渡しを巻き戻さない。

## 足元を舞台照明として強調

同日の追加要望で、足元の光をスポットライトの代わりとして強めた。
床の光の中心係数を0.24→0.50、描画面の直径を3.25→3.65へ広げ、外周と波紋も少し明るくした。
`ere_song_uplight.gdshader` の薄い扇状の光を足元から重ね、胸より上で消すことで顔の白飛びを避ける。
光の板は2.25×2.10、足元から同じ時計で呼吸し、音符と一緒に余韻で消える。
Cとギフトの時刻は維持。購入素材の開始時の光片も継続利用する。

追加調整後のビルドは警告・エラー0。`EreGiftCheck` の停止・倍速・死亡・再戦・実台本の検証に合格。
Forward+の連続描画でも顔・HP・青い陣営リングを確認。
[舞台照明版GIF](ere-song-uplight.gif)／[舞台照明版の動画](ere-song-uplight.mp4)。
再作成：`export_preview.py --source C:/works/VillainProto/.tmp/ere-uplight-v2 --name ere-song-uplight`。

## 購入素材

### 演奏開始音の試用（2026-10-29）

ユーザーの依頼で400 Sounds Packの `Musical Effects/harpsichord_mystery.wav` を採用して試す。
原本を変更せず `ere_harpsichord.wav` としてコピーし、演奏開始0.02秒後に一度だけ-6 dBで再生。
約1.497秒、原音の高さ、ループなし。フェードイン0.02秒、演出の終端では0.18秒でフェード。
通常速度では原音の自然な余韻が先に終わる。既存の受け渡し／追加手番音へつなぐ。
コピー元と導入手順は `DemoApp/assets/audio/se/SOURCES.md`。購入音源とimportファイルはGit除外。

`EreGiftCheck` で音源の実再生、停止、倍速でのピッチ維持と再開を追加検証し合格。
音声位置の停止検査はミキサーのブロック単位の揺れを考慮し、0.12秒待機に対して許容0.025秒。
DemoAppビルドは警告・エラー0、`EreMotionCheck --installed` も合格。
Forward+のMovieWriterから実際の映像と音を同時収録。341コマ／30fps、約11.37秒。
MP4は音量の正規化やSEの後付けをせず変換。実音声のピーク0.2256、演奏開始区間のピーク0.1217。
[音付きプレビュー](ere-harpsichord-trial.mp4)。

```powershell
Godot_console.exe --path DemoApp --fixed-fps 30 --write-movie C:/works/VillainProto/.tmp/ere-harpsichord-game.avi res://EreGiftCheck.tscn -- --movie
D:/comfyui/venv/Scripts/python.exe design/art/ere/song-start-v1/export_audio_preview.py
```

購入済み EffectBlocks v4の `energy/electric_waves.tscn` と `impacts/impact_3.tscn`、採用記録を確認。
雷の輪は音楽に見えないため使わず、`impact_3` だけを楽器の小さな光片として追加採用。
既存ヘルパーがインスタンスの材を複製し、金色・小さい粒・短い寿命に調整する。原本は変更しない。
五線譜と足元の光は専用のコードとシェーダー。購入素材なしでも主な演出は表示できる。

## 確認

- DemoAppビルド成功。未変更BattleCore内の既存2警告が残る。
- `EreGiftCheck`：開始から表示、文字なし、停止、倍速、演奏者の死亡、余韻の消去、再戦、実台本の対象・件数・最終HPを確認。
- `EreMotionCheck --installed`：頭なしとForward+の両方で合格。本番Mainで演奏Cと開始の光を同時に停止し、演奏→2体への受け渡し→追加手番への接続を確認。
- `EreGiftCheck --clip`：Forward+で演奏開始から味方／敵へのギフトと追加攻撃までを連続描画。描画エラーなし。
- Godot終了時の既存のRID／ObjectDB警告は残る。
- `audit` はずれ0件。`compare` / `dump` も再出力したが、同時進行の未コミットのリリ調整が含まれる。既存生成物との差は反撃編成の第2波99.5→100.0の1セルと、リリの説明・保持札。今回の編集はDemoAppと演出記録のみで、BattleCore／BattleSim／生成物には書き込んでいない。

実描画の連番は `.tmp/ere-song-start-v1/`、検証ログは `.tmp/ere-song-start-*.log`。
[プレビューGIF](ere-song-start.gif)／[全体の確認動画](ere-song-start.mp4)（味方→敵・無音）。
`export_preview.py` で再作成できる。モーション候補A／Bの選択結果は `performance-v2/notes.md`。
