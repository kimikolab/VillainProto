# ハギ・前傾突進の踏み込み

2026-10-25。「もっと前傾姿勢で突っ込む感じ」に対応。
腰を後ろへ引き、頭と肩を先行させ、胴を前へ倒した踏み込みへ変更した。
鎌は腰の前で低く横に構える。両手で同じ一直線の柄を握り、灰色／くすんだ赤の布と髪を後方へ流す。

原本は `hagi-charge-right-v2.png`（1145×1374、RGBA透過）。
本編 `DemoApp/assets/portraits/battle/hagi_step_idle_right.png` と同一。
変更前のゲーム画像は `previous-game-step.png` に保存し、`step-v1/` の原本・動画も保持した。
生成指示は `generation-prompts.json`。柄が曲がった案や大きな穴が2つある案は採用していない。
採用版は大きな楕円の透過穴1つ、外側の小さな楕円は金属で塞いでいる。

高さ1374/1536の補正で、待機絵と画素あたりの体格を揃える。前傾して低くなった胴を引き伸ばさない。
前足の靴底1247pxを接地させ、下余白は127/1374。銀の刃の伸長も、横に構えた鎌の輪郭へ合わせ直した。
移動・溜め・振り抜きの時間、戦闘判定やダメージは変更していない。
エフェクト素材は既存のEffectBlocks光粒と銀の弧を継続使用する。

## 確認

- DemoAppビルド成功。
- `HagiPresentationCheck`で9戦の台本・勝敗・最終HP・攻撃／刈り取り件数が一致。
- 新画像の接地・画素スケール・停止・倍速・敵側の反転・中断／再戦・シークを確認。
- `HagiMovieCapture`で踏み込みの前傾、握り、振りかぶりへの接続を実画面で目視確認。
- 終了時の既存Godot RID／ObjectDB解放警告は残る。

動画は周囲の演出を省いた確認用。本編の描画処理・実戦の攻撃列を使用。

- [刈り取り・等速MP4](video/hagi-reap-charge-1x.mp4)
- [刈り取り・0.5倍速GIF](video/hagi-reap-charge-half-speed.gif)
- [連続攻撃・等速MP4](video/hagi-chain-charge-1x.mp4)
- [連続攻撃・0.5倍速GIF](video/hagi-chain-charge-half-speed.gif)
