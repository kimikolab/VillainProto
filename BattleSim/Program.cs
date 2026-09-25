using BattleCore;
using static Common;

// =====================================================================================
// 第139期 段0 —— **トップレベル文をやめて、本体を大きなスタックのスレッドで走らせる**。
//
// 以前はここから 68,000 行がトップレベル文だった。C# はそれを1つの `<Main>$` に畳むので、
// **全モードの局所変数が1つのスタックフレームに同居する**——実測では
// `<Main>$` のフレームだけで既定 1MB のスタックをほぼ食い尽くしていて、
// **`Enumerable.Sum` の1呼び出しでオーバーフローする**ところまで来ていた。
//
// 第138期は同じ症状を「`BattleEngine.Run` の引数を2本増やしたから」と読んだが、
// **引数は最後の一押しで、原因ではない**——第139期に `eaec7e6` をクリーンな
// `git worketree` に出して測ると、**引数を1本も足していない HEAD で `compare` が落ちる**
// （23 行目 `反撃改3 (カド×ハギ)`・exit 127・`docs/balance.md` が途中で壊れる）。
// `compare quality` と `chain` は `<Main>$` の中の集計そのもので落ちていた。
//
// **中身は1行も動かしていない。** `class` と `Body(string[] args)` で囲っただけで、
// 局所変数もローカル関数も並びもそのまま（トップレベル文はもともと `<Main>$` の本体なので、
// **生成されるコードはほぼ同じ**）。**盤面・乱数・順序には1ビットも触っていない。**
//
// **採らなかった形**: `compare` の分岐だけをローカル関数に切り出して渡す版は、
// **本体が参照するトップレベルの局所変数がすべて表示クラスへ巻き上げられ、
// Roslyn がメモリ 52GB を掴んでビルドが終わらなくなった**（実測）。
// **囲うなら全部**——部分的に囲うとクロージャになる。
//
// **構造的な直し（`Run` の 49 本の引数を1つの束に畳む・モードを別クラスへ割る）はこの期ではやらない。**
// 呼び出し口が数百あり、「計測器と測定対象を同時に動かさない」に正面から反する。
// =====================================================================================
internal static class Prog
{
    internal static void Body(string[] args)
    {

// 総当たりシミュレータ。WPF を通さず戦闘ロジックだけを叩く。
// 手動プレイでは見つからない「強すぎる組み合わせ」と「死に駒」を機械的に洗い出す。

int stageIndex = args.Length > 0 && int.TryParse(args[0], out int s) ? s : 1;
string focusId = args.Length > 1 ? args[1] : "";

// compare / dump / layout は docs/ に貼れる Markdown をそのまま吐くので、
// 「対象ステージ」の見出しと stageIndex の解決はこの3モードの分岐を抜けた後で行う。
// （3モードともステージ引数を無視して全ステージを回すため、内容としても誤りになる）

if (focusId == "audit") { AuditDiag.Run(args, stageIndex); return; }
if (focusId == "roster") { RosterDiag.Run(args, stageIndex); return; }

if (focusId == "derive") { DeriveDiag.Run(args, stageIndex); return; }

if (focusId == "curse") { CurseDiag.Run(args, stageIndex); return; }

if (focusId == "hex") { HexDiag.Run(args, stageIndex); return; }

if (focusId == "encore") { EncoreDiag.Run(args, stageIndex); return; }

if (focusId == "betray") { BetrayDiag.Run(args, stageIndex); return; }

if (focusId == "pulse") { PulseDiag.Run(args, stageIndex); return; }

if (focusId == "route") { RouteDiag.Run(args, stageIndex); return; }

if (focusId == "swap") { SwapDiag.Run(args, stageIndex); return; }

if (focusId == "gullet") { GulletDiag.Run(args, stageIndex); return; }

if (focusId == "yoke") { YokeDiag.Run(args, stageIndex); return; }

if (focusId == "hush") { HushDiag.Run(args, stageIndex); return; }

if (focusId == "sever") { SeverDiag.Run(args, stageIndex); return; }

if (focusId == "suture") { SutureDiag.Run(args, stageIndex); return; }

if (focusId == "expose") { ExposeDiag.Run(args, stageIndex); return; }

if (focusId == "shove") { ShoveDiag.Run(args, stageIndex); return; }

if (focusId == "dull") { DullDiag.Run(args, stageIndex); return; }

if (focusId == "relay") { RelayModeDiag.Run(args, stageIndex); return; }

if (focusId == "slander") { SlanderDiag.Run(args, stageIndex); return; }

if (focusId == "overbear") { OverbearDiag.Run(args, stageIndex); return; }

if (focusId == "scale") { ScaleDiag.Run(args, stageIndex); return; }

if (focusId == "scapegoat" && (args.Length > 2 ? args[2] : "") == "phase0") { ScapegoatDiag.Phase0(args, stageIndex); return; }

if (focusId == "scapegoat") { ScapegoatDiag.Run(args, stageIndex); return; }

if (focusId == "divert" && (args.Length > 2 ? args[2] : "") == "phase0") { DivertDiag.Phase0(args, stageIndex); return; }

if (focusId == "divert" && (args.Length > 2 ? args[2] : "") == "probe") { DivertDiag.Probe(args, stageIndex); return; }

if (focusId == "divert") { DivertDiag.Run(args, stageIndex); return; }

if (focusId == "census") { CensusDiag.Run(args, stageIndex); return; }

if (focusId == "guard") { GuardDiag.Run(args, stageIndex); return; }

if (focusId == "whet") { WhetDiag.Run(args, stageIndex); return; }

if (focusId == "creak") { CreakDiag.Run(args, stageIndex); return; }


if (focusId == "carry") { CarryDiag.Run(args, stageIndex); return; }

if (focusId == "draft") { DraftDiag.Run(args, stageIndex); return; }

if (focusId == "draft2") { Draft2Diag.Run(args, stageIndex); return; }

if (focusId == "draft3") { Draft3Diag.Run(args, stageIndex); return; }

if (focusId == "slope") { SlopeDiag.Run(args, stageIndex); return; }


if (focusId == "wound") { WoundDiag.Run(args, stageIndex); return; }

if (focusId == "wcost") { WcostDiag.Run(args, stageIndex); return; }

if (focusId == "blade") { BladeDiag.Run(args, stageIndex); return; }

if (focusId == "body") { BodyDiag.Run(args, stageIndex); return; }

if (focusId == "traits") { TraitsDiag.Run(args, stageIndex); return; }

if (focusId == "pairs") { PairsDiag.Run(args, stageIndex); return; }

if (focusId == "pairs2") { Pairs2Diag.Run(args, stageIndex); return; }

if (focusId == "checkup") { CheckupDiag.Run(args, stageIndex); return; }

if (focusId == "breadth") { BreadthDiag.Run(args, stageIndex); return; }


if (focusId == "thorn") { ThornDiag.Run(args, stageIndex); return; }


if (focusId == "suture2") { Suture2Diag.Run(args, stageIndex); return; }

if (focusId == "mender") { MenderDiag.Run(args, stageIndex); return; }

if (focusId == "blaze2") { Blaze2Diag.Run(args, stageIndex); return; }

if (focusId == "gauge") { GaugeDiag.Run(args, stageIndex); return; }


if (focusId == "gather") { GatherDiag.Run(args, stageIndex); return; }
if (focusId == "deep") { DeepDiag.Run(args, stageIndex); return; }
if (focusId == "soak") { SoakDiag.Run(args, stageIndex); return; }
if (focusId == "lastslot") { LastslotDiag.Run(args, stageIndex); return; }

if (focusId == "creak3") { Creak3Diag.Run(args, stageIndex); return; }

if (focusId == "spend") { SpendDiag.Run(args, stageIndex); return; }

if (focusId == "burn") { BurnDiag.Run(args, stageIndex); return; }

if (focusId == "pace") { PaceDiag.Run(args, stageIndex); return; }

if (focusId == "tempo") { TempoDiag.Run(args, stageIndex); return; }

if (focusId == "hold") { HoldDiag.Run(args, stageIndex); return; }



if (focusId == "taillight") { TaillightDiag.Run(args, stageIndex); return; }

if (focusId == "tomo" && args.Length > 2 && args[2] == "yield") { TomoDiag.Yield(args, stageIndex); return; }

if (focusId == "tomo") { TomoDiag.Run(args, stageIndex); return; }

if (focusId == "hold2") { Hold2Diag.Run(args, stageIndex); return; }

if (focusId == "spread") { SpreadDiag.Run(args, stageIndex); return; }

if (focusId == "favor" && (args.Length > 2 ? args[2] : "") == "phase0") { FavorDiag.Phase0(args, stageIndex); return; }

if (focusId == "favor") { FavorDiag.Run(args, stageIndex); return; }

// turn モード: 火選りを**手番へ降ろす**（第60期）。
//
// 第58期 9-2 の実測が出発点。ターンの順序は `TickStatuses` → `OnTurnStart` → 行動順ループで、
// 火の粉は `OnAfterAttack` ——つまり **`OnTurnStart` の機構は供給に対して構造的に1ターン遅れる**。
// 第1ターンの発火時点では盤上の誰も燃えておらず、**強化するはずの熾のホタをそのターンだけ鈍らせていた**
// （弱体の受け手に 2.00 量/戦 ＝ 第1ターンの1回 × `Loss` 2 がちょうど載っていた）。
// **係数では詰められない**（`Gain` を上げてもこの1回は消えない）。
//
// **engine には何も足さない。** `FavorTrait` に `OnAction` と `ActsOnPattern` の分岐を置き
// （継ぎ当て＝`MenderTrait` の形の踏襲）、版の切り替えは**駒の側**でやる
// ——`ActsOnPattern` は `UnitDef.Actions` を読むので、規則（`Run` の引数）では切り替えられない。
// 診断のローカルに `Actions = [Skill]` を持つヒヨの複製を組む（`gradient` / `aim` と同じ扱い）。
//
//     dotnet run --project BattleSim -c Release 0 turn phase0  # 実装前の地図（止めうる経路・粛・速さ・現在の出力）
//     dotnet run --project BattleSim -c Release 0 turn          # 主表 V0/V1/V2/V3 × 4行 × 5波 と Q1〜Q6
//     dotnet run --project BattleSim -c Release 0 turn sweep    # Gain/Loss の掃引 4点
//     dotnet run --project BattleSim -c Release 0 turn alt      # 帰属の符号を別 seed 帯（200..599）で追試


if (focusId == "turn" && (args.Length > 2 ? args[2] : "") == "phase0") { TurnDiag.Phase0(args, stageIndex); return; }


if (focusId == "turn") { TurnDiag.Run(args, stageIndex); return; }

// miasma モード: 瘴気（グザ）を手番へ降ろす（第61期）。
//
// **移設が動かす一番大きい量は「ターン頭の発火順」である。** ターンの順序は
// `TickStatuses` → `OnTurnStart`（**席順の昇順**）→ 行動順ループなので、
// `OnTurnStart` に置いた機構どうしの前後は**席の番号だけ**で決まる。瘴気と澱み喰い
// （ヴィオ）はどちらもターン頭で、グザがヴィオより前の席なら撒いた毒はその場で
// 吸い上げられて**味方は1点も払わない**——「毒の代金を誰が払うか」が席の左右で
// 切り替わる隠れた判断になっていた。手番（速5）へ降ろすと必ずヴィオが先になる。
//
// **版の切り替えは駒の側で行う**（`ActsOnPattern` は `UnitDef.Actions` を読むので
// 規則では切り替わらない）。診断のローカルに `UnitDef` を置き、`UnitCatalog` は触らない
// ——`gradient` / `aim` / `turn` と同じ扱い。
//
//     dotnet run --project BattleSim -c Release 0 miasma phase0  # 実装前の地図（発火順・出力・分母）
//     dotnet run --project BattleSim -c Release 0 miasma         # 主表 V0/V1/V2/V3 × 8行 × 5波 と Q1〜Q7
//     dotnet run --project BattleSim -c Release 0 miasma seat    # 席の入れ替えの追試（Q1 の直接の証拠）
//     dotnet run --project BattleSim -c Release 0 miasma alt     # 帰属の符号を別 seed 帯（200..599）で追試


if (focusId == "miasma" && (args.Length > 2 ? args[2] : "") == "phase0") { MiasmaDiag.Phase0(args, stageIndex); return; }

if (focusId == "miasma") { MiasmaDiag.Run(args, stageIndex); return; }

if (focusId == "blaze" && (args.Length > 2 ? args[2] : "") == "phase0") { BlazeDiag.Phase0(args, stageIndex); return; }

if (focusId == "blaze") { BlazeDiag.Run(args, stageIndex); return; }

if (focusId == "funnel") { FunnelDiag.Run(args, stageIndex); return; }

if (focusId == "yield") { YieldDiag.Run(args, stageIndex); return; }

if (focusId == "replay") { ReplayDiag.Run(args, stageIndex); return; }

if (focusId == "goad") { GoadDiag.Run(args, stageIndex); return; }

if (focusId == "finisher") { FinisherDiag.Run(args, stageIndex); return; }

if (focusId == "wave2") { Wave2Diag.Run(args, stageIndex); return; }

if (focusId == "ledger") { LedgerDiag.Run(args, stageIndex); return; }

if (focusId == "lit") { LitDiag.Run(args, stageIndex); return; }

if (focusId == "reader") { ReaderDiag.Run(args, stageIndex); return; }

if (focusId == "offturn") { OffturnDiag.Run(args, stageIndex); return; }

if (focusId == "watch") { WatchDiag.Run(args, stageIndex); return; }

if (focusId == "compare") { CompareDiag.Run(args, stageIndex); return; }

if (focusId == "engage") { EngageDiag.Run(args, stageIndex); return; }

if (focusId == "seats") { SeatsDiag.Run(args, stageIndex); return; }


if (focusId == "boss") { BossDiag.Run(args, stageIndex); return; }

// =====================================================================================
// tank モード（第118期） —— 時間を買う機構（自己回復タンク）を作って測る
//
// **本体は `BattleSim/Tank.cs`**（診断を別ファイルに置いた最初の例）。
// このファイルの top-level statements は 67,000 行が**全部で1つのメソッド**で、
// Release のビルドに 4 分かかる（実測）。診断1本ぶんのローカルとクロージャをそこへ足す理由が無いので、
// **クロージャを1つも作らない形**（static メソッドと static フィールドだけ）で外に出した。
// **ここは振り分けの数行だけ。** `derive rules` の走査もこの形に対応させてある（第118期）。
//
//     dotnet run --project BattleSim -c Release 0 tank phase0   # 表P（経路表・§1-B の再測定）
//     dotnet run --project BattleSim -c Release 0 tank run      # 表A〜E
//     dotnet run --project BattleSim -c Release 0 tank check    # 自己検査
if (focusId == "tank")
{
    TankDiag.Run(args.Length > 2 ? args[2] : "phase0");
    return;
}

// wound2 モード: 傷という通貨の棚卸し（第120期・**測定だけ**）。中身は `Wound2.cs`。
// **engine に規則は1本も足していない**——在庫の走査・消滅の帳簿・実際に減った HP の計数だけ。
//
//     dotnet run --project BattleSim -c Release 0 wound2 phase0 / run / check
//
// 第121期: 版の引数を1つ足した（**既存3モードの呼び出しは1文字も変えない**）。
//
//     dotnet run --project BattleSim -c Release 0 wound2 spill phase0 / run / check
if (focusId == "wound2")
{
    Wound2Diag.Run(args.Length > 2 ? args[2] : "phase0", args.Length > 3 ? args[3] : "phase0",
                   args.Length > 4 ? args[4] : "");
    return;
}

// time モード: 軸が回る前に落ちる問題（第126期）。中身は `Time.cs`。
// **Phase 0 は盤面を1ビットも動かさない**——読むのは計数
// （`UnitTally.LastActiveTurn` / `Deaths` / `DamageTaken` / `DamageToEnemy`）だけ。
// `TankDiag` / `Wound2Diag` と同じく、**ここは振り分けの数行だけ**（Release のビルド時間）。
//
//     dotnet run --project BattleSim -c Release 0 time phase0   # Q0-1〜Q0-8
if (focusId == "time")
{
    TimeDiag.Run(args.Length > 2 ? args[2] : "phase0");
    return;
}

// grade モード: 段（格上げ）を作る期（第127期）。中身は `Grade.cs`。
// **Phase 0 は盤面を1ビットも動かさない**（走査と数え物だけ）。段1 も台は診断のローカルで、
// `Presets` / `EnemyCatalog.Stages` / `UnitCatalog.All` は1文字も触らない。
// `TankDiag` / `Wound2Diag` / `TimeDiag` と同じく**ここは振り分けの数行だけ**（Release のビルド時間）。
//
//     dotnet run --project BattleSim -c Release 0 grade phase0   # Q0-1〜Q0-9
//     dotnet run --project BattleSim -c Release 0 grade run      # 段1（V0〜V4 × 台2つ）
if (focusId == "grade")
{
    GradeDiag.Run(args.Length > 2 ? args[2] : "phase0");
    return;
}

// grade2 モード: 段の載せ替え（第128期）。中身は `Grade2.cs`。
// **Phase 0 と段1 は盤面を1ビットも動かさない**（走査・数え物・既存の計数の読み直しだけ）。
//
//     dotnet run --project BattleSim -c Release 0 grade2 phase0  # Q0-1〜Q0-9
//     dotnet run --project BattleSim -c Release 0 grade2 stock   # 段1 の棚卸し（→ docs/stock.md）
//     dotnet run --project BattleSim -c Release 0 grade2 run     # 段2 の測定（発火率・到達率）
//     dotnet run --project BattleSim -c Release 0 grade2 check [採用前のbalance.md]
if (focusId == "grade2")
{
    Grade2Diag.Run(args.Length > 2 ? args[2] : "phase0", args.Length > 3 ? args[3] : "");
    return;
}

// survive モード: 指標の直しと、「起動まで守れるか」（第129期）。中身は `Survive.cs`。
// **Phase 0 と段2 は盤面を1ビットも動かさない**——器具（`TraitId.Undying`）の保持者は
// `UnitCatalog.All` に1枚もおらず、台は診断のローカルで `Presets` を1文字も触らない。
// `TankDiag` / `Wound2Diag` / `TimeDiag` / `GradeDiag` と同じく**ここは振り分けの数行だけ**。
//
//     dotnet run --project BattleSim -c Release 0 survive phase0  # Q0-1〜Q0-9
//     dotnet run --project BattleSim -c Release 0 survive run     # 段2（延命台）
//     dotnet run --project BattleSim -c Release 0 survive check [採用前のbalance.md]
if (focusId == "survive")
{
    SurviveDiag.Run(args.Length > 2 ? args[2] : "phase0", args.Length > 3 ? args[3] : "");
    return;
}

// ember モード: 火を配る（第130期）。中身は `Relay.cs`。
// **Phase 0 は盤面を1ビットも動かさない**（走査と数え物だけ）。段1 は `EmberRule` で版を振る
// ——`EmberRule.Off` は第129期までの盤面と 305 セル 0 件で一致する（`Ignite` は乱数を引かない）。
// `SurviveDiag` / `Grade2Diag` と同じく**ここは振り分けの数行だけ**。
//
//     dotnet run --project BattleSim -c Release 0 ember phase0  # Q0-1〜Q0-9
//     dotnet run --project BattleSim -c Release 0 ember run     # 段1（主判定 P1 ＋ 盤面）
//     dotnet run --project BattleSim -c Release 0 ember check [採用前のbalance.md]
if (focusId == "ember")
{
    RelayDiag.Run(args.Length > 2 ? args[2] : "phase0", args.Length > 3 ? args[3] : "");
    return;
}

// wildfire モード（第133期） —— 撒いた火を読む（ボルグ）。
// **敵が燃えていることを読む駒が1枚も無い**という穴を埋める。実装は `BattleSim/Wildfire.cs`。
if (focusId == "wildfire")
{
    WildfireDiag.Run(args.Length > 2 ? args[2] : "phase0", args.Length > 3 ? args[3] : "");
    return;
}

// stacks モード（第134期） —— 重ね掛けの実測と、盤面ルールの対称性。
// **測定だけの期。機構を1つも足さない。** 実装は `BattleSim/Stacks.cs`。
//
//     dotnet run --project BattleSim -c Release 0 stacks phase0  # Q0-1〜Q0-8（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 stacks burn    # 段1（重ね掛けの実測）
//     dotnet run --project BattleSim -c Release 0 stacks rules   # 段2（盤面ルールの対称性）
//     dotnet run --project BattleSim -c Release 0 stacks check [採用前のbalance.md]
if (focusId == "stacks")
{
    StacksDiag.Run(args.Length > 2 ? args[2] : "phase0", args.Length > 3 ? args[3] : "");
    return;
}

// parry モード: ガルドは何で死んでいるか（と、受け流し）（第135期）。中身は `Parry.cs`。
// **段1 は測るだけ**——engine に足したのは計数のノブだけで、既定では配列を1本も確保しない。
// **ここで規則の型名を書かない**——`derive rules` は直前のクラス宣言でファイルとモードを結ぶので、
// 振り分けのコメントに型名を書くと `利用者` 列が1つ手前のモード（`ember`）にも付く（第129期）。
// `Presets` / `EnemyCatalog.Stages` / `UnitCatalog.All` は1文字も触らない。
// `SurviveDiag` / `RelayDiag` と同じく**ここは振り分けの数行だけ**（Release のビルド時間）。
//
//     dotnet run --project BattleSim -c Release 0 parry phase0            # Q0-1〜Q0-10
//     dotnet run --project BattleSim -c Release 0 parry harm > docs/harm.md  # 段1 の生成物
//     dotnet run --project BattleSim -c Release 0 parry check [採用前のbalance.md]
if (focusId == "parry")
{
    ParryDiag.Run(args.Length > 2 ? args[2] : "phase0", args.Length > 3 ? args[3] : "");
    return;
}

// wall モード（第136期） —— ガルドを壁にする（確実な庇い・受け流し・中継）。本体は `Wall.cs`。
// **ここは振り分けの数行だけ**（Release のビルド時間）。**規則の型名をここに書かない**（第129期）。
//
//     dotnet run --project BattleSim -c Release 0 wall phase0            # Q0-1〜Q0-10
//     dotnet run --project BattleSim -c Release 0 wall n                 # 段2 の N の決め方（§5-2）
//     dotnet run --project BattleSim -c Release 0 wall run [段1のbalance.md]  # 段2/段3 の版 × 群A/B/C
//     dotnet run --project BattleSim -c Release 0 wall check [段1のbalance.md]  # 自己検査
if (focusId == "wall")
{
    WallDiag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3)));
    return;
}

