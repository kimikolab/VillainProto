# Codex への依頼 —— ソムの光の衣の雷の返し（第434期・DemoApp だけ）

規定のソム（背かれのソム）に、第434期から「光の衣の雷の返し」が入った（`UnitCatalog.Som`・札 `VeilShockBreak` ／ `VeilShockOnceAll` ／ `VeilShockSpareHero`・報告 `design/PHASE434_SOM_VEIL_SETTLE.md`）。
**新しい出来事は足していない。** 返しは台本に `BattleEventKind.StatusGain`（`Text` ＝ `shock`・書き手 ＝ ソムの `InstanceId`）として、もう流れている。`BattleCore` は触らないこと。コミットは BattleSim 側とは分ける。

## 頭に（第429期からの持ち越し・未処理なら）

- `Main.cs` の `StatusGain` の汎用の枝は、知らない `Text` を「＋3 bomb_plant」のような浮き文字 ／ 印 ／ 音 ／ ログの行で出してしまう（第429期 `design/CODEX_BRIEF_CHIRI.md` の 6 行目）。チリの爆弾の 5 つ（`BattleContext.BombPlantText` ほか）は作業中の `Main.Chiri.cs` ／ `ChiriPresentation.cs` で拾われているが、**汎用の枝が知らない `Text` を汎用の印にしない**直しがまだなら、先にそれを入れる。

## 見せ方（5 行）

1. 光の衣が砕けた一撃（膜が割れる演出の直後・台本では衣の持ち主への `Damage` の後に続く `StatusGain shock`・書き手 ＝ ソム）で、砕いた敵とその隣の敵へ雷が枝分かれして走る。
2. 雷はソム 1 人につき 1 ターンに 1 度だけ走る。2 度目の砕けでは台本に `StatusGain shock` が来ないので、膜が割れる演出だけを出す。
3. 勇者の一撃で砕けたときも雷は出ない（同上）。膜が割れる演出はいつもどおり。
4. 文字列は読まない。新しい出来事は足していない（`StatusGain` の `shock` だけ）。`Main.Som.cs` の衣の帳簿（衣 ＝ 破片）の前提は変わらない。
5. 確かめ: 「試遊・光の衣 ソム」のボス（`--demo-playtest-stage=0`・雷が 1 度も走らない）と本編 第4波（`--demo-stage=4`・走る・1 ターンに 1 度）。

```
Godot_console.exe --path DemoApp --headless --quit-after 120000 -- --demo-autoplay --demo-quit --demo-fast "--demo-preset=試遊・光の衣 ソム" --demo-playtest-stage=0
Godot_console.exe --path DemoApp --headless --quit-after 120000 -- --demo-autoplay --demo-quit --demo-fast "--demo-preset=試遊・光の衣 ソム" --demo-stage=4
```

合否は `DEMO_SMOKE_COMPLETE` の行で読む。map11 の門 4 本（`--map11-phase0` ／ `171` ／ `172` ／ `173`）と check 4 本（Discharge ／ ScorePanel ／ NineWave ／ Lili）も通すこと。
