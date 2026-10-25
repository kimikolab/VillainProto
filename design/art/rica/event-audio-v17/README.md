# リィカの覚醒・着地・吸収SE v17

2026-10-25。指定された5つのSEを本編とFxViewerへ接続する。
以下は初版の記録。着地音は[v18で-6 dBへ調整](../landing-audio-v18/README.md)した。

| 位置 | 指定音源 | 音量 |
|---|---|---:|
| 3層の段へ上昇 | Plus Damage 001 | -8 dB |
| 6層の段へ上昇 | Plus Damage 003 | -8 dB |
| 9層の段へ上昇 | Plus Damage 006 | -8 dB |
| 三連撃の最後の接地 | Mecha Limb Explosion 001 | -12 dB |
| 味方から光が流れ始める | Effect Success 001 | -16 dB |

元ファイルと配置名は [SOURCES.md](../../../../DemoApp/assets/audio/se/SOURCES.md)。原本は無加工でコピーし、[SHA256](source-check.json)で5本すべての一致を確認した。音源本体とインポート情報はGitから除外する。

## 再生の条件

覚醒は `ApplyDeathState` で実層の写しの段が上昇したときだけ。減衰・同じ段での積み増しでは再生せず、0層から9層など一度に進む場合は到達段の音を鳴らす。

着地は実際の `rica_descend` の接触を待ち、高さが0になった `ShowGraveSlash` の入口で1回再生する。3体に命中しても3音にはしない。従来のSword Critical・刀音の重ね合わせも継続する。

吸収は生きている同陣営のリィカへ光を出す場合だけ。複数のリィカがいても1音とし、180ms以内の連続した流入はまとめる。音だけの判断で層・HP・攻撃件数は変えない。

専用の3枠（覚醒・着地・吸収）を使い、斬撃の4枠と競合しない。ピッチ1で速度による音程変更なし。一時停止に追従し、非表示・再戦で停止と吸収の抑制時刻をリセットする。未配置の専用音は省略する。

## 確認

[ゲームの実音と実映像・22秒](rica-awaken-absorb-landing-v17.mp4)。3層→6層→9層→仲間3体から吸収→三連撃の着地の順。
Godot Movie Makerが実際のゲームの音声ミックスと描画を同時に記録したもの。後から音を重ねていない。BGMなし。

FxViewerの「死・リィカの覚醒」→「リィカ 覚醒・光の吸収（実戦）」でも、実台本から段の変化と吸収を確認できる。`--fx-cue=live.rica.death` で直接選べる。段別攻撃の既存Cueでも初期段の覚醒と落下斬りの着地音を確認できる。

- ビルド：警告0・エラー0。
- RicaMotionCheck：頭なしで520項目通過。7攻撃・4段階・左右反転・接触・停止・速度・中断・再戦・HP。
- DeathAxisCheck：15戦で観測有無の実台本と結果が一致し、再戦も通過。
- FxViewer：8 Cue・左右反転を通過。新しい実戦Cueのプリセット解決エラーなし。
- 実録画：[capture.json](capture.json)。覚醒3回・吸収1音（光は3体分）・着地1音（攻撃は3打）を検査した。
- [動画検証](video-check.json)：660コマ・30fps・1440×900、全コマ再デコード。5箇所すべてに録音信号を確認し、音声ピーク0.377でクリップなし。
- ログは `output/rica-motion-integration/v17-*.log`。終了時の既存RID・Font・ObjectDB解放警告は残る。

## 再生成

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore
New-Item -ItemType Directory -Force .tmp/rica-event-audio-v17
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --windowed --resolution 1440x900 --write-movie C:/works/VillainProto/.tmp/rica-event-audio-v17/review.avi --fixed-fps 30 --disable-vsync res://RicaMotionCheck.tscn -- --rica-event-audio-capture
& 'D:/comfyui/venv/Scripts/python.exe' design/art/rica/event-audio-v17/encode_review.py
```