// shard モード（第137期） —— 砕けの鍵を自前にする（ヒビ）。本体は `Shard.cs`。
// **ここは振り分けの数行だけ**。**規則の型名をここに書かない**（第129期）。
//
//     dotnet run --project BattleSim -c Release 0 shard phase0   # Q0-1 の実測（現行の帳簿）
//     dotnet run --project BattleSim -c Release 0 shard scan     # 段1 の台の下見（床と天井）
//     dotnet run --project BattleSim -c Release 0 shard run      # 掰引（3点）× 台 × 既存５行
//     dotnet run --project BattleSim -c Release 0 shard check docs/balance.md   # 自己検査
if (focusId == "shard")
{
    ShardDiag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3)));
    return;
}

// brace モード（第143期） —— ササの転生（身を固めて、はね返した分を撒く）。本体は `Modes/Brace.cs`。
if (focusId == "brace")
{
    BraceDiag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3)));
    return;
}

// tumult モード（第144期） —— バサの転生（敵陣を掻き回し、前に出した敵を転ばせる）。本体は `Modes/Tumult.cs`。
if (focusId == "tumult")
{
    TumultDiag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3)));
    return;
}

// mark モード（第150期） —— 標（`Marked`）の軸を診る。本体は `Modes/Mark.cs`。
if (focusId == "mark")
{
    MarkDiag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3)));
    return;
}

// gust モード（第151期） —— バサの手番を「突風」にする。本体は `Modes/Gust.cs`。
if (focusId == "gust")
{
    GustDiag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3)));
    return;
}

// ward モード（第153期） —— 預かり（回復に「時間」の次元を入れる）。本体は `Modes/Ward.cs`。
if (focusId == "ward")
{
    WardDiag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3)));
    return;
}

// wardcost モード（第154期） —— 預かりの代金を付け替える。本体は `Modes/Wardcost.cs`。
if (focusId == "wardcost")
{
    WardcostDiag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3)));
    return;
}

// toll モード（第155期） —— 贖いのアガ（前借りと取り立て）。本体は `Modes/Toll.cs`。
if (focusId == "toll")
{
    TollDiag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3)));
    return;
}

// stage モード（第157期） —— ステージ単位で駒を測る。本体は `Modes/Stage.cs`。
if (focusId == "stage")
{
    StageDiag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3)));
    return;
}

// stall モード（第152期） —— 膠着（30 ターン上限）を塞ぐ。本体は `Modes/Stall.cs`。
if (focusId == "stall")
{
    StallDiag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3)));
    return;
}

// susu モード（第179期） —— A群の転生 1枚目：拾い屋のスス。本体は `Modes/Susu.cs`。
if (focusId == "susu")
{
    SusuDiag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3)));
    return;
}

// mudohex モード（第182期） —— 泥人形ムドの呪いを既定でオンにする。本体は `Modes/MudoHex.cs`。
if (focusId == "mudohex") { MudoHexDiag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3))); return; }

// mudo モード（第181期） —— 泥人形ムドの手直し。本体は `Modes/Mudo.cs`。
if (focusId == "mudo")
{
    MudoDiag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3)));
    return;
}

// rebirth3 モード（第183期） —— B群の転生 最後の2枚（ヴェル／ラウ）。本体は `Modes/Rebirth3.cs`。
if (focusId == "rebirth3") { Rebirth3Diag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3))); return; }
// mark184 モード（第184期） —— A群の転生 1〜2枚目（標の軸: ヒサ・ザン ＋ 敵の標の被ダメージ増）。本体は `Modes/Mark184.cs`。
if (focusId == "mark184") { Mark184Diag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3))); return; }
// rebirtha2 モード（第185期） —— A群の転生 3〜5枚目（クグ・シガ・バン）。本体は `Modes/RebirthA2.cs`。
if (focusId == "rebirtha2") { RebirthA2Diag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3))); return; }
// sora186 モード（第186期） —— 逸らしのソラ（半分を逸らす）。本体は `Modes/Sora186.cs`。
if (focusId == "sora186") { Sora186Diag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3))); return; }

// escale モード（第187期） —— 敵の難易度のつまみ（数値で一律に強くする）。本体は `Modes/EnemyScale.cs`。
if (focusId == "escale") { EnemyScaleDiag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3))); return; }
// kata モード（第188期） —— ナタの枠に新駒「触媒のカタ」（起爆）。本体は `Modes/Kata.cs`。
if (focusId == "kata") { KataDiag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3))); return; }
// debuff モード（第189期） —— デバッファー3枚の転生（ネル・クビ・ハネ）。本体は `Modes/Debuff*.cs`。
if (focusId == "debuff") { DebuffDiag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3))); return; }
// beni モード（第190期） —— 毒喰らいのベニの転生（反転の結界）。本体は `Modes/Beni*.cs`。
if (focusId == "beni") { BeniDiag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3))); return; }
// mio モード（第194期） —— 澱みのミオの転生（一点濃縮）。本体は `Modes/Mio*.cs`。
if (focusId == "mio") { MioDiag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3))); return; }
// sid モード（第195期） —— 毒吐きのスィドの転生（ガルド抜きで耐える毒パ）。本体は `Modes/Sid*.cs`。
if (focusId == "sid") { SidDiag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3))); return; }
// galdlast モード（第198期） —— ガルドの最後の段（剣の段）。本体は `Modes/GaldLast*.cs`。
if (focusId == "galdlast") { GaldLastDiag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3))); return; }
// form2 モード（第200期） —— 味方の陣形パターン2（ひし形）。本体は `Modes/Formation2.cs`。
if (focusId == "form2") { Formation2Diag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3))); return; }

// rebirth2 モード（第180期） —— B群の転生 3〜5枚目（ムド／ヴィオ／ガン）。本体は `Modes/Rebirth2.cs`。
if (focusId == "rebirth2")
{
    Rebirth2Diag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3)));
    return;
}

// rebirth モード（第178期） —— B群の転生（熾のホタ／逆しまのウツ）。本体は `Modes/Rebirth.cs`。
if (focusId == "rebirth")
{
    RebirthDiag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3)));
    return;
}

// haste モード（第149期） —— 行動順という通貨の値段。本体は `Modes/Haste.cs`。
if (focusId == "haste")
{
    HasteDiag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3)));
    return;
}

// tumult2 モード（第148期） —— バサの手番を混乱に使う。本体は `Modes/Tumult2.cs`。
if (focusId == "tumult2")
{
    Tumult2Diag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3)));
    return;
}

// derange モード（第147期） —— バサの転倒を混乱に置き換える。本体は `Modes/Derange.cs`。
if (focusId == "derange")
{
    DerangeDiag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3)));
    return;
}

// confuse モード（第146期） —— 混乱（動かされた駒は次の攻撃を自軍へ向ける）。本体は `Modes/Confuse.cs`。
if (focusId == "confuse")
{
    ConfuseDiag.Run(args.Length > 2 ? args[2] : "phase0", string.Join(" ", args.Skip(3)));
    return;
}

// sweep モード（第141期） —— 全診断の exit 検査。本体は `Sweep.cs`。
// `CLAUDE.md` のコマンド表を自分で読み、引数の穴の無い本を1本ずつ上限つきの子プロセスで走らせる。
// **合格 = 異常終了 0 本**。毎期は回さない（80 分前後）——`UnitCatalog.All` ／ `Retired` ／ `Presets` に触る期の受け入れ条件。
//
//     dotnet run --project BattleSim -c Release 0 sweep [上限秒] [絞り込み]   # 全部（上限 90 秒）
//     dotnet run --project BattleSim -c Release 0 sweep list                  # 一覧だけ（戦闘0回）
if (focusId == "sweep")
{
    SweepDiag.Run(args.Skip(2).ToArray());
    return;
}

if (focusId == "handoff") { HandoffDiag.Run(args, stageIndex); return; }

if (focusId == "cost") { CostDiag.Run(args, stageIndex); return; }

if (focusId == "gradient") { GradientDiag.Run(args, stageIndex); return; }

if (focusId == "aim") { AimDiag.Run(args, stageIndex); return; }

if (focusId == "flip") { FlipDiag.Run(args, stageIndex); return; }

if (focusId == "bridge") { BridgeDiag.Run(args, stageIndex); return; }

if (focusId == "bill") { BillDiag.Run(args, stageIndex); return; }

if (focusId == "charge") { ChargeDiag.Run(args, stageIndex); return; }

if (focusId == "timing") { TimingDiag.Run(args, stageIndex); return; }

if (focusId == "power") { PowerDiag.Run(args, stageIndex); return; }

if (focusId == "bench") { BenchDiag.Run(args, stageIndex); return; }

if (focusId == "wave") { WaveDiag.Run(args, stageIndex); return; }

if (focusId == "dissect") { DissectDiag.Run(args, stageIndex); return; }

if (focusId == "output") { OutputDiag.Run(args, stageIndex); return; }

if (focusId == "convert") { ConvertDiag.Run(args, stageIndex); return; }

if (focusId == "chain") { ChainDiag.Run(args, stageIndex); return; }

if (focusId == "run") { RunDiag.Run(args, stageIndex); return; }

if (focusId == "choice") { ChoiceDiag.Run(args, stageIndex); return; }

if (focusId == "recover") { RecoverDiag.Run(args, stageIndex); return; }

