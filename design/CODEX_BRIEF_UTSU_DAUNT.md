# Codex への依頼 —— 逆しまのウツが萎縮で冴える（第437期・DemoApp だけ）

規定のウツ（逆しまのウツ）に、第437期から「萎縮して迎えた一振りは、すべての発が倍」が入った（`UnitCatalog.Utsu`・札 `UtsuDauntVolley`・報告 `design/PHASE437_UTSU_DAUNT_SETTLE.md`）。
**新しい出来事は足していない。** `BattleCore` は触らないこと。コミットは BattleSim 側とは分ける。文字列（ログ）は読まない。

## 台本で読めること

- ウツが萎縮した瞬間: `BattleEventKind.StatusGain`（`Text` ＝ `daunted`（`StatusKeys.Daunted`）・書き手 ＝ クビの `InstanceId`）。今の萎縮のアイコンと同じ出来事。
- 萎縮したウツの次の手番の一振り（`Attack` → `Damage` が手数の分だけ続く・ウツは 1〜5 発）は、**全発が倍**。規定のウツ（札 `UtsuDauntVolley`）では萎縮は半分にならない。
- 萎縮が消えるのは一振りの最後の発の後（その後の `StatusSnapshot` には萎縮が無い）。

## 見せ方（3 行）

1. 萎縮したウツの一振りの 1 発目の `Damage` の直前に、萎縮のアイコンが反転する短い演出（縮む → 張る）を出す（ウツが「怯えで腕が冴えた」瞬間）。
2. 一振りの間はウツの萎縮のアイコンを残し、一振りの最後の `Damage` で消す（`StatusSnapshot` は次のターンの頭なので待たない）。
3. 新しい `BattleEventKind` は要らない（萎縮の `StatusGain` と、ウツの手番の `Attack` ／ `Damage` だけで読める）。**購入済み素材を先に確認する**（CONTRIBUTING.md の「GodotApp / DemoApp の背景・演出開発」）。

## 確かめ

「試遊・呪い 萎縮」（第437期に足した台・クビとウツが隣）があればそれで、無ければ `逆しま改 (クビ×ウツ)`（`compare` の行）で、ボス（`--demo-playtest-stage=0`）と本編 第4波（`--demo-stage=3`）を単発で再生する。合否は `DEMO_SMOKE_COMPLETE` の行。map11 の門 4 本（`--map11-phase0` ／ `171` ／ `172` ／ `173`）と check 4 本（Discharge ／ ScorePanel ／ NineWave ／ Lili）も通すこと。

```
Godot_console.exe --path DemoApp --headless --quit-after 120000 -- --demo-autoplay --demo-quit --demo-fast "--demo-preset=試遊・呪い 萎縮" --demo-playtest-stage=0
Godot_console.exe --path DemoApp --headless --quit-after 120000 -- --demo-autoplay --demo-quit --demo-fast "--demo-preset=試遊・呪い 萎縮" --demo-stage=3
```
