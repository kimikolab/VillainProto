# 第214期 Codex 向けメモ —— 感電・雷のカタ・血詠みのアカの台本

盤面は `design/PHASE214_KATA_SHOCK.md`。**このメモは再生（絵と音）の材料だけ**で、BattleCore / BattleSim 側は台本を出すところまでで止めてある。
**Codex の担当ファイルは1つも触っていない。**

## 1. 新しい出来事（`BattleEventKind`・末尾に3つ足した・既存の番号は動いていない）

| 種類 | いつ | `ActorId` | `TargetId` | `Amount` | `Slot` | ほか |
|---|---|---|---|---|---|---|
| `Thunder` | カタの手番の `Skill`（`Text` =「雷を落とした」）の後、当たった順に1件ずつ | カタ | 当たった敵 | 1発の名目 | **何発目か（1 始まり＝跳ねの順番）** | `StatusRemaining` = 命中の前に数えた状態異常の種類、`Text` = その内訳（表示名をカンマ区切り・例「毒,雷」）、`Team` = 敵の陣営 |
| `ShockSpent` | 感電が弾けた瞬間（起爆） | 連鎖を起こした一撃の主（刻みなら null） | 放電する駒 | 0 | **連鎖の何段目か（0 ＝ 起点）** | `HpAfter` = その時点の HP（倒れていれば 0）、`Team` |
| `Discharge` | 放電1本（`ShockSpent` の直後に、隣の駒の数だけ） | 放電した駒 | 隣の駒 | 放電の量（8） | 何段目の放電か（1 始まり） | ベニの結界の内側なら `SourceTrait = Inverse`・`InverterId` が立ち、直後は `Heal`。それ以外は直後に `Damage`（出どころ ＝ 放電した駒・`FriendlyFire = true`） |

**感電が付いた瞬間**は既存の `StatusGain`（`Text = "shock"`・`ActorId` = カタ）。雷が当たった敵には `Thunder` → `Damage` →（生きていれば）`StatusGain` の順、
雷を落とし終えた後にカタの隣の味方へ `StatusGain`（代金）が並ぶ。**`StatusSnapshot` の表示名は「雷」**（`StatusKeys.LabelOf(StatusKeys.Shock)`）。

### 並びの例（敵の前3 に雷 → 中央 → 後3 に跳ね、あとでボルグの一撃が前3 を弾く）

```
Skill        カタ「雷を落とした」
Thunder      カタ → 前3（Slot 1・種類 1「毒」・6×2=12）   Damage カタ → 前3   StatusGain shock 前3
Thunder      カタ → 中央（Slot 2）                        Damage …           StatusGain shock 中央
Thunder      カタ → 後3（Slot 3）                         Damage …           StatusGain shock 後3
StatusGain   shock カタの隣の味方（代金）
…
Attack       ○○ → 前3   Damage ○○ → 前3
ShockSpent   前3（Slot 0・ActorId ＝ ○○）   Discharge 前3 → 中央（Slot 1）  Damage   Discharge 前3 → 後3（Slot 1） Damage
ShockSpent   中央（Slot 1）                   Discharge 中央 → 前1（Slot 2） Damage   …
ShockSpent   後3（Slot 1）                    …
```

- 連鎖は**幅優先**（段の若い順・同じ段は席番号の順）。**1つの駒は1回の連鎖で1回しか放電しない**
- 倒れた駒も自分の席から放電する（`ShockSpent` の `HpAfter = 0`）
- **粛（第二波）でも止まらない**（行動ではないので）

## 2. カタ

- Id は **`kata` のまま**（立ち絵のキー）。**名前は「禍導のカタ」**（第215期の後に決定・仮名は「雷のカタ」）、フレーバー「雷を呼ぶのではない。落ちる場所を選んでいるだけ。」。攻 6・手番は `Skill`「雷を落とした」だけ（通常攻撃は出ない）
- 旧カタ（起爆）は BattleCore に `UnitCatalog.KataOld` として残っているが、**`UnitCatalog.Kata` は雷のカタになった**。
  `BeniMioCheck.cs`（`support` の台の後1）と `SpecialsCheck.cs`（毒の台の後3）は `UnitCatalog.Kata` を使っているので、**今は雷のカタで再生される**
  （`SpecialsCheck --verify` は `SPECIALS_CHECK_OK`。**`BeniMioCheck --verify` は `support` の台の「回復の反転の陽性対照」で落ちる**——
  第213期では1回だけ起きていた反転が、雷のカタで戦の流れが変わって起きなくなった。**直しはその台の `UnitCatalog.Kata` → `UnitCatalog.KataOld`**）

## 3. 血詠みのアカ（ススの差し替え）

- **Id `susu`・札・数値・台本の中身はそのまま**（第213期と台本の指紋が一致）。変わったのは `Name` / `PlusText` / `MinusText` / `Flavor` とログの文言だけ
- **台本の文字列は変えていない**: 行動の札「灰に手を伸ばした」、`AshActionLabels.Hold`「灰を集めて放つ準備」／`Release`「灰を上空へ放つ」、`StatusSnapshot` の表示名「灰」
- 画面に「灰」が残る場所（Codex の担当）: `StatusIconArt.cs` の灰のアイコン、`Main.cs` の予告「貯めた灰を全体へ放つ準備」、`SusuPortraitCheck.cs` の文言。
  **表示を「血」にするなら再生側で**（`AshActionLabels` の文字列を読んで分岐しているので、BattleCore 側で変えると分岐が外れる）
