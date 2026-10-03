# 第258期 報告 —— 爆炎・敵上げ満（W4）を規定に

指示書は design/PHASE258_FOE_SURGE_ADOPT_SPEC.md。**測定は回していない**（規定化と文書だけ）。

## 1. 規定化

- 規定のボルグに **`BlazeFoeSurgeMax`（爆炎・敵上げ満）** を足した（札の末尾・第257期の W4 と札の並びまで同じ）。爆炎で当たった敵だけ火勢を 4 に、味方は上げない。
- `units.md` のボルグの文面の末尾に「爆炎で焼いた敵の火を、一気に青白まで燃え上がらせる（火勢4）——燃える敵は叩くたびに炎が噴き上がる」を足した。
- 旧のボルグは **`UnitCatalog.BorgW0`**（`All` に入れない）。第254〜257期の器具を旧に固定した:
  - `blazesurge`（V0 ／ V2 ／ V4 の土台）・`foesurge`（W0 ／ W2 ／ W4 ／ ref の土台）→ `BorgW0`
  - `burnhit` ／ `burnadopt`（台を組む `BurnHitDiag.Mark` が台の規定のボルグを `BorgW0` に差し替える・`PickVer` の土台も）
  - `giftorder check`・`blazesurge check`・`foesurge check` の「規定の駒と比べる」項目 → `BorgW0`
- 敵上げ2（`BlazeFoeSurge2`）は対照の札として残した（保持者 0 枚）。

## 2. 受け入れ

| # | 条件 | 結果 |
|---|---|---|
| 1 | 規定化で動いた行 | **`compare` は 305 セル 0 件**（ボルグのいる行も勝率は動かない）。`docs/` のほかの生成物は、ボルグのいる行で残存・決着T・活動量・席の順位が小数で動いた（`chain` / `quality` / `pulse` / `harm` / `stock` / `engage` / `crossing` / `reseat` / `watch` / `roster_audit`）——ヒヨのいない台ではボルグが**爆炎・独り**を撃ち、そこで敵上げが働くため。`units.md` はボルグの文面と札の表の保持者の2行 |
| 2 | 第233〜257期の燃焼の器具の台本・自己検査が前後で一致 | ○ HEAD のビルドと規定化後のビルドで同じ台本 38 本（digest 21 本 ＝ `borgfront` / `fireward` / `firescale` / `firelevel` / `fireburst` / `enemyfire` / `firecycle` / `firetri` / `firefinish` / `fireatk` / `firekindle` ＋ `giftorder` N0〜N2 ＋ `blazesurge` V0〜V4 ＋ `foesurge` W0 ／ W2 ／ W4 ／ ref、`shockdigest` 16 モード ＋ `m219 cat`）と自己検査 21 本を回し、**全文一致**。違ったのは `foesurge check` の「敵上げの札の保持者は 0 枚」の1項目だけ（規定化そのもの）——期待を「敵上げ2 0 枚 ／ 敵上げ満 1 枚」と「規定のボルグ ＝ W4 の札」に直して 28 / 28 |
| 3 | `docs/` 再生成・`audit` ずれ 0 | ○ 15 ファイルを再生成・`rules.md` は差分なし・`audit` ずれ 0 件 |
| 4 | DemoApp の門 | ○ `--map11-phase0` / `171` / `172` / `173` すべて `ok=True` |
| 5 | Codex・別のセッションの変更に触らない | ○ 自分のファイルだけを add した（`DemoApp` の未コミットの変更・`design/CODEX_FIRE_PROGRESS.md` は触っていない） |

## 3. 文書

- `CODEX_BRIEF_FIRE.md`: §0 に敵上げの1行、§8 のチェックリストを第258期の規定に（「爆炎・敵上げ」を足し、第254期の「爆炎・上げ」は対照だけに）、今の規定の盤面の確認用の1戦を `foesurge log W4 T3-244 0 4 1` に。§15 の見出しを「第258期に敵上げ満が規定」に。
- `FIRE_AXIS_SUMMARY.md`: 第257〜258期（§14・§15）、ボルグの札の表に `BlazeFoeSurgeMax`、則 R360、再発の欄。
