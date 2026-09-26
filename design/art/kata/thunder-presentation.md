# 禍導のカタ・感電の演出適用

対象: design/CODEX_BRIEF_KATA.md §2〜5。§6のアカは既存実装を維持。
BattleCoreは変更しない。対象・跳ね・段・反転・HPは発行された台本だけから取得する。

## 実装

- Thunderごとに杭を対象へ飛ばし、初発は上空、次発は前の対象から青白い電弧を渡す。種類数で幅・火花の広がり・音量を変える。刺さった杭は消える。
- ShockSpentの同段をまとめて電弧を表示。段の間は0.24秒を速度で割る。HP・死亡・副作用のイベント順は維持。
- shockの付与・状態の写しで青白い火花とアイコンを維持、消費・死亡・勝利で解除。倒れた放電元は死亡アニメーション位置でなく元の席から放電する。
- InverseのDischargeは着弾後、既存の反転描画を青白起点で再利用して回復へ変化する。HPは後続Healに任せる。
- 味方へのshockはカタから漏れる電弧で表示。雷・放電による死亡は短い痙攣を挟んで既存の死亡描画へ。
- BeniMioCheckのsupport編成だけKataOldへ修正。

## 音・追加アート候補

指定の本番MP3を適用済み（元ファイルとのSHA256一致を確認）。ファイルが無い場合だけ確認用の仮音へフォールバックする。

- DemoApp/assets/audio/se/kata_thunder.mp3: 効果音ラボ/戦闘/雷魔法4.mp3（最初の重い落雷）
- DemoApp/assets/audio/se/kata_discharge.mp3: 効果音ラボ/戦闘/雷魔法1.mp3（短い跳ね・放電）。段ごとに1音、段数で音程と音量を控えめに上げる。再生速度で音程は変えない。

杭なし本体（白目修正版kata-idle-right-body-v2.png）と独立杭（kata-stake-v1.png）を生成し適用済み。ゲーム用の本体はbattle/kata_idle_right.png、杭はassets/fx/kata_stake.png。
KataStakes3Dが3本を浮遊させ、同じスプライトを対象へ発射・帰還させる。死亡時は飛行を解除して非表示、蘇生時に復帰、勝利絵では描き込み済みの杭と重ねない。
追加の詠唱顔・大きなポーズ差分は必須ではない。

## 検証

- DemoAppビルド: 警告0・エラー0。
- ThunderCheck: 第一波・第二波の実台本、再実行、落雷/放電/反転の件数、最終HP一致。第一波5発/16経路/反転8件、第二波2発/7経路。
- BeniMioCheck --verify: BENI_MIO_CHECK_OK。supportの反転陽性1件も回復。
- 1倍/2倍のプレビュー、感電の写し・解除・死亡・蘇生、死亡後の放電起点を検査。
- thunder-1-fx-check.png、chain-2-fx-check.png等に画面を保存。
- 音声はDummyドライバで検証。実際の聴感・本番SEの音量バランスは未確認。

実行例: Godot --path DemoApp res://ThunderCheck.tscn --audio-driver Dummy --rendering-method gl_compatibility
プレビューのみ: 上記末尾へ -- --preview
