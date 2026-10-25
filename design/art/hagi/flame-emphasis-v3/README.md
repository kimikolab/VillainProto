# ハギ：赤い炎の強調

2026-10-26。「三日月のシルエットはOK、赤い炎をもう少し派手に」という要望を反映した。

- [通常攻撃→刈り取り→連撃、通常速度・ゲーム実音声](video/hagi-flame-emphasis-1x.mp4)
- [刈り取り、半速GIF](video/reap-half.gif)
- [連撃、半速GIF](video/chain-half.gif)
- [静止画](peak.png)
- [変更前の三日月版](../crescent-sweep-v2/README.md)

変更は `hagi_reap_aura.gdshader` の炎だけ。内側への広がりを約23%増やし、透過の薄さを抑え、赤の彩度と発光を強めた。太く長い火の筋と内側へ巻く二本目の筋を加え、余韻も少し長くした。白い刃のシェーダー、三日月の形・幅、水平の軌道、命中時刻、時計、攻撃の原画と打数には変更を加えていない。

購入済み素材の採用記録と使用中のEffectBlocks `sparkle.png` を確認し、既存の火花を継続利用した。素材原本とBattleCore、生成済みdocsは変更していない。

今回の確認はGPU実描画と実音声の60fps録画。518フレーム、通常1打、列の刈り取り1打2体、連撃2打2体で正常終了し、シェーダーのコンパイルエラー・描画エラー・終了警告はない。前版との比較で炎の広がりと赤、顔とHP表示、承認済みの三日月の形を確認した。前版の9台本検査は [前版の検査記録](../crescent-sweep-v2/verification.json) にあり、今回の炎の見た目調整では再実行していない。今回の記録は [verification.json](verification.json)。

```powershell
& 'C:/tools/Godot_v4.7.2-stable_mono_win64/Godot_console.exe' --path DemoApp --fixed-fps 60 --write-movie 'C:/works/VillainProto/output/hagi-movie/raw-flame-emphasis-v3.avi' res://HagiMovieCapture.tscn -- --hagi-acrobatics --hagi-movie-dir=res://../design/art/hagi/flame-emphasis-v3/video
& 'D:/comfyui/venv/Scripts/python.exe' -X utf8 design/art/hagi/attack-acrobatics-v1/encode_preview.py --raw output/hagi-movie/raw-flame-emphasis-v3.avi --output-dir design/art/hagi/flame-emphasis-v3/video --name hagi-flame-emphasis
```
