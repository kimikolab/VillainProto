using BattleCore;
using static Common;
using CW = CheckWaveDiag;

// =====================================================================================
// relic —— 第270期「レリックの器と最初の札」。
// 指示書は design/PHASE270_RELIC_FOUNDATION_SPEC.md ／ 報告は design/PHASE270_RELIC_FOUNDATION.md。
// 器は `Formation.SetRelic`（枠ごとに1枚）・札は `RelicCatalog`（BattleCore/Relics.cs）。
//
//     dotnet run --project BattleSim -c Release 0 relic p0              # 札の一覧・読み手の在籍（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 relic check           # 器の自己検査（付与・1枠1枚・重複・発火順・付けない枠の不変）
//     dotnet run --project BattleSim -c Release 0 relic sweep           # 気配の確認: ボスの規定形 × 7台 × 札7枚 × 付け先の規則2つ（seed 0..199）
//     dotnet run --project BattleSim -c Release 0 relic keep            # 個性の保存: 移動の台のヨミに軋む足を付けて、ヨミの軋み・与ダメが残るか
//     dotnet run --project BattleSim -c Release 0 relic log <台> <札> <規則> [seed]   # 1戦のログ（札は日本語名・規則は 攻 ／ 前）
//
// **測るのは向き（札の文面と合っているか）まで。** 固有の勝者探し・混成率・総当たりは第271期。
// **レリックの測定結果は転生の評価（`roster_audit`・転生候補の棚）に書き戻さない**（指示書 §0）。
// =====================================================================================
static partial class RelicDiag
{
    const int Seeds = 200;
    const int Window = CW.BossTurns;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        switch (mode)
        {
            case "p0": P0(); return;
            case "check": Check(); return;
            case "sweep": Sweep(); return;
            case "keep": Keep(); return;
            case "p0j": P0Judge(); return;               // 第271期
            case "grid": Grid(); return;                 // 第271期
            case "junk": Junk(); return;                 // 第271期
            case "mainwin": MainWin(); return;           // 第271期（参考）
            case "log": LogOne(args.Length > 3 ? args[3] : "移動", args.Length > 4 ? args[4] : "軋む足", args.Length > 5 ? args[5] : "攻", args.Length > 6 ? int.Parse(args[6]) : 0); return;
            default: Console.WriteLine("relic: モードは p0 / check / sweep / keep / log ／ p0j / grid / junk（第271期）。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 付け先の規則（固定・結果を見て変えない）。
    //   攻: 素の攻撃力が最大の枠（同値は枠番号の小さいほう）——その台の主砲
    //   前: 枠 0（前1）——最も殴られる・押される席の代表
    // ---------------------------------------------------------------------------------
    static readonly string[] Rules = { "攻", "前" };
    static int SlotBy(Formation f, string rule) => rule switch
    {
        "攻" => f.Occupied().OrderByDescending(o => o.Def.Attack).ThenBy(o => o.Slot).First().Slot,
        "前" => 0,
        _ => throw new ArgumentException(rule),
    };

    static RelicInfo RelicByName(string n) => RelicCatalog.All.First(r => r.Name == n);

    /// <summary>札が発火した印（ログの行）。保持者の名前で絞る（弾性はハネの素の札と同じ文なので名前が要る）。</summary>
    static bool Fired(TraitId id, string holder, string line) => id switch
    {
        TraitId.RelicCreak => line.Contains($"{holder} の軋む足（"),
        TraitId.RelicFireArrow => line.Contains($"{holder} の火付けの矢が"),
        TraitId.RelicVenomStep => line.Contains($"{holder} の毒の足跡が"),
        TraitId.Spring => line.Contains($"{holder} が殴ってきた"),
        TraitId.RelicWilt => line.Contains($"{holder} の心が萎えた"),
        TraitId.RelicNumbStep => line.Contains($"{holder} は足が痺れて転んだ"),
        TraitId.RelicHarden => line.Contains($"{holder} は身を固めた"),
        _ => false,
    };

    // ---------------------------------------------------------------------------------
    // 集計（台本を読むだけ・盤面は動かさない）。
    // ---------------------------------------------------------------------------------
    sealed class Agg
    {
        public long N, Wins, Kill, KillT, ToHero, HeroDealt, Healed, Fires, HolderDealt, FirstDeath, FirstDeathT;
        public void Merge(Agg o)
        {
            N += o.N; Wins += o.Wins; Kill += o.Kill; KillT += o.KillT; ToHero += o.ToHero; HeroDealt += o.HeroDealt; Healed += o.Healed;
            Fires += o.Fires; HolderDealt += o.HolderDealt; FirstDeath += o.FirstDeath; FirstDeathT += o.FirstDeathT;
        }
        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e, int holderIx, TraitId? relic)
        {
            N++;
            int hero = e[0].InstanceId;
            var mine = p.Select(u => u.InstanceId).ToHashSet();
            UnitState? holder = holderIx < p.Count ? p[holderIx] : null;
            int? hid = holder?.InstanceId;
            int? killT = null, firstT = null;
            foreach (var x in r.Events)
            {
                switch (x.Kind)
                {
                    case BattleEventKind.Damage when x.TargetId == hero && x.Turn <= Window: ToHero += x.Amount; if (hid is int h0 && x.ActorId == h0) HolderDealt += x.Amount; break;
                    case BattleEventKind.Damage when x.ActorId == hero && x.TargetId is int t && mine.Contains(t) && x.Turn <= Window: HeroDealt += x.Amount; break;
                    case BattleEventKind.Heal when x.TargetId is int hh && mine.Contains(hh) && x.Turn <= Window: Healed += x.Amount; break;
                    case BattleEventKind.Death when x.TargetId is int d:
                        if (d == hero) killT ??= x.Turn;
                        if (mine.Contains(d)) firstT ??= x.Turn;
                        break;
                }
            }
            if (relic is TraitId id && holder is not null)
                Fires += r.Log.Count(l => Fired(id, holder.Def.Name, l.Text));
            if (r.PlayerWon) Wins++;
            if (killT is int kt) { Kill++; KillT += kt; }
            if (firstT is int ft) { FirstDeath++; FirstDeathT += ft; }
        }
        public double Win => 100.0 * Wins / N;
        public double PerN(long x) => (double)x / N;
        public string KillTs => Kill == 0 ? "—" : $"T{(double)KillT / Kill:F1}";
    }

    static Func<List<UnitState>> Boss => () => BattleEngine.MaterializeEnemy(EnemyCatalog.BossRegularWave, EnemyScaleRule.None);

    /// <summary><paramref name="frame"/> は付け先の枠（0〜4）。駒は戦闘中に席を動くので、席ではなく `Materialize` の並び（枠の昇順）で引く。</summary>
    static Agg Measure(Formation f, int frame, TraitId? relic)
    {
        int holderIx = f.Occupied().TakeWhile(o => o.Slot != frame).Count();
        var parts = new Agg[Seeds];
        Parallel.For(0, Seeds, i =>
        {
            var (r, p, e) = CW.Fight(f, Boss, i, verbose: true);
            var a = new Agg(); a.Take(r, p, e, holderIx, relic); parts[i] = a;
        });
        var all = new Agg();
        foreach (var a in parts) all.Merge(a);
        return all;
    }

    static string Seats(Formation f) => string.Join(" ／ ", f.Occupied().Select(o =>
        f.Shape.FrameNames[o.Slot] + ":" + o.Def.Name + (f.RelicAt(o.Slot) is TraitId r ? $"〔{RelicCatalog.Info(r).Name}〕" : "")));

    // ---------------------------------------------------------------------------------
    // p0: 札の一覧と読み手の在籍（戦闘0回）。
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        Console.WriteLine("# relic p0 —— 第270期 レリックの一覧（戦闘0回）");
        Console.WriteLine();
        Console.WriteLine("| 札 | 種別 | 文面 | 橋 ／ 読み手 | TraitId | 素の保持者（`Everyone`） |");
        Console.WriteLine("|---|---|---|---|---|---|");
        foreach (var r in RelicCatalog.All)
        {
            var holders = UnitCatalog.Everyone.Where(u => u.Traits.Contains(r.Id)).Select(u => u.Name).Distinct().ToList();
            Console.WriteLine($"| {r.Name} | {r.Kind} | {r.Text} | {r.Bridge} | `{r.Id}` | {(holders.Count == 0 ? "0 枚" : string.Join("・", holders))} |");
        }
        Console.WriteLine();
        Console.WriteLine("## ゴミの札の読み手（`UnitCatalog.All` に居るか）");
        Console.WriteLine();
        Console.WriteLine($"- 萎える心 → 逆しま（`Perverse`）: {string.Join("・", UnitCatalog.All.Where(u => u.Traits.Contains(TraitId.Perverse)).Select(u => u.Name))}");
        Console.WriteLine($"- 痺れる足 → 号令（`Rally`）・目覚まし（`Reveille`）: {string.Join("・", UnitCatalog.All.Where(u => u.Traits.Contains(TraitId.Rally) || u.Traits.Contains(TraitId.Reveille)).Select(u => u.Name))}");
        Console.WriteLine($"- （参考）据え（`Bulwark`）の保持者: {UnitCatalog.All.Count(u => u.Traits.Contains(TraitId.Bulwark))} 枚");
        Console.WriteLine();
        Console.WriteLine("## 7台の付け先（規則 攻 ／ 前）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 編成 | 攻 | 前 |");
        Console.WriteLine("|---|---|---|---|");
        foreach (string b in CW.Boards7)
        {
            var f = CW.BoardOf(b);
            Console.WriteLine($"| {b} | {Seats(f)} | {f[SlotBy(f, "攻")]!.Name} | {f[SlotBy(f, "前")]?.Name ?? "（空き）"} |");
        }
    }

