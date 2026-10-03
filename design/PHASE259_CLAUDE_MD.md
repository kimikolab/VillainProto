# 第259期 報告 —— CLAUDE.md のコンパクト化（4,807 行 → 406 行・以後増えない構造）

指示書は `design/PHASE259_CLAUDE_MD_SPEC.md`。**盤面は1ビットも動いていない**（`compare` 305 セル 0 件・`docs/rules.md` 0 行差分・`BattleCore` の差分はコメント1行だけ）。

## 0. 結果

| 量 | 前 | 後 |
|---|--:|--:|
| `CLAUDE.md` の行数 | 4,807 | **406** |
| `CLAUDE.md` のバイト | 882,120 | **47,492** |
| 段落の落ち（照合スクリプト） | — | **0 / 193** |
| `sweep list` の本数 | 779 | 779（行も全一致・違いは見出しの1行だけ） |
| `compare` 305 セル | — | 0 件差分 |
| `docs/rules.md` | — | 0 行差分 |
| `audit` | ずれ 0 件 | ずれ 0 件 ＋ **新しい門 OK**（行 406/500・バイト 47,492/51,200・履歴の句 2/警告 5） |

**門が本物か**: `CLAUDE.md` に 120 行足して `audit` を回すと `**×**`・exit 1 になることを確かめてから戻した。

## 1. 旧 `CLAUDE.md` の節の大きさと行き先

| 節 | 行 | 文字 | 行き先 |
|---|--:|--:|---|
| 現状値 | 1,673 | 130,761 | `design/HISTORY_PHASES.md`（逐語）。残したのは直近の値1ブロック＋規則 7 本 |
| コマンド | 992 | 112,015 | `design/COMMANDS.md`（逐語・`sweep` の読み先）。残したのは 14 本 |
| BattleContext の節 | 864 | 76,901 | `design/ENGINE_HOOKS.md`（逐語）。残したのは窓口の不変条件 |
| 期別（第84期以降） | 181 | 73,799 | `design/PHASE_INDEX.md`（逐語）。常駐 0 行 |
| 一般則の索引 | 383 | 30,853 | `design/RULES_INDEX.md`（逐語）。残したのは 3 行 |
| 構成と絶対のルール | 294 | 19,528 | `design/DEMOAPP_HISTORY.md`（逐語）。残したのは各ディレクトリ 1〜5 行と絶対のルール 3 本 |
| 特性 = イベントハンドラ | 88 | 5,108 | `design/ENGINE_HOOKS.md`（逐語）。残したのは不変条件 |
| 規約 (G1)〜(G17)・新機構の判定規約・決定性・ログ・構造化イベント・バランス調整・設計判断・知見の索引（帯） | 262 | 約 16,000 | **そのまま**（逐語で残した） |

**指示書と違えた点が 2 つ**（どちらも 50 KB の門のため。50 KB は §1 で決定済みの上限で、最初の組み立ては 454 行・59,324 バイトで超えていた）:

1. **隊列と攻撃パターン（42 行・7.4 KB）を書き直した**。全文は `design/ENGINE_HOOKS.md` の末尾に逐語で移し、`CLAUDE.md` には X 字の表・陣形・4つの攻撃型の不変条件（約 20 行）を残した。`Grade.cs` が引用する「庇う・標的の介入は Single にしか効かない」の文はそのまま残っている。
2. **知見の索引の「主題別」の表（24 行・7.3 KB）を `design/PHASE_INDEX.md` の冒頭へ移した**（指示書は「帯・主題別はほぼそのまま残す」）。主題 → 期 の索引は期の一覧と同じファイルにあるほうが引きやすく、`CLAUDE.md` の帯の表（5 行）から1行で辿れる。

## 2. Phase 0 の結果

### 2-1. `Sweep.cs` の読み方

`Extract` は行頭 `    dotnet run --project BattleSim -c Release `（4 空白・連結で組む）の行だけを見て、` #` 以降と ` > ` 以降を落とし、`<...>` を含む行を飛ばし、`[...]` を空白に置き換え、` / ` で版を割る。**行の中身しか見ないので、表を逐語で移せば同じ出力になる**。移動前の `sweep list`（815 行）を保存し、移動後と `Compare-Object` で突き合わせた——違いは見出しの1行（`CLAUDE.md` → `design/COMMANDS.md`）だけ。

