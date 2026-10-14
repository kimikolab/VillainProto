using System.Reflection;
using BattleCore;
using static Common;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using T323 = Tou323Diag;

// =====================================================================================
// credit341 —— 第341期【軽い期・数え方だけ】戦績の「仕掛け」の列: 粉塵爆発 ／ 焼をトウに、糸玉の放電をクグに。
// 指示書は design/PHASE341_CREDIT_COLUMNS_SPEC.md ／ 報告は design/PHASE341_CREDIT_COLUMNS.md。**規則を1つも変えない**（計数と戦績パネルの列だけ）。
//
//     dotnet run --project BattleSim -c Release 0 credit341 seed7            # §3 「試遊・感電 ポンの台」× ボス × seed 7（ポンが見た戦）の駒ごとの与 ／ 被 ／ 仕掛け
//     dotnet run --project BattleSim -c Release 0 credit341 avg              # §3 同じ台 × 近衛 ／ 大隊（seed 0..99 の平均）: 駒ごとの与(敵) と仕掛け(敵)
//     dotnet run --project BattleSim -c Release 0 credit341 check            # §4 自己検査
// =====================================================================================
static partial class Credit341Diag
{
    const int Seeds = 100;
    const string Tag = "第341期";
    internal const string PonRow = "試遊・感電 ポンの台";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        switch (mode)
        {
            case "seed7": Seed7(args.Length > 3 ? int.Parse(args[3]) : 7); return;
            case "avg": Avg(); return;
            case "check": Check(); return;
            default: Console.WriteLine("credit341: モードは seed7 / avg / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    internal static Formation Pon() => Presets.Versions.First(r => r.Name == PonRow).F;

    /// <summary>駒の表示名（`Def.Id` → 名前・召喚 ／ 糸玉も）。</summary>
    static string NameOf(string id, IEnumerable<UnitState> p, S287.Wave w)
    {
        var d = p.FirstOrDefault(u => u.Def.Id == id)?.Def ?? UnitCatalog.Everyone.FirstOrDefault(u => u.Id == id)
              ?? new[] { UnitCatalog.Spore, UnitCatalog.Fodder, UnitCatalog.SilkBall }.FirstOrDefault(u => u.Id == id);
        return d is null ? id : S307.Short(d);
    }

    // ---------------------------------------------------------------------------------
    // §3 seed 7
    // ---------------------------------------------------------------------------------
    static void Seed7(int seed)
    {
        var w = T323.WaveOf("boss");
        var p = BattleEngine.Materialize(Pon(), BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, w.Make(), seed, verbose: true);
        Console.WriteLine($"# {Tag} §3 「{PonRow}」（{S307.Seats(Pon())}）× ボス × seed {seed}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        Console.WriteLine();
        Console.WriteLine("与(敵) ／ 与(味) ／ 被 ／ 被(味) は戦績パネル（`docs/pulse.md`）と同じ出どころ。仕掛け(敵) ／ 仕掛け(味) は第341期の列（粉塵爆発 ／ 焼 → トウ・糸玉の放電 → クグ）。");
        Console.WriteLine("仕掛けで受けた ＝ 受けた側から見た、仕掛けの列に入ったダメージ（勇者の被(味) のうち仕掛けの列に入った量）。内訳 ＝ 爆発 ／ 焼 ／ 糸玉。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 陣営 | 与(敵) | 与(味) | 被 | 被(味) | 仕掛け(敵) | 仕掛け(味) | 内訳（爆発 ／ 焼 ／ 糸玉） | 仕掛けで受けた |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|---|--:|");
        var team = p.ToDictionary(u => u.Def.Id, u => u.TeamId);
        foreach (var (id, t) in r.TallyByUnit.OrderBy(x => team.TryGetValue(x.Key, out int tm) ? tm : 9).ThenBy(x => x.Key, StringComparer.Ordinal))
        {
            string side = team.TryGetValue(id, out int tm) ? (tm == BattleContext.PlayerTeam ? "味方" : "敵") : (id == "silkball" ? "糸玉" : "敵");
            Console.WriteLine($"| {NameOf(id, p, w)} | {side} | {t.DamageToEnemy} | {t.DamageToAlly} | {t.DamageTaken} | {t.TakenFromAlly} | {t.CreditToEnemy} | {t.CreditToAlly} | {t.Credit341Dust} ／ {t.Credit341Burn} ／ {t.Credit341Ball} | {t.Credit341Taken} |");
        }
    }

    // ---------------------------------------------------------------------------------
    // §3 近衛 ／ 大隊（seed 0..99 の平均）
    // ---------------------------------------------------------------------------------
    static void Avg()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var f = Pon();
        Console.WriteLine($"# {Tag} §3 「{PonRow}」（{S307.Seats(f)}）× 近衛 ／ 大隊・seed 0..{Seeds - 1}・1戦あたり");
        Console.WriteLine();
        Console.WriteLine("割合 ＝ 味方の与(敵) の合計に占める割合（与(敵) 列）と、与(敵) ＋ 仕掛け(敵) の合計に占める割合（手柄の列）。仕掛け(敵) は爆ぜた駒の与(味) ／ 被(味) にも入るので、手柄の列の合計は二重を含む。");
        foreach (string wn in new[] { "guard", "bat" })
        {
            var w = T323.WaveOf(wn);
            var parts = new Dictionary<string, UnitTally>[Seeds];
            int[] wins = new int[Seeds];
            Parallel.For(0, Seeds, s =>
            {
                var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                var r = BattleEngine.Run(p, w.Make(), s, verbose: false);
                wins[s] = r.PlayerWon ? 1 : 0;
                parts[s] = p.Select(u => u.Def.Id).Distinct().ToDictionary(id => id, id => r.TallyByUnit.GetValueOrDefault(id) ?? new UnitTally());
            });
            var ids = f.Occupied().Select(o => o.Def.Id).ToArray();
            var sum = ids.ToDictionary(id => id, _ => new UnitTally());
            foreach (var d in parts) foreach (var id in ids) sum[id].Add(d[id]);
            long dealt = ids.Sum(id => sum[id].DamageToEnemy), credit = ids.Sum(id => sum[id].DamageToEnemy + sum[id].CreditToEnemy);
            Console.WriteLine();
            Console.WriteLine($"## {w.Name}（勝率 {Pct(wins.Sum(), Seeds)}%）");
            Console.WriteLine();
            Console.WriteLine("| 駒 | 与(敵) | 割合 | 仕掛け(敵) | 仕掛け(味) | 与(敵) ＋ 仕掛け(敵) | 割合 | 仕掛け(敵) の内訳（爆発 ／ 焼 ／ 糸玉・敵味方の和） |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|---|");
            foreach (var (_, d) in f.Occupied())
            {
                var t = sum[d.Id];
                Console.WriteLine($"| {S307.Short(d)} | {Per(t.DamageToEnemy, Seeds)} | {Pct(t.DamageToEnemy, dealt)}% | {Per(t.CreditToEnemy, Seeds)} | {Per(t.CreditToAlly, Seeds)} | {Per(t.DamageToEnemy + t.CreditToEnemy, Seeds)} | {Pct(t.DamageToEnemy + t.CreditToEnemy, credit)}% | {Per(t.Credit341Dust, Seeds)} ／ {Per(t.Credit341Burn, Seeds)} ／ {Per(t.Credit341Ball, Seeds)} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §4 自己検査
    // ---------------------------------------------------------------------------------
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static readonly UnitDef Dummy = new()
    {
        Id = "dummy341", Name = "木偶", MaxHp = 5000, Attack = 1, Speed = 1, Pattern = AttackPattern.Single, Traits = Array.Empty<TraitId>(),
        PlusText = "", MinusText = "", Flavor = "",
    };
    static readonly MethodInfo DustM = typeof(BattleContext).GetMethod("DustExplode", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("DustExplode が見つからない");
    static readonly MethodInfo BurnM = typeof(BattleContext).GetMethod("PowderSelfBurn", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("PowderSelfBurn が見つからない");

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# credit341 check —— 第341期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        var foes = Formation.Build(front1: Dummy, front3: Dummy, center: Dummy, back1: Dummy, back3: Dummy);
        var mates = Formation.Build(front1: T323.Wall, front3: T323.Mate, center: UnitCatalog.Tou, back1: T323.Mate, back3: T323.Wall);

        // ---- 粉塵爆発: 隣の駒の HP の減りの合計 ＝ トウの仕掛け（爆発の分）・敵の隣は仕掛け(敵)、味方の隣は仕掛け(味)
        {
            var ctx = T323.Ctx(mates, foes, out var p, out var e);
            var tou = T323.Seat(p, 2);
            var hp0 = e.Concat(p).ToDictionary(u => u, u => u.Hp);
            DustM.Invoke(ctx, new object?[] { tou, T323.Seat(e, 0), 2, 2, tou });
            long foeDrop = e.Sum(u => hp0[u] - u.Hp);
            var t1 = T323.Tal(ctx, "tou");
            long dust1 = t1.Credit341Dust, en1 = t1.CreditToEnemy;
            var hp1 = p.ToDictionary(u => u, u => u.Hp);
            DustM.Invoke(ctx, new object?[] { tou, T323.Seat(p, 0), 1, 1, null });
            long allyDrop = p.Sum(u => hp1[u] - u.Hp);
            var t = T323.Tal(ctx, "tou");
            Expect("粉塵爆発: 隣の駒の HP の減りの合計 ＝ トウの仕掛け（爆発の分）——敵の受け手の隣は仕掛け(敵)、味方の受け手の隣は仕掛け(味)",
                foeDrop > 0 && allyDrop > 0 && en1 == foeDrop && dust1 == foeDrop && t.CreditToAlly == allyDrop && t.Credit341Dust == foeDrop + allyDrop && t.Credit341Burn == 0 && t.Credit341Ball == 0,
                $"敵の隣 {foeDrop} → 仕掛け(敵) {en1}・味方の隣 {allyDrop} → 仕掛け(味) {t.CreditToAlly}");
        }

        // ---- 焼: 弾けた本人の HP の減り ＝ トウの仕掛け（焼の分）
        {
            var ctx = T323.Ctx(mates, foes, out var p, out var e);
            var foe = T323.Seat(e, 1); var ally = T323.Seat(p, 1);
            int f0 = foe.Hp, a0 = ally.Hp;
            BurnM.Invoke(ctx, new object?[] { foe, 2, null });
            BurnM.Invoke(ctx, new object?[] { ally, 1, null });
            var t = T323.Tal(ctx, "tou");
            Expect("焼: 弾けた本人の HP の減り ＝ トウの仕掛け（焼の分）——敵は仕掛け(敵)・味方は仕掛け(味)",
                f0 - foe.Hp > 0 && t.CreditToEnemy == f0 - foe.Hp && t.CreditToAlly == a0 - ally.Hp && t.Credit341Burn == (f0 - foe.Hp) + (a0 - ally.Hp) && t.Credit341Dust == 0,
                $"敵 {f0 - foe.Hp} → {t.CreditToEnemy}・味方 {a0 - ally.Hp} → {t.CreditToAlly}");
        }

        // ---- 戦の中で: 爆発 ／ 焼 ／ 糸玉の仕掛けが、既存の計数（`Dx327Dealt` ／ `TdBurn*` ／ `SilkDealt`）と合う・クグに糸玉の分が入る
        {
            long dust = 0, dx = 0, burn = 0, td = 0, ball = 0, silk = 0, other = 0, taken = 0, credited = 0; int n = 0, diff = 0;
            foreach (string wn in new[] { "boss", "guard", "bat", "2", "5" })
            {
                var w = T323.WaveOf(wn);
                for (int sd = 0; sd < 10; sd++)
                {
                    var r1 = BattleEngine.Run(BattleEngine.Materialize(Pon(), BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                    var r0 = BattleEngine.Run(BattleEngine.Materialize(Pon(), BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                    n++;
                    if (r1.PlayerWon != r0.PlayerWon || r1.Turns != r0.Turns) diff++;
                    foreach (var (id, t) in r0.TallyByUnit)
                    {
                        if (id == "tou") { dust += t.Credit341Dust; burn += t.Credit341Burn; dx += t.Dx327Dealt?.Sum() ?? 0; td += t.TdBurnAlly + t.TdBurnFoe; }
                        else if (id == "kugu") { ball += t.Credit341Ball; silk += t.SilkDealt; }
                        else other += t.CreditToEnemy + t.CreditToAlly;
                        taken += t.Credit341Taken; credited += t.CreditToEnemy + t.CreditToAlly;
                    }
                }
            }
            Expect($"「{PonRow}」× 5 波 × seed 0..9（{n} 戦）: トウの爆発の分 ＝ `Dx327Dealt` の和・焼の分 ＝ `TdBurnAlly` ＋ `TdBurnFoe`・クグの糸玉の分 ≧ `SilkDealt`（糸を伝った分を含む）・ほかの駒の仕掛けは 0・受けた側の和 ＝ 仕掛けの和",
                dust == dx && dust > 0 && burn == td && ball >= silk && ball > 0 && other == 0 && taken == credited,
                $"爆発 {dust} ／ {dx}・焼 {burn} ／ {td}・糸玉 {ball} ／ {silk}・ほか {other}・受けた {taken} ／ {credited}");
            Expect($"同じ {n} 戦: verbose の有無で勝敗 ／ 決着T が同じ", diff == 0, $"違い {diff}");
        }

        // ---- トウ ／ クグのいない台で仕掛けの列が 0
        {
            long any = 0; int n = 0;
            foreach (var (name, f) in Presets.Playtest.Where(r => r.F.Occupied().All(o => o.Def.Id is not ("tou" or "kugu"))).Take(4))
                foreach (string wn in new[] { "boss", "guard", "2" })
                    for (int sd = 0; sd < 5; sd++)
                    {
                        var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), T323.WaveOf(wn).Make(), sd, verbose: false);
                        n++;
                        any += r.TallyByUnit.Values.Sum(t => t.CreditToEnemy + t.CreditToAlly + t.Credit341Taken);
                    }
            Expect($"トウ ／ クグのいない試遊の台 4 × ボス ／ 近衛 ／ 第2波 × seed 0..4（{n} 戦）: 仕掛けの列はすべて 0", any == 0 && n > 0, $"和 {any}");
        }

        // ---- 乱数を新たに引いていない
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            int engine = Now("BattleCore/BattleEngine.cs"), traits = Now("BattleCore/Traits.cs");
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show ec617b27:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int he = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, ht = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第340期（ec617b27）と同じ）", engine == he && traits == ht && he > 0, $"engine {he} → {engine}・traits {ht} → {traits}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
