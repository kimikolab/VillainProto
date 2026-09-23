# 責め苦のシガ 立ち絵適用

- 正面待機: `DemoApp/assets/portraits/shiga.png` ← `shiga-front-v1.png`
- 戦闘右向き: `DemoApp/assets/portraits/battle/shiga_idle_right.png` ← `shiga-idle-right-v1.png`
- 承認済み案: `shiga-concept-v2.png`
- 生成: Codex 内蔵 image_gen。全文は `shiga-production-v1-prompts.md`。

両方1024×1536の透過PNG。ロングヘアー、露出を増やした赤紫の衣装、巻き角、尻尾、鞭を維持。
正面は追加編集で余分な鞭の分岐を除去。戦闘は右向きで鞭を前に構え、もう片方の手を胸元に寄せる。
戦闘絵のアルファ128以上の最下端は1487行目（0始まり）。下端48px/1536 = 0.03125を `UiKit.BattlePortraitBottomPaddingRatio` に設定。
既存のID命名規約で自動読込。戦闘ロジック・数値の変更なし。

## 確認

- `dotnet build DemoApp/DemoApp.csproj --no-restore`: 警告0・エラー0。
- `selection-game-check.png` / `battle-game-check.png`: クグと同じ編成で表示、背景透過、右向き、足元を目視確認。
- 撮影終了コード0、`SHIGA_ART_CAPTURE result=0`。
- 撮影環境のログ・シェーダーキャッシュの制限、証明書読込と既存のアンカー・終了時RID解放警告あり。撮影は完了。

再撮影: `Godot --path DemoApp --script ../design/art/shiga/capture-integration.gd --audio-driver Dummy --rendering-method gl_compatibility`

## 怖気づき差分

- `DemoApp/assets/portraits/battle/shiga_frightened_idle_right.png` ← `shiga-frightened-right-v1.png`。
- 内蔵 image_gen。全文は `shiga-frightened-v1-prompt.md`。
- 1024×1536透過PNG。最下端1503（アルファ128以上）、下端32px/1536 = 0.02083。
- 肩をすくめ、鞭を胸元へ引き寄せ、得意げな顔が驚きに変わる右向き差分。
- 既存の `Stun / Struck` の書き手と対象が同じシガの場合だけ切替。敵から受けた痺れで新たに怖気づき絵にはしない。
- 手番喪失の表示後、痺れなしのスナップショット、死亡・復活・戦闘終了で解除。新しい再生では新しい駒を作る。
- 戦闘コア・数値・台本形式は変更なし。

確認: DemoAppビルド成功（警告0・エラー0）。第二波seed0の実戦で `SHIGA_FRIGHT_CAPTURE start=true recovered_alive=true`、終了コード0。
`frightened-game-check.png` / `recovered-game-check.png` で専用絵、通常絵への復帰、透過と足元を確認。

再撮影: `Godot --path DemoApp --script ../design/art/shiga/capture-frightened.gd --audio-driver Dummy --rendering-method gl_compatibility -- --demo-stage=1 --demo-seed=0`