### 2-2. `CLAUDE.md` を読んでいる器具（`grep -rn "CLAUDE.md" BattleSim BattleCore`）

| 器具 | 何に使っているか | 処置 |
|---|---|---|
| `Sweep.cs` `Extract` / `FindRoot` | コマンド表のパース ／ リポジトリの目印 | **読み先を `design/COMMANDS.md` に**。目印はそのまま（ファイルは残る） |
| `Modes/Reader.cs` `RdIndexAudit`（`reader phase0` の表P-1） | コマンド表の `→ LESSONS_*.md` の参照を数える | **読み先を `design/COMMANDS.md` に**（無ければ `CLAUDE.md`＝古い worktree）。指示書の「Sweep.cs の読み先1箇所」の外だが、同じ表を読む同じ性格の器具なので一緒に直した |
| `Modes/Derive.cs` `dvClaude`（`docs/rules.md` の `CLAUDE.md / LESSONS` 列） | 型名が `CLAUDE.md` ＋ `LESSONS_*.md` に現れるか | **移した 6 ファイルも索くようにした**。これで `docs/rules.md` は 0 行差分（足さなければ ○ が消えて動いていた）。どれも `PHASE*.md` ではないので「初出」の列には効かない |
| `Modes/Mire.cs` / `Formation2.P201.cs` / `Whip.cs` / `Shock.cs` / `Scorch.cs` / `Taillight.cs` | リポジトリルートの目印 | そのまま（無害） |
| `Modes/Tumult2.cs` Q0-7 | 「`CLAUDE.md` の走査」と**書いてある**が走査はしていない（手書きの表） | そのまま |

### 2-3. コード内コメントの引用の対応表

| ファイル:行 | 引用 | 旧 `CLAUDE.md` の行 | 移動先 | 処置 |
|---|---|--:|---|---|
| `Stacks.cs:263` | `ctx.Heal` を呼ぶのは9経路 | 373 | `ENGINE_HOOKS.md` | ポインタを直した |
| `Stacks.cs:307` | 止まるのはこの窓口を通る4本だけ | 352 | `ENGINE_HOOKS.md` | 同上 |
| `BattleEngine.cs:443` | 棘・仇討ち・軋み・追い打ちの4本だけ | 352 | `ENGINE_HOOKS.md` | 同上 |
| `Modes/Wave2.cs:35` | 駒の説明文から数えると必ず抜ける | 384 | `ENGINE_HOOKS.md` | 同上 |
| `Modes/Lastslot.cs:183` / `Modes/Overbear.cs:58` | 台が死んでいる | 1623（R087 の索引行） | `RULES_001_097.md` R087 | 同上 |
| `Program.cs:619` / `Modes/Brace.cs:472` / `Sweep.cs` | `CLAUDE.md` のコマンド表 | 3580〜 | `COMMANDS.md` | 同上 |
| `Grade.cs:369` | 庇う・標的の介入は Single にしか効かない | 1312 | **新 `CLAUDE.md` に残っている** | 変更なし |
| `Relay.cs:246` | 特性の発動（`OnAfterAttack`）は攻撃1回につき1度、主目標に対してのみ | 931 | **新 `CLAUDE.md` に残っている** | 変更なし |
| `Modes/Miasma.cs:265` | 情報セルの定義が2つある | 3575 | **新 `CLAUDE.md` に残っている**（現状値の規則） | 変更なし |
| `Traits.cs:8937` / `9444` | `ApplyDamage` がダメージ処理の単一窓口 | 393 | **新 `CLAUDE.md` に残っている** | 変更なし |
| `Models.cs:166` | BattleContext = 盤面への唯一の窓口 | 393（見出し） | 見出しはそのまま | 変更なし |