if (focusId == "ablate") { AblateDiag.Run(args, stageIndex); return; }

if (focusId == "confirm") { ConfirmDiag.Run(args, stageIndex); return; }

if (focusId == "seats2") { Seats2Diag.Run(args, stageIndex); return; }

if (focusId == "reseat") { ReseatDiag.Run(args, stageIndex); return; }

if (focusId == "layout") { LayoutDiag.Run(args, stageIndex); return; }

if (focusId == "dump") { DumpDiag.Run(args, stageIndex); return; }

EnemyCatalog.Stage stage = EnemyCatalog.Stages[stageIndex];
Console.WriteLine($"対象ステージ: {stage.Name}\n");

if (focusId == "life") { LifeDiag.Run(args, stageIndex); return; }

if (focusId == "cross") { CrossDiag.Run(args, stageIndex); return; }


if (focusId == "ptrace") { PtraceDiag.Run(args, stageIndex); return; }

if (focusId == "demo") { DemoDiag.Run(args, stageIndex); return; }

const int SeedsPerFormation = 20;

var units = UnitCatalog.All.Where(u => u.Id != "spore").ToList();
var records = new List<(double WinRate, UnitDef?[] Slots)>();

foreach (var combo in Combinations(units, 4))
{
    if (focusId.Length > 0 && combo.All(u => u.Id != focusId)) continue;

    foreach (var slots in SlotPermutations(combo))
    {
        var f = new Formation();
        for (int i = 0; i < FormationRules.PlayableSlotCount; i++) f[i] = slots[i];

        int wins = 0;
        for (int seed = 0; seed < SeedsPerFormation; seed++)
            if (BattleEngine.Run(f, stage.Enemy, seed, verbose: false).PlayerWon)
                wins++;

        records.Add((wins / (double)SeedsPerFormation, slots));
    }
}

Console.WriteLine($"検証した編成: {records.Count} 通り × {SeedsPerFormation} 回\n");

Console.WriteLine("--- 勝率の高い編成 TOP 10 ---");
foreach (var r in records.OrderByDescending(r => r.WinRate).Take(10))
    Console.WriteLine($"  {r.WinRate,6:P0}  {Describe(r.Slots)}");

Console.WriteLine("\n--- ユニット別 平均勝率 ---");
double overall = records.Average(r => r.WinRate);
foreach (UnitDef u in units)
{
    var with = records.Where(r => r.Slots.Any(x => x?.Id == u.Id)).ToList();
    if (with.Count == 0) continue;
    double avg = with.Average(r => r.WinRate);
    double best = with.Max(r => r.WinRate);
    string flag = avg < overall - 0.05 ? "  ← 平均以下" : "";
    Console.WriteLine($"  {u.Name,-16} 平均 {avg,6:P1} / 最高 {best,6:P0}{flag}");
}
Console.WriteLine($"  （全体平均 {overall:P1}）");

Console.WriteLine("\n--- ペア相性 TOP 10 ---");
var pairs = new List<(string Key, double Avg, int N)>();
for (int i = 0; i < units.Count; i++)
for (int j = i + 1; j < units.Count; j++)
{
    var with = records.Where(r =>
        r.Slots.Any(x => x?.Id == units[i].Id) &&
        r.Slots.Any(x => x?.Id == units[j].Id)).ToList();
    if (with.Count == 0) continue;
    pairs.Add(($"{units[i].Name} + {units[j].Name}", with.Average(r => r.WinRate), with.Count));
}
foreach (var p in pairs.OrderByDescending(p => p.Avg).Take(10))
    Console.WriteLine($"  {p.Avg,6:P1}  {p.Key}");

Console.WriteLine("\n--- ペア相性 WORST 5 ---");
foreach (var p in pairs.OrderBy(p => p.Avg).Take(5))
    Console.WriteLine($"  {p.Avg,6:P1}  {p.Key}");

return;











// ---- 波の代金診断（第5期 cost / gradient が共有） ----
























    }
}





/// <summary>
/// 参照台1つ × 編成1つぶんの出力（第17期 Phase HA）。<see cref="MeasureOutput"/> が埋める。
///
/// **seed ごとの生の値を持っているのが要点。** 半割（測定の信頼性の上限）を
/// 同じ計測から取り出すために要る——2回走らせると、半割の値そのものに実行間の
/// ばらつきが乗る（第13期 <c>bench</c> と同じ作法）。
///
/// **どの列も盤面には一切影響しない。** verbose=true の <c>Events</c> を読み直しているだけ。
/// </summary>
sealed class OutputTrace
{
    /// <summary>立ち上がりを見る範囲。T1 / T3 / T5 を取るので 5 で足りる。</summary>
    public const int Ramp = 5;

    public required int Seeds { get; init; }

    /// <summary>seed ごとの、敵に通した総打点（直接 + 毒燃。敵同士の巻き込みは除く）。</summary>
    public required double[] Damage { get; init; }
    /// <summary>seed ごとのターン数。</summary>
    public required double[] Turns { get; init; }
    /// <summary>seed ごとの T1..T5 の**累積**打点。決着後は増えないので、そのまま頭打ちになる。</summary>
    public required double[][] Cum { get; init; }

    /// <summary>手番の振りに帰属した打点の合計（全 seed）。</summary>
    public required double Swing { get; init; }
    /// <summary>出どころのある打点の合計（振り + 反撃・破裂・追い打ち・生贄）。</summary>
    public required double Direct { get; init; }
    /// <summary>毒・燃焼の打点の合計。<c>ApplyDamage</c> が source を渡さないので出どころが無い。</summary>
    public required double Dot { get; init; }

    /// <summary>ターン数が <see cref="Ramp"/> 未満だった試行数。**(B) が測れているかの検定。**</summary>
    public required int Short { get; init; }
    public required int AllyWipe { get; init; }
    public required int FoeWipe { get; init; }

    /// <summary>検算用。敵の tally から数えた同じ量（第13期の受け手側測定）。</summary>
    public required double TallyDamage { get; init; }
    /// <summary>
    /// 敵の撃破数の合計（全 seed）。**受け手側から数える**——毒・燃焼の削りは出どころを
    /// 持たないので、味方側の <c>Kills</c> には載らない（第13期 Phase DA）。
    /// 第18期が「出力が撃破に変換されているか」を読むために足した列で、
    /// **第17期の (A)(B)(C) はこの列を一切見ない**（`output` の出力は1文字も動かない）。
    /// </summary>
    public required long Kills { get; init; }

    /// <summary>
    /// オーバーキルの合計（全 seed）。<c>ApplyDamage</c> は残HPで切り詰めないので、
    /// <c>Damage</c> イベントの <c>Amount</c> には超過分が入っている
    /// ——**(A) は「敵のHPに変換された量」ではなく「振り下ろした量」を測っている。**
    /// 1体ごとに「通した合計 − 最大HP」で数える（総打点 − 敵の総HP では、生き残った駒の
    /// ぶんまで引いてしまう）。
    /// </summary>
    public required double Overkill { get; init; }

    /// <summary>検算用。敵同士の巻き込み。参照台は単一 def の単体攻撃なので 0 のはず。</summary>
    public required long FoeFromAlly { get; init; }

    /// <summary>
    /// **(A) 実効打点/ターン。** seed の部分集合で取れるようにしてあるのは半割のため。
    /// 平均の平均ではなく**総打点 ÷ 総ターン数**（試行ごとの長さが違うので、
    /// 比の平均を取ると短い試行に重みが寄る）。
    /// </summary>
    public double Rate(Func<int, bool> take)
    {
        double d = 0, t = 0;
        for (int s = 0; s < Seeds; s++) if (take(s)) { d += Damage[s]; t += Turns[s]; }
        return t <= 0 ? double.NaN : d / t;
    }

    /// <summary>(A) 全 seed 版。</summary>
    public double RateAll => Rate(_ => true);

    /// <summary>T 番目（1 起点）までの累積打点の試行平均。</summary>
    public double CumAt(int turn) => Cum.Average(c => c[turn - 1]);

    /// <summary>
    /// **(B) 立ち上がりの傾き。** `(T5 − T3) ÷ 2` は T4〜T5 の1ターンあたり打点、
    /// `T1` は初手の1ターンあたり打点。**その比**なので 1.0 が「まったく育たない」。
    /// 甲群（出力が時間で育つ）は 1 を大きく超え、乙群（一撃圏に縛られる）は 1 付近になるはず。
    /// </summary>
    public double Ramp15 => CumAt(1) <= 0 ? double.NaN : ((CumAt(5) - CumAt(3)) / 2) / CumAt(1);

    /// <summary>
    /// **(C) 手番外率（%）。** 打点のうち手番の振り以外（反撃・破裂・追い打ち・生贄・毒燃）
    /// から出たぶん。**近似ではなく実測**——計画書 §4-1 は `総攻 × 手番数` を引く近似を
    /// 示していたが、`Events` から振りの範囲を切れるので引き算の近似は要らない。
    /// </summary>
    public double OffTurnPct => Direct + Dot <= 0 ? double.NaN : (Direct + Dot - Swing) * 100.0 / (Direct + Dot);

    /// <summary>
    /// (C) の直接ダメージだけ版。**`dissect` の `振に帰属%` の裏返し**（100 − あれ）なので、
    /// 第16期の「溜め改 S4 で 78%」と直接突き合わせられる。
    /// </summary>
    public double OffTurnDirectPct => Direct <= 0 ? double.NaN : (Direct - Swing) * 100.0 / Direct;

    /// <summary>打点の内訳（%）。振り / 手番外の直接 / 毒燃。</summary>
    public double SwingPct => Direct + Dot <= 0 ? double.NaN : Swing * 100.0 / (Direct + Dot);
    public double ReactPct => Direct + Dot <= 0 ? double.NaN : (Direct - Swing) * 100.0 / (Direct + Dot);
    public double DotPct => Direct + Dot <= 0 ? double.NaN : Dot * 100.0 / (Direct + Dot);
}

/// <summary>
/// 1事例（編成 × 波）ぶんの解剖材料（第16期 Phase GA）。<see cref="MeasureTrace"/> が埋める。
///
/// **タプルではなく型にしてあるのは列が 25 本あるから。** 名前付きタプルでも書けるが、
/// 25 要素の型注釈が呼び出し側と関数側の2箇所に写ることになり、片方だけ直す事故が起きる。
///
/// **どの列も盤面には一切影響しない。** verbose=true の <c>Events</c> を読み直しているだけで、
/// <c>BattleCore</c> には1文字も足していない（第16期 §1「やらないこと」）。
/// </summary>
sealed class WaveTrace
{
    /// <summary>ターン推移を出す範囲。既存5波の決着はほぼ 3〜9T なので 12 で足りる。</summary>
    public const int Profile = 12;

    public required int Seeds { get; init; }
    public required int Party { get; init; }
    public required int Foes { get; init; }
    public required int FoeHpTotal { get; init; }
    public required int AllyHpTotal { get; init; }

    // --- 結末 ---
    public required double WinRate { get; init; }
    /// <summary>30T 打ち切りでの敗北率。**全滅とは中身が違う**（削り切れなかった側）。</summary>
    public required double DrawRate { get; init; }
    /// <summary>全滅での敗北率（削られ切った側）。</summary>
    public required double WipeRate { get; init; }
    public required double TurnsWin { get; init; }
    public required double TurnsWinSd { get; init; }
    public required double TurnsLose { get; init; }
    public required double AliveOnWin { get; init; }

    // --- 味方の出力 ---
    /// <summary>1戦あたり味方が振った回数（Attack イベント）。反撃はここを通らない。</summary>
    public required double AllySwings { get; init; }
    /// <summary>1振りで実際に削った敵の数。**範囲が何体を巻き込んだか**の実測。</summary>
    public required double HitsPerSwing { get; init; }
    /// <summary>1振りの主目標への打点。一撃圏の分母になる。</summary>
    public required double PrimaryDmg { get; init; }
    /// <summary>
    /// 敵に通した直接ダメージのうち、**手番の振りに帰属したぶん**の割合。
    /// 残りは手番外（反撃・破裂・追い打ち・生贄）から来ている。`pulse` の
    /// 「振 ≒ 0 / 干渉 大 = 反応型」を、量の側で見た列。
    /// </summary>
    public required double SwingShare { get; init; }
    /// <summary>1戦あたり敵に通した直接ダメージ（毒・燃焼を含まない）。</summary>
    public required double DirectToFoe { get; init; }
    /// <summary>1戦あたり敵に通した毒・燃焼のダメージ。出どころを持たないので直接とは分ける。</summary>
    public required double DotToFoe { get; init; }
    public required double FoeDeaths { get; init; }
    /// <summary>敵1体を落とすのに振った回数。**一撃圏の実測版。**</summary>
    public required double SwingsPerKill { get; init; }
    /// <summary>(直接 + 毒燃) ÷ 敵の総HP。1.00 を超えたぶんが過剰殺傷と敵の回復。</summary>
    public required double ShaveRatio { get; init; }

    // --- 敵の出力 ---
    public required double FoeSwings { get; init; }
    public required double AllyTaken { get; init; }
    /// <summary>味方の被ダメのうち後列（slot 4/5）が受けた割合。貫きが後列に届いたかを見る。</summary>
    public required double BackShare { get; init; }
    public required double DotToAlly { get; init; }

    // --- 毒 ---
    /// <summary>敵に乗った毒の総段数のピーク（試行平均）。</summary>
    public required double PoisonPeak { get; init; }
    public required double PoisonPeakTurn { get; init; }
    /// <summary>敵が落ちた時点で乗ったままだった毒の段数の合計。**乗り切る前に落ちた量。**</summary>
    public required double PoisonWasted { get; init; }

    // --- 推移（ターン開始時点の平均生存数。決着後は決着時の盤面で埋める） ---
    public required double[] AllyAlive { get; init; }
    public required double[] FoeAlive { get; init; }
}

/// <summary>
/// 業（第49期）の1測定ぶんの集計。<b>タプルではなく型にしてあるのは列が 20 本あるから</b>
/// （名前付きタプルでも書けるが、20 要素の型注釈が呼び出し側と関数側の2箇所に写る）。
/// <c>WaveTrace</c>（第16期）と同じ理由。
///
/// <b>どの列も盤面には一切影響しない。</b> <c>BattleResult</c> の計数を読み直しているだけ。
/// </summary>
sealed class SgStat
{
    public double Win, Turns;
    public double Takes, Missed, Full;
    public double KindAvg, KindMax, Met, First, Never;
    public double Swings, Fired;
    public double FoeDot, FoeSkips, MarkPulls;
    public double SelfDot, SelfSkips, AllyDot, AllySkips, Life;
    public Dictionary<string, double> TakeByKind = new();
    public Dictionary<string, double> WriteByKind = new();
    public Dictionary<string, double> TakeFrom = new();
}

/// <summary>
/// 逸らし（第50期）の1測定ぶんの集計。<b>タプルではなく型にしてあるのは列が 20 本あるから</b>
/// （<c>WaveTrace</c> / <c>SgStat</c> と同じ理由）。
/// <b>どの列も盤面には一切影響しない。</b> <c>BattleResult</c> の計数を読み直しているだけ。
/// </summary>
sealed class GdStat
{
    public double Win, Turns;
    public double Fires, Idle, Given, Switches, MarkLost, ToPerverse;
    public Dictionary<string, double> TargetTo = new();
    public Dictionary<string, double> Dmg = new();
    public Dictionary<string, double> Taken = new();
    public Dictionary<string, double> Last = new();
    public Dictionary<string, double> Deaths = new();
}

