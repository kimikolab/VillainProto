# ザンの立ち絵適用

- 正面: `DemoApp/assets/portraits/zan.png` ← `zan-front-v2.png`
- 戦闘: `DemoApp/assets/portraits/battle/zan_idle_right.png` ← `zan-idle-right-v1.png`

内蔵 image_gen で承認済み concept v4 から生成。プロンプトは `zan-production-v1-prompts.md`。
正面は v2 で重心と手の高さに差をつけた非対称の立ち姿へ更新。編集プロンプトは `zan-front-v2-prompt.md`。透過と実ゲームの編成表示を再確認し、撮影起動は終了コード0。元の v1 は保存。
両方1024×1536の透過PNG。紺のフード、長い銀灰色の髪、長袖、手甲、細身の双短剣を維持。
戦闘絵のアルファ128以上の最下端は1523行目（0始まり）。下端12pxの余白率0.0078を UiKit に設定。表示高は既定2.25。

## 確認

- DemoApp ビルド成功、警告0・エラー0。
- 編成・戦闘画面で画像の読込、背景透過、向き、足元を目視確認。
- `selection-game-check.png` / `battle-game-check.png` に撮影結果を保存。
- 撮影起動は終了コード0、`ZAN_ART_CAPTURE result=0`。
- 実行環境のログ・シェーダーキャッシュ書込制限、証明書読込エラーと既存のアンカー・終了時RID解放警告あり。撮影は完了。
- 戦闘ロジック・数値の変更なし。今回のコード変更は画像下端の余白設定のみ。

再撮影: `Godot --path DemoApp --script ../design/art/zan/capture-integration.gd --audio-driver Dummy --rendering-method gl_compatibility`
