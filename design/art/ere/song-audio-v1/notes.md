# エレ：提供曲2本の適用比較

追加要望の「時計が終わるまで音を続ける」版は [clock-notes.md](clock-notes.md)。以下は最初の3秒版の記録。

2026-10-29。ユーザー提供の約7秒のSuno生成音源を、現在の演奏Cと光・音符に重ねた。
この最初の比較時点では通常のMainは前回のチェンバロを維持。後に時計まで流すA/Bを両方採用し、交互再生へ移行した（上記の追記参照）。

| プレビュー | 提供音源 | 原本長 | 切り出し区間 |
|---|---|---|---|
| [A](ere-ah-a.mp4) | Ah.mp3 | 6.7735秒 | 0.08〜3.08秒 |
| [B](ere-ah-b.mp4) | Ah (1).mp3 | 6.7600秒 | 1.10〜4.10秒 |

Bは冒頭に静かな部分があるため、1.10秒から採る。音量比較のため、両方RMS約-18 dBFSへそろえて
ゲーム内では-8 dBで再生。原本のピッチとテンポを維持。原本には書き込まない。
加工は3秒の切り出し・音量・切断点10msのフェードのみ。
ゲーム側は演奏開始20ms後から鳴らし、終端240msでフェード、演奏3秒で停止する。
既存の受け渡し／追加手番のSEも含めて比較できる。

両動画とも味方へのギフト→敵へのギフト、各341コマ／30fps／約11.37秒。
音はGodot MovieWriterの実録音をそのままAACへ変換。動画だけ音を後付けする方式は使わない。
1152×720。映像と音源の切り出し記録は各JSONに保存。

## 再作成

提供音源の場所とSHA256は `clips.json`、配置手順は `DemoApp/assets/audio/se/SOURCES.md`。
`prepare.py` は PyAV と NumPy を利用。購入素材からの前回候補も残し、今回は提供曲を比較用に接続。

```powershell
D:/comfyui/venv/Scripts/python.exe design/art/ere/song-audio-v1/prepare.py
dotnet build DemoApp/DemoApp.csproj --no-restore
Godot_console.exe --headless --editor --path DemoApp --import
Godot_console.exe --path DemoApp --fixed-fps 30 --write-movie C:/works/VillainProto/.tmp/ere-ah-a-game.avi res://EreGiftCheck.tscn -- --movie --ere-audio=a
Godot_console.exe --path DemoApp --fixed-fps 30 --write-movie C:/works/VillainProto/.tmp/ere-ah-b-game.avi res://EreGiftCheck.tscn -- --movie --ere-audio=b
D:/comfyui/venv/Scripts/python.exe design/art/ere/song-start-v1/export_audio_preview.py --source C:/works/VillainProto/.tmp/ere-ah-a-game.avi --target C:/works/VillainProto/design/art/ere/song-audio-v1/ere-ah-a.mp4 --sound "A: Ah.mp3 / 0.08-3.08s / -8 dB" --width 1152
D:/comfyui/venv/Scripts/python.exe design/art/ere/song-start-v1/export_audio_preview.py --source C:/works/VillainProto/.tmp/ere-ah-b-game.avi --target C:/works/VillainProto/design/art/ere/song-audio-v1/ere-ah-b.mp4 --sound "B: Ah (1).mp3 / 1.10-4.10s / -8 dB" --width 1152
```

## 確認結果

- DemoAppビルド成功。既存の未変更BattleCore内2警告、エラー0。
- `EreGiftCheck -- --ere-audio=a` と `b` が両方合格。音源の配置と実再生、原音ピッチ、停止・再開・倍速、死亡・中断・再戦・対象・件数・最終HP・後片付けを検査。
- Forward+で両曲を録画。描画エラーなし、演奏C・足元の光・音符を維持。
- 実録音に音割れなし。最大ピークA=0.3028、B=0.2653。
- 演奏中0.5〜3.0秒の250msごとのRMSを調べ、A=-31.2〜-22.5 dBFS、B=-36.3〜-24.2 dBFS。前回のような長い無音区間なし。
- 音源2本とimportファイルのGit除外を確認。BattleCore／BattleSim／生成物は編集しない。