// **主判定の固定行集合（19行）と歯止めの線。第60期に確定した**（第59期 9-4 の移行案への回答）。
//
// **歯止めは全61行の平均ではなくこの集合の上で測る。** 第41〜59期の「第五波が歯止めを割った」は
// すべて**分母の話**で、波そのものは第40期（曝きの採用）から1つも動いていない
// ——実測でも第58〜60期に行を5本足す間、この17〜19行の第五波平均は 1ビットも動いていない。
//
// 中身は第31期の16行 + `突き出し`（17行）から、第60期に**1行を差し替え・2行を足した**もの:
//   差し替え `裂き (キリ×エグ)` → `裂き×責め苦 (キリ×エグ×シガ)`（情報セル 2 → 3）
//   追加     `止め改 (トメ×薙ぎ)`   ——標の**敵側**の読み手（第53期）。#4 は味方側で無代表だった
//   追加     `引き受け (ウケ×ドハ)` ——`ctx.Dull` を通る行が主判定に1つも無かった（第42期）
// **`死軸×ホタ (ゾト×熾)` は保留のまま第111期に `Presets.Compare` から落ちた**
// ——`後衛特化+後備え`（#16）との r が **+1.0000**・max|Δ| **0.5pt** で 61 行でいちばん冗長で、
// しかも情報セルが 1（第89期 (P2) の席の差し替えで 3 → 1 に落ちていた）。**主判定には一度も入っていない。**
// 代わりに入ったのは `灯×薙ぎ (トモ×ドルガ)` だが、**これも主判定には入れていない**
// （主判定の集合を動かすと歯止め 33.2 の測り直しが要る。第111期 §8）。
//
// **行名で引いている。** `CompareBuilds()` の行名を変えたらここも直すこと
// （`spread` の §4 が見つからない行を警告として出す）。
/// <summary>
/// 第68期（carry）の集計セル。<b>行 × 駒</b> ごとに、5波 × seed 帯ぶんを積む。
/// <c>BattleResult</c> の計数を読み直しているだけで、<b>盤面には一切影響しない</b>。
/// </summary>
sealed class CyCell
{
    public int Trials;
    public long Attacks, AtkGain, Taken, Deaths;

    /// <summary>キーごとの届いた累計と回数（<see cref="UnitTally.CarryKeys"/> の並び）。</summary>
    public readonly long[] Amount, Count;

    /// <summary>格子ごとの到達した試行数と、その到達ターンの和。添字は <c>キー * 格子数 + 格子</c>。</summary>
    public readonly int[] ProbeN;
    public readonly double[] ProbeT;

    public CyCell(int keys, int probes)
    {
        Amount = new long[keys];
        Count = new long[keys];
        ProbeN = new int[keys * probes];
        ProbeT = new double[keys * probes];
    }
}

/// <summary>
/// 第116期（`reader load`）の観測。<b>どの列も盤面には一切影響しない</b>
/// ——<see cref="BattleResult"/> の計数を読み直しているだけ。
/// </summary>
sealed class LdStat
{
    public int N, FirstN;
    public double Win, Turns, Reach, Alive, Over, OverSwung, Sweeps, Splash, Swings, Bonus, BonusMax, First, Dmg, Kills;
    public double[] Probe = new double[UnitTally.ReaderProbes.Length];
}

static class Baseline
{
    public static readonly string[] PrimaryRows =
    {
        "隊列崩し (バサ×ヨミ×セロ)",          // 移動
        "燃焼 (ボルグ×ホタ)",                  // 燃焼（ボルグの毎ターン供給）
        "縛め収入型 (クグ×バン×ガン)",        // 縛め
        "仇討ち×砕け (ヒビ×ザン)",            // 標（味方側の読み手）
        "刻み×抉り (ノミ×エグ)",              // 傷（ノミ入口）
        "裂き×責め苦 (キリ×エグ×シガ)",      // 傷（キリ入口）**第60期に差し替え**
        "耐久 (ガルド×ノノ)",                  // 耐久（第36期の申し送りは第59期に解消）
        "溜め改 (クグ×バン×ガン)",            // 溜め
        "逆しま (ネル×ウツ)",                  // 逆しま
        "追撃×据え (ハギ×バン)",              // 追撃
        "置き去り×分散回復",                   // 置き去り／回復
        "毒+耐久 (ベニ×トウ)",                 // 毒
        "速攻 (ボルグ×ムド)",                  // 速攻
        "反撃改2 (ガン×カド)",                 // カウンター
        "惨禍×死の連鎖",                       // 死（番人）
        "後衛特化+後備え",                     // 後備え（番人。情報セルでは測らない）
        "突き出し (セロ×ヨミ)",                // 移動（予備）
        "止め改 (トメ×薙ぎ)",                  // 標（敵側の読み手）**第60期に追加**
        "引き受け (ウケ×ドハ)",                // 弱体の窓口（`ctx.Dull` の横取り）**第60期に追加**
    };

    /// <summary>
    /// 第五波の歯止め。<b>確定時（第60期）の主判定19行の第五波平均 38.2% − 5.0pt。</b>
    /// **旧値の 40.0 を据え置かなかった**のは、あれが 42行時代の**全行平均**から来た数字で、
    /// 第59期の提案20行がちょうど 40.0 で**線上に乗った**のが偶然だったため。
    /// **線は「明らかに成立しなくなる線」であって調整目標ではない**ので、線上に置くのが一番まずい。
    /// </summary>
    public const double PrimaryFifthFloor = 33.2;
}

/// <summary>
/// 瘴気の移設（第61期）の計数。<b>どの列も盤面には一切影響しない</b>
/// ——<c>BattleResult</c> の計数を読み直しているだけ。
/// </summary>
sealed class MsStat
{
    public double Win, Turns;
    public double Fires, ToFoe, ToAlly, BiteAlly, BiteFoe, TicksAlly, TicksFoe;
    public double Swings, Dmg, Life, TeamDmg, TeamTaken;
    public double VioDmg, BeniHeal;
}

sealed class FvStat
{
    public double Win, Turns;
    public double Fires, Idle, Whetted, Dulled, Given, Taken, ToPyre;
    public double Swings, Dmg, Life, TeamDmg;
    public Dictionary<string, double> WhetTo = new();
    public Dictionary<string, double> DullTo = new();
}

/// <summary>
/// 横流し（第62期）の計数。<b>どの列も盤面には一切影響しない</b>
/// ——<c>BattleResult</c> の計数を読み直しているだけ。
/// </summary>
sealed class FlStat
{
    public double Win, Turns;
    public double WhetTotal, DullTotal, Taken, Dead, DeadNew, DullTaken, DullDead;
    public double Hoard, HoardNew, Perverse, Flips;
    public double[] Route = new double[WhetRoutes.Count];
    public double[] TakenRoute = new double[WhetRoutes.Count];
    public double[] DullRoute = new double[DullRoutes.Count];
    public double[] DullTakenRoute = new double[DullRoutes.Count];
    public Dictionary<string, double> To = new();
    public Dictionary<string, double> DullTo = new();
    public Dictionary<string, double> DullFrom = new();
    public Dictionary<string, double> Got = new();
    public Dictionary<string, double> Atk = new();
    public Dictionary<string, double> Lost = new();
}

sealed class KdStat
{
    public double Win, Turns;
    public double Fires, Idle, Whetted, Dulled, Given, Taken, ToPyre, Hoard;
    public double WhetTotal, DullTotal;
    public Dictionary<string, double> WhetTo = new();
    public Dictionary<string, double> DullTo = new();
    public Dictionary<string, double> Dmg = new();
    public Dictionary<string, double> Taken2 = new();
    public Dictionary<string, double> Last = new();
    public Dictionary<string, double> Atk = new();
    public Dictionary<string, double> Got = new();
}

// 第59期 blaze。**陣営は敵側の `Def.Id` 集合で割る**——胞子のように戦闘中に湧いた味方も
// 味方に入る（`TallyByUnit` は `Def.Id` で引くので、`InstanceId` の範囲では割れない）。
sealed class BzStat
{
    public double Win, Turns;
    public double Lit, Relit, LitAlly;
    public double BurnDmgA, BurnDmgF, BurnDeathA, BurnDeathF;
    public double Deaths, RicaAtk;
    public Dictionary<string, double> Atk = new();
    public Dictionary<string, double> BurnAtk = new();
    public Dictionary<string, double> Dmg = new();
}

sealed class FnStat
{
    public double Win, Turns;
    public double Fires, Idle, Cross, Consumed, Kills;
    public double WaitSum, WaitCount, AllySingles, Starved;
    public double Supply, SupplyFresh, DvSingles, DvOnMarked;
    public Dictionary<string, double> TargetTo = new();
    public Dictionary<string, double> Dmg = new();
    public Dictionary<string, double> Taken = new();
    public Dictionary<string, double> Last = new();
    public Dictionary<string, double> Deaths = new();
}

sealed class DvStat
{
    public double Win, Turns;
    public double Fires, Strips, Focus, FocusFresh, MarkedFoe;
    public int MarkedFoeMax;
    public double AllySingles, AllyOnMarked, FoeSingles, FoeOnMarked, AllyPulls, FoePulls;
    public double SelfTaken, Life, KadoLife, KadoInter;
    public bool HasKado;
    public Dictionary<string, double> StripFrom = new();
    public Dictionary<string, double> FocusTo = new();
    public Dictionary<string, double> KillTurn = new();
    public Dictionary<string, double> KillCount = new();
}

/// <summary>
/// ドラフト台（第69期）の1帯ぶんの走査結果。<b>盤面には一切影響しない</b>
/// ——<c>BattleEngine.Run</c> の勝敗を数え直しているだけ。
/// </summary>
sealed class DfResult
{
    public required int N { get; init; }
    public required int M { get; init; }
    public required int W { get; init; }
    public required int Band { get; init; }

    /// <summary>[標本][版 * 波数 + 波] の勝数。版 0 = R（無作為）/ 版 1 = H（規則）。</summary>
    public required int[][] Full { get; init; }

    /// <summary>[標本][(版 * 在席対象駒数 + 添字) * 波数 + 波] の勝数（素体に差し替えた版）。</summary>
    public required int[][] Plain { get; init; }

    /// <summary>[標本] その標本に居る対象駒の添字（<c>dfTargets</c> の並び）。</summary>
    public required int[][] Present { get; init; }

    /// <summary>[標本][0..4] 抽選された5体（<c>UnitCatalog.All</c> の添字。抽選順）。</summary>
    public required int[][] Members { get; init; }
}

/// <summary>
/// 特性 → 通貨のキー（<see cref="UnitTally.CarryKeys"/> の添字）の対応表。
///
/// <para><b>出典は <c>BattleCore/Traits.cs</c> の grep</b>——<c>StatusKeys.*</c> の
/// <c>SetCounter</c> / <c>Counter</c> ／ <c>ctx.Whet</c> ／ <c>ctx.Dull</c> ／ <c>ctx.Ignite</c> ／
/// <c>ctx.SwapSlots</c> ／ <c>OnMoved</c> / <c>OnAllyMoved</c> / <c>OnDamaged</c> の override。
/// <b>engine 側の窓口は駒に属さないのでここには入らない</b>（第50期の窓口一覧の裏返し）。</para>
///
/// <para><b><c>Trait</c> に属性を足さない</b>——判定の根拠が「誰かが属性を正しく付けたか」に
/// 化けて grep で検算できなくなる（第48期 census の作法）。</para>
///
/// <para><b>1箇所に集めてある。</b> 第68期 <c>carry solo</c> が作り、第69期 <c>draft</c> が写し、
/// 第70期 <c>draft2</c> で3つ目の写しになるところだった——<b>2つ目の診断がコピーを持った瞬間に
/// 「1箇所に集める」が消える</b>（CLAUDE.md の <c>WaveCatalog()</c> の申し送りと同じ理由）。
/// <b>移しただけで中身は1文字も変えていない</b>（<c>carry solo</c> の出力が byte 一致することが検算）。</para>
/// </summary>
/// <summary>
/// 第80期の器具 —— <b>在席差</b>の帳簿（駒1枚と駒の組）。
///
/// <para><b>帰属（素体差し替え）ではない。</b>標本ごとの勝率 y を、その標本に在席した駒の組み合わせで
/// 4群（両方在席 / A だけ / B だけ / どちらも不在）に分けた平均の差で読む（指示書 §0-2）:</para>
///
/// <para><c>単独(A) = mean(A 在席) − mean(A 不在)</c>／
/// <c>組(A,B) = mean(両方在席) − mean(どちらも不在)</c>／
/// <c>相乗(A,B) = 組 − 単独(A) − 単独(B)</c></para>
///
/// <para>標準誤差は、相乗を4群の平均の線形結合として書き、群ごとの標本分散から合成する
/// （群の間は互いに素なので共分散は 0）。溜めるのは 駒 × 駒 の <c>n / Σy / Σy²</c> だけで、
/// 「A だけ」「どちらも不在」の群は駒ごと・全体の累計から引き算で作る。</para>
/// </summary>
/// <summary>
/// 棘の傷（第84期）の計数。<b>どの列も盤面には一切影響しない</b>——verbose の `Events` と `Log` を読み直しているだけ。
/// </summary>
sealed class ThStat
{
    public double N, Wins, Turns;
    public double Fires, FiresEv, FiresAlive, FiresUnderHush;    // 棘の発火（Log）／同（Events）／相手が生きていた（Events・InstanceId）／粛の保持者が生きている間（Events）
    public double Wounds, WoundsAlly;                           // 棘が傷を書いた回数（敵／味方）
    public double CarryWoundFoe, CarryWoundAlly;                // `CarryCount[CarryWound]` の合計（敵側／味方側。書き手を問わない）
    public double Stock, StockMax;                              // 敵側の傷の在庫のターン平均／最大
    public double Gouge, Trace, Sever, SeverWounds, SeverReached, Suture;
    public double DmgB, Healed;
    public void AddFrom(ThStat o)
    {
        N += o.N; Wins += o.Wins; Turns += o.Turns;
        Fires += o.Fires; FiresEv += o.FiresEv; FiresAlive += o.FiresAlive; FiresUnderHush += o.FiresUnderHush;
        Wounds += o.Wounds; WoundsAlly += o.WoundsAlly; CarryWoundFoe += o.CarryWoundFoe; CarryWoundAlly += o.CarryWoundAlly;
        Stock += o.Stock; StockMax += o.StockMax;
        Gouge += o.Gouge; Trace += o.Trace; Sever += o.Sever; SeverWounds += o.SeverWounds; SeverReached += o.SeverReached; Suture += o.Suture;
        DmgB += o.DmgB; Healed += o.Healed;
    }
}

sealed class PairAcc
{
    readonly int _u;
    public int N;
    public double S, Q;
    public readonly int[] NA;
    public readonly double[] SA, QA;
    public readonly int[,] N11;
    public readonly double[,] S11, Q11;

    public PairAcc(int units)
    {
        _u = units;
        NA = new int[units]; SA = new double[units]; QA = new double[units];
        N11 = new int[units, units]; S11 = new double[units, units]; Q11 = new double[units, units];
    }

    /// <summary>1標本を積む。<paramref name="team"/> は駒の添字（重複なし）。</summary>
    public void Add(IReadOnlyList<int> team, double y)
    {
        N++; S += y; Q += y * y;
        for (int i = 0; i < team.Count; i++)
        {
            int a = team[i];
            NA[a]++; SA[a] += y; QA[a] += y * y;
            for (int j = i + 1; j < team.Count; j++)
            {
                int b = team[j];
                N11[a, b]++; N11[b, a]++;
                S11[a, b] += y; S11[b, a] += y;
                Q11[a, b] += y * y; Q11[b, a] += y * y;
            }
        }
    }

    public double MeanIn(int a) => NA[a] == 0 ? double.NaN : SA[a] / NA[a];
    public double MeanOut(int a) => N - NA[a] == 0 ? double.NaN : (S - SA[a]) / (N - NA[a]);
    public double Solo(int a) => MeanIn(a) - MeanOut(a);

    static double Var(int n, double s, double q) => n < 2 ? 0.0 : Math.Max(0.0, (q - s * s / n) / (n - 1));

    public readonly record struct Stat(int N11, double SoloA, double SoloB, double Pair, double Syn, double Se);

