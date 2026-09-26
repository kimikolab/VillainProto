# 血詠みのアカ DemoApp適用

## 採用素材

- 通常待機: aka-standing-v2.png → DemoApp/assets/portraits/susu.png。
- 戦闘待機: aka-idle-right-v2.png → DemoApp/assets/portraits/battle/susu_idle_right.png。
- 溜め・放出: 同じ戦闘待機絵を susu_charging_idle_right.png / susu_release_idle_right.png に適用。専用ポーズは未制作。

内部IDは susu のまま。名前・フレーバーは既存の第214期の変更を使用し、戦闘ロジックは変更していない。
表示高は成人として2.50。戦闘画像の足元余白は0.01628。旧ススの放出画像用の1.5倍拡大と横ずらしを解除し、全状態で体格・足元を固定。
溜め予告の表示を「血」に変更。台本の識別用文字列は変更していない。

## 検証

- DemoAppビルド成功。最終ビルド警告0・エラー0（初回はBattleCoreの既存CS0162警告1件）。
- SusuPortraitCheck: 現行ロスターから外れたノノをカドに変更。第一波 seed 7で SUSU_PORTRAIT_CHECK_OK。溜め・放出・通常復帰・燃焼・死亡・復帰・空撃ち・勝利絵を確認。
- 第二波 seed 0では全状態通過条件を満たさず失敗。成功と混同しない。
- capture-integration.gd で実画面を撮影し、selection-game-check.png / battle-game-check.png を目視確認。
- Godot実行時には既存のUIアンカー警告・終了時RID警告と、サンドボックス環境の証明書/キャッシュアクセス警告が出る。

ゲームの数値・BattleCore・BattleSim・docsは変更していない。
