# 縛めのクグ 立ち絵適用

- 正面待機: `DemoApp/assets/portraits/kugu.png` ← `kugu-front-v1.png`
- 戦闘右向き: `DemoApp/assets/portraits/battle/kugu_idle_right.png` ← `kugu-idle-right-v1.png`
- 承認済み案: `kugu-concept-v1.png`
- 生成: Codex 内蔵 image_gen。全文プロンプトは `kugu-production-v1-prompts.md`。

両方1024×1536の透過PNG。六つの琥珀色の目、墨色と紫の装甲、糸巻きの手甲、割れた腰布を維持。
戦闘絵のアルファ128以上の最下端は1489行目（0始まり）。下端余白46px / 1536 = 0.02995を `UiKit.BattlePortraitBottomPaddingRatio` に追加。
画像は既存のID命名規約で読み込まれる。戦闘ロジック・数値は変更していない。

## 確認

- `dotnet build DemoApp/DemoApp.csproj --no-restore`: 成功、警告0・エラー0。
- `selection-game-check.png` / `battle-game-check.png`: 実画面で透過、表示、右向き、足元を目視確認。
- 撮影プロセス終了コード0、`KUGU_ART_CAPTURE result=0`。
- 撮影時には環境のログ・シェーダーキャッシュ書込制限、証明書読込エラー、既存のアンカー・終了時RID解放警告あり。撮影は完了。

再撮影: `Godot --path DemoApp --script ../design/art/kugu/capture-integration.gd --audio-driver Dummy --rendering-method gl_compatibility`

## 拘束維持の立ち絵と糸の演出

- `DemoApp/assets/portraits/battle/kugu_binding_idle_right.png` ← `kugu-binding-right-v1.png`。
- 内蔵 image_gen で生成。全文は `kugu-binding-v1-prompt.md`。
- 1024×1536、実透過PNG。アルファ128以上の最下端1470、下端余白率65/1536 = 0.04232。
- `BattlePawn3D.ActionPortrait` で拘束中だけ専用絵へ変更。
- `BindingSilk3D` は三本の糸が伸び、対象に二周半の帯が締まる。維持中は両者の表示位置へ追従し、解除時は緩んで消える。
- `BattlefieldView3D.Binding` は書き手のInstanceIdごとに保持。片方の死亡・戦闘終了・新しい戦闘開始で表示を解除する。
- 開始は既存の `StatusGain / Grappled / Amount=1`。解除は `GrappleTrait.Release` から表示専用の `Amount=0` を出す。被弾条件などを表示側で再判定しない。
- 開戦時の大縛りは既存の痺れ表示のまま。この専用絵と持続する糸は手番の組み付きに対応する。

### 検証

- ビルド: 警告0・エラー0。
- `compare` の勝率表は追加前後で全文一致（ビルド時の既存警告の出力順だけが異なる）。`audit` は不整合0件。
- クグ2体を含む5波×40seed＝200戦: 拘束開始885件・解除456件のActorId/TargetId対応を検査。verbose有無で勝敗・ターン・生存数・全ユニット帳簿が一致。
- 実戦再生: `KUGU_BINDING_CAPTURE start=true release=true reset=0`。
- 複数書き手の個別解除、対象死亡時の掃除、リセット後のポーズ復帰を確認: `KUGU_BINDING_LIFECYCLE multi_source=ok target_death_cleanup=ok reset_pose=ok`。
- `binding-game-check.png` / `released-game-check.png` を目視確認。撮影終了コード0。撮影環境の警告は上記と同じ。

再撮影・表示の検証: `Godot --path DemoApp --script ../design/art/kugu/capture-binding.gd --audio-driver Dummy --rendering-method gl_compatibility`