    /// <summary>組 (a, b) の統計。両方在席が 0 標本なら NaN。</summary>
    public Stat Of(int a, int b)
    {
        int n11 = N11[a, b];
        if (n11 == 0) return new Stat(0, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        double s11 = S11[a, b], q11 = Q11[a, b];
        int n10 = NA[a] - n11, n01 = NA[b] - n11, n00 = N - NA[a] - NA[b] + n11;
        double s10 = SA[a] - s11, s01 = SA[b] - s11, s00 = S - SA[a] - SA[b] + s11;
        double q10 = QA[a] - q11, q01 = QA[b] - q11, q00 = Q - QA[a] - QA[b] + q11;
        double m11 = s11 / n11, m00 = n00 == 0 ? double.NaN : s00 / n00;
        double soloA = Solo(a), soloB = Solo(b);
        double pair = m11 - m00;
        double syn = pair - soloA - soloB;
        // 相乗 = c11·m11 + c10·m10 + c01·m01 + c00·m00
        int nA = NA[a], nB = NA[b], oA = N - nA, oB = N - nB;
        double c11 = 1.0 - (double)n11 / nA - (double)n11 / nB;
        double c10 = -(double)n10 / nA + (double)n10 / oB;
        double c01 = -(double)n01 / nB + (double)n01 / oA;
        double c00 = -1.0 + (double)n00 / oA + (double)n00 / oB;
        double v = 0;
        if (n11 > 0) v += c11 * c11 * Var(n11, s11, q11) / n11;
        if (n10 > 0) v += c10 * c10 * Var(n10, s10, q10) / n10;
        if (n01 > 0) v += c01 * c01 * Var(n01, s01, q01) / n01;
        if (n00 > 0) v += c00 * c00 * Var(n00, s00, q00) / n00;
        return new Stat(n11, soloA, soloB, pair, syn, Math.Sqrt(v));
    }
}

static class TraitKeyMap
{
    public static readonly Dictionary<TraitId, int[]> TraitKeys = new()
    {
        // 強化
        [TraitId.Rally]      = new[] { UnitTally.CarryWhet, UnitTally.CarryIdle },
        [TraitId.Bind]       = new[] { UnitTally.CarryWhet, UnitTally.CarryStun },
        [TraitId.Grapple]    = new[] { UnitTally.CarryStun },                                   // 第185期（大縛りだけが痺れを書く。組み付きは専用キー）
        [TraitId.Drifter]    = new[] { UnitTally.CarryWhet, UnitTally.CarryMove },
        [TraitId.Goad]       = new[] { UnitTally.CarryWhet, UnitTally.CarryMark },
        [TraitId.Favor]      = new[] { UnitTally.CarryWhet, UnitTally.CarryDull, UnitTally.CarryBurn },
        [TraitId.Colossus]   = new[] { UnitTally.CarryWhet, UnitTally.CarryHit },
        [TraitId.Perverse]   = new[] { UnitTally.CarryWhet, UnitTally.CarryDull },
        [TraitId.Funnel]     = new[] { UnitTally.CarryWhet, UnitTally.CarryDull },
        // 弱体
        [TraitId.Curse]      = new[] { UnitTally.CarryDull },
        [TraitId.Cower]      = new[] { UnitTally.CarryDull },
        [TraitId.Shove]      = new[] { UnitTally.CarryDull, UnitTally.CarryMove },
        [TraitId.Bear]       = new[] { UnitTally.CarryDull, UnitTally.CarryArmor },
        [TraitId.Relay]      = new[] { UnitTally.CarryDull },
        [TraitId.Sharer]     = new[] { UnitTally.CarryDull, UnitTally.CarryHit },
        // 毒
        [TraitId.Miasma]     = new[] { UnitTally.CarryPoison },
        [TraitId.Venom]      = new[] { UnitTally.CarryPoison, UnitTally.CarryHit },
        [TraitId.Amplifier]  = new[] { UnitTally.CarryPoison, UnitTally.CarryWound },   // 第89期 (P1) の採用で 2 本目
        [TraitId.Contagion]  = new[] { UnitTally.CarryPoison },
        [TraitId.Devour]     = new[] { UnitTally.CarryPoison },
        [TraitId.Inverse]    = new[] { UnitTally.CarryPoison, UnitTally.CarryBurn },            // 第190期（ベニの転生・毒と燃焼の刻みを読む）
        [TraitId.Taint]      = new[] { UnitTally.CarryPoison },                                 // 第190期（隣の味方に毒を書く）
        [TraitId.Kindle]     = new[] { UnitTally.CarryBurn },                                   // 第191期（隣の味方に火を書く）
        [TraitId.InverseLeak]= Array.Empty<int>(),                                               // 第190期（マイナス側はキーを持たない・下の注と同じ）
        [TraitId.Concentrate]= new[] { UnitTally.CarryPoison, UnitTally.CarryWound },            // 第194期（旧 `Amplifier` と同じ2本・印は毒と燃焼の刻みを2回にする）
        [TraitId.ConcentrateLeak] = Array.Empty<int>(),                                          // 第194期（マイナス側はキーを持たない）
        [TraitId.Spew]       = new[] { UnitTally.CarryPoison },                                 // 第195期（スィドの吐き・敵に毒を書く）
        [TraitId.Numb]       = Array.Empty<int>(),                                               // 第195期（印は毒の層を読むだけ・判定は engine）
        [TraitId.SpewFixed]  = new[] { UnitTally.CarryPoison },                                 // 第196期（対照・散らさない吐き）
        [TraitId.VenomHeavy] = new[] { UnitTally.CarryPoison, UnitTally.CarryHit },             // 第196期（S+反の版・`Venom` と同じ2本）
        [TraitId.Guren]      = new[] { UnitTally.CarryPoison, UnitTally.CarryBurn },            // 第197期（紅蓮・敵全員に火と毒を書く）
        [TraitId.GurenStrike]= new[] { UnitTally.CarryBurn },                                   // 第197期（対照・毒の代わりに直撃）
        [TraitId.GurenLow]   = new[] { UnitTally.CarryPoison, UnitTally.CarryBurn },            // 第197期（対照・閾値 6）
        [TraitId.GurenFull]  = new[] { UnitTally.CarryPoison, UnitTally.CarryBurn },            // 第197期（参考・満額）
        [TraitId.LastStandHold] = new[] { UnitTally.CarryHit },                                 // 第199期（規定）
        [TraitId.LastStandHoldOldScar] = new[] { UnitTally.CarryHit },                          // 第199期（対照）
        [TraitId.LastStandHoldNoStock] = new[] { UnitTally.CarryHit },                          // 第199期（対照）
        [TraitId.LastStandHoldMutualLoss] = new[] { UnitTally.CarryHit },                       // 第199期（対照）
        [TraitId.LastStandScar] = new[] { UnitTally.CarryHit },                                 // 第198期（剣＋傷・対照）
        [TraitId.LastStand]  = new[] { UnitTally.CarryHit },                                    // 第198期（剣の段・被弾に斬り返す）
        [TraitId.LastStandPlain] = Array.Empty<int>(),                                          // 第198期（対照・返しなし）
        [TraitId.LastStandShield]= Array.Empty<int>(),                                          // 第198期（参考・盾剣）
        [TraitId.Blightfed]  = new[] { UnitTally.CarryPoison },
        // 燃焼
        [TraitId.Cinder]     = new[] { UnitTally.CarryBurn },
        [TraitId.Bomber]     = new[] { UnitTally.CarryBurn },
        [TraitId.Pyre]       = new[] { UnitTally.CarryBurn },
        // 痺れ
        [TraitId.Paralyze]   = new[] { UnitTally.CarryStun },
        [TraitId.Torment]    = new[] { UnitTally.CarryStun, UnitTally.CarryIdle },
        [TraitId.Shame]      = new[] { UnitTally.CarryStun, UnitTally.CarryIdle },              // 第185期（責め苦と同じ「動けない」を読む）
        [TraitId.Gouge]      = new[] { UnitTally.CarryStun, UnitTally.CarryWound },
        [TraitId.Avenge]     = new[] { UnitTally.CarryStun, UnitTally.CarryMark, UnitTally.CarryHit },
        // 標
        [TraitId.Marker]     = new[] { UnitTally.CarryMark },
        [TraitId.Beckon]     = new[] { UnitTally.CarryMark },                                   // 第184期（旧 Marker の転生）
        [TraitId.Vendetta]   = new[] { UnitTally.CarryMark, UnitTally.CarryHit },               // 第184期（旧 Avenge の転生・怯みは外した）
        [TraitId.Divert]     = new[] { UnitTally.CarryMark },
        [TraitId.Finisher]   = new[] { UnitTally.CarryMark },
        // 破片
        [TraitId.Shatter]    = new[] { UnitTally.CarryArmor, UnitTally.CarryHit },
        [TraitId.Scale]      = new[] { UnitTally.CarryArmor },
        // 傷
        [TraitId.Rend]       = new[] { UnitTally.CarryWound },
        [TraitId.Carve]      = new[] { UnitTally.CarryWound },
        [TraitId.Sever]      = new[] { UnitTally.CarryWound },
        [TraitId.Suture]     = new[] { UnitTally.CarryWound },
        // 第74期に切り出したマイナス4枚は**キーを持たない**（意図的）。
        // この表は「駒 → キー」を Trait 経由で作る道具で、`KeysOf` は駒の Traits の**和**を取る。
        // 分割はプラス側の TraitId をそのまま残しているので、**和は1ビットも変わらない**
        // ——マイナス側にキーを足すと第68期以降のキーの数え方（駒で数える）が動いてしまう。
        // 抉りの `CarryStun` を `Overreach` へ移さないのも同じ理由（駒の側の和が答え）。
        [TraitId.ThinBlade]  = Array.Empty<int>(),
        [TraitId.Overreach]  = Array.Empty<int>(),
        [TraitId.Await]      = Array.Empty<int>(),
        [TraitId.Seal]       = Array.Empty<int>(),
        // 手番
        [TraitId.Bulwark]    = new[] { UnitTally.CarryIdle },
        // 繕いの傷読み（第92期に採用）。**これでノノに初めてキーが立つ**
        // ——第83期の「キーを1つも持たない駒は 8 / 51 体」は **7 / 51** になった（第80〜83期の派生値は動く）。
        [TraitId.Mender]     = new[] { UnitTally.CarryWound },
        // 被弾（damage の層に立つ読み手・書き手）
        [TraitId.Rage]       = new[] { UnitTally.CarryHit },
        [TraitId.Thorns]     = new[] { UnitTally.CarryHit },
        [TraitId.Guardian]   = new[] { UnitTally.CarryHit, UnitTally.CarryWound },   // 傷の引き取り（第90期に採用）
        [TraitId.RearGuard]  = new[] { UnitTally.CarryHit },
        [TraitId.Splash]     = new[] { UnitTally.CarryHit },
        // 移動
        [TraitId.Shuffler]   = new[] { UnitTally.CarryMove },
        [TraitId.Coward]     = new[] { UnitTally.CarryMove },
        [TraitId.ThornGuard] = new[] { UnitTally.CarryMove, UnitTally.CarryHit },
        [TraitId.Displaced]  = new[] { UnitTally.CarryMove },
        // 第106期。散開（ササ）が被弾のたびに隣の味方を弾くようになったので、移動の書き手になった
        // （`derive scan` の観測 0.05 回/戦）。**第78期の入口・発火口と第80〜83期の独立の広さは動く。**
        [TraitId.Loose]      = new[] { UnitTally.CarryMove },
        // 第79期の候補駒（首刈りのオノ）が使う2枚。撃破は 11 本のキーに無く、支援拒否は通貨を書きも読みもしない。
        // **どちらも空**で、`KeysOf` の和は動かない（ガルド＝Guardian・Stoic の値が変わらないことが検算）。
        [TraitId.Executioner] = Array.Empty<int>(),
        [TraitId.Stoic]       = Array.Empty<int>(),
        // 第108期の尾灯（トモ）。**強化の書き手**（`ctx.Whet` の `WhetRoute.Taillight`）。
        // **消灯は `ctx.Dull` を通さない**ので弱体のキーは立たない——素の攻撃力より弱くはしていないし、
        // 集約・転嫁の横取りに晒すつもりも無い（`TaillightTrait` の doc）。
        // **手番の譲渡もキーを持たない**——`IdleTurn` は「差し出した手番」の記録で、
        // 譲渡は engine の `TakeTurn` を1回余分に呼ぶだけで counter を1つも書かない。
        [TraitId.Taillight]   = new[] { UnitTally.CarryWhet },
        // 第103期の背かれ（ソム）。**撃破は 11 本のキーに無い**ので空
        // ——餌を敵陣に置くのは「体を1つ増やす」であって、通貨を1つも書かない
        // （`derive scan` の観測でも `Betrayed` は 0 件）。
        [TraitId.Betrayed]    = Array.Empty<int>(),
        // 敵側の2枚（第94期 (T2) の観測で出た欠落）。**`UnitCatalog.All` の 51 体は1枚も持たない**ので、
        // 第80〜83期のロスター側の派生値は動かない（`KeysOf` が変わるのは敵の駒だけ）。
        [TraitId.Condemn]     = new[] { UnitTally.CarryStun },     // 観測（断罪）
        [TraitId.Expose]      = new[] { UnitTally.CarryMove },     // 観測（曝き）
    };

    /// <summary>その駒が書き手または読み手になっているキーの一覧（重複なし・昇順）。</summary>
    public static int[] KeysOf(UnitDef d)
        => d.Traits.SelectMany(t => TraitKeys.TryGetValue(t, out int[]? k) ? k : Array.Empty<int>())
                   .Distinct().OrderBy(x => x).ToArray();
}

/// <summary>
/// 第78期の器具その1 —— <b>発火口</b>（その特性が反応するイベントの種類）。
///
/// <para><b>リフレクションで数えない。</b>この表は手で作ってここに置く（指示書 §2-1）。
/// 理由は第70期の集約と同じ——写しを増やさないため。<see cref="TraitKeyMap"/> と同じ場所に置いてある。</para>
///
/// <para><b>engine は擬似フック。</b>駒ごとのフックでは書けない機構は
/// <c>BattleEngine.cs</c> に窓口を持つ（CLAUDE.md の「engine も通貨の読み手である」）。
/// 札（<see cref="TraitId.Bear"/> / <see cref="TraitId.Relay"/> / <see cref="TraitId.Funnel"/> /
/// <see cref="TraitId.Seal"/> / <see cref="TraitId.RearGuard"/>）は本体が全部 engine にあるので、
/// これを数えないと<b>発火口 0 の機構</b>が出てしまう。<b>数えるのはその特性自身の挙動が
/// engine にある場合だけ</b>——標を読む <c>SelectTargetChain</c> のような「通貨の窓口」は、
/// 書き手（<see cref="TraitId.Marker"/>）の発火口ではないので数えない。</para>
///
/// <para><b><see cref="Trait.OnCarryOver"/> は数えない。</b>会戦の境界でしか呼ばれず、
/// この期が測るのは単発戦なので<b>原理的に 0 回</b>（判定式の自己検査 (b)）。</para>
/// </summary>
static class TraitHookMap
{
    public const string Engine = "engine";

