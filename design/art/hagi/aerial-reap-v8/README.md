# ハギ：三日月の刃を細く鋭く

2026-10-27。v7が太すぎて赤いバナナに見えるという確認への調整。空中斬撃の専用シェーダーだけを変更し、中央の厚みを半径0.56から0.30へ減らした。厚みの分布を絞って切っ先へ長く細く抜き、白い芯を外刃に沿う細い線へ限定。輪郭のぼかしと赤い外光を抑え、丸く膨らんだ発光をなくした。

一本の三日月を共有して刈り取り時に増光する仕組み、216度の開いた弧、縦斬りの面、原画とモーションは継続。新規画像生成なし。購入済みEffectBlocksの一覧と実sparkle.pngを確認し、既存光粒を継続使用した。

- [通常速度・実戦3場面](video/hagi-aerial-reap-v8.mp4)
- [ボスへの止め・半速](video/air-boss-finish-fx-half.mp4)
- [半速GIF](video/air-boss-finish-fx-half.gif)

Main.EnterViewerLiveのGPU実描画を収録。切り出し・見出し・半速化以外の加工なし。GPU上でシェーダーのコンパイルと3場面の再生を確認し、全6動画/GIFをデコードした。C#は変更しておらず再ビルド・既存の全動作検査は繰り返していない。Godot終了時の既存Font/CanvasItem/ObjectDB解放警告は残る。

今回とv7のshader-snapshot.gdshaderを比較用に保存。検証はverification.json、撮影区間と実命中はcapture-segments.json。再現はv7 READMEの録画・encode_preview.py・verify_preview.pyのパスをv8へ置き換える。
