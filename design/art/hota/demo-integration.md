# ホタの DemoApp 適用

## 採用素材

- `DemoApp/assets/portraits/battle/hota_idle_right.png`: 非燃焼。
- `DemoApp/assets/portraits/battle/hota_burning_idle_right.png`: 燃焼。
- `DemoApp/assets/portraits/hota.png`: 第2案の左側に描かれた正面・非燃焼の立ち絵を単独素材化し、編成・勝利表示に使用。戦闘用の構えとは分離。

元の2枚は維持。内蔵 image_gen で格子背景を単色緑に置換した画像を採用した。PNG自体は透過ではなく、既存の表示シェーダーで背景を抜く。緑単色を検出した場合だけ輪郭の緑の混色も除去する。両差分は1024×1536で、足元の余白率は共通0.012。

背景置換プロンプトの指示: Edit ONLY background. Replace ALL gray/white checkerboard with flat uniform pure chroma-key GREEN RGB(0,255,0) #00FF00. Preserve exact character identity, pose, costume, sword, scale and 1024x1536 framing. All empty spaces among hair, limbs and flames must also be green. No green bounce light, no shadows or gradient, no checkerboard, opaque green background for shader removal. 燃焼版では既存の炎とその輪郭も維持するよう指定。

## 状態の扱い

`Main` が既に再生していた燃焼付与と状態スナップショットの通知を `BattlePawn3D.SetBurning` で受ける。通常と燃焼のテクスチャをSprite3Dとシェーダーの両方で更新する。差分がない駒は通常画像を使う。死亡時は通常へ戻し、勝利後の通知では勝利画像を上書きしない。戦闘ルールは変更していない。

## 確認

- DemoApp ビルド: 警告0・エラー0。
- `PortraitStateCheck.tscn`: 非燃焼、燃焼、消火、死亡、復帰、勝利、差分なしのフォールバック、敵側の反転が合格。
- 描画確認: `portrait-state-check.png`。輪郭の緑を除去後、再撮影して確認。
- 燃焼（ボルグ×ホタ）: `DEMO_SMOKE_COMPLETE events=96 turns=4 won=True`。終了時にGodotのフォント/RID解放警告あり。
- 既存ReleaseのBattleSimで `compare`: docs/balance.md の61行すべて一致。
- 同 `audit`: ずれているファイル0件。

状態確認はGodotで `--path DemoApp res://PortraitStateCheck.tscn` を実行する。描画保存は末尾に `-- --capture` を付ける（headlessでは付けない）。