**移動前から旧 `CLAUDE.md` に無かった引用（この期の前から古い・触っていない）**: `Program.cs:1227`「`WaveCatalog()` の申し送り」・`Program.cs:1558`「engine も通貨の読み手である」・`Time.cs:317`「誰の助けも届かない駒に唯一届く支援」・`Traits.cs:3235`「ここが空の波は独立した波として存在していない」・`Modes/Betray.cs:836`「8 箇所」・`Modes/Encore.cs:220`「固定順」。第112期に `LESSONS_*.md` へ出た文か、言い換えである。`outputs/fire-check/staged-build/` の写しは触っていない。

### 2-4. 節境界

見出しの文字列（`## 構成と絶対のルール` など 12 本）で切った。行番号は使っていない（切る側のスクリプトは `assert` で見出しが1つだけ当たることを確かめる）。

## 3. 手順と検算

1. **分割**（`split.py`・使い捨て）: 旧 `CLAUDE.md` を見出しで切り、6 ファイルへ逐語で書いた。各ファイルの冒頭に 3〜4 行の説明（由来・以後どう足すか）を足した以外は1文字も変えていない。
2. **照合**（`design/PHASE259_verify_paragraphs.py`・残した）: 旧 `CLAUDE.md` を空行区切りの 193 段落に割り、新 `CLAUDE.md` ＋ 6 ファイルのどれかに逐語で含まれるかを数える。**落ち 0 件**（内訳: 新 `CLAUDE.md` 72 ／ `HISTORY_PHASES` 88 ／ `ENGINE_HOOKS` 12 ／ `COMMANDS` 7 ／ `RULES_INDEX` 6 ／ `PHASE_INDEX` 5 ／ `DEMOAPP_HISTORY` 3）。
   ```
   python design/PHASE259_verify_paragraphs.py <旧CLAUDE.md> .
   ```
3. **新 `CLAUDE.md`**: 残す節は旧ファイルから見出しで切って逐語、書き直す節（冒頭の原則と追記の規則・構成・アーキテクチャ・隊列・一般則の 3 行・現状値のブロックと規則・コマンド 14 本・期別の 1 行）は下書きから組んだ。
4. **`sweep list` の一致**・**`derive rules` 0 行差分**は §0 のとおり。
5. **`audit` の門**: 500 行 ／ 50 KB で ×（exit 1）、「本目」「第nn期に…足した」の句が 5 行以上で警告（× にしない）。いまの値は 2（冒頭の「n本目」の注意書きと、規約 (G7) の「第94期に3つ目を足した」）。

## 4. コミット

| | 中身 |
|---|---|
| (i) | 6 ファイルへの移動（中間の `CLAUDE.md` ＝ 旧から移した節を抜いただけ）＋ `Sweep.cs` / `Reader.cs` の読み先 ＋ `Derive.cs` の索き先 |
| (ii) | 新 `CLAUDE.md` の書き直し ＋ コメントのポインタ 8 箇所 ＋ 照合スクリプト ＋ この報告 ＋ `PHASE_INDEX.md` の第259期の行 |
| (iii) | `audit` の門 |

## 5. 以後の期の作法（新 `CLAUDE.md` の冒頭にも書いた）

- `CLAUDE.md` に書けるのは **(a) 変わった規則** と **(b) 現状値ブロックの差し替え** の2つだけ。
- 期の一行 → `design/PHASE_INDEX.md` の末尾。測定の中身・予測の○×・踏んだ穴 → `design/PHASEnnn_*.md` と `LESSONS_*.md`。期の報告（`compare` がどう動いたか）→ `design/HISTORY_PHASES.md` の冒頭。engine の窓口に足したもの → `design/ENGINE_HOOKS.md`。新しいコマンド → `design/COMMANDS.md`（`sweep list` に載る）。新しい則 → `RULES_243_.md` ＋ `RULES_INDEX.md`。
- **「n本目」「第nn期に…足した」を `CLAUDE.md` に書かない**（R151。`audit` が数える）。

## 6. この期に決めないこと（指示書 §8 のまま）

`design/COMMANDS.md` の生成物化（`sweep` 側に一覧を持たせて表を生成する「向きを逆にした」案）・期の一行の文字数の機械化。