    /// <summary>特性 → 発火口の一覧（<c>OnCarryOver</c> を除く）。<b>手で作った表。</b></summary>
    public static readonly Dictionary<TraitId, string[]> TraitHooks = new()
    {
        // --- マイナス側 ---
        [TraitId.Splash]      = new[] { "OnAfterAttack" },
        [TraitId.Coward]      = new[] { "OnTurnStart" },
        [TraitId.Stoic]       = new[] { "BlocksSupport", Engine },              // SupportTargets
        [TraitId.Sacrifice]   = new[] { "OnBattleStart" },
        [TraitId.Drain]       = new[] { "OnTurnStart" },
        [TraitId.Sluggish]    = new[] { "CanAct", "CanReact" },
        [TraitId.Splitter]    = new[] { "OnDeath" },
        [TraitId.Bomber]      = new[] { "OnDeath" },
        [TraitId.Frail]       = new[] { "ModifyIncomingDamage" },
        [TraitId.Fixate]      = new[] { Engine },                              // SelectTargetCore
        // --- プラス側 ---
        [TraitId.Rage]        = new[] { "OnDamaged" },
        [TraitId.Sniper]      = new[] { "ModifyAttack", "ModifyPattern" },
        [TraitId.Curse]       = new[] { "OnBattleStart" },
        [TraitId.Guardian]    = new[] { "OnDamaged", Engine },                 // SelectTargetChain
        [TraitId.Martyr]      = new[] { "OnDamaged", Engine },
        [TraitId.Necro]       = new[] { "OnTurnStart", "ModifyPattern", "OnAnyDeath" },
        [TraitId.Colossus]    = new[] { "OnDeath", Engine },                   // ApplyDamage の巨躯
        [TraitId.Executioner] = new[] { "OnKill" },
        [TraitId.Reviver]     = new[] { "OnAllyDeath" },
        [TraitId.Ephemeral]   = Array.Empty<string>(),                         // 旗。誰も反応しない
        [TraitId.Betrayed]    = new[] { "OnTurnStart" },                       // 第103期
        [TraitId.Venom]       = new[] { "OnDamaged" },
        [TraitId.Thorns]      = new[] { "OnDamaged" },
        [TraitId.Marker]      = new[] { "OnBattleStart" },
        [TraitId.Beckon]      = new[] { "OnBattleStart", "OnAction", Engine },   // 第184期（半減は ApplyDamage）
        [TraitId.Flee]        = new[] { "OnAction" },                            // 第184期
        [TraitId.Vendetta]    = new[] { "OnAllyDamaged" },                       // 第184期
        [TraitId.Recoil]      = Array.Empty<string>(),                           // 第184期（仇指しの中で読まれる札）
        [TraitId.Grapple]     = new[] { "OnBattleStart", "OnAction", "OnDamaged", "OnDeath", Engine },   // 第185期（手番を失うのは TakeTurnCore）
        [TraitId.Shame]       = new[] { "OnAfterAttack", Engine },               // 第185期（標的の選好は SelectTargetChain）
        [TraitId.Footing]     = new[] { "OnAction", Engine },                    // 第185期（層の軽減・範囲の盾は engine）
        [TraitId.Planted]     = new[] { Engine },                                // 第185期（SwapSlots の入口）
        [TraitId.Concentrate] = new[] { "OnTurnStart", "OnAction", Engine },     // 第194期（印の効果は TickStatuses と起爆）
        [TraitId.ConcentrateLeak] = Array.Empty<string>(),                       // 第194期（濃縮の中で読まれる札）
        [TraitId.Spew]        = new[] { "OnTurnStart", "OnAction" },             // 第195期（スィドの吐き）
        [TraitId.Numb]        = new[] { Engine },                                // 第195期（減少は PerformAttackBody の萎縮の直後）
        [TraitId.SpewFixed]   = new[] { "OnTurnStart", "OnAction" },             // 第196期（対照・保持者 0 枚）
        [TraitId.VenomHeavy]  = new[] { "OnDamaged" },                           // 第196期（S+反の版・保持者 0 枚）
        [TraitId.Guren]       = new[] { "OnAction", Engine },                    // 第197期（溜めるのは InverseSip）
        [TraitId.GurenStrike] = new[] { "OnAction", Engine },                    // 第197期（対照・保持者 0 枚）
        [TraitId.GurenLow]    = new[] { "OnAction", Engine },                    // 第197期（対照・保持者 0 枚）
        [TraitId.GurenFull]   = new[] { "OnAction", Engine },                    // 第197期（参考・保持者 0 枚）
        [TraitId.LastStandHold] = new[] { "OnAllyDeath", "OnDamaged", "OnBattleStart", "OnCarryOver", Engine }, // 第199期（受け流した刃は engine が書く・勝敗の1行）
        [TraitId.LastStandHoldOldScar] = new[] { "OnAllyDeath", "OnDamaged", "OnBattleStart", "OnCarryOver", Engine },
        [TraitId.LastStandHoldNoStock] = new[] { "OnAllyDeath", "OnDamaged", "OnBattleStart", "OnCarryOver", Engine },
        [TraitId.LastStandHoldMutualLoss] = new[] { "OnAllyDeath", "OnDamaged", "OnBattleStart", "OnCarryOver" },
        [TraitId.LastStandScar] = new[] { "OnAllyDeath", "OnDamaged", "OnBattleStart", "OnCarryOver" }, // 第198期（剣＋傷・累計は Guardian が書く）
        [TraitId.LastStand]   = new[] { "OnAllyDeath", "OnDamaged", "OnBattleStart", "OnCarryOver" },   // 第198期（剣・対照）
        [TraitId.LastStandPlain] = new[] { "OnAllyDeath", "OnBattleStart", "OnCarryOver" },              // 第198期（対照・保持者 0 枚）
        [TraitId.LastStandShield]= new[] { "OnAllyDeath", "OnBattleStart", "OnCarryOver" },              // 第198期（参考・保持者 0 枚）
        [TraitId.Deflect]     = new[] { "OnCarryOver", Engine },                 // 第186期（逸らしは ApplyDamage の入口）
        [TraitId.Thrust]      = new[] { "OnCarryOver", Engine },                 // 第186期 追補（列の指定と倍率は engine）
        [TraitId.ThrustPlain] = new[] { "OnCarryOver", Engine },                 // 第186期 追補（対照・保持者 0 枚）
        [TraitId.Mender]      = new[] { "OnTurnStart", "OnAction" },
        [TraitId.Amplifier]   = new[] { "OnTurnStart", "OnAction" },
        [TraitId.Contagion]   = new[] { "OnAnyDeath" },
        [TraitId.Miasma]      = new[] { "OnTurnStart", "OnAction" },
        [TraitId.Immobile]    = new[] { "CanAct", "SurrendersTurn" },
        [TraitId.Havoc]       = new[] { "OnBattleStart", Engine },             // ApplyDamage
        [TraitId.Paralyze]    = new[] { "OnAfterAttack" },
        [TraitId.Devour]      = new[] { "OnTurnStart", Engine },               // TickStatuses
        [TraitId.Rally]       = new[] { "OnBattleStart", "OnTurnStart" },
        [TraitId.Blightfed]   = new[] { "OnTurnStart" },
        [TraitId.Displaced]   = new[] { "ModifyPattern", "OnMoved", Engine },  // 型の差し替え / NoteCreak
        [TraitId.Shuffler]    = new[] { "OnTurnStart" },
        [TraitId.Bind]        = new[] { "OnBattleStart", "OnTurnStart" },
        [TraitId.Bulwark]     = new[] { "OnBattleStart", Engine },             // ApplyDamage の据え
        [TraitId.Drifter]     = new[] { "OnAllyMoved" },
        [TraitId.Perverse]    = new[] { "ModifyAttack" },
        [TraitId.Sharer]      = new[] { "OnDamaged", Engine },                 // ApplyDamage の分かち
        [TraitId.Loose]       = new[] { "OnBattleStart", "OnDamaged", Engine },   // 第106期に OnDamaged（弾き）が付いた
        [TraitId.Cower]       = new[] { "OnBattleStart", Engine },
        [TraitId.Pursuer]     = new[] { "OnTurnStart", "CanAct", "OnAnyDeath", "SurrendersTurn" },
        [TraitId.RearGuard]   = new[] { Engine },                              // 札。本体は SelectTargetChain
        [TraitId.Cinder]      = new[] { "OnAfterAttack" },
        [TraitId.Pyre]        = new[] { "ModifyAttack", "ModifyPattern" },
        [TraitId.Condemn]     = new[] { "OnDamaged" },
        [TraitId.Shatter]     = new[] { "OnDamaged" },
        [TraitId.ThornGuard]  = new[] { "OnAction", "OnDamaged", Engine },
        [TraitId.Carve]       = new[] { "OnAfterAttack" },
        [TraitId.Forsake]     = new[] { "OnBattleStart", "OnTurnStart" },
        [TraitId.Torment]     = new[] { "OnAfterAttack" },
        [TraitId.Avenge]      = new[] { "OnDamaged", "OnAllyDamaged" },
        [TraitId.Rend]        = new[] { "OnAfterAttack" },
        [TraitId.Gouge]       = new[] { "OnAfterAttack" },
        [TraitId.Sever]       = new[] { "OnAfterAttack", Engine },             // 選好（SeverTrait.Prefers）
        [TraitId.Suture]      = new[] { "OnAfterAttack", Engine },
        [TraitId.Alms]        = new[] { "OnTurnStart" },
        [TraitId.Expose]      = new[] { "OnAfterAttack" },
        [TraitId.Slander]     = new[] { "OnAfterAttack" },
        // --- プラスとマイナスが1つの動作の表と裏 ---
        [TraitId.Shove]       = new[] { "OnMoved", "OnAllyMoved" },
        [TraitId.Bear]        = new[] { Engine },                              // 札。本体は Dull
        [TraitId.Relay]       = new[] { Engine },                              // 札。本体は Dull
        [TraitId.Overbear]    = new[] { "OnTurnStart", "ModifyAttack", "OnAfterAttack" },
        [TraitId.Scale]       = new[] { "OnTurnStart", "ModifyPattern", "OnAfterAttack", "OnAllyDeath", Engine },
        [TraitId.Scapegoat]   = new[] { "OnTurnStart", "OnAfterAttack" },
        [TraitId.Divert]      = new[] { "OnTurnStart" },
        [TraitId.Goad]        = new[] { "OnTurnStart" },
        [TraitId.Finisher]    = new[] { "OnAfterAttack", Engine },             // 標の段 ＋ 倍率
        [TraitId.Favor]       = new[] { "OnTurnStart", "OnAction" },
        [TraitId.Funnel]      = new[] { Engine },                              // 札。本体は Whet / Dull
        [TraitId.Hex]         = new[] { "OnDamaged" },                        // 第96期。共有の段は engine 側
        [TraitId.Taillight]   = new[] { "OnTurnStart", "OnAction", "OnAnyDeath" },   // 第108期
        // --- 第74期に切り出したマイナス ---
        [TraitId.ThinBlade]   = new[] { "ModifyAttack", Engine },              // PerformAttack の条件版
        [TraitId.Overreach]   = new[] { "OnKill" },
        [TraitId.Await]       = new[] { "CanAct", "SurrendersTurn" },
        [TraitId.Seal]        = new[] { Engine },                              // 札。本体は SutureTrait の中
        // --- 盤面ルール ---
        [TraitId.Inversion]   = new[] { "OnBattleStart", "OnDeath", Engine },
        [TraitId.Drought]     = new[] { "OnBattleStart", "OnDeath", Engine },
        [TraitId.Yoke]        = new[] { "OnBattleStart", "OnDeath", Engine },
        [TraitId.Hush]        = new[] { "OnBattleStart", "OnDeath", Engine },
    };

    /// <summary>その駒の発火口の一覧（特性の和・重複なし）。</summary>
    public static string[] HooksOf(UnitDef d)
        => d.Traits.SelectMany(t => TraitHooks.TryGetValue(t, out string[]? h) ? h : Array.Empty<string>())
                   .Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
}

/// <summary>
/// 第78期の器具その2 —— <b>入口</b>（その駒の出力を成立させる<b>外部の供給</b>の本数）。
///
/// <para>定義は第77期に揃える（指示書 §2-1）——ヨミの <c>AtkBonus</c> は
/// <b>移動（軋み）＋ 強化（Whet）の2本</b>、<c>WhetReceived</c> は<b>強化だけの1本</b>。
/// 一般形にすると:</para>
///
/// <para><b>入口 ＝ その駒の「読み」のうち、同じ駒の「書き」では満たせないものの数。</b>
/// キーは <see cref="UnitTally.CarryKeys"/> の 11 本。<b>場所（scope）まで一致しないと打ち消せない</b>
/// ——これが第58期の「供給者は自分の撒いたものを持たない」（火の粉はボルグ自身には移らない）と
/// 第77期の「ヨミは自分では動かない」を同じ形で書いたもの。</para>
///
/// <para><b>被弾（<see cref="UnitTally.CarryHit"/>）だけは敵が供給する。</b>
/// 主の量は<b>味方側の入口</b>（＝標本ごとに在席が揺れるもの・第77期の在庫率）で数え、
/// 被弾を含めた版は別列で併記する。</para>
///
/// <para><b>死・撃破は 11 本のキーに無い</b>ので、墓守・継ぎ接ぎ・処刑・追い打ちの入口は 0 と数える
/// （表A の脚注に明記する）。<b><see cref="TraitId.Scale"/> の自給は打ち消さない</b>
/// ——鱗は自分で破片を作るが、その供給は<b>味方の死</b>という外部の事象に縛られている。</para>
/// </summary>
static class TraitEntryMap
{
    /// <summary>通貨が乗っている場所。<c>Any</c> は自分でも味方でもよい。</summary>
    public enum Where { Self, Ally, Foe, Any }