    // ---------------------------------------------------------------------------------
    // check: 器の自己検査。
    // ---------------------------------------------------------------------------------
    static void Check()
    {
        int pass = 0, fail = 0;
        void Ok(string name, bool ok, string detail = "")
        {
            if (ok) pass++; else fail++;
            Console.WriteLine($"- [{(ok ? "○" : "×")}] {name}{(detail.Length > 0 ? $"（{detail}）" : "")}");
        }
        static bool SameLog(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.Log.Select(l => l.Text).SequenceEqual(b.Log.Select(l => l.Text));
        Console.WriteLine("# relic check —— 第270期 器の自己検査");
        Console.WriteLine();

        var rows = CompareBuilds();
        {
            // (a) 付けない枠は従来と完全同一: 札の列が `Resolve(def.Traits)` と同じ実体の並び・Relic は null。
            bool same = true; int n = 0;
            foreach (var (_, f) in rows)
                foreach (var u in BattleEngine.Materialize(f, BattleContext.PlayerTeam))
                {
                    same &= u.Relic is null && u.Traits.SequenceEqual(TraitCatalog.Resolve(u.Def.Traits)); n++;
                }
            Ok("(a) 札を付けない編成: 全駒の特性の列が素の札と同一・`Relic` は null（`compare` の全行）", same, $"{rows.Length} 行・{n} 駒");
        }
        {
            // (b) 付けて外した編成（SetRelic → null）は付けない編成と台本が一致。
            bool same = true; int n = 0;
            foreach (string b in CW.Boards7)
            {
                var f0 = CW.BoardOf(b);
                var f1 = f0.WithRelic(0, TraitId.RelicCreak).WithRelic(0, null);
                for (int s = 0; s < 8; s++) { same &= SameLog(CW.Fight(f0, Boss, s).R, CW.Fight(f1, Boss, s).R); n++; }
            }
            Ok("(b) 付けて外した編成は付けない編成と台本一致（7台 × seed 0..7・ボスの規定形）", same, $"{n} 戦");
        }
        {
            // (c) レリックでない札は弾く。
            bool threw = false;
            try { Formation.Build(front1: UnitCatalog.Gan).SetRelic(0, TraitId.Rage); } catch (ArgumentException) { threw = true; }
            Ok("(c) `RelicCatalog` に無い札（被弾強化 `Rage`）は `SetRelic` が例外", threw);
        }
        {
            // (d) 1枠1枚: 2枚目は置き換え（積まない）・札は末尾・`Def.Traits` は不変。
            var f = Formation.Build(front1: UnitCatalog.Gan);
            f.SetRelic(0, TraitId.RelicCreak);
            f.SetRelic(0, TraitId.RelicFireArrow);
            var u = BattleEngine.Materialize(f, BattleContext.PlayerTeam)[0];
            bool ok = u.Traits.Count == UnitCatalog.Gan.Traits.Count + 1
                && u.Traits[^1].Id == TraitId.RelicFireArrow && !u.HasTrait(TraitId.RelicCreak)
                && u.Traits.Take(UnitCatalog.Gan.Traits.Count).Select(t => t.Id).SequenceEqual(UnitCatalog.Gan.Traits)
                && UnitCatalog.Gan.Traits.SequenceEqual(new[] { TraitId.Rally, TraitId.Reveille }) && u.Relic == TraitId.RelicFireArrow;
            Ok("(d) 1枠1枚: 2枚目は置き換え・札は素の札の後ろ（発火順は素の札が先）・`Def.Traits` は不変", ok,
                string.Join(", ", u.Traits.Select(t => t.Id)));
        }
        {
            // (e) 重複: ハネ（素の札に Spring）に弾性（Spring）を付けても札は増えず、台本は付けないときと一致。
            var f0 = CW.BoardOf("移動");
            int hs = f0.Occupied().First(o => o.Def.Id == "hane").Slot;
            var f1 = f0.WithRelic(hs, TraitId.Spring);
            var u = BattleEngine.Materialize(f1, BattleContext.PlayerTeam).First(x => x.Def.Id == "hane");
            bool same = u.Traits.Count(t => t.Id == TraitId.Spring) == 1 && u.Traits.Count == u.Def.Traits.Count && u.Relic == TraitId.Spring;
            for (int s = 0; s < 8; s++) same &= SameLog(CW.Fight(f0, Boss, s).R, CW.Fight(f1, Boss, s).R);
            Ok("(e) 重複: 素の札と同じ札（ハネ × 弾性）は積まない・台本は付けないときと一致（seed 0..7）", same);
        }
        {
            // (f) Clone は札も写す・元の編成は WithRelic で変わらない。
            var f0 = CW.BoardOf("雷");
            var f1 = f0.WithRelic(2, TraitId.RelicHarden);
            Ok("(f) `WithRelic` は写しにだけ付ける・`Clone` は札も写す", f0.RelicAt(2) is null && !f0.HasRelics && f1.Clone().RelicAt(2) == TraitId.RelicHarden);
        }
        {
            // (g) 札は素の駒の保持者 0 枚（弾性＝Spring はハネの素の札なので除く）。
            var own = RelicCatalog.All.Where(r => r.Id != TraitId.Spring).Select(r => r.Id).ToHashSet();
            int holders = UnitCatalog.Everyone.Count(u => u.Traits.Any(own.Contains));
            Ok("(g) 新しい6枚の札の素の保持者がロスター（`Everyone`）に 0 枚", holders == 0, $"{holders} 枚");
        }
        {
            // (h) ゴミの札の読み手がロスター（`All`）に居る（指示書 §0 原則3）。
            bool utsu = UnitCatalog.All.Any(u => u.Traits.Contains(TraitId.Perverse));
            bool gan = UnitCatalog.All.Any(u => u.Traits.Contains(TraitId.Rally));
            Ok("(h) ゴミの札の読み手が `All` に居る（萎える心 → ウツ・痺れる足 → ガン）", utsu && gan);
        }
        {
            // (i) 身を固める: 最大HP が素の攻撃力 × 3 だけ増え、攻撃力は 0。同じ駒で2戦しても足すのは1度だけ（会戦で駒が持ち越される形）。
            var f = Formation.Build(front1: UnitCatalog.Gan).WithRelic(0, TraitId.RelicHarden);
            var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var u = p[0];
            int want = UnitCatalog.Gan.MaxHp + UnitCatalog.Gan.Attack * RelicHardenTrait.HpPerAtk;
            Func<List<UnitState>> weak = () => BattleEngine.Materialize(Formation.Build(front1: UnitCatalog.Spore), BattleContext.EnemyTeam, EnemyScaleRule.None);
            BattleEngine.Run(p, weak(), 0, verbose: false);
            int after1 = u.MaxHp, atk1 = u.CurrentAttack;
            if (u.IsAlive) BattleEngine.Run(p, weak(), 1, verbose: false);
            Ok("(i) 身を固める: 最大HP ＝ 素 ＋ 攻 × 3・攻撃力 0・2戦目で重ねない", after1 == want && atk1 == 0 && u.MaxHp == want,
                $"最大HP {UnitCatalog.Gan.MaxHp} → {after1} → {u.MaxHp}（期待 {want}）・攻 {atk1}");
        }
        {
            // (j) 7枚それぞれが、規則「攻」「前」のどちらかで7台のどこかで1度以上発火する（札が繋がっているか＝鎖の門）。
            var miss = new List<string>();
            foreach (var r in RelicCatalog.All)
            {
                long fires = 0;
                foreach (string b in CW.Boards7)
                    foreach (string rule in Rules)
                    {
                        var f0 = CW.BoardOf(b); int s0 = SlotBy(f0, rule);
                        var f = f0.WithRelic(s0, r.Id);
                        string name = f[s0]!.Name;
                        for (int s = 0; s < 4; s++) fires += CW.Fight(f, Boss, s).R.Log.Count(l => Fired(r.Id, name, l.Text));
                    }
                if (fires == 0) miss.Add(r.Name);
            }
            Ok("(j) 7枚とも発火する（7台 × 規則2つ × seed 0..3 のどこかで1度以上）", miss.Count == 0, miss.Count == 0 ? "" : "発火 0: " + string.Join("・", miss));
        }
        Console.WriteLine();
        Console.WriteLine($"合計 {pass + fail} 項目: ○ {pass} ／ × {fail}");
    }

    // ---------------------------------------------------------------------------------
    // sweep: 気配の確認（7台 × 札7枚 × 規則2つ・ボスの規定形・seed 0..199）。
    // ---------------------------------------------------------------------------------
    static void Sweep()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# relic sweep —— 第270期 気配の確認（ボスの規定形 × 7台 × 札7枚 × 付け先の規則2つ・倍率なし・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("付け先の規則（固定）: **攻** ＝ 素の攻撃力が最大の枠（同値は枠番号の小さいほう）／ **前** ＝ 枠0（前1）。各セルは札なしの同じ台との差。");
        Console.WriteLine($"指標: 勝率・勇者への与ダメ（{Window} ターンまで・1戦あたり）・倒しT・純実入り（勇者が入れた − 隊の回復・{Window} ターンまで）・発火（1戦あたり）・付けた駒の勇者への与ダメ（元の役割が残っているか）。");
        Console.WriteLine();

        var cells = new List<(string B, string Rule, RelicInfo? R)>();
        foreach (string b in CW.Boards7)
        {
            cells.Add((b, "", null));
            foreach (string rule in Rules) foreach (var r in RelicCatalog.All) cells.Add((b, rule, r));
        }
        var res = new Dictionary<(string, string, TraitId?), Agg>();
        var holderBase = new Dictionary<(string, string), Agg>();
        foreach (var (b, rule, r) in cells)
        {
            var f0 = CW.BoardOf(b);
            if (r is null)
            {
                foreach (string ru in Rules) holderBase[(b, ru)] = Measure(f0, SlotBy(f0, ru), null);
                res[(b, "", null)] = holderBase[(b, Rules[0])];
                continue;
            }
            int s0 = SlotBy(f0, rule);
            res[(b, rule, r.Id)] = Measure(f0.WithRelic(s0, r.Id), s0, r.Id);
        }

        Console.WriteLine("## 表0 札なし（基準）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 勝率 | 勇者への与ダメ | 倒しT | 純実入り | 付け先 攻（与ダメ） | 付け先 前（与ダメ） |");
        Console.WriteLine("|---|--:|--:|--:|--:|---|---|");
        foreach (string b in CW.Boards7)
        {
            var a = res[(b, "", null)]; var f0 = CW.BoardOf(b);
            Console.WriteLine($"| {b} | {a.Win:F1} | {a.PerN(a.ToHero):F0} | {a.KillTs} | {a.PerN(a.HeroDealt - a.Healed):F0} | {f0[SlotBy(f0, "攻")]!.Name}（{holderBase[(b, "攻")].PerN(holderBase[(b, "攻")].HolderDealt):F0}） | {f0[SlotBy(f0, "前")]!.Name}（{holderBase[(b, "前")].PerN(holderBase[(b, "前")].HolderDealt):F0}） |");
        }
        Console.WriteLine();

        foreach (string rule in Rules)
        {
            Console.WriteLine($"## 表1-{rule} 札ごとの差（規則 {rule}・セルは **倒しT の差** ／ 勝率の差 pt。倒しT は倒した戦の平均で、負の差 ＝ 早く倒せた）");
            Console.WriteLine();
            Console.WriteLine("| 札 | " + string.Join(" | ", CW.Boards7) + " | 向き（早い ／ 遅い ／ ±0 の台数・|差| < 0.1T を ±0） |");
            Console.WriteLine("|---|" + string.Concat(CW.Boards7.Select(_ => "--:|")) + "---|");
            foreach (var r in RelicCatalog.All)
            {
                int up = 0, down = 0, flat = 0;
                var parts = CW.Boards7.Select(b =>
                {
                    var a = res[(b, rule, r.Id)]; var z = res[(b, "", null)];
                    double w = a.Win - z.Win;
                    if (a.Kill == 0 || z.Kill == 0) return $"— ／ {w:+0.0;−0.0;±0.0}";
                    double d = (double)a.KillT / a.Kill - (double)z.KillT / z.Kill;
                    if (Math.Abs(d) < 0.1) flat++; else if (d < 0) up++; else down++;
                    return $"{d:+0.0;−0.0;±0.0} ／ {w:+0.0;−0.0;±0.0}";
                }).ToList();
                Console.WriteLine($"| {r.Name} | " + string.Join(" | ", parts) + $" | 早{up} ／ 遅{down} ／ ±0 {flat} |");
            }
            Console.WriteLine();
            Console.WriteLine($"## 表3-{rule} 勇者への与ダメの差（{Window} ターンまで・1戦あたり）——**参考**。早く倒すほど勇者の回復が入らず合計は減り、負けた戦は窓いっぱい回るので増える（交絡する）");
            Console.WriteLine();
            Console.WriteLine("| 札 | " + string.Join(" | ", CW.Boards7) + " |");
            Console.WriteLine("|---|" + string.Concat(CW.Boards7.Select(_ => "--:|")));
            foreach (var r in RelicCatalog.All)
                Console.WriteLine($"| {r.Name} | " + string.Join(" | ", CW.Boards7.Select(b =>
                {
                    var a = res[(b, rule, r.Id)]; var z = res[(b, "", null)];
                    return $"{a.PerN(a.ToHero) - z.PerN(z.ToHero):+0;−0;±0}";
                })) + " |");
            Console.WriteLine();
            Console.WriteLine($"## 表2-{rule} 発火（1戦あたり）／ 付けた駒の勇者への与ダメ（札なし → 札あり）");
            Console.WriteLine();
            Console.WriteLine("| 札 | " + string.Join(" | ", CW.Boards7) + " |");
            Console.WriteLine("|---|" + string.Concat(CW.Boards7.Select(_ => "--:|")));
            foreach (var r in RelicCatalog.All)
                Console.WriteLine($"| {r.Name} | " + string.Join(" | ", CW.Boards7.Select(b =>
                {
                    var a = res[(b, rule, r.Id)]; var z = holderBase[(b, rule)];
                    return $"{a.PerN(a.Fires):F1} ／ {z.PerN(z.HolderDealt):F0} → {a.PerN(a.HolderDealt):F0}";
                })) + " |");
            Console.WriteLine();
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // keep: 個性の保存（指示書 §4）。移動の台のヨミに軋む足 → ヨミの軋み（+9 ／ +22・割り込み）の回数と与ダメが残るか。
    // ---------------------------------------------------------------------------------
    static void Keep()
    {
        Console.WriteLine("# relic keep —— 第270期 個性の保存（移動の台・ボスの規定形・seed 0..199）");
        Console.WriteLine();
        var f0 = CW.BoardOf("移動");
        int ys = f0.Occupied().First(o => o.Def.Id == "yomi").Slot;
        Console.WriteLine("| 版 | ヨミの軋み（回/戦） | 軋む足（回/戦） | ヨミの勇者への与ダメ（/戦） | 隊の勇者への与ダメ（/戦） | 勝率 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        foreach (var (name, relic) in new (string, TraitId?)[] { ("札なし", null), ("ヨミ〔軋む足〕", TraitId.RelicCreak), ("ヨミ〔身を固める〕", TraitId.RelicHarden) })
        {
            var f = relic is TraitId r ? f0.WithRelic(ys, r) : f0;
            long own = 0, rel = 0, yd = 0, all = 0, wins = 0;
            var lockObj = new object();
            Parallel.For(0, Seeds, i =>
            {
                var (res, p, e) = CW.Fight(f, Boss, i);
                var y = p.First(u => u.Def.Id == "yomi"); int hero = e[0].InstanceId;
                long o1 = res.Log.Count(l => l.Text.Contains($"{y.Def.Name} は突き飛ばされるほど据わる"));
                long r1 = res.Log.Count(l => l.Text.Contains($"{y.Def.Name} の軋む足（"));
                long d1 = res.Events.Where(x => x.Kind == BattleEventKind.Damage && x.TargetId == hero && x.Turn <= Window && x.ActorId == y.InstanceId).Sum(x => (long)x.Amount);
                long a1 = res.Events.Where(x => x.Kind == BattleEventKind.Damage && x.TargetId == hero && x.Turn <= Window).Sum(x => (long)x.Amount);
                lock (lockObj) { own += o1; rel += r1; yd += d1; all += a1; if (res.PlayerWon) wins++; }
            });
            Console.WriteLine($"| {name} | {(double)own / Seeds:F2} | {(double)rel / Seeds:F2} | {(double)yd / Seeds:F0} | {(double)all / Seeds:F0} | {100.0 * wins / Seeds:F1} |");
        }
    }

    static void LogOne(string board, string relicName, string rule, int seed)
    {
        var f0 = CW.BoardOf(board);
        var r = RelicByName(relicName);
        var f = f0.WithRelic(SlotBy(f0, rule), r.Id);
        var (res, _, _) = CW.Fight(f, Boss, seed);
        Console.WriteLine($"# {board}（{Seats(f)}）× ボスの規定形 × seed {seed} → {(res.PlayerWon ? "勝ち" : "負け")} T{res.Turns}");
        foreach (var l in res.Log) Console.WriteLine(l.Text);
    }
}
