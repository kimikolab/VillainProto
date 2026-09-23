# ヒサの立ち絵適用

- 正面: `DemoApp/assets/portraits/hisa.png` ← `hisa-front-v1.png`
- 戦闘（右向き）: `DemoApp/assets/portraits/battle/hisa_idle_right.png` ← `hisa-idle-right-v1.png`

内蔵 image_gen で作成。プロンプトは `hisa-production-v1-prompts.md`。
採用コンセプト v2 の顔・衣装を維持し、武器と術式エフェクトは付けない。
両画像は1024×1536の透過PNG。生成画像の透明部分には背景色RGBが残るが、実ゲームではアルファで透過することを確認。
戦闘絵のアルファ128以上の最下端は1512行目（0始まり）。下端23pxの余白率0.0150を UiKit に設定。
表示高は既定2.25を使用。

## 検証

- DemoApp ビルド成功、警告0・エラー0。
- 実際の編成画面と戦闘画面で画像読込・背景透過・足元を確認。
- `selection-game-check.png` / `battle-game-check.png` に確認画像を保存。
- 検証起動は終了コード0、`HISA_ART_CAPTURE result=0`。
- 実行環境のログ・シェーダーキャッシュ書込制限、証明書読込エラー、および既存のアンカー・終了時RID解放警告が出たが、撮影は完了。
- 戦闘ロジック・数値の変更なし。今回のコード変更は画像下端の余白設定1行のみ。

再撮影: `Godot --path DemoApp --script ../design/art/hisa/capture-integration.gd --audio-driver Dummy --rendering-method gl_compatibility`