    /// <summary>その特性の出力が要求する「外から来る通貨」。</summary>
    public static readonly Dictionary<TraitId, (int Key, Where W)[]> Reads = new()
    {
        [TraitId.Rally]      = new[] { (UnitTally.CarryIdle, Where.Ally) },
        [TraitId.Bulwark]    = new[] { (UnitTally.CarryIdle, Where.Ally) },
        [TraitId.Drifter]    = new[] { (UnitTally.CarryMove, Where.Ally) },
        [TraitId.Displaced]  = new[] { (UnitTally.CarryMove, Where.Self) },
        [TraitId.Shove]      = new[] { (UnitTally.CarryMove, Where.Any) },
        [TraitId.Sniper]     = new[] { (UnitTally.CarryMove, Where.Self) },
        [TraitId.Perverse]   = new[] { (UnitTally.CarryWhet, Where.Self), (UnitTally.CarryDull, Where.Self) },
        [TraitId.Funnel]     = new[] { (UnitTally.CarryWhet, Where.Any) },
        [TraitId.Bear]       = new[] { (UnitTally.CarryDull, Where.Ally) },
        [TraitId.Relay]      = new[] { (UnitTally.CarryDull, Where.Ally) },
        // 第94期 (T2) の観測で足した分は、行末に「観測」と書いてある。
        // **走らせて観測した事実**（`derive scan`）で、手で読んで足したものは1件も無い。
        [TraitId.Divert]     = new[] { (UnitTally.CarryMark, Where.Self), (UnitTally.CarryMark, Where.Ally),
                                       (UnitTally.CarryMark, Where.Foe) },                  // 観測
        [TraitId.Goad]       = new[] { (UnitTally.CarryMark, Where.Ally) },                 // 観測
        [TraitId.Carve]      = new[] { (UnitTally.CarryWound, Where.Foe) },                 // 観測（なぞり）
        [TraitId.Bind]       = new[] { (UnitTally.CarryStun, Where.Foe),
                                       (UnitTally.CarryStun, Where.Ally) },                 // 観測（既に縛られているかを見る）
        [TraitId.Amplifier]  = new[] { (UnitTally.CarryPoison, Where.Foe), (UnitTally.CarryWound, Where.Foe) },
        // 繕いの傷読み（第92期に採用）。患者は必ず同陣営（`MostHurtAlly`）なので `Where.Ally`。
        [TraitId.Mender]     = new[] { (UnitTally.CarryWound, Where.Ally) },
        [TraitId.Devour]     = new[] { (UnitTally.CarryPoison, Where.Foe) },
        [TraitId.Contagion]  = new[] { (UnitTally.CarryPoison, Where.Foe), (UnitTally.CarryWound, Where.Any),
                                       (UnitTally.CarryPoison, Where.Ally) },               // 観測（味方の死体からも撒く）
        // 滲み則（第90期に採用）。**engine の規則なので `TraitId` は増えていない**が、
        // この4枚は定義を1文字も変えずに傷の読み手になった。
        // **`Where.Any`**——指示書 §2-4 は `Where.Foe` と書いていたが、理想61行の実測では
        // **滲みの 100% が味方側に落ちる**（`soak ideal` の Q2）ので `Foe` は事実に反する。
        [TraitId.Miasma]     = new[] { (UnitTally.CarryWound, Where.Any) },
        [TraitId.Venom]      = new[] { (UnitTally.CarryWound, Where.Any),
                                       (UnitTally.CarryPoison, Where.Foe) },                // 観測
        // **火の粉（`Cinder`）は第91期に外した**——燃焼は非スタックなので深さを足しても点け直しで消える。
        [TraitId.Blightfed]  = new[] { (UnitTally.CarryPoison, Where.Ally) },
        [TraitId.Pyre]       = new[] { (UnitTally.CarryBurn, Where.Self) },
        [TraitId.Favor]      = new[] { (UnitTally.CarryBurn, Where.Ally) },
        [TraitId.Shame]      = new[] { (UnitTally.CarryStun, Where.Foe), (UnitTally.CarryIdle, Where.Foe) },   // 第185期
        [TraitId.Torment]    = new[] { (UnitTally.CarryStun, Where.Foe),
                                       (UnitTally.CarryIdle, Where.Foe) },                  // 観測
        [TraitId.Avenge]     = new[] { (UnitTally.CarryMark, Where.Ally), (UnitTally.CarryHit, Where.Ally) },
        [TraitId.Vendetta]   = new[] { (UnitTally.CarryMark, Where.Ally), (UnitTally.CarryHit, Where.Ally) },   // 第184期
        [TraitId.Finisher]   = new[] { (UnitTally.CarryMark, Where.Foe) },
        [TraitId.Scale]      = new[] { (UnitTally.CarryArmor, Where.Self) },
        [TraitId.Gouge]      = new[] { (UnitTally.CarryWound, Where.Foe) },
        [TraitId.Sever]      = new[] { (UnitTally.CarryWound, Where.Foe) },
        // 縫いの両側読み（第85期に採用した `SutureSide.Both`）が**表に反映されていなかった**。
        // 第94期 (T2) の観測で 0.69 回/戦（味方側の糸口）が出た。
        [TraitId.Suture]     = new[] { (UnitTally.CarryWound, Where.Foe),
                                       (UnitTally.CarryWound, Where.Ally) },                // 観測
        // 処刑（Executioner）は撃破を読むが撃破は 11 本のキーに無いので入口 0、支援拒否（Stoic）は読みを持たない。
        // **第79期の候補駒（オノ）はこの2枚だけなので、この表には載らない＝入口 0 が正しい答え**（第78期 (b) の注意）。
        // 被弾（敵が供給する）
        [TraitId.Rage]       = new[] { (UnitTally.CarryHit, Where.Self) },
        [TraitId.Thorns]     = new[] { (UnitTally.CarryHit, Where.Self) },
        [TraitId.LastStandHold] = new[] { (UnitTally.CarryHit, Where.Self) },               // 第199期（規定の斬り返し）
        [TraitId.LastStandScar] = new[] { (UnitTally.CarryHit, Where.Self) },               // 第198期（剣＋傷の斬り返し）
        [TraitId.LastStand]  = new[] { (UnitTally.CarryHit, Where.Self) },                  // 第198期（剣の段の斬り返し）
        [TraitId.Shatter]    = new[] { (UnitTally.CarryHit, Where.Self),
                                       (UnitTally.CarryArmor, Where.Ally) },                // 観測
        [TraitId.Condemn]    = new[] { (UnitTally.CarryHit, Where.Self) },
        [TraitId.Frail]      = new[] { (UnitTally.CarryHit, Where.Self) },
        // 傷の引き取り（第90期に採用）は**供給ではなく中継**だった（第94期 (T2)。下の `Supplies` の注）。
        // 引き取りは「隣の味方に傷があること」を要求するので、**読みの側にだけ立つ**。
        [TraitId.Guardian]   = new[] { (UnitTally.CarryHit, Where.Ally),
                                       (UnitTally.CarryWound, Where.Ally) },                // 観測
        [TraitId.Martyr]     = new[] { (UnitTally.CarryHit, Where.Ally) },
        [TraitId.RearGuard]  = new[] { (UnitTally.CarryHit, Where.Ally) },
        [TraitId.ThornGuard] = new[] { (UnitTally.CarryHit, Where.Ally) },
        [TraitId.Colossus]   = new[] { (UnitTally.CarryHit, Where.Ally) },
        [TraitId.Sharer]     = new[] { (UnitTally.CarryHit, Where.Ally) },
        // 業（`All` に載っていない棄却駒。参考のために置いてある）
        [TraitId.Scapegoat]  = new[] { (UnitTally.CarryPoison, Where.Ally), (UnitTally.CarryStun, Where.Ally),
                                       (UnitTally.CarryMark, Where.Ally), (UnitTally.CarryBurn, Where.Ally) },
    };

    /// <summary>
    /// その特性が供給できる通貨と、その置き場所。
    ///
    /// <para><b>鱗（<see cref="TraitId.Scale"/>）だけは載せていない</b>（上の doc）。
    /// 第94期 (T2) の観測は <c>(破片, 自分) 0.14 回/戦</c> を出すが、<b>これは意図的な除外である</b>
    /// ——鱗は自分で破片を作るが、その供給は<b>味方の死</b>という外部の事象に縛られているので、
    /// 自分の読み <c>(破片, 自分)</c> を打ち消させない。<b>載せると入口が 1 → 0 になり、
    /// 第78期の「入口」の定義が変わる。</b> ここだけは観測より doc の側を採る。</para>
    ///
    /// <para><b>それ以外の欠落 13 件・過剰 1 件は、第94期 (T2) の観測どおりに直した</b>
    /// （行末に「観測」と書いてある）。</para>
    /// </summary>
    public static readonly Dictionary<TraitId, (int Key, Where W)[]> Supplies = new()
    {
        [TraitId.Carve]      = new[] { (UnitTally.CarryWound, Where.Foe) },
        [TraitId.Rend]       = new[] { (UnitTally.CarryWound, Where.Foe) },
        // **傷の引き取り（`GatherRule`）はここから外した**（第94期 (T2)）。
        // ガルドは隣の味方から傷を「移す」だけで**盤面の総量を1つも増やさない**
        // ——`derive scan` の実測で **増 0.31 ／ 減 0.31 ／ 純増 0.00 回/戦**。
        // 第92期が「ガルドの傷は中継であって供給ではない」と書いて別の期に送った1件で、
        // **走らせた観測がそのまま同じ答えを出した**（Q1）。
        // 要求の側（隣に傷があること）は `Reads` に立ててある。
        [TraitId.Venom]      = new[] { (UnitTally.CarryPoison, Where.Foe),
                                       (UnitTally.CarryPoison, Where.Ally) },               // 観測（毒撃の隣への漏れ）
        [TraitId.Miasma]     = new[] { (UnitTally.CarryPoison, Where.Foe), (UnitTally.CarryPoison, Where.Ally),
                                       (UnitTally.CarryPoison, Where.Self) },               // 観測
        // **疫み（ラウ）と澱み（ミオ）が載っていなかった**（第92期が見つけて別の期に送った2件）。
        // 疫みは死体の毒を撒き直し、澱みは第89期の `IgniteRule` で
        // 「傷を持ち毒を持たない敵」に毒 1 を置く。**どちらも敵に毒を置いている。**
        [TraitId.Contagion]  = new[] { (UnitTally.CarryPoison, Where.Foe) },                // 観測
        [TraitId.Amplifier]  = new[] { (UnitTally.CarryPoison, Where.Foe) },                // 観測
        // 火の粉は自分には移らない（第58期）。だから熾火の (燃, Self) を打ち消さない。
        [TraitId.Cinder]     = new[] { (UnitTally.CarryBurn, Where.Foe), (UnitTally.CarryBurn, Where.Ally) },
        [TraitId.Bomber]     = new[] { (UnitTally.CarryBurn, Where.Foe), (UnitTally.CarryBurn, Where.Ally) },
        [TraitId.Paralyze]   = new[] { (UnitTally.CarryStun, Where.Foe) },
        [TraitId.Grapple]    = new[] { (UnitTally.CarryStun, Where.Foe) },                  // 第185期（開戦時の大縛り）
        [TraitId.Torment]    = new[] { (UnitTally.CarryStun, Where.Self) },
        [TraitId.Avenge]     = new[] { (UnitTally.CarryStun, Where.Self) },                 // 観測（ターン外の行動の代金）
        [TraitId.Condemn]    = new[] { (UnitTally.CarryStun, Where.Foe) },                  // 観測（敵側の断罪）
        [TraitId.Marker]     = new[] { (UnitTally.CarryMark, Where.Ally) },
        [TraitId.Beckon]     = new[] { (UnitTally.CarryMark, Where.Ally) },                 // 第184期
        [TraitId.Vendetta]   = new[] { (UnitTally.CarryMark, Where.Foe) },                  // 第184期（殴った敵に標）
        [TraitId.Flee]       = new[] { (UnitTally.CarryMove, Where.Ally) },                 // 第184期（入れ替え）
        [TraitId.Goad]       = new[] { (UnitTally.CarryMark, Where.Ally), (UnitTally.CarryWhet, Where.Ally) },
        [TraitId.Divert]     = new[] { (UnitTally.CarryMark, Where.Foe), (UnitTally.CarryMark, Where.Self) },
        [TraitId.Rally]      = new[] { (UnitTally.CarryWhet, Where.Ally),
                                       (UnitTally.CarryWhet, Where.Self) },                 // 観測（号令は自分にも乗る）
        // **大縛り（`BindEnemy`）が載っていなかった**——開戦時に最速の敵を確定で縛る。
        [TraitId.Bind]       = new[] { (UnitTally.CarryWhet, Where.Ally), (UnitTally.CarryStun, Where.Ally),
                                       (UnitTally.CarryStun, Where.Foe) },                  // 観測
        [TraitId.Drifter]    = new[] { (UnitTally.CarryWhet, Where.Ally) },
        // 第108期の尾灯。**灯は必ず味方1体**（自分は対象外）。
        // 消灯は総量を減らすが `Dull` を通さないので「中継」の判定（増と減の両方を書く）には
        // かからない——`derive scan` の中継判定は `NoteCarry` / `SetCounter` の観測から作るので、
        // <c>AtkBonus</c> を直に引く消灯はどちらの側にも現れない。
        [TraitId.Taillight]  = new[] { (UnitTally.CarryWhet, Where.Ally) },
        [TraitId.Favor]      = new[] { (UnitTally.CarryWhet, Where.Ally), (UnitTally.CarryDull, Where.Ally) },
        [TraitId.Colossus]   = new[] { (UnitTally.CarryWhet, Where.Ally),
                                       (UnitTally.CarryWhet, Where.Self) },                 // 観測（吐き戻しは壁自身にも返る）
        [TraitId.Funnel]     = new[] { (UnitTally.CarryWhet, Where.Ally) },
        [TraitId.Curse]      = new[] { (UnitTally.CarryDull, Where.Foe), (UnitTally.CarryDull, Where.Ally),
                                       (UnitTally.CarryDull, Where.Self) },                 // 観測
        [TraitId.Cower]      = new[] { (UnitTally.CarryDull, Where.Ally),
                                       (UnitTally.CarryDull, Where.Self) },                 // 観測
        [TraitId.Relay]      = new[] { (UnitTally.CarryDull, Where.Foe) },                  // 観測（転嫁の流し先）
        [TraitId.Sharer]     = new[] { (UnitTally.CarryDull, Where.Ally) },
        [TraitId.Slander]    = new[] { (UnitTally.CarryDull, Where.Foe) },
        [TraitId.Shove]      = new[] { (UnitTally.CarryDull, Where.Ally), (UnitTally.CarryMove, Where.Foe) },
        [TraitId.Bear]       = new[] { (UnitTally.CarryArmor, Where.Self) },
        [TraitId.Shatter]    = new[] { (UnitTally.CarryArmor, Where.Ally) },
        [TraitId.Shuffler]   = new[] { (UnitTally.CarryMove, Where.Ally) },
        [TraitId.Coward]     = new[] { (UnitTally.CarryMove, Where.Self),
                                       (UnitTally.CarryMove, Where.Ally) },                 // 観測（押しのけた側も動く）
        [TraitId.ThornGuard] = new[] { (UnitTally.CarryMove, Where.Self), (UnitTally.CarryMove, Where.Ally) },
        // 第106期。弾かれた側と、席を明け渡した側の両方が動く（`SwapSlots` が2体を通知する）。
        [TraitId.Loose]      = new[] { (UnitTally.CarryMove, Where.Ally) },
        [TraitId.Expose]     = new[] { (UnitTally.CarryMove, Where.Foe) },
        // のろまは SurrendersTurn を偽にしないので、捨てた手番が号令・据えに売れる。
        // 不動・刃待ち・追い打ちは偽にするので供給しない（第74期の警告）。
        [TraitId.Sluggish]   = new[] { (UnitTally.CarryIdle, Where.Self) },
    };

    static bool Covers(Where supply, Where read)
        => supply == read || supply == Where.Any || read == Where.Any;

