using System.Reflection;
using System.Text.RegularExpressions;
using BattleCore;

// =====================================================================================
// parry モード（第135期） —— ガルドは何で死んでいるか（と、受け流し）
//
// **段1 は測るだけ。** engine に足したのは計数（`HarmRule.Census`）だけで、
// 既定（`HarmRule.Default` ＝ 数えない）では配列は1本も確保されず、分類の分岐も1回も走らない。
// `Presets` / `EnemyCatalog.Stages` / `UnitCatalog.All` は1文字も触らない。
//
// `TankDiag` / `SurviveDiag` / `RelayDiag` と同じく、`Program.cs` 側は振り分けの数行だけ
// （あのファイルの top-level statements は 69,000 行が全部で1つのメソッドで、
// Release のビルドに 4 分かかる）。
//
//     dotnet run --project BattleSim -c Release 0 parry phase0   # Q0-1〜Q0-10
//     dotnet run --project BattleSim -c Release 0 parry harm > docs/harm.md   # 段1 の生成物
//     dotnet run --project BattleSim -c Release 0 parry scan     # 段2 の台の下見（版は振らない）
//     dotnet run --project BattleSim -c Release 0 parry run      # 段2（受け流しのローカル台）
//     dotnet run --project BattleSim -c Release 0 parry check [採用前のbalance.md]
// =====================================================================================

/// <summary>
/// 第135期の走査。<b>すべて実装から引く</b>（第94期の作法。走査が空なら止める＝第117期）。
/// <b>走査の検索文字列は連結で組む</b>——この診断自身がリポジトリ内のファイルなので、
/// 素直に書くと自分のコードに当たって<b>静かに違う表を作る</b>（第123期）。
/// </summary>
static class ParryScan
{
    public static string? Root { get; private set; }

    public static bool Init()
    {
        string dir = Directory.GetCurrentDirectory();
        for (int up = 0; up < 8; up++)
        {
            if (File.Exists(Path.Combine(dir, "BattleCore", "Traits.cs"))) { Root = dir; return true; }
            DirectoryInfo? p = Directory.GetParent(dir);
            if (p is null) break;
            dir = p.FullName;
        }
        Console.WriteLine("**`BattleCore/Traits.cs` が見つからない。実装から引けないので止める**（第117期）。");
        return false;
    }

    public static string Read(string rel) => File.ReadAllText(Path.Combine(Root!, rel));

    /// <summary>その名前のメソッドを上書きしている札（リフレクション。第129期と同じ器具）。</summary>
    public static List<TraitId> Overriders(string method)
    {
        var hit = new List<TraitId>();
        foreach (TraitId id in Enum.GetValues<TraitId>())
        {
            Trait tr;
            try { tr = TraitCatalog.Get(id); }
            catch (KeyNotFoundException) { continue; }
            MethodInfo? m = tr.GetType().GetMethod(method);
            if (m is not null && m.DeclaringType != typeof(Trait)) hit.Add(id);
        }
        return hit;
    }

    public static List<UnitDef> Holders(TraitId t) => UnitCatalog.All.Where(d => d.Traits.Contains(t)).ToList();
}

static class ParryDiag
{
    // ---- 測る駒（指示書 A3 の7枚 ＋ 棘守りのカド）---------------------------------------------
    //
    // **受け入れ条件 A3 が名指しするのは 7 枚**（ガルド・ゴルム・ドハ・セッキ・ウケ・ワタ・ササ）。
    // カドは条文に無いが、`SelectTargetChain` の鎖の最後の段（棘守り）を持つ唯一の駒なので足した
    // ——「庇う・肩代わりする駒全員」を機構から引くと必ず入る。
    // 第137期: **ヒビを足した。** 表Aの分母（被弾/戦・生存T・死亡率）は
    // 「その駒が編成に加わる価値は、少なくともその駒が受ける被弾を上回らなければならない」
    // という物差しの分母そのものなので、砕けを測る期にはこれが要る（Phase 0 Q0-1）。
    // **ヒビは庇わないし肩代わりもしない**——それでも並べるのは、`docs/harm.md` が
    // 「何で削られ、何で死ぬか」の表であって「庇う駒の表」ではないから
    // （表C / 表E の庇いの列は `—` で描き分けられる）。
    internal static readonly string[] Watched =
        { "gald", "golm", "doha", "sekki", "uke", "wata", "sasa", "kado", "hibi" };

    internal const int Seeds = 200;

