# ハギ第2版の返し刃・実装確認プレビュー

2026-10-26。`CODEX_BRIEF_HAGI_2.md` を受けて追加済みの演出を確認するための録画。戦闘や演出の仕様は変更せず、専用の撮影シーンを追加した。

- `video/hagi-return-preview.mp4`: 3場面をそれぞれ通常速度 → 1/4速の順に並べた約26秒の比較。無音。
- `video/after-hit-return-quarter.gif`: 通常斬撃の命中直前から返し刃、振り抜きまでの拡大GIF。1/4速。
- `video/*-1x.mp4`: 各場面の通常速度・ゲーム内SE付き。
- `video/*-quarter.mp4`: 待機から踏み込み、帰還までを残した各場面の1/4速。無音。

## 撮影した実台本

| 場面 | 台本 | 出来事 | 通常の振り / 返し刃 / 刈り取り人数 |
| --- | --- | --- | --- |
| 通常斬撃から返し刃 | 試遊・死神と墓守 × 近衛 | 341〜346 | 1 / 1 / 1 |
| 孤立した敵への二度斬り | 試遊・ハギ 哀歌 × ボス | 30〜37 | 2 / 0 / 0 |
| 連続して刈る | 試遊・ハギ キャリー × 大隊 | 457〜488 | 2 / 3 / 3 |

最初の場面は通常斬撃80で敵のHPが195になり、その直後の返し刃195で0になる。二度斬りは通常の2振りであり、命中後の刈り取りを行う返し刃とは別の動作として比較に入れた。

`Main.EnterViewerLive` に完全な実台本を渡し、`SeekTo` で該当箇所へ移動して通常速度で再開した。表示のために攻撃やダメージを作っていない。録画中の振り・返し刃の件数が選択した実台本と一致することを撮影シーン内で検査している。元の時刻、出来事、HP、使用セルは `capture-segments.json` に保存。

見やすさのため撮影シーンだけで無関係な駒を非表示にし、シーク前の一時エフェクト・古い数字を消してカメラを寄せた。対象の状態、位置関係、攻撃、命中、HP変化、死亡の再生は本編の処理を使用。末尾は停止して余韻を見せる。拡大GIFは踏み込み前の待機を省略しているため、全動作はMP4で確認する。

1/4速は録画の時間軸だけを4倍にし、動きの補間・武器や斬撃の後付けをしていない。見出しはプレビュー動画のみに追加。

## 再現

リポジトリ直下から実行する。原録画は `output/` に置き、配布する動画と収録情報だけをこのフォルダへ保存する。

```powershell
dotnet build DemoApp/DemoApp.csproj --no-restore -v:q
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --write-movie 'C:/works/VillainProto/output/hagi-return-preview/raw-final.avi' --fixed-fps 60 --disable-vsync res://HagiReturnPreviewCapture.tscn --log-file 'C:/works/VillainProto/output/hagi-return-preview/gpu-final.log'
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 design/art/hagi/return-preview-v1/encode_preview.py --raw output/hagi-return-preview/raw-final.avi
```

GPUはVulkan Forward+ / RTX 5090。最終ビルドは警告0・エラー0。3場面の接触件数は一致し、収録は終了コード0で完了した。終了時だけ既存のFontAdvanced RID / CanvasItem / ObjectDB解放警告が出る。詳細と動画の形式は `verification.json`。