    /// <summary>その駒の入口（キーの一覧・重複なし）。<paramref name="withFoe"/> が偽なら被弾を外す。</summary>
    public static int[] EntriesOf(UnitDef d, bool withFoe)
    {
        var sup = d.Traits.SelectMany(t => Supplies.TryGetValue(t, out var s) ? s : Array.Empty<(int Key, Where W)>())
                          .ToArray();
        var need = new List<int>();
        foreach (TraitId t in d.Traits)
        {
            if (!Reads.TryGetValue(t, out var rs)) continue;
            foreach ((int key, Where w) in rs)
            {
                if (!withFoe && key == UnitTally.CarryHit) continue;
                if (sup.Any(x => x.Key == key && Covers(x.W, w))) continue;
                need.Add(key);
            }
        }
        return need.Distinct().OrderBy(x => x).ToArray();
    }
}

/// <summary>第87期 `blaze2` の集計（診断専用。盤面には一切影響しない）。</summary>
sealed class Bz2Stat
{
    public double N, Wins, Turns;
    public double AmpFires, AmpThickened, AmpIgnitable, AmpIgnitableBodies, AmpIgnited, AmpIgniteAmount;
    public double AmpIgniteWoundBefore, AmpIgniteWoundAfter, AmpIgnitePoisonAfter;
    public double FirstIgnitableSum, FirstIgnitableN, FirstIgniteSum, FirstIgniteN;
    public double IgnitePoison, IgniteTicks, IgniteLogs, FoeAmpIgnited;
    public double PaperRaw, PaperCapped, PaperBodies, PaperTurns;       // 紙（§1-1。Y0 の Events から組む）
    public double FoeTaken, AllyTaken, CarryWoundFoe, CarryPoisonFoe;
    public double GougeFires, GougeOut, SutureFires, SutureHealed, MendFires, MendHealed;   // 持続係数の検算（第84〜86期）
    public double BeniHealed, RauSpread;                                // 下流の読み手（Q4）
    public void AddFrom(Bz2Stat o)
    {
        N += o.N; Wins += o.Wins; Turns += o.Turns;
        AmpFires += o.AmpFires; AmpThickened += o.AmpThickened; AmpIgnitable += o.AmpIgnitable;
        AmpIgnitableBodies += o.AmpIgnitableBodies; AmpIgnited += o.AmpIgnited; AmpIgniteAmount += o.AmpIgniteAmount;
        AmpIgniteWoundBefore += o.AmpIgniteWoundBefore; AmpIgniteWoundAfter += o.AmpIgniteWoundAfter;
        AmpIgnitePoisonAfter += o.AmpIgnitePoisonAfter;
        FirstIgnitableSum += o.FirstIgnitableSum; FirstIgnitableN += o.FirstIgnitableN;
        FirstIgniteSum += o.FirstIgniteSum; FirstIgniteN += o.FirstIgniteN;
        IgnitePoison += o.IgnitePoison; IgniteTicks += o.IgniteTicks; IgniteLogs += o.IgniteLogs; FoeAmpIgnited += o.FoeAmpIgnited;
        PaperRaw += o.PaperRaw; PaperCapped += o.PaperCapped; PaperBodies += o.PaperBodies; PaperTurns += o.PaperTurns;
        FoeTaken += o.FoeTaken; AllyTaken += o.AllyTaken; CarryWoundFoe += o.CarryWoundFoe; CarryPoisonFoe += o.CarryPoisonFoe;
        GougeFires += o.GougeFires; GougeOut += o.GougeOut;
        SutureFires += o.SutureFires; SutureHealed += o.SutureHealed;
        MendFires += o.MendFires; MendHealed += o.MendHealed;
        BeniHealed += o.BeniHealed; RauSpread += o.RauSpread;
    }
}

/// <summary>第86期 `mender` の集計（診断専用。盤面には一切影響しない）。</summary>
sealed class MdStat
{
    public double N, Wins, Turns;
    public double SpillWounds, SpillHits;                        // 巻き込み則の書き込み（全）／味方の刃の着弾（生存・Events）
    public double[] SpillByWriter = new double[6];               // 巻き込み則の書き手別（mdWriterIds の並び）
    public double StockAlly, StockAllyMax, StockFoe, AllyWoundTurns;
    public double MendFires, MendSeen, MendDepth, MendDry, MendHealed, MendPaid, MendFoePatient;
    public double NonoLife, NonoDeaths, GaldPatient;
    public double FoeMendFires, FoeMendSeen;
    public double TeamHealed, TeamTaken;
    public double CarryWoundAlly, CarryWoundFoe, DeathWoundAlly, DeathWoundFoe, EndWoundAlly, EndWoundFoe;
    public void AddFrom(MdStat o)
    {
        N += o.N; Wins += o.Wins; Turns += o.Turns;
        SpillWounds += o.SpillWounds; SpillHits += o.SpillHits;
        for (int k = 0; k < SpillByWriter.Length; k++) SpillByWriter[k] += o.SpillByWriter[k];
        StockAlly += o.StockAlly; StockAllyMax += o.StockAllyMax; StockFoe += o.StockFoe; AllyWoundTurns += o.AllyWoundTurns;
        MendFires += o.MendFires; MendSeen += o.MendSeen; MendDepth += o.MendDepth; MendDry += o.MendDry;
        MendHealed += o.MendHealed; MendPaid += o.MendPaid; MendFoePatient += o.MendFoePatient;
        NonoLife += o.NonoLife; NonoDeaths += o.NonoDeaths; GaldPatient += o.GaldPatient;
        FoeMendFires += o.FoeMendFires; FoeMendSeen += o.FoeMendSeen;
        TeamHealed += o.TeamHealed; TeamTaken += o.TeamTaken;
        CarryWoundAlly += o.CarryWoundAlly; CarryWoundFoe += o.CarryWoundFoe;
        DeathWoundAlly += o.DeathWoundAlly; DeathWoundFoe += o.DeathWoundFoe;
        EndWoundAlly += o.EndWoundAlly; EndWoundFoe += o.EndWoundFoe;
    }
}

sealed class SuStat
{
    public double N, Wins, Turns;
    public double KadoFires, KadoAdjacent;                       // 棘の発火（Log）／カドの隣接数（開戦時の席。1戦ごとに加算）
    public double ThornWoundAlly, ThornWoundFoe;                 // 棘が傷を書いた回数（味方／敵。W1 の供給）
    public double SpillWounds, SpillHits, KadoSpill;             // 巻き込み則の書き込み（全）／味方の刃の着弾（生存・Events）／カドが巻き込み則で書いた回数
    public double[] SpillByWriter = new double[6];               // 巻き込み則の書き手別（suWriterIds の並び）
    public double StockAlly, StockAllyMax, StockFoe, StockFoeMax, AllyWoundTurns;   // 在庫（ターン平均／最大）と味方側に傷があったターン数
    public double SutureFoe, SutureAlly, SutureDry, SutureHealed, HariAttacks, HariDeaths, KadoDeaths;
    public double TeamHealed, TeamTaken;
    public double CarryWoundAlly, CarryWoundFoe, DeathWoundAlly, DeathWoundFoe, EndWoundAlly, EndWoundFoe;
    public double Sever, SeverWounds, SeverReached;
    public void AddFrom(SuStat o)
    {
        N += o.N; Wins += o.Wins; Turns += o.Turns;
        KadoFires += o.KadoFires; KadoAdjacent += o.KadoAdjacent;
        ThornWoundAlly += o.ThornWoundAlly; ThornWoundFoe += o.ThornWoundFoe;
        SpillWounds += o.SpillWounds; SpillHits += o.SpillHits; KadoSpill += o.KadoSpill;
        for (int k = 0; k < SpillByWriter.Length; k++) SpillByWriter[k] += o.SpillByWriter[k];
        StockAlly += o.StockAlly; StockAllyMax += o.StockAllyMax; StockFoe += o.StockFoe; StockFoeMax += o.StockFoeMax; AllyWoundTurns += o.AllyWoundTurns;
        SutureFoe += o.SutureFoe; SutureAlly += o.SutureAlly; SutureDry += o.SutureDry; SutureHealed += o.SutureHealed; HariAttacks += o.HariAttacks; HariDeaths += o.HariDeaths; KadoDeaths += o.KadoDeaths;
        TeamHealed += o.TeamHealed; TeamTaken += o.TeamTaken;
        CarryWoundAlly += o.CarryWoundAlly; CarryWoundFoe += o.CarryWoundFoe; DeathWoundAlly += o.DeathWoundAlly; DeathWoundFoe += o.DeathWoundFoe; EndWoundAlly += o.EndWoundAlly; EndWoundFoe += o.EndWoundFoe;
        Sever += o.Sever; SeverWounds += o.SeverWounds; SeverReached += o.SeverReached;
    }
}

/// <summary>
/// 灯の対象選択（第113期・<c>lit</c>）の集計。<b>どの列も盤面には一切影響しない</b>
/// ——<c>BattleResult</c> の計数を読み直しているだけ。
/// </summary>
sealed class LtStat
{
    public int N, Wins;
    public double Turns;
    public double Fires, Switches, Yields, Stalls, Gate2, Doused, Lumen, Peak, Idle;
    public double StallStun, StallSlumber, StallCanAct;
    public double YieldDmg, TeamDmg, NoDeath, NoTarget, Saw2;
    public double SkipStatic, SkipNow;
    /// <summary>灯の受け手（駒名 → 灯した回数）。</summary>
    public Dictionary<string, int> LitBy = new(StringComparer.Ordinal);
    /// <summary>濾された駒（駒名 → 回数）。<c>SkipStaticBy</c> は W1 の静的な濾し、<c>SkipNowBy</c> は W2。</summary>
    public Dictionary<string, int> SkipStaticBy = new(StringComparer.Ordinal);
    public Dictionary<string, int> SkipNowBy = new(StringComparer.Ordinal);

    public double Win => N == 0 ? 0 : Wins * 100.0 / N;
    public double Per(double x) => N == 0 ? 0 : x / N;

    public static void Bump(Dictionary<string, int> bag, string key, int n)
        => bag[key] = bag.TryGetValue(key, out int had) ? had + n : n;

    public void AddFrom(LtStat o)
    {
        N += o.N; Wins += o.Wins; Turns += o.Turns;
        Fires += o.Fires; Switches += o.Switches; Yields += o.Yields; Stalls += o.Stalls;
        Gate2 += o.Gate2; Doused += o.Doused; Lumen += o.Lumen; Peak += o.Peak; Idle += o.Idle;
        StallStun += o.StallStun; StallSlumber += o.StallSlumber; StallCanAct += o.StallCanAct;
        YieldDmg += o.YieldDmg; TeamDmg += o.TeamDmg;
        NoDeath += o.NoDeath; NoTarget += o.NoTarget; Saw2 += o.Saw2;
        SkipStatic += o.SkipStatic; SkipNow += o.SkipNow;
        foreach (var kv in o.LitBy) Bump(LitBy, kv.Key, kv.Value);
        foreach (var kv in o.SkipStaticBy) Bump(SkipStaticBy, kv.Key, kv.Value);
        foreach (var kv in o.SkipNowBy) Bump(SkipNowBy, kv.Key, kv.Value);
    }
}

/// <summary>
/// 積み過ぎ（第115期・<c>reader</c>）の1戦ぶんの観測。<b>どの列も盤面には一切影響しない</b>
/// ——<c>BattleResult</c> の計数を読み直しているだけ。
/// </summary>
sealed class RdRow
{
    public int Bench, Ver, Wave, Turns;
    public bool Won;
    /// <summary>読み手が生きていたターン数（門1 の分母）と、閾値以上だったターン数（分子）。</summary>
    public int Alive, Over, FirstOver;
    /// <summary><c>PerformAttack</c> を通った総回数 ／ そのうち薙ぎ（門2）／ 閾値以上で振ったターン数。</summary>
    public int Swings, Sweeps, OverSwung;
    public int BonusSum, BonusMax;
    public int[] Probe = System.Array.Empty<int>();
    public int Dmg, TeamDmg, Whet;
}

/// <summary>
/// ボスの土台（第117期・<c>boss</c>）の1戦ぶんの観測。<b>どの列も盤面には一切影響しない</b>
/// ——<c>BattleResult</c> の計数（<c>BossRule</c>）を読み直しているだけ。
/// </summary>
sealed class BsRow
{
    /// <summary>決着ターン（<c>BattleResult.Turns</c>）と勝敗。</summary>
    public int Turns;
    public bool Won;
    /// <summary>味方が最後にターン頭で生きていたターン（＝生存T）。</summary>
    public int PartyAlive;
    /// <summary>育つ側の生きていたターン数 ／ 1度でも振ったターン数（<b>空振り = 差</b>）。</summary>
    public int GrowAlive, GrowSwing, GrowLastAlive, GrowDmg;
    /// <summary>育つ側の <c>CurrentAttack</c>（開戦・前半末・終端・最大）。<b>生の <c>AtkBonus</c> ではない。</b></summary>
    public int Atk1, Atk3, AtkEnd, AtkMax;
    /// <summary>味方の与ダメ（前半3T ／ 後半3T ／ 総計）。</summary>
    public int DmgEarly, DmgLate, DmgTotal;
    /// <summary>
    /// <b>育つ側だけ</b>の与ダメ（前半3T ／ 後半3T）。味方全体の傾きには
    /// 「他の駒が倒れた」が混ざるので、育ちの本人だけを切り出した分子を別に持つ。
    /// </summary>
    public int GrowEarly, GrowLate;
    /// <summary>前半と後半が重ならない戦か（<c>Turns &gt;= 6</c>）。<b>傾きの分母はこれが真の戦だけ。</b></summary>
    public bool Ok;
}

/// <summary>
/// 第125期 —— `offturn` の走査子。<b>実装から引く表の共通部分</b>を1箇所に置く。
///
/// <para>メソッド1本ぶんの本文を<b>波括弧を数えて</b>切り出す（行番号では切らない・規約 (G15)）。
/// <b>走査が空なら呼び出し側が止める。</b></para>
/// </summary>
static class OffturnScan
{
    /// <summary>
    /// <b>第124期の観察ログとまったく同じ6行・同じ波・同じ seed</b>
    /// （<c>design/PHASE124_WATCH_LOG.md</c> の見出しから採った。第123期とも同一）。
    /// <c>Stage</c> は 0 始まり（第1波 = 0）。<b>行名は `Presets` と完全一致で照合する。</b>
    /// </summary>
    public static readonly (string Name, int Stage, int Seed)[] WatchRows =
    {
        ("死軸×ヒヨ (ゾト×火選り)",   3, 1),
        ("隊列崩し (バサ×ヨミ×セロ)", 1, 3),
        ("置き去り×分散回復",         1, 0),
        ("逆しま (ネル×ウツ)",        4, 15),
        ("止め改 (トメ×薙ぎ)",        3, 6),
        ("刻み×抉り (ノミ×エグ)",     4, 0),
    };

    /// <summary>署名から始まるメソッド1本ぶんの本文（波括弧を数える）。引けなければ空文字。</summary>
    public static string Body(string src, string signature)
    {
        int a = src.IndexOf(signature, StringComparison.Ordinal);
        if (a < 0) return "";
        int b = src.IndexOf('{', a);
        if (b < 0) return "";
        int depth = 0;
        for (int i = b; i < src.Length; i++)
        {
            if (src[i] == '{') depth++;
            else if (src[i] == '}' && --depth == 0) return src.Substring(b, i - b + 1);
        }
        return "";
    }

    /// <summary>その位置を含む <c>class Xxx : Trait</c> の名前（直前の宣言）。</summary>
    public static string EnclosingTrait(string src, int at)
    {
        string name = "—";
        foreach (System.Text.RegularExpressions.Match m in
                 System.Text.RegularExpressions.Regex.Matches(src, @"class\s+(\w+)\s*:\s*Trait"))
        {
            if (m.Index > at) break;
            name = m.Groups[1].Value;
        }
        return name;
    }

    /// <summary>
    /// <c>DemoApp/Main.cs</c> の <c>ApplyEvent</c> から、出来事の種類ごとの間（秒・速度 ×1）を引く。
    /// <b>条件付きの間も上から数える</b>——旧版と新版を<b>同じ規則</b>で数えるので比較は等質になる。
    /// <c>raw: true</c> の待ち（一時停止のポーリング）は <c>ApplyEvent</c> の外なので入らない。
    /// </summary>
    /// <b>無条件の間（<c>Map</c>）と条件付きの間（<c>Cond</c>）を分ける。</b>
    /// 条件付きは「その種類の出来事1件あたり必ず掛かる時間」ではないので、
    /// <b>総尺の重み付けに使うと上限しか出せない</b>——旧版にも新版にもあるので、
    /// <b>同じ規則で分けて、総尺は無条件のぶんだけで出す</b>（条件付きは件数を別に書く）。
    /// 判定は「その文（直前の <c>;</c> / <c>{</c> / <c>}</c> から <c>Delay</c> まで）に
    /// <c>if (</c> が含まれるか」。
    public static (Dictionary<string, double> Map, Dictionary<string, double> Cond, int Sites, int CondSites)
        Beats(string demoSrc)
    {
        var map = new Dictionary<string, double>(StringComparer.Ordinal);
        var cond = new Dictionary<string, double>(StringComparer.Ordinal);
        int sites = 0, condSites = 0;
        int a = demoSrc.IndexOf("private async Task " + "ApplyEvent", StringComparison.Ordinal);
        if (a < 0) return (map, cond, 0, 0);
        int b = demoSrc.IndexOf('{', a);
        if (b < 0) return (map, cond, 0, 0);
        int depth = 0, end = demoSrc.Length;
        for (int i = b; i < demoSrc.Length; i++)
        {
            if (demoSrc[i] == '{') depth++;
            else if (demoSrc[i] == '}' && --depth == 0) { end = i; break; }
        }
        string body = demoSrc.Substring(b, end - b);
        var marks = System.Text.RegularExpressions.Regex
            .Matches(body, @"case BattleEventKind\.(\w+)").ToList();
        for (int i = 0; i < marks.Count; i++)
        {
            int from = marks[i].Index;
            int to = i + 1 < marks.Count ? marks[i + 1].Index : body.Length;
            string span = body.Substring(from, to - from);
            double sum = 0, guarded = 0;
            foreach (System.Text.RegularExpressions.Match d in System.Text.RegularExpressions.Regex
                     .Matches(span, @"Delay\(\s*([0-9]*\.?[0-9]+)\s*\)"))
            {
                double v = double.Parse(d.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                int st = span.LastIndexOfAny(new[] { ';', '{', '}' }, d.Index);
                string stmt = span.Substring(st + 1, d.Index - st - 1);
                if (stmt.Contains("if (", StringComparison.Ordinal)) { guarded += v; condSites++; }
                else { sum += v; sites++; }
            }
            string kind = marks[i].Groups[1].Value;
            map[kind] = map.GetValueOrDefault(kind) + sum;
            cond[kind] = cond.GetValueOrDefault(kind) + guarded;
        }
        return (map, cond, sites, condSites);
    }
}