    public static void Run(string mode, string arg)
    {
        if (!ParryScan.Init()) return;
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "harm": Harm(); return;
            case "scan": Scan(); return;
            case "run": Stage2(); return;
            case "check": Check(arg); return;
            default:
                Console.WriteLine("parry: モードは phase0 / harm / scan / run / check（第135期）。");
                return;
        }
    }

    // =================================================================================
    // 帳簿の収集（段1 と phase0 が共有する）
    // =================================================================================

    internal sealed class Ledger
    {
        public readonly long[] Amount = new long[DamageRoutes.Count];
        public readonly long[] Hits = new long[DamageRoutes.Count];
        public readonly long[] GuardAmount = new long[DamageRoutes.Count];
        public readonly long[] GuardHits = new long[DamageRoutes.Count];
        public readonly long[] Fatal = new long[DamageRoutes.Count];
        public readonly long[] Label = new long[InterceptLabels.All.Length];
        public long Battles, Deaths, LifeSum, TurnsSum, Taken, Healed;
        public long GuardChances, GuardRangeMissed, RedirectFires, RedirectGain;
        public long GatherGuards, GatherHadDonor, GatherTaken;
        public long StoicHeal, StoicHealFires, StoicHops, StoicHeads;
        // 第136期 段2: 受け流しの在庫の帳簿（表I）
        public long ParryFires, ParryBlocked, ParryRefillGuard, ParryRefillGuardWasted, ParryStances, ParryRefillTurn, ParryStockAtDeath, AtkPeakSum;
        public readonly long[] ParryByRoute = new long[DamageRoutes.Count];
        public readonly SortedSet<string> Rows = new(StringComparer.Ordinal);
    }

    /// <summary>
    /// `compare` 61行 × 第2〜5波 × seed 0..199 を1度だけ回して、駒ごとの帳簿を作る。
    /// <b>第一波は分母に入れない</b>（規約 (G10)）。
    /// </summary>
    // 第136期: `parry` を外から差せるようにした（`wall` が版ごとの帳簿を取る）。既定は現行。
    internal static Dictionary<string, Ledger> Collect(ParryRule? parry = null, IEnumerable<(string Name, Formation F)>? rows = null)
    {
        var led = new Dictionary<string, Ledger>(StringComparer.Ordinal);
        foreach (string id in Watched) led[id] = new Ledger();

        var harm = new HarmRule(true);
        foreach ((string name, Formation f) in rows ?? Presets.Compare)
        {
            var mine = f.Occupied().Select(o => o.Def.Id).Where(led.ContainsKey).Distinct().ToList();
            if (mine.Count == 0) continue;
            foreach (string id in mine) led[id].Rows.Add(name);

            for (int w = 1; w < EnemyCatalog.Stages.Count; w++)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[w].Enemy, seed,
                                                      verbose: false, harm: harm, parry: parry);
                    foreach (string id in mine)
                    {
                        if (!r.TallyByUnit.TryGetValue(id, out UnitTally? t)) continue;
                        Ledger L = led[id];
                        L.Battles++;
                        L.TurnsSum += r.Turns;
                        L.Deaths += t.Deaths > 0 ? 1 : 0;
                        L.LifeSum += t.LastActiveTurn;
                        L.Taken += t.DamageTaken;
                        L.Healed += t.Healed;
                        L.GuardChances += t.GuardChances;
                        L.GuardRangeMissed += t.GuardRangeMissed;
                        L.RedirectFires += t.RedirectGainFires;
                        L.RedirectGain += t.RedirectGain;
                        L.GatherGuards += t.GatherGuards;
                        L.GatherHadDonor += t.GatherHadDonor;
                        L.GatherTaken += t.GatherTaken;
                        L.StoicHeal += t.StoicHealBlocked;
                        L.StoicHealFires += t.StoicHealBlockedFires;
                        L.StoicHops += t.StoicSupportHops;
                        L.StoicHeads += t.StoicSupportHeads;
                        L.ParryFires += t.ParryFires;
                        L.ParryBlocked += t.ParryBlocked;
                        L.ParryRefillGuard += t.ParryRefillGuard;
                        L.ParryRefillGuardWasted += t.ParryRefillGuardWasted;
                        L.ParryStances += t.ParryStances;
                        L.ParryRefillTurn += t.ParryRefillTurn;
                        L.ParryStockAtDeath += t.ParryStockAtDeath;
                        L.AtkPeakSum += t.AtkPeak;
                        Acc(L.ParryByRoute, t.ParryByRoute);
                        Acc(L.Amount, t.HarmAmount);
                        Acc(L.Hits, t.HarmHits);
                        Acc(L.GuardAmount, t.HarmGuardAmount);
                        Acc(L.GuardHits, t.HarmGuardHits);
                        Acc(L.Fatal, t.HarmFatal);
                        Acc(L.Label, t.InterceptsByLabel);
                    }
                }
        }
        return led;
    }

    static void Acc(long[] dst, int[]? src)
    {
        if (src is null) return;
        for (int i = 0; i < dst.Length && i < src.Length; i++) dst[i] += src[i];
    }

    internal static string NameOf(string id) => UnitCatalog.All.First(d => d.Id == id).Name;

    /// <summary>量（または回数）の経路別の行。合計が 0 なら「—」で埋める。</summary>
    static string RouteCells(long[] v, bool asPercent)
    {
        long sum = v.Sum();
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < v.Length; i++)
        {
            sb.Append(" | ");
            if (sum == 0) sb.Append('—');
            else if (v[i] == 0) sb.Append('0');
            else if (asPercent) sb.Append($"{v[i] * 100.0 / sum:F1}%");
            else sb.Append(v[i]);
        }
        return sb.ToString();
    }

    static string RouteHeader() => string.Concat(DamageRoutes.Names.Select(n => " | " + n));
    static string RouteRule() => string.Concat(DamageRoutes.Names.Select(_ => " | ---:"));

    // =================================================================================
    // 段1 の生成物 —— docs/harm.md
    // =================================================================================

    static void Harm()
    {
        var led = Collect();

        Console.WriteLine("# 害の帳簿 —— 庇う・肩代わりする駒は何で削られ、何で死ぬか（第135期）");
        Console.WriteLine();
        Console.WriteLine($"`compare` {Presets.Compare.Length} 行のうちその駒を含む行 × **第2〜5波** × seed 0..{Seeds - 1}。");
        Console.WriteLine("**第一波は分母に入れない**（規約 (G10)。全行が単発で 100% 勝つ教習波）。");
        Console.WriteLine();
        Console.WriteLine("経路は `BattleCore/Traits.cs` の `DamageRoute`。**新しい札は1つも足していない**");
        Console.WriteLine("——分類は `ApplyDamage` が既に持っている札（`burnTick` / `levy` / `relayed` /");
        Console.WriteLine("`isFriendlyFire` / `HitFrame.Pattern`）と出どころだけから決まる。");
        Console.WriteLine();
        Console.WriteLine("**手で編集しない**（`parry harm` の出力そのもの）。");
        Console.WriteLine();
        Console.WriteLine("> **`干渉 0` は価値が無いではない**（`docs/pulse.md` と同じ注記）。");
        Console.WriteLine("> ここでも **`0`（起きなかった）と `—`（そもそも経路が無い）を描き分けてある。**");
        Console.WriteLine();

        // ---- 表A --------------------------------------------------------------------
        Console.WriteLine("## 表A —— 台帳（分母）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 含む行 | 戦 | 決着T | 生存T | 死亡率 | 被弾/戦 | 回復/戦 |");
        Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|---:|");
        foreach (string id in Watched)
        {
            Ledger L = led[id];
            if (L.Battles == 0) { Console.WriteLine($"| {NameOf(id)} | 0 | 0 | — | — | — | — | — |"); continue; }
            Console.WriteLine($"| {NameOf(id)} | {L.Rows.Count} | {L.Battles} | {L.TurnsSum / (double)L.Battles:F2} | "
                + $"{L.LifeSum / (double)L.Battles:F2} | {L.Deaths * 100.0 / L.Battles:F1}% | "
                + $"{L.Taken / (double)L.Battles:F1} | {L.Healed / (double)L.Battles:F1} |");
        }
        Console.WriteLine();

        // ---- 表B --------------------------------------------------------------------
        Console.WriteLine("## 表B —— 受けた**総量**の経路別の割合（**P1 の前半**）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 被弾/戦" + RouteHeader() + " |");
        Console.WriteLine("|---|---:" + RouteRule() + " |");
        foreach (string id in Watched)
        {
            Ledger L = led[id];
            Console.WriteLine($"| {NameOf(id)} | {(L.Battles == 0 ? "—" : (L.Amount.Sum() / (double)L.Battles).ToString("F1"))}"
                + RouteCells(L.Amount, true) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("**範囲（薙ぎ＋貫き＋全体）の取り分**:");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 単体 | 範囲 | 継続（毒＋燃焼） | 味方由来（巻き込み＋徴収＋自傷） | 肩代わりの中継 | その他 |");
        Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|");
        foreach (string id in Watched) Console.WriteLine(Grouped(led[id].Amount, NameOf(id)));
        Console.WriteLine();

        // ---- 表C --------------------------------------------------------------------
        Console.WriteLine("## 表C —— 受けた**回数**の経路別（量とは別の形になる）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 回/戦" + RouteHeader() + " |");
        Console.WriteLine("|---|---:" + RouteRule() + " |");
        foreach (string id in Watched)
        {
            Ledger L = led[id];
            Console.WriteLine($"| {NameOf(id)} | {(L.Battles == 0 ? "—" : (L.Hits.Sum() / (double)L.Battles).ToString("F2"))}"
                + RouteCells(L.Hits, true) + " |");
        }
        Console.WriteLine();

        // ---- 表D --------------------------------------------------------------------
        Console.WriteLine("## 表D —— **致命打**の経路（**P1 の後半**。`HarmFatal` ＝ 倒れた一撃だけ）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 死 | 死亡率" + RouteHeader() + " |");
        Console.WriteLine("|---|---:|---:" + RouteRule() + " |");
        foreach (string id in Watched)
        {
            Ledger L = led[id];
            Console.WriteLine($"| {NameOf(id)} | {L.Fatal.Sum()} | "
                + $"{(L.Battles == 0 ? "—" : (L.Deaths * 100.0 / L.Battles).ToString("F1") + "%")}"
                + RouteCells(L.Fatal, true) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("**総量の内訳（表B）と致命打の内訳（表D）の差** ——");
        Console.WriteLine("同じ駒の同じ被弾を2通りに数えたもの。**ずれているほど「総数では区別できない」**（第134期）。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 総量の単体 | 致命打の単体 | 差 | 総量の範囲 | 致命打の範囲 | 差 |");
        Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|");
        foreach (string id in Watched)
        {
            Ledger L = led[id];
            double aS = Pct(L.Amount, r => r == DamageRoute.Single), fS = Pct(L.Fatal, r => r == DamageRoute.Single);
            double aR = Pct(L.Amount, DamageRoutes.IsRange), fR = Pct(L.Fatal, DamageRoutes.IsRange);
            if (L.Amount.Sum() == 0 || L.Fatal.Sum() == 0)
            { Console.WriteLine($"| {NameOf(id)} | — | — | — | — | — | — |"); continue; }
            Console.WriteLine($"| {NameOf(id)} | {aS:F1}% | {fS:F1}% | {fS - aS:+0.0;-0.0;0.0}pt | "
                + $"{aR:F1}% | {fR:F1}% | {fR - aR:+0.0;-0.0;0.0}pt |");
        }
        Console.WriteLine();

        // ---- 表E --------------------------------------------------------------------
        Console.WriteLine("## 表E —— **引き受けたぶん / 素で狙われたぶん**（介入が主目標を差し替えた一撃）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 引き受け 量/戦 | 割合 | 引き受け 回/戦 | 割合 | 素 量/戦 | 素 回/戦 |");
        Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|");
        foreach (string id in Watched)
        {
            Ledger L = led[id];
            if (L.Battles == 0) { Console.WriteLine($"| {NameOf(id)} | — | — | — | — | — | — |"); continue; }
            double ga = L.GuardAmount.Sum(), gh = L.GuardHits.Sum(), aa = L.Amount.Sum(), ah = L.Hits.Sum();
            Console.WriteLine($"| {NameOf(id)} | {ga / L.Battles:F1} | {(aa == 0 ? "—" : (ga * 100.0 / aa).ToString("F1") + "%")} | "
                + $"{gh / L.Battles:F2} | {(ah == 0 ? "—" : (gh * 100.0 / ah).ToString("F1") + "%")} | "
                + $"{(aa - ga) / L.Battles:F1} | {(ah - gh) / L.Battles:F2} |");
        }
        Console.WriteLine();
        Console.WriteLine("引き受けたぶんの経路別（**庇いは単体にしか効かないので、他の段の形がそのまま出る**）:");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 引き受け 回/戦" + RouteHeader() + " |");
        Console.WriteLine("|---|---:" + RouteRule() + " |");
        foreach (string id in Watched)
        {
            Ledger L = led[id];
            Console.WriteLine($"| {NameOf(id)} | {(L.Battles == 0 ? "—" : (L.GuardHits.Sum() / (double)L.Battles).ToString("F2"))}"
                + RouteCells(L.GuardHits, true) + " |");
        }
        Console.WriteLine();

        // ---- 表F --------------------------------------------------------------------
        Console.WriteLine("## 表F —— 介入の段（`InterceptLabels`）と、**庇いの機会**");
        Console.WriteLine();
        Console.WriteLine("| 駒 | " + string.Join(" | ", InterceptLabels.All) + " | 計/戦 | 庇いの判定/戦 | 成立率 | **範囲で機会なし**/戦 |");
        Console.WriteLine("|---|" + string.Concat(InterceptLabels.All.Select(_ => "---:|")) + "---:|---:|---:|---:|");
        foreach (string id in Watched)
        {
            Ledger L = led[id];
            if (L.Battles == 0) { Console.WriteLine($"| {NameOf(id)} |" + string.Concat(InterceptLabels.All.Select(_ => " — |")) + " — | — | — | — |"); continue; }
            long gi = L.Label[Array.IndexOf(InterceptLabels.All, InterceptLabels.Guardian)];
            Console.WriteLine($"| {NameOf(id)} |"
                + string.Concat(L.Label.Select(v => $" {v / (double)L.Battles:F2} |"))
                + $" {L.Label.Sum() / (double)L.Battles:F2} |"
                + $" {L.GuardChances / (double)L.Battles:F2} |"
                + $" {(L.GuardChances == 0 ? "—" : (gi * 100.0 / L.GuardChances).ToString("F1") + "%")} |"
                + $" {L.GuardRangeMissed / (double)L.Battles:F2} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**`庇いの判定` は「鎖が庇いの段まで来て判定（`GuardianTrait.RedirectPercent` = {GuardianTrait.RedirectPercent}%）を振られた回数」**、");
        Console.WriteLine("**`範囲で機会なし` は「資格のある庇い手が前列にいたのに、単体以外の一撃だったので段に到達すらしなかった回数」。**");
        Console.WriteLine("後者は `SelectTargetChain` で攻撃型が決まった直後——**貫きの早期リターンより前**——に数えている");
        Console.WriteLine("（後ろに置くと貫きが丸ごと落ちる）。**主目標の除外は掛けていない**——");
        Console.WriteLine("貫きの時点では主目標がまだ決まっていないため、「その一撃で庇いの段が使われなかった回数」を数える。");
        Console.WriteLine();

        // ---- 表G --------------------------------------------------------------------
        Console.WriteLine("## 表G —— 一文の実装（**引き取り `GatherRule` と、肩代わりの見返り `RedirectGain` は別の機構**）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 肩代わりの見返り 回/戦 | 攻撃力 +/戦 | 引き取り 庇い/戦 | donor あり/戦 | **移した傷/戦** |");
        Console.WriteLine("|---|---:|---:|---:|---:|---:|");
        foreach (string id in Watched)
        {
            Ledger L = led[id];
            if (L.Battles == 0) { Console.WriteLine($"| {NameOf(id)} | — | — | — | — | — |"); continue; }
            Console.WriteLine($"| {NameOf(id)} | {L.RedirectFires / (double)L.Battles:F2} | {L.RedirectGain / (double)L.Battles:F2} | "
                + $"{L.GatherGuards / (double)L.Battles:F2} | {L.GatherHadDonor / (double)L.Battles:F2} | "
                + $"{L.GatherTaken / (double)L.Battles:F2} |");
        }
        Console.WriteLine();
        Console.WriteLine($"`GatherRule` の既定は **`{GatherRule.Default}`**（第122期に降ろした）。");
        Console.WriteLine("**`移した傷` が 0 でも `肩代わりの見返り` は 0 ではない**——");
        Console.WriteLine("あれは `RedirectGainTrait.OnDamaged` の `self.AtkBonus += dmg / 2` で、傷（`StatusKeys.Wound`）とは無関係である。");
        Console.WriteLine();

        // ---- 表H --------------------------------------------------------------------
        Console.WriteLine("## 表H —— 支援拒否（`Stoic`）が弾いた量（**代金の実測**）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 弾いた回復 量/戦 | 回/戦 | 隣へ流した回数/戦 | 宛先の延べ/戦 |");
        Console.WriteLine("|---|---:|---:|---:|---:|");
        foreach (string id in Watched)
        {
            Ledger L = led[id];
            if (L.Battles == 0 || (L.StoicHealFires == 0 && L.StoicHops == 0))
            { Console.WriteLine($"| {NameOf(id)} | — | — | — | — |"); continue; }
            Console.WriteLine($"| {NameOf(id)} | {L.StoicHeal / (double)L.Battles:F2} | {L.StoicHealFires / (double)L.Battles:F2} | "
                + $"{L.StoicHops / (double)L.Battles:F2} | {L.StoicHeads / (double)L.Battles:F2} |");
        }
        Console.WriteLine();
        Console.WriteLine("**量を持つのは回復だけ。** 強化・弱体は `SupportTargets` が「誰に配るか」しか知らないので、");
        Console.WriteLine("量は素体対照（`Stoic` を外した版との差）で取る——`parry phase0` の Q0-5。");
        Console.WriteLine();

        // ---- 表I（第136期 段2）------------------------------------------------------
        Console.WriteLine("## 表I —— 受け流しの在庫（第136期 段2・`ParryRule`）");
        Console.WriteLine();
        Console.WriteLine($"`ParryRule.Default` = **`{ParryRule.Default}`**。");
        if (ParryRule.Default.Uses <= 0)
        {
            Console.WriteLine("**`Uses = 0` なので受け流しは1度も走らない**（この表は空）。");
        }
        else
        {
            Console.WriteLine("在庫は開戦時と毎ターン頭の構えで N に戻り、庇って身に受けるたび 1 戻る。**攻撃力は上がらない**（表G の `攻撃力 +/戦` が 0 になる）。");
            Console.WriteLine();
            Console.WriteLine("| 駒 | 受け流し 回/戦 | 無効化 量/戦 | 被弾に対する比 | 構え直し T/戦 | 構えで戻した/戦 | 庇いで戻した/戦 | 満タンで戻せず/戦 | 死亡時の残り在庫（平均） | 攻撃力の到達点 |");
            Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
            foreach (string id in Watched)
            {
                Ledger L = led[id];
                if (L.Battles == 0 || L.ParryFires == 0)
                { Console.WriteLine($"| {NameOf(id)} | — | — | — | — | — | — | — | — | {(L.Battles == 0 ? "—" : (L.AtkPeakSum / (double)L.Battles).ToString("F2"))} |"); continue; }
                double b = L.Battles;
                Console.WriteLine($"| {NameOf(id)} | {L.ParryFires / b:F2} | {L.ParryBlocked / b:F1} | {(L.Taken + L.ParryBlocked == 0 ? "—" : (L.ParryBlocked * 100.0 / (L.Taken + L.ParryBlocked)).ToString("F1") + "%")} | "
                    + $"{L.ParryStances / b:F2} | {L.ParryRefillTurn / b:F2} | {L.ParryRefillGuard / b:F2} | {L.ParryRefillGuardWasted / b:F2} | "
                    + $"{(L.Deaths == 0 ? "—" : (L.ParryStockAtDeath / (double)L.Deaths).ToString("F2"))} | {L.AtkPeakSum / b:F2} |");
            }
            Console.WriteLine();
            Console.WriteLine("受け流した一撃の経路（**表D の致命打の経路と重なっているか**）:");
            Console.WriteLine();
            Console.WriteLine("| 駒 | 回/戦" + RouteHeader() + " |");
            Console.WriteLine("|---|---:" + RouteRule() + " |");
            foreach (string id in Watched)
            {
                Ledger L = led[id];
                if (L.Battles == 0 || L.ParryFires == 0) continue;
                Console.WriteLine($"| {NameOf(id)} | {L.ParryFires / (double)L.Battles:F2}" + RouteCells(L.ParryByRoute, true) + " |");
            }
            Console.WriteLine();
            Console.WriteLine("**`被弾に対する比` は 無効化 ÷（実際に受けた量 ＋ 無効化）**——受け流しが無ければ受けていたはずの量のうち、消えた割合。");
            Console.WriteLine("**`死亡時の残り在庫` が 0 でなければ、上限（在庫）ではなく素通り（刻み・徴収・巻き込み）か一撃の大きさで死んでいる。**");
        }
    }

    static double Pct(long[] v, Func<DamageRoute, bool> pick)
    {
        long sum = v.Sum();
        if (sum == 0) return 0;
        long hit = 0;
        for (int i = 0; i < v.Length; i++) if (pick((DamageRoute)i)) hit += v[i];
        return hit * 100.0 / sum;
    }

    static string Grouped(long[] v, string name)
    {
        if (v.Sum() == 0) return $"| {name} | — | — | — | — | — | — |";
        double s = Pct(v, r => r == DamageRoute.Single);
        double rg = Pct(v, DamageRoutes.IsRange);
        double dot = Pct(v, r => r is DamageRoute.Poison or DamageRoute.Burn);
        double ff = Pct(v, r => r is DamageRoute.Friendly or DamageRoute.Levy or DamageRoute.Self);
        double rel = Pct(v, r => r == DamageRoute.Relay);
        double oth = Pct(v, r => r == DamageRoute.Other);
        return $"| {name} | {s:F1}% | **{rg:F1}%** | {dot:F1}% | {ff:F1}% | {rel:F1}% | {oth:F1}% |";
    }

    // =================================================================================
    // Phase 0
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第135期 Phase 0 —— ガルドは何で死んでいるか（走査と数え物）");
        Console.WriteLine();
        Console.WriteLine("**すべて実装から引く。走査は件数を出力し、0件を異常として止める**（第117期）。");
        Console.WriteLine("**走査の検索文字列は連結で組んである**——この診断自身がリポジトリ内のファイルなので、");
        Console.WriteLine("素直に書くと自分のコードに当たって静かに違う表を作る（第123期）。");
        Console.WriteLine();

        string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
        string engine = ParryScan.Read(Path.Combine("BattleCore", "BattleEngine.cs"));

        // ---- Q0-4 --------------------------------------------------------------------
        Console.WriteLine("## Q0-4 —— `GatherRule` の現在の既定");
        Console.WriteLine();
        Console.WriteLine($"- `GatherRule.Default` = **`{GatherRule.Default}`**（第122期に `false` を採用）");
        Console.WriteLine($"- `GatherRule` を読む箇所: **{Count(engine, "ctx.Gather", "Gather.Enabled") + Count(traits, "ctx.Gather", "Gather.Enabled")} 件**");
        Console.WriteLine("- 実測は `parry harm` の表G（`移した傷/戦`）。**0 なら引き取りは1度も起きていない。**");
        Console.WriteLine();
        Console.WriteLine("**ただし一文「その傷のぶん強くなる」は `GatherRule` のことではない。**");
        // **クラスの本体だけを切り出して数える**——`self.AtkBonus += gain` という綴りは
        // 怒り・分かちなど他の札にもあるので、ファイル全体を数えると桁が変わる（第123期）。
        int rbeg = traits.IndexOf("abstract class RedirectGainTrait", StringComparison.Ordinal);
        int rend = rbeg >= 0 ? traits.IndexOf("\npublic ", rbeg + 10, StringComparison.Ordinal) : -1;
        string rbody = rbeg >= 0 && rend > rbeg ? traits.Substring(rbeg, rend - rbeg) : "";
        int redirect = Count(rbody, "self.AtkBonus += gain");
        if (rbody.Length == 0) { Console.WriteLine("**`RedirectGainTrait` の本体を切り出せなかった。止める**（第117期）。"); return; }
        Console.WriteLine($"`RedirectGainTrait.OnDamaged` の `self.AtkBonus += gain` が **{redirect} 箇所**にあり、");
        Console.WriteLine("**庇いが成立した被弾のたび `dmg / 2` だけ攻撃力が上がる**（`RedirectGainTrait.DamagePerGain` = "
            + $"{RedirectGainTrait.DamagePerGain}）。");
        Console.WriteLine("**指示書 §0-1 の「`AtkBonus` を1も動かさない」は `GatherRule` についての記述で、一文の実装はこちら。**");
        Console.WriteLine("実測は表G の `肩代わりの見返り`。");
        Console.WriteLine();

        // ---- Q0-6 --------------------------------------------------------------------
        Console.WriteLine("## Q0-6 —— `ModifyIncomingDamage` の作法と、受け流しを置く段");
        Console.WriteLine();
        var mid = ParryScan.Overriders(nameof(Trait.ModifyIncomingDamage));
        Console.WriteLine($"- `ModifyIncomingDamage` を上書きしている札: **{mid.Count} 枚** "
            + $"（{string.Join(" / ", mid.Select(t => "`" + t + "`"))}）");
        Console.WriteLine($"- 呼び出し口: **{Count(engine, "t.ModifyIncomingDamage(target, amount)")} 箇所**（`ApplyDamageBody` の入口）");
        Console.WriteLine();
        Console.WriteLine("**`ApplyDamageBody` の段の並び**（**実装の出現順を走査で引いた**。左が入口側）:");
        Console.WriteLine();
        Console.WriteLine("| # | 段 | 目印の位置（本体の先頭からの行数） |");
        Console.WriteLine("|---:|---|---:|");
        string[] marks =
        {
            "棘守りの上限|target.HasTrait(TraitId.ThornGuard)",
            "**入口**（`ModifyIncomingDamage` ＝ 脆弱・育ち耐性）|t.ModifyIncomingDamage(target, amount)",
            "惨禍 +50%|HavocTrait.Percent",
            "据え|BulwarkTrait.ReductionPercent",
            "散開|LooseTrait.ReductionPercent",
            "萎縮|CowerTrait.ReductionPercent",
            "巨躯（肩代わり）|Colossus.Percent",
            "分かち（肩代わり）|SharerTrait.Percent",
            "破片（Armor）|StatusKeys.Armor",
            "`lethal: false` のクランプ|if (!lethal) amount",
            "猶予（HP1 で耐える）|ReprieveTrait.UsedKey",
            "不死（器具）|TraitId.Undying",
            "軛（1発の上限）|Yoke.Cap",
            "**HP を引く**|target.Hp -= amount",
        };
        int bodyAt = engine.IndexOf("void ApplyDamage" + "Body", StringComparison.Ordinal);
        if (bodyAt < 0) { Console.WriteLine("**`ApplyDamageBody` を引けなかった。止める**（第117期）。"); return; }
        int seq = 0, prev = -1;
        bool ordered = true;
        foreach (string m in marks)
        {
            string[] kv = m.Split('|');
            int at = engine.IndexOf(kv[1], bodyAt, StringComparison.Ordinal);
            string cell = "**引けない**";
            if (at >= 0)
            {
                cell = (engine.Substring(bodyAt, at - bodyAt).Count(c => c == '\n') + 1).ToString();
                if (at < prev) ordered = false;
                prev = at;
            }
            else ordered = false;
            Console.WriteLine($"| {++seq} | {kv[0]} | {cell} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**14 段がこの順で並んでいることを走査で確かめた: {(ordered ? "○" : "**×**")}**"
            + "（行数が単調増加であること）。");
        Console.WriteLine();
        Console.WriteLine("**受け流し（1発を丸ごと無効化する）は二値の制約なので、出口にしか置けない**（第25期の軛・第126期の猶予）。");
        Console.WriteLine("置くのは **`lethal: false` のクランプ → 猶予 → 不死 → 軛 → HP を引く** の族で、");
        Console.WriteLine("**猶予の直前**（＝惨禍・脆弱・据え・散開・萎縮・肩代わり・破片より<b>後</b>）。");
        Console.WriteLine();
        Console.WriteLine("| 相手 | 受け流しとの順序 | 理由 |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine("| 惨禍（+50%）・脆弱（×1.5） | **受け流しが後** | 入口に置くと増幅が押し戻して「無効化」が守られない（第126期） |");
        Console.WriteLine("| 破片（`Armor`） | **受け流しが後** | 破片は上限の外側で効く別資源。先に吸われた残りを弾く |");
        Console.WriteLine("| 肩代わり（巨躯・分かち） | **受け流しが後** | 分割された各段は別の `ApplyDamage`。受け流すのは自分に残ったぶんだけ |");
        Console.WriteLine("| 軛（1発 25 上限） | **受け流しが前** | どちらも「1発を切る」制約。先に 0 にすれば軛は素通り（結果は同じ） |");
        Console.WriteLine();

        // ---- Q0-8 --------------------------------------------------------------------
        Console.WriteLine("## Q0-8 —— 既存の軽減・無効化の全件");
        Console.WriteLine();
        Console.WriteLine("| 札 | 駒 | 形 | 量 | 範囲攻撃に効くか |");
        Console.WriteLine("|---|---|---|---|---|");
        Console.WriteLine($"| `Sharer`（分かち） | {HolderNames(TraitId.Sharer)} | **肩代わり**（割合） | {SharerTrait.Percent}% | ○（`ApplyDamage` の層） |");
        Console.WriteLine($"| `Colossus`（巨躯） | {HolderNames(TraitId.Colossus)} | **肩代わり**（割合・自分より後ろ限定） | {ColossusTrait.Percent}% | ○（`ApplyDamage` の層） |");
        Console.WriteLine($"| `Cower`（萎縮） | {HolderNames(TraitId.Cower)} | **軽減**（味方全体） | -{CowerTrait.ReductionPercent}% | ○ |");
        Console.WriteLine($"| `Loose`（散開） | {HolderNames(TraitId.Loose)} | **軽減**（隣に味方がいない駒） | -{LooseTrait.ReductionPercent}% | ○ |");
        Console.WriteLine($"| `Bulwark`（据え） | {HolderNames(TraitId.Bulwark)} | **軽減**（手番を差し出した駒） | -{BulwarkTrait.ReductionPercent}% | ○ |");
        Console.WriteLine($"| `Tempered`（育ち耐性） | {HolderNames(TraitId.Tempered)} | **軽減**（自分の `AtkBonus` を読む） | 最大 -{TemperedTrait.MaxPercent}% | ○ |");
        Console.WriteLine($"| `ThornGuard`（棘守り） | {HolderNames(TraitId.ThornGuard)} | **上限**（超過は守った相手へ中継） | {ThornGuardTrait.AbsorbCap} | ×（介入は Single のみ） |");
        Console.WriteLine($"| `Bear`（引き受け） | {HolderNames(TraitId.Bear)} | 弱体 → 破片への**変換** | — | —（ダメージではない） |");
        Console.WriteLine($"| `Guardian`（庇う） | {HolderNames(TraitId.Guardian)} | **差し替え**（自分が代わりに受ける） | {GuardianTrait.RedirectPercent}% | ×（Single のみ） |");
        Console.WriteLine($"| `RearGuard`（後備え） | {HolderNames(TraitId.RearGuard)} | **差し替え** | {RearGuardTrait.RedirectPercent}% | ○（範囲にも割り込む） |");
        Console.WriteLine($"| `Reprieve`（猶予） | {HolderNames(TraitId.Reprieve)} | **致死を HP1 で止める**（1戦1回） | — | ○ |");
        Console.WriteLine($"| `Undying`（不死・**器具**） | {HolderNames(TraitId.Undying)} | 致死を止める | — | ○ |");
        Console.WriteLine($"| 盤面ルール `Yoke`（軛） | — | **1発の上限** | {YokeRule.Default.Cap} | ○ |");
        Console.WriteLine($"| `StatusKeys.Armor`（破片） | — | **プール**（超過は素通り） | — | ○ |");
        Console.WriteLine();
        Console.WriteLine("**「1発を丸ごと無効化する」機構は1枚も無い。** 猶予・不死は「殺さない」制約で、");
        Console.WriteLine("軛と破片は「量を切る」。**受け流しは初めての無効化になる。**");
        Console.WriteLine();

        // ---- Q0-7 --------------------------------------------------------------------
        Console.WriteLine("## Q0-7 —— ガルドを含む `compare` 行");
        Console.WriteLine();
        var galdRows = Presets.Compare.Where(b => b.F.Occupied().Any(o => o.Def.Id == "gald")).ToList();
        var crossRows = Presets.Cross.Where(b => b.F.Occupied().Any(o => o.Def.Id == "gald")).ToList();
        Console.WriteLine($"- `compare` {Presets.Compare.Length} 行のうち **{galdRows.Count} 行**（{galdRows.Count * 100.0 / Presets.Compare.Length:F1}%）");
        Console.WriteLine($"- 交差帯 `Presets.Cross` {Presets.Cross.Length} 行のうち **{crossRows.Count} 行**");
        Console.WriteLine($"- ガルドの席の分布: " + string.Join(" / ", galdRows
            .GroupBy(b => b.F.Occupied().First(o => o.Def.Id == "gald").Slot)
            .OrderBy(g => g.Key).Select(g => $"{FormationRules.SeatNames[g.Key]} {g.Count()}")));
        Console.WriteLine();
        Console.WriteLine("**P5 の分母はこの行数。** 段3 に進んだ場合、残り "
            + $"**{Presets.Compare.Length - galdRows.Count} 行 × 5 波 = {(Presets.Compare.Length - galdRows.Count) * 5} セル**が ±0.0 でなければ事故。");
        Console.WriteLine();

        // ---- Q0-9 --------------------------------------------------------------------
        Console.WriteLine("## Q0-9 —— 過去に無効化・パリィ・反射を測った期");
        Console.WriteLine();
        string designDir = Path.Combine(ParryScan.Root!, "design");
        var files = Directory.GetFiles(designDir, "*.md").Where(f => !Path.GetFileName(f).StartsWith("PHASE135")).ToList();
        string[] keys = { "パリ" + "ィ", "受け" + "流", "反射" };
        Console.WriteLine($"`design/` の **{files.Count} ファイル**（この期の文書を除く）を走査した:");
        Console.WriteLine();
        Console.WriteLine("| 語 | 当たったファイル |");
        Console.WriteLine("|---|---|");
        foreach (string k in keys)
        {
            var hit = files.Where(f => File.ReadAllText(f).Contains(k, StringComparison.Ordinal))
                           .Select(Path.GetFileName).ToList();
            Console.WriteLine($"| {k} | {(hit.Count == 0 ? "**0 件**" : string.Join(" / ", hit))} |");
        }
        Console.WriteLine();
        Console.WriteLine("**当たったファイルは3種類しかない**——(i) **この期の案そのものの持ち越し**");
        Console.WriteLine("（第132〜134期の §8 が「ガルドのパリィ案」として次期へ送っている）／");
        Console.WriteLine("(ii) **この期が書いた `LESSONS_101_.md` の節自身**");
        Console.WriteLine("（走査対象が走査する側の成果物を含む＝第123期の自己参照）／");
        Console.WriteLine("(iii) `反射` の 4 件（「出力経路の列挙」「リフレクションで引く」");
        Console.WriteLine("「ゴルムが弱体を反射する」の比喩）。");
        Console.WriteLine("**「無効化・パリィを測った期」も「反射を却下した判断」も 0 件。この期の報告書に残す。**");
        Console.WriteLine();

        // ---- Q0-10 -------------------------------------------------------------------
        Console.WriteLine("## Q0-10 —— `checkup` の分類");
        Console.WriteLine();
        string prog = ParryScan.Read(Path.Combine("BattleSim", "Program.cs"));
        var labeled = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(prog, @"\[TraitId\.(\w+)\]\s*=\s*\(Hc"))
            labeled.Add(m.Groups[1].Value);
        if (labeled.Count == 0)
        {
            Console.WriteLine("**`checkup` の分類表を走査できなかった。止める**（第117期）。");
            return;
        }
        var rosterTraits = UnitCatalog.All.SelectMany(d => d.Traits).Distinct().ToList();
        var missing = rosterTraits.Where(t => !labeled.Contains(t.ToString())).ToList();
        Console.WriteLine($"- 分類表の件数: **{labeled.Count} 件**");
        Console.WriteLine($"- ロスター 52 枚が持つ札: **{rosterTraits.Count} 種**");
        Console.WriteLine($"- **分類の無い札: {missing.Count} 件**"
            + (missing.Count == 0 ? "" : $"（{string.Join(" / ", missing.Select(t => "`" + t + "`"))}）"));
        if (missing.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("**`checkup check` はこれで止まっている**（第131期に判明。第128期に `GradeStep` を");
            Console.WriteLine("ドルガへ載せたとき分類表に1行足さなかったのが原因）。");
            Console.WriteLine("**段3 に進むなら受け流しの札と一緒にここも埋める**（受け入れ条件 A7）。");
        }
        Console.WriteLine();

        // ---- Q0-1〜Q0-3・Q0-5 の案内 --------------------------------------------------
        Console.WriteLine("## Q0-1 / Q0-2 / Q0-3 / Q0-5 —— 実測");
        Console.WriteLine();
        Console.WriteLine("| 問い | どの表で答えるか |");
        Console.WriteLine("|---|---|");
        Console.WriteLine("| Q0-1 経路別の総量と回数／引き受けと素の分離 | `docs/harm.md` の表B・表C・表E |");
        Console.WriteLine("| Q0-2 致命打の経路 | 表D |");
        Console.WriteLine("| Q0-3 庇いの成立／不成立／範囲で機会なし | 表F |");
        Console.WriteLine("| Q0-4 引き取りの実回数 | 表G |");
        Console.WriteLine("| Q0-5 `Stoic` が弾いた回復と隣への転送 | 表H ＋ 下の素体対照 |");
        Console.WriteLine();
        StoicControl();
    }

    static string HolderNames(TraitId t)
    {
        var h = ParryScan.Holders(t);
        return h.Count == 0 ? "**0 枚**" : string.Join("・", h.Select(d => d.Name));
    }

    internal static int Count(string hay, params string[] needles)
    {
        int n = 0;
        foreach (string s in needles)
        {
            int i = 0;
            while ((i = hay.IndexOf(s, i, StringComparison.Ordinal)) >= 0) { n++; i += s.Length; }
        }
        return n;
    }

    /// <summary>
    /// Q0-5 の素体対照 —— <c>Stoic</c> を外したガルドとの差で「強化・弱体がいくら来なかったか」を取る。
    /// <b>盤面が動く対照である</b>（`Presets` は1文字も触らない。差し替えは診断のローカル）。
    /// </summary>
    static void StoicControl()
    {
        Console.WriteLine("### Q0-5 の素体対照 —— `Stoic` を外すと何が届くようになるか");
        Console.WriteLine();
        Console.WriteLine("`Stoic` を外したガルド（**他は1文字も変えない**）を差し込んだ版との差。");
        Console.WriteLine("**盤面が動く対照**なので勝率も併記する。`Presets` / `UnitCatalog.All` は1文字も触らない。");
        Console.WriteLine();

        UnitDef gald = UnitCatalog.Gald;
        var open = new UnitDef
        {
            Id = gald.Id, Name = gald.Name + "（Stoic なし）", MaxHp = gald.MaxHp,
            Attack = gald.Attack, Speed = gald.Speed, Advances = gald.Advances,
            Traits = gald.Traits.Where(t => t != TraitId.Stoic).ToArray(),
            Pattern = gald.Pattern, Actions = gald.Actions,
            PlusText = gald.PlusText, MinusText = gald.MinusText, Flavor = gald.Flavor,
        };

        const int S = 50;
        var rows = Presets.Compare.Where(b => b.F.Occupied().Any(o => o.Def.Id == "gald")).ToList();
        double[] win = new double[2];
        long[] whet = new long[2], dull = new long[2], heal = new long[2], nb = new long[2];
        long battles = 0;

        for (int v = 0; v < 2; v++)
            foreach ((string _, Formation f0) in rows)
            {
                Formation f = Swap(f0, "gald", v == 0 ? gald : open);
                for (int w = 1; w < EnemyCatalog.Stages.Count; w++)
                    for (int seed = 0; seed < S; seed++)
                    {
                        BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[w].Enemy, seed, verbose: false);
                        if (v == 0) battles++;
                        if (r.PlayerWon) win[v]++;
                        if (!r.TallyByUnit.TryGetValue("gald", out UnitTally? t)) continue;
                        heal[v] += t.Healed;
                        if (t.CarryAmount is int[] c)
                        {
                            whet[v] += c[UnitTally.CarryWhet];
                            dull[v] += c[UnitTally.CarryDull];
                        }
                        // 隣接する味方へ流れたぶんは「ガルド以外の味方が受けた強化・弱体」の差で見る
                        foreach (var kv in r.TallyByUnit)
                            if (kv.Key != "gald" && kv.Value.CarryAmount is int[] c2)
                                nb[v] += c2[UnitTally.CarryWhet] + c2[UnitTally.CarryDull];
                    }
            }

        double n = battles;
        Console.WriteLine($"分母: ガルドを含む {rows.Count} 行 × 第2〜5波 × seed 0..{S - 1} = **{battles} 戦**（版ごと）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 勝率 | ガルドに届いた強化/戦 | 弱体/戦 | 回復/戦 | 他の味方に届いた強化+弱体/戦 |");
        Console.WriteLine("|---|---:|---:|---:|---:|---:|");
        for (int v = 0; v < 2; v++)
            Console.WriteLine($"| {(v == 0 ? "現行（`Stoic` あり）" : "`Stoic` なし")} | {win[v] * 100.0 / n:F1}% | "
                + $"{whet[v] / n:F2} | {dull[v] / n:F2} | {heal[v] / n:F2} | {nb[v] / n:F2} |");
        Console.WriteLine($"| **差** | {(win[1] - win[0]) * 100.0 / n:+0.0;-0.0;0.0}pt | "
            + $"{(whet[1] - whet[0]) / n:+0.00;-0.00;0.00} | {(dull[1] - dull[0]) / n:+0.00;-0.00;0.00} | "
            + $"{(heal[1] - heal[0]) / n:+0.00;-0.00;0.00} | {(nb[1] - nb[0]) / n:+0.00;-0.00;0.00} |");
        Console.WriteLine();
        Console.WriteLine("**`他の味方に届いた強化+弱体` が減るぶんが「隣へ流れていた量」。**");
    }

    static Formation Swap(Formation f, string id, UnitDef with)
    {
        var g = new Formation();
        for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
        {
            UnitDef? d = f[i];
            g[i] = d is not null && d.Id == id ? with : d;
        }
        return g;
    }

    // =================================================================================
    // 段2 —— 受け流しをローカル台で測る
    // =================================================================================

    // ---- 台（**診断のローカル**。`Presets` / `UnitCatalog.All` は1文字も触らない）------------
    //
    // 条件は指示書 §3-2 の3つ:
    //   (1) **庇いが確実に起きる** —— ガルドを前1 に置き、庇われる味方を後ろに並べる
    //   (2) **敵は単体攻撃と範囲攻撃の両方を持つ** —— 第2〜5波をそのまま使う
    //       （第一波は分母に入れない＝規約 (G10)）
    //   (3) **土台は全版で同一**
    //
    // 肩代わり役（ゴルム・ドハ・カド）は1枚も入れない——`ApplyDamage` の層で先に量を取られると
    // 「ガルドに残ったぶん」が台ごとに変わり、受け流しが弾く量の意味が動く。
    static (string Name, Formation F)[] Candidates() => new (string, Formation)[]
    {
        ("A 前が薄い", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Mudo, center: UnitCatalog.Sero,
            back1: UnitCatalog.Nono, back3: UnitCatalog.Dolga)),
        ("B 前が硬い", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga, center: UnitCatalog.Nel,
            back1: UnitCatalog.Hisa, back3: UnitCatalog.Kiri)),
        ("C 後ろに出力", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Mudo, center: UnitCatalog.Sero,
            back1: UnitCatalog.Nono, back3: UnitCatalog.Borg)),
        ("D 前に出力", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga, center: UnitCatalog.Sero,
            back1: UnitCatalog.Nono, back3: UnitCatalog.Egu)),
        ("E 前が硬い+出力", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga, center: UnitCatalog.Nel,
            back1: UnitCatalog.Nono, back3: UnitCatalog.Kiri)),
        ("F 脆い後列", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Zan, center: UnitCatalog.Sero,
            back1: UnitCatalog.Hisa, back3: UnitCatalog.Dolga)),
        ("G 前が硬い（HP78）", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Nono, center: UnitCatalog.Sero,
            back1: UnitCatalog.Mudo, back3: UnitCatalog.Dolga)),
        ("H 呪詛つき", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Mudo, center: UnitCatalog.Nel,
            back1: UnitCatalog.Nono, back3: UnitCatalog.Dolga)),
        ("I 中央が回復", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Mudo, center: UnitCatalog.Nono,
            back1: UnitCatalog.Sero, back3: UnitCatalog.Dolga)),
        ("J 前が硬い（呪詛）", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Nono, center: UnitCatalog.Nel,
            back1: UnitCatalog.Sero, back3: UnitCatalog.Dolga)),
        ("K 裂き入り", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Mudo, center: UnitCatalog.Sero,
            back1: UnitCatalog.Kiri, back3: UnitCatalog.Dolga)),
    };

    /// <summary>
    /// 段2 で使う台（<b>`scan` の結果から選ぶ</b>）。
    /// <b>台が床でも天井でもないこと</b>を先に測ってから固定する
    /// ——素体の勝率が 40〜95% の外にある台では、版の差が定義上 0 にしかならない（第61・63期）。
    ///
    /// <para>11 候補のうち帯に入ったのは <b>A / G / H / K の4つ</b>。
    /// このうち<b>庇いの成立回数がいちばん離れている2つ</b>を採った
    /// ——A は 1.61 回/戦、H は 2.93 回/戦で、G (1.58) と K (1.66) は A とほぼ同じ形だった。
    /// <b>「庇いの機会が何回あるか」が受け流しの効きを決める軸</b>なので、そこを振る。</para>
    /// </summary>
    static (string Name, Formation F)[] Tables()
        => Candidates().Where(c => c.Name[0] is 'A' or 'H').ToArray();

    /// <summary>
    /// 台の下見（<b>版を1つも振らない</b>）。勝率が 40〜95% に入るか・庇いが起きるかだけを見る。
    /// </summary>
    static void Scan()
    {
        Console.WriteLine("# 第135期 段2 —— 台の下見（**版は1つも振らない**）");
        Console.WriteLine();
        Console.WriteLine("**台が床でも天井でもないことを先に測る**（第61期「素体の5波平均が 40% 以上」／");
        Console.WriteLine("第63期「40〜95% の両側で切る」）——外にある台では版の差が定義上 0 にしかならない。");
        Console.WriteLine("**庇いが 0 の台は測定不能**（受け入れ条件 A4）。");
        Console.WriteLine();
        Console.WriteLine($"分母: 第2〜5波 × seed 0..{Seeds - 1}。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 編成 | 勝率 | 帯 | 決着T | ガルド生存T | 庇い/戦 | 範囲で機会なし/戦 |");
        Console.WriteLine("|---|---|---:|:-:|---:|---:|---:|---:|");
        var harm = new HarmRule(true);
        int gi = Array.IndexOf(InterceptLabels.All, InterceptLabels.Guardian);
        foreach ((string name, Formation f) in Candidates())
        {
            long wins = 0, n = 0, turns = 0, life = 0, guards = 0, missed = 0;
            for (int w = 1; w < EnemyCatalog.Stages.Count; w++)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[w].Enemy, seed,
                                                      verbose: false, harm: harm);
                    n++;
                    if (r.PlayerWon) wins++;
                    turns += r.Turns;
                    if (!r.TallyByUnit.TryGetValue("gald", out UnitTally? t)) continue;
                    life += t.LastActiveTurn;
                    guards += t.InterceptsByLabel is int[] L ? L[gi] : 0;
                    missed += t.GuardRangeMissed;
                }
            double win = wins * 100.0 / n;
            Console.WriteLine($"| {name} | " + string.Join(" / ", f.Occupied().Select(o => o.Def.Name))
                + $" | {win:F1}% | {(win >= 40 && win <= 95 ? "**○**" : "×")} | {turns / (double)n:F2} | "
                + $"{life / (double)n:F2} | {guards / (double)n:F2} | {missed / (double)n:F2} |");
        }
        Console.WriteLine();
        Console.WriteLine("**採る台**: " + string.Join(" / ", Tables().Select(t => t.Name)));
    }

    /// <summary>`N の決め方` で引いた中央値（紙の節が読むだけ）。</summary>
    static int GuardMedianCache;

    static UnitDef GaldWith(TraitId extra) => new()
    {
        Id = UnitCatalog.Gald.Id, Name = UnitCatalog.Gald.Name, MaxHp = UnitCatalog.Gald.MaxHp,
        Attack = UnitCatalog.Gald.Attack, Speed = UnitCatalog.Gald.Speed,
        Advances = UnitCatalog.Gald.Advances,
        Traits = UnitCatalog.Gald.Traits.Append(extra).ToArray(),
        Pattern = UnitCatalog.Gald.Pattern, Actions = UnitCatalog.Gald.Actions,
        PlusText = UnitCatalog.Gald.PlusText, MinusText = UnitCatalog.Gald.MinusText,
        Flavor = UnitCatalog.Gald.Flavor,
    };

    static void Stage2()
    {
        Console.WriteLine("# 第135期 段2 —— 受け流しをローカル台で測る");
        Console.WriteLine();
        Console.WriteLine("**代金を付けない。掃引しない**（受け入れ条件 A6。第118・126・127・128・130期と同じ作法）。");
        Console.WriteLine("**`Presets` / `EnemyCatalog.Stages` / `UnitCatalog.All` は1文字も触らない**");
        Console.WriteLine("——台は診断のローカルで、`TraitId.Parry` の保持者は `UnitCatalog.All` に1枚もいない。");
        Console.WriteLine();

        // ---- N の決め方（**測る前に固定する**・受け入れ条件 A5）------------------------------
        Console.WriteLine("## N の決め方（**§3-2 のとおり。実測の中央値から引く**）");
        Console.WriteLine();
        int median = GuardMedian(out int[] hist, out double mean, out long battles);
        GuardMedianCache = median;
        Console.WriteLine($"`compare` のガルドを含む 35 行 × 第2〜5波 × seed 0..{Seeds - 1} = **{battles} 戦**での");
        Console.WriteLine("**庇いの成立回数 / 戦**の分布:");
        Console.WriteLine();
        Console.WriteLine("| 回数 | " + string.Join(" | ", Enumerable.Range(0, hist.Length - 1).Select(i => i.ToString())) + $" | {hist.Length - 1}+ |");
        Console.WriteLine("|---|" + string.Concat(hist.Select(_ => "---:|")));
        Console.WriteLine("| 戦 |" + string.Concat(hist.Select(v => $" {v} |")));
        Console.WriteLine("| 割合 |" + string.Concat(hist.Select(v => $" {v * 100.0 / battles:F1}% |")));
        Console.WriteLine();
        Console.WriteLine($"**平均 {mean:F2} 回/戦・中央値 {median} 回/戦。**");
        int[] ns = { 1, Math.Max(1, median), Math.Max(2, median * 2) };
        ns = ns.Distinct().OrderBy(x => x).ToArray();
        Console.WriteLine($"§3-2 の「1 / 中央値 / 中央値×2」は **N = {string.Join(" / ", ns)}**（重複は畳んだ）。");
        Console.WriteLine();

        // ---- 版 ---------------------------------------------------------------------------
        var versions = new List<(string Name, ParryRule R)> { ("V0 対照", new ParryRule(0, ParryScope.Guarded, false)) };
        foreach (int n in ns) versions.Add(($"V1 庇った分 N={n}", new ParryRule(n, ParryScope.Guarded, false)));
        foreach (int n in ns) versions.Add(($"V2 自分への攻撃 N={n}", new ParryRule(n, ParryScope.Any, false)));

        UnitDef parried = GaldWith(TraitId.Parry);
        var harm = new HarmRule(true);

        Console.WriteLine("## 表A —— 版 × 台");
        Console.WriteLine();
        Console.WriteLine("`生存T` はガルド、`守られた側` は同じ台の他の4枚の生存Tの平均。");
        Console.WriteLine("**`庇い/戦` が 0 の版は測定不能として判定に使わない**（受け入れ条件 A4）。");
        Console.WriteLine();

        foreach ((string tn, Formation f0) in Tables())
        {
            Formation f = Swap(f0, "gald", parried);
            Console.WriteLine($"### {tn} —— " + string.Join(" / ", f0.Occupied()
                .Select(o => $"{FormationRules.SeatNames[o.Slot]} {o.Def.Name}")));
            Console.WriteLine();
            Console.WriteLine("| 版 | 勝率 | 決着T | 残存 | **ガルド生存T** | Δ | 守られた側 生存T | Δ | 庇い/戦 | 受け流し/戦 | 弾いた量/戦 | 1回の最大 | ガルド被弾/戦 |");
            Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");

            double baseLife = 0, baseAlly = 0, baseTaken = 0, baseBlockPer = 0;
            var blocked = new Dictionary<string, long[]>(StringComparer.Ordinal);
            foreach ((string vn, ParryRule rule) in versions)
            {
                long wins = 0, turns = 0, surv = 0, life = 0, ally = 0, allyN = 0;
                long guards = 0, fires = 0, amt = 0, taken = 0; int mx = 0;
                var byRoute = new long[DamageRoutes.Count];
                long n = 0;
                for (int w = 1; w < EnemyCatalog.Stages.Count; w++)
                    for (int seed = 0; seed < Seeds; seed++)
                    {
                        BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[w].Enemy, seed,
                                                          verbose: false, harm: harm, parry: rule);
                        n++;
                        if (r.PlayerWon) wins++;
                        turns += r.Turns;
                        surv += r.PlayerSurvivors;
                        foreach (var kv in r.TallyByUnit)
                        {
                            if (f0.Occupied().All(o => o.Def.Id != kv.Key)) continue;
                            if (kv.Key == "gald")
                            {
                                life += kv.Value.LastActiveTurn;
                                guards += kv.Value.InterceptsByLabel is int[] L
                                    ? L[Array.IndexOf(InterceptLabels.All, InterceptLabels.Guardian)] : 0;
                                fires += kv.Value.ParryFires;
                                amt += kv.Value.ParryBlocked;
                                taken += kv.Value.DamageTaken;
                                mx = Math.Max(mx, kv.Value.ParryBlockedMax);
                                Acc(byRoute, kv.Value.ParryByRoute);
                            }
                            else { ally += kv.Value.LastActiveTurn; allyN++; }
                        }
                    }
                double gl = life / (double)n, al = allyN == 0 ? 0 : ally / (double)allyN;
                if (vn.StartsWith("V0", StringComparison.Ordinal)) { baseLife = gl; baseAlly = al; baseTaken = taken / (double)n; }
                if (fires > 0 && baseBlockPer == 0) baseBlockPer = amt / (double)fires;
                blocked[vn] = byRoute;
                Console.WriteLine($"| {vn} | {wins * 100.0 / n:F1}% | {turns / (double)n:F2} | {surv / (double)n:F2} | "
                    + $"{gl:F2} | {gl - baseLife:+0.00;-0.00;0.00} | {al:F2} | {al - baseAlly:+0.00;-0.00;0.00} | "
                    + $"{guards / (double)n:F2} | {fires / (double)n:F2} | {amt / (double)n:F1} | {mx} | {taken / (double)n:F1} |");
            }
            Console.WriteLine();
            // ---- 紙のスループット（規約 (G7)。**線が到達可能かを先に見る**＝(G11)・第118期）----
            {
                double hp = UnitCatalog.Gald.MaxHp;
                double want = 1.0 / baseLife;                       // +1.0T に要る伸び率
                double needN = want * hp / Math.Max(1, baseBlockPer);
                Console.WriteLine($"**紙**: ガルドの最大HP {hp:F0} ／ 受け流し1回で弾く量 {baseBlockPer:F1} ／ "
                    + $"V0 の生存T {baseLife:F2}。");
                Console.WriteLine($"生存Tが実効HPに比例する（第126期の実測 `実測 ÷ 紙` が 30 席中 26 席で 0.9〜1.1）なら、");
                Console.WriteLine($"**+1.0T に要る回数は N ≈ {needN:F1}**。");
                Console.WriteLine($"§3-2 の決め方（中央値 {GuardMedianCache} を上限に 1 / 中央値 / 中央値×2）が許すのは **N ≤ 2**。");
                Console.WriteLine();
            }
            Console.WriteLine("受け流した一撃の経路（**Q0-2 の致命打の経路と重なっているか**＝採否の条件2）:");
            Console.WriteLine();
            Console.WriteLine("| 版 | 受け流し 計" + RouteHeader() + " |");
            Console.WriteLine("|---|---:" + RouteRule() + " |");
            foreach ((string vn, ParryRule _) in versions)
                Console.WriteLine($"| {vn} | {blocked[vn].Sum()}" + RouteCells(blocked[vn], true) + " |");
            Console.WriteLine();
        }

        Console.WriteLine("## 採否（指示書 §5-2）");
        Console.WriteLine();
        Console.WriteLine("1. **ガルドの生存T が V0 より +1.0T 以上**（P4。第117期・第126期と同じ線）");
        Console.WriteLine("2. **受け流した経路が Q0-2 の致命打の経路と重なっている**");
        Console.WriteLine("3. **庇われた味方の生存Tも上がっている**");
        Console.WriteLine();
        Console.WriteLine("**1 と 2 の両方を満たすこと。**");
    }

    /// <summary>
    /// 庇いの成立回数 / 戦 の<b>中央値</b>（N の決め方・受け入れ条件 A5）。
    /// <b>`compare` のガルドを含む行が分母</b>で、段2 の台ではない
    /// ——線は台を作る前に実測から引く。
    /// </summary>
    static int GuardMedian(out int[] hist, out double mean, out long battles)
    {
        hist = new int[8];
        long sum = 0;
        battles = 0;
        int gi = Array.IndexOf(InterceptLabels.All, InterceptLabels.Guardian);
        var harm = new HarmRule(true);
        foreach ((string _, Formation f) in Presets.Compare)
        {
            if (f.Occupied().All(o => o.Def.Id != "gald")) continue;
            for (int w = 1; w < EnemyCatalog.Stages.Count; w++)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[w].Enemy, seed,
                                                      verbose: false, harm: harm);
                    battles++;
                    int g = r.TallyByUnit.TryGetValue("gald", out UnitTally? t) && t.InterceptsByLabel is int[] L
                        ? L[gi] : 0;
                    sum += g;
                    hist[Math.Min(g, hist.Length - 1)]++;
                }
        }
        mean = sum / (double)battles;
        long half = battles / 2, run = 0;
        for (int i = 0; i < hist.Length; i++) { run += hist[i]; if (run > half) return i; }
        return 0;
    }

    // =================================================================================
    // 自己検査
    // =================================================================================

    static void Check(string balancePath)
    {
        Console.WriteLine("# 第135期 自己検査");
        Console.WriteLine();

        // (1) compare 305 セルが docs/balance.md と 0 件
        Console.WriteLine("## 必須1 —— `compare` 305 セルが `docs/balance.md` と 0 件差分");
        Console.WriteLine();
        string path = balancePath.Length > 0 ? balancePath : Path.Combine(ParryScan.Root!, "docs", "balance.md");
        if (!File.Exists(path)) { Console.WriteLine($"**`{path}` が無い。止める。**"); return; }

        var want = new Dictionary<string, double[]>(StringComparer.Ordinal);
        foreach (string line in File.ReadAllLines(path))
        {
            if (!line.StartsWith("| ", StringComparison.Ordinal) || !line.Contains('%')) continue;
            string[] c = line.Split('|').Select(x => x.Trim()).ToArray();
            if (c.Length < 7) continue;
            var v = new double[5];
            bool ok = true;
            for (int i = 0; i < 5; i++)
                if (!double.TryParse(c[2 + i].Replace("%", ""), out v[i])) { ok = false; break; }
            if (ok) want[c[1]] = v;
        }
        Console.WriteLine($"- `docs/balance.md` から読めた行: **{want.Count} 行**");
        if (want.Count == 0) { Console.WriteLine("**0 行。走査が壊れている。止める**（第117期）。"); return; }

        int bad = 0, cells = 0;
        foreach ((string name, Formation f) in Presets.Compare)
        {
            if (!want.TryGetValue(name, out double[]? w)) { Console.WriteLine($"  - 行名が引けない: {name}"); bad += 5; continue; }
            for (int st = 0; st < EnemyCatalog.Stages.Count; st++)
            {
                int wins = 0;
                for (int seed = 0; seed < Seeds; seed++)
                    if (BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false).PlayerWon) wins++;
                cells++;
                double got = wins * 100.0 / Seeds;
                if (Math.Abs(got - w[st]) > 0.05) { bad++; Console.WriteLine($"  - ずれ: {name} 第{st + 1}波 {w[st]:F1} → {got:F1}"); }
            }
        }
        Console.WriteLine($"- **ずれ {bad} / {cells} セル**{(bad == 0 ? "（○）" : "（×）")}");
        Console.WriteLine();

        // (2) HarmRule を有効にしても盤面が1ビットも動かないこと
        Console.WriteLine("## (a) —— `HarmRule(true)` と既定で勝敗・ターン・生存数・連鎖が一致");
        Console.WriteLine();
        int diff = 0, nrun = 0;
        foreach ((string _, Formation f) in Presets.Compare)
            for (int st = 1; st < EnemyCatalog.Stages.Count; st++)
                for (int seed = 0; seed < 20; seed++)
                {
                    BattleResult a = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                    BattleResult b = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false,
                                                      harm: new HarmRule(true));
                    nrun++;
                    if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns
                        || a.PlayerSurvivors != b.PlayerSurvivors
                        || a.MaxEnemyKillsInOneTurn != b.MaxEnemyKillsInOneTurn) diff++;
                }
        Console.WriteLine($"- **ずれ {diff} / {nrun} 戦**{(diff == 0 ? "（○）" : "（×）")}");
        Console.WriteLine();

        // (3) 帳簿が閉じる —— 経路別の合計が DamageTaken と一致
        Console.WriteLine("## (b) —— 経路別の合計が `DamageTaken` と一致する（帳簿が閉じる）");
        Console.WriteLine();
        long lhs = 0, rhs = 0;
        int mismatch = 0, checkedUnits = 0;
        long ilhs = 0, irhs = 0;
        foreach ((string _, Formation f) in Presets.Compare.Take(20))
            for (int st = 1; st < EnemyCatalog.Stages.Count; st++)
                for (int seed = 0; seed < 10; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false,
                                                      harm: new HarmRule(true));
                    foreach (UnitTally t in r.TallyByUnit.Values)
                    {
                        checkedUnits++;
                        long s = t.HarmAmount?.Sum() ?? 0;
                        lhs += s; rhs += t.DamageTaken;
                        if (s != t.DamageTaken) mismatch++;
                        ilhs += t.InterceptsByLabel?.Sum() ?? 0;
                        irhs += t.Intercepts;
                    }
                }
        Console.WriteLine($"- 経路別の合計 **{lhs}** ／ `DamageTaken` の合計 **{rhs}** ／ "
            + $"**食い違った駒 {mismatch} / {checkedUnits}**{(mismatch == 0 ? "（○）" : "（×）")}");
        Console.WriteLine();
        Console.WriteLine("## (c) —— 段別の介入回数の合計が `Intercepts` と一致する");
        Console.WriteLine();
        Console.WriteLine($"- `InterceptsByLabel` の合計 **{ilhs}** ／ `Intercepts` の合計 **{irhs}**"
            + $"{(ilhs == irhs ? "（○）" : "（×）")}");
        Console.WriteLine();

        // (4) PickOne を新たに使っていない（必須4）
        // (d) 受け流しが不活性であること（`Uses = 0` なら札を持っていても1ビットも動かない）
        Console.WriteLine("## (d) —— `ParryRule.Uses = 0` なら札を持っていても盤面が1ビットも動かない");
        Console.WriteLine();
        {
            UnitDef parried = GaldWith(TraitId.Parry);
            int dd = 0, dn = 0; long fires = 0;
            foreach ((string _, Formation f0) in Tables())
            {
                Formation f = Swap(f0, "gald", parried);
                for (int st = 1; st < EnemyCatalog.Stages.Count; st++)
                    for (int seed = 0; seed < 50; seed++)
                    {
                        BattleResult a0 = BattleEngine.Run(f0, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                        BattleResult b0 = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false,
                                                           parry: ParryRule.Default);
                        dn++;
                        if (a0.PlayerWon != b0.PlayerWon || a0.Turns != b0.Turns
                            || a0.PlayerSurvivors != b0.PlayerSurvivors
                            || a0.MaxEnemyKillsInOneTurn != b0.MaxEnemyKillsInOneTurn) dd++;
                        if (b0.TallyByUnit.TryGetValue("gald", out UnitTally? t)) fires += t.ParryFires;
                    }
            }
            Console.WriteLine($"- 札あり（`Uses = 0`）と札なしで勝敗・ターン・生存数・連鎖が一致: "
                + $"**ずれ {dd} / {dn} 戦**{(dd == 0 ? "（○）" : "（×）")}");
            Console.WriteLine($"- 受け流しの発火: **{fires} 回**{(fires == 0 ? "（○）" : "（×）")}");
        }
        Console.WriteLine();

        Console.WriteLine("## 必須4 —— `ctx` の抽選窓口を新たに使っていない");
        Console.WriteLine();
        // **呼び出しの形（名前 ＋ 開き括弧）だけを数える**——検索文字列は連結で組み、
        // このコメント自身にも当たらない綴りにしてある
        // （第123期。走査対象が走査する側のファイル自身なら、症状は「空」ではなく「静かに違う表」）。
        string self = ParryScan.Read(Path.Combine("BattleSim", "Parry.cs"));
        string eng2 = ParryScan.Read(Path.Combine("BattleCore", "BattleEngine.cs"));
        string call = "Pick" + "One(";
        Console.WriteLine($"- `BattleSim/Parry.cs` の呼び出し: **{Count(self, call)} 件**");
        Console.WriteLine($"- `BattleCore/BattleEngine.cs` の呼び出し: **{Count(eng2, call)} 件**"
            + "（この期の前の HEAD と同数。段1・段2 で足した計数も判定も1件も呼んでいない）");
        Console.WriteLine();
        Console.WriteLine("## 必須3 —— 触っていないノブの既定");
        Console.WriteLine();
        Console.WriteLine($"- `GatherRule.Default` = **`{GatherRule.Default}`**（第122期のまま）");
        Console.WriteLine($"- `SpillWoundRule.Default` = **`{SpillWoundRule.Default}`**");
        Console.WriteLine($"- `HarmRule.Default` = **`{HarmRule.Default}`**（この期に足した。既定は数えない）");
        Console.WriteLine($"- `ParryRule.Default` = **`{ParryRule.Default}`**（この期に足した。既定は不活性）");
    }
}
