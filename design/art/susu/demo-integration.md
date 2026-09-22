# ススの DemoApp 適用

## 採用素材

- 編成・選択画面: `DemoApp/assets/portraits/susu.png` ← `susu-standing-v1.png`。
- 戦闘中: `DemoApp/assets/portraits/battle/susu_idle_right.png` ← `susu-idle-right-v1.png`。
- 最初のちりとりを持つ右向き案を採用。手で灰を持つ別案は未採用。

画像は透過PNGをそのままコピー。`UiKit` の駒IDによる自動読込を使用する。
小柄なインプの体格として表示高を1.65に設定（ボルグ2.65、カド2.40）。
足元の下端余白率は0.0352。アルファ128以上の実体が最終行1481にあり、
1536pxのキャンバスに対して54pxの余白を補正する。
戦闘ロジック・数値・プリセットは変更していない。

## 検証

- `dotnet build DemoApp/DemoApp.csproj --no-restore`: 警告0、エラー0。
- 実ゲームでスス・ボルグ・カド・ノノ・ガルドを配置し、編成と戦闘画面を撮影。
  `selection-game-check.png` / `battle-game-check.png`。
- `capture-integration.gd` は表示確認専用。通常起動時の編成には影響しない。
- 既存ReleaseのBattleSimでcompareを実行し、勝率表の全行が一致。
- audit: ずれているファイル0件。
- 撮影終了時に既存のPartyBarアンカー警告とフォントRID解放警告が出た。画像読込エラーはない。

再撮影: Godotで `--path DemoApp --script ../design/art/susu/capture-integration.gd --audio-driver Dummy --rendering-method gl_compatibility`。

## 貯める・放つの差分

- 貯める: `susu_charging_idle_right.png` ← `susu-charge-right-v1.png`。
- 放つ: `susu_release_idle_right.png` ← `susu-release-right-v3.png`（袋を右上へ向けて全放出）。
- 貯める通知で既存の `ChargeAura3D` とチャージ音を開始。放出通知で解除して攻撃絵へ切り替え、
  同時着弾と見せ場の表示後に通常のちりとり絵へ戻す。敵が1体でも着弾まで攻撃絵を保つ。
- `AshTrait` は既存の周期分岐から表示イベントだけを追加。UI側で周期を数え直さない。
- 燃焼通知が来ても貯める／放つ絵を保ち、死亡・再生終了・勝利時に状態を解除。
- 放出画像は灰の領域を含むためキャンバス表示高1.5倍・足元余白0.0196・横補正0.25。
  本体の背丈と足元を通常絵に揃え、敵側表示では横補正も反転する。

検証: `SusuPortraitCheck.tscn` で実際の戦闘の貯める→放つ→通常復帰、チャージ演出、
燃焼、死亡、復帰、灰なしの通常攻撃時の解除、勝利絵の保護を確認。
`PortraitStateCheck.tscn` の既存キャラの検査も合格。
compare は今回の変更前後で全行一致。`susu check` の灰収支も一致、auditずれ0件。
DemoAppビルドは警告0・エラー0。BattleSimは既存診断コードの警告12件・エラー0。

再検証: Godotで `--path DemoApp res://SusuPortraitCheck.tscn --audio-driver Dummy --rendering-method gl_compatibility -- --capture --demo-stage=1 --demo-seed=0`。
確認画像は `action-charging-game-check.png` / `action-release-game-check.png` / `action-returned-game-check.png`。
