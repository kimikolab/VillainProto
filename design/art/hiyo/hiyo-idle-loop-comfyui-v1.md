# ヒヨ 戦闘待機ループ（ComfyUI 08・seed 2022）

**状態: 採用・ゲームに接続（2026-10-11）。** 3 案（seed 2022 / 3033 / 5055）からユーザーが 2022 を選んだ。
「ループは短いのにいちばん自然」。3033 は部位がばらばらに動き（髪が分からないタイミングで揺れる）、5055 は屈伸が大きすぎた。
作り方と 3 案の数値は `design/art/hane/hane-idle-loop-v1-notes.md` の「ヒヨ」節。

元絵: `DemoApp/assets/portraits/battle/hiyo_ready_right.png`（支援役の構え）。
連番: `DemoApp/assets/portraits/battle/idle_loop/hiyo/hiyo_idle_00〜18.png`（640×960・19 コマ・16fps・約 1.2 秒・13.7 MB）。
元絵と同じキャンバスを縮小したものなので、足元の余白比（0.0215）と表示の高さ（1.89）はそのまま。靴底の行は元絵と同じ 938/960。

## 接続

- `BattlePawn3D.IdleLoop.cs`: `idle_loop/<駒ID>/` に PNG があれば、その駒の**通常の待機絵のときだけ**連番を回す（.cs に駒を書かない）。
  `Sprite3D.Texture` とシェーダーの `portrait_texture` を両方差し替え、`PixelSize` は「待機絵と同じ高さ ÷ 連番の高さ」。
  再生速度（`AnimationSpeed`）に追従。駒ごとに位相をずらす。毎フレームの駆動は子ノード `BattleIdleLoopDriver`（`BattlePawn3D._Process` は触らない）。
- `RefreshBattlePortrait` の最後で `UpdateIdleLoop(key, height)`。動作差分（贈り物・煽り・回収など）のあいだは止まり、戻ると再開。
- `Configure` で `AddChild(_sprite)` の直後に 1 行（開戦から回す）。
- 勝利・死亡では止める（どちらも `RefreshBattlePortrait` を通らないので、駆動側で `_victory` / `_alive` を見る）。
- 炎の纏い（`UpdateFireVisual`）は `_sprite.Texture` を読むので、連番のシルエットに自動で追従する。

## ボルグ・ホタへの適用（2026-10-11）

同じ仕組みに連番を置いただけ（コードは燃焼差分の扱いの 1 点だけ足した）。

| 駒 | 連番 | コマ | 長さ | 容量 | 靴底の行（静止絵 / 連番） |
|---|---|---|---|---|---|
| ボルグ | `idle_loop/borg/`（`borg2_s2022_final`） | 44 | 約 2.75 秒 | 22.4 MB | 922 / 921 |
| ホタ | `idle_loop/hota/`（`hota2_s2022_final`・crossfade 6） | 36 | 約 2.25 秒 | 27.5 MB | 947 / 947 |

- **ホタは燃焼の差分絵（`hota_burning_idle_right.png`）を持つので、燃えているあいだは差分の静止絵を出し、鎮火で連番へ戻す。**
  差分の無い駒（ボルグ・ヒヨ）は燃えていても回る。
- `PortraitStateCheck` はホタの通常時を静止絵と比べていたので、待機ループ中は連番のどれかであることを確かめる形にした（`CheckIdle`）。
- 実戦の画面（`火選り (ヒヨ×ホタ)`・第三波）で 3 体とも回ることを撮影で確認。ボルグの炎の纏いは連番のシルエットに追従する。
- 読み込み: 3 体ぶん 99 コマ（約 64 MB の PNG）。`IdleLoopCheck` は 3 体ぶんを読んで 4 秒で終わる。VRAM は非圧縮＋ミップマップで 1 コマ約 3 MB。

## 検証

- `IdleLoopCheck.tscn`（新規）: 開戦から再生・コマ送り・倍速・贈り物の差分で停止と再開・燃焼中も再生・死亡で停止・蘇生で再開・勝利絵を保つ・敵側の反転・連番の無い駒（ホタ）は静止絵のまま → `IDLE_LOOP_CHECK_OK`。
  `-- --capture`（窓あり）で `idle-loop-game/` に画面を保存。
- 既存: `PortraitStateCheck` → `PORTRAIT_STATE_CHECK_OK`、`MovementPortraitCheck -- --verify` → `MOVEMENT_PORTRAIT_CHECK_OK`、`FireCheck -- --replay` → `FIRE_CHECK_OK`。
- 実戦の画面（`火選り (ヒヨ×ホタ)`・第三波）で、後列のヒヨが弾んでいることを 6 枚の撮影で確認。
