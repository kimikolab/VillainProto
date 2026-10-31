using BattleCore;
using static Common;
using I = Inv348Diag;
using L = Lili416Diag;
using S307 = Som307Diag;

// nomi423 の続き（版を並べるモード）。
//     dotnet run --project BattleSim -c Release 0 nomi423 boards   # §2-3 ／ §2-4: 5 行 × {規定, NM-w, NM-w3} × 4 波・seed 0..99 ＋ ノミ → ミサの勝率 ＋ ボスの推移（NM-w ／ NM-w3）
//     dotnet run --project BattleSim -c Release 0 nomi423 mixed    # §2-5: 残りの 4 行 × {規定, NM-w} × 本編 5 波 ＋ 近衛 ／ 大隊・seed 0..199
//     dotnet run --project BattleSim -c Release 0 nomi423 seats    # §6 次の下見: 試遊・燃焼 ポンの台 ／ 試遊・自傷 × 4 波・席ごとの被弾（爆弾を置く席の候補）
//     dotnet run --project BattleSim -c Release 0 nomi423 log <0|w|w3> <行名の頭> <波の鍵 boss|guard|bat|s4|m1..m5> [seed]
//     dotnet run --project BattleSim -c Release 0 nomi423 check    # 自己検査
static partial class Nomi423Diag
{
    static readonly (string Key, string Label, UnitDef D)[] Vers = { ("0", "規定", UnitCatalog.Nomi), ("w", "NM-w", UnitCatalog.NomiNMw), ("w3", "NM-w3", UnitCatalog.NomiNMw3) };
    static readonly string[] RestRows = { "刻み×抉り (ノミ×エグ)", "刻み×断ち (ノミ×ナタ)", "見境 (ミサ×ソラ)", "毒×傷 (ノミ×グザ×ミオ)" };
    static readonly (string Key, I.W W)[] WaveKeys = new[] { ("boss", I.Boss), ("guard", I.Guard), ("bat", I.Bat), ("s4", I.StrongWaves[2]) }
        .Concat(Enumerable.Range(1, 5).Select(i => ($"m{i}", L.Mains[i - 1]))).ToArray();

    static partial void More(string mode, Func<int, string, string> a, ref bool handled)
    {
        handled = true;
        switch (mode)
        {
            case "boards": Boards(); break;
            case "mixed": Mixed(); break;
            case "seats": Seats(); break;
            case "log": Log(a(3, "w"), a(4, "刻み (ノミ単騎)"), a(5, "boss"), int.Parse(a(6, "0"))); break;
            case "check": Check(); break;
            default: handled = false; break;
        }
    }

    sealed class Fx
    {
        public long N, Wins, WinT, Fell, Dmg, Volleys, Shots, Writes, Peak, PeakMax, Reads, ReadAmt, Encore, YokeCut, Hits;
        public readonly Dictionary<string, long> Death = new(), DeathT = new();
        public void Add(Fx o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Fell += o.Fell; Dmg += o.Dmg; Volleys += o.Volleys; Shots += o.Shots; Writes += o.Writes;
            Peak += o.Peak; PeakMax = Math.Max(PeakMax, o.PeakMax); Reads += o.Reads; ReadAmt += o.ReadAmt; Encore += o.Encore; YokeCut += o.YokeCut; Hits += o.Hits;
            foreach (var (k, v) in o.Death) Death[k] = Death.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.DeathT) DeathT[k] = DeathT.GetValueOrDefault(k) + v;
        }
        public double Win => N == 0 ? double.NaN : 100.0 * Wins / N;
        public string WT => $"{F1(Win)}・{(Wins == 0 ? "—" : ((double)WinT / Wins).ToString("0.0"))}T";
        public string Fall => Death.Count == 0 ? "—" : string.Join("、", Death.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key} {Pc(kv.Value, N)}・T{Per1(DeathT[kv.Key], kv.Value)}"));
    }

    static Fx One(Formation f, I.W w, int seed, bool yoke = false)
    {
        var (r, p, e) = I.Raw(f, w, seed, verbose: yoke);
        var a = new Fx { N = 1 };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        foreach (var u in p.Where(u => u.LastDeathTurn > 0)) { a.Fell++; string k = Short(u.Def); a.Death[k] = a.Death.GetValueOrDefault(k) + 1; a.DeathT[k] = a.DeathT.GetValueOrDefault(k) + u.LastDeathTurn; }
        if (r.TallyByUnit.TryGetValue("nomi", out var t))
        {
            a.Dmg = t.DamageToEnemy; a.Volleys = t.PelletVolleys; a.Shots = t.PelletShots; a.Writes = t.WoundWrites; a.Reads = t.Wd423Reads; a.ReadAmt = t.Wd423ReadAmt; a.Encore = t.EncoreFires;
        }
        long pk = e.DistinctBy(u => u.Def.Id).Max(u => r.TallyByUnit.TryGetValue(u.Def.Id, out var tt) ? tt.Wd423Peak : 0);
        a.Peak = pk; a.PeakMax = pk;
        if (yoke)
        {
            int nid = p.First(u => u.Def.Id == "nomi").InstanceId;
            foreach (var ev in r.Events)
            {
                if (ev.Kind == BattleEventKind.Damage && ev.ActorId == nid && !ev.Relayed) a.Hits++;
                if (ev.Kind == BattleEventKind.Sealed && ev.ActorId == nid && ev.Text == SealedLabels.Yoke) a.YokeCut++;
            }
        }
        return a;
    }

    static Fx Measure(Formation f, I.W w, int n = Seeds, bool yoke = false)
    {
        var parts = new Fx[n];
        Parallel.For(0, n, s => parts[s] = One(f, w, s, yoke));
        var all = new Fx();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    static void Boards()
    {
        Console.WriteLine($"# 第423期 §4 —— ノミを含む 5 行 × {{{string.Join(", ", Vers.Select(v => v.Label))}}} × {string.Join(" ／ ", Four.Select(w => w.Name))}・seed 0..{Seeds - 1}");
        Console.WriteLine();
        var res = new Dictionary<(string, string, string), Fx>();
        var misa = new Dictionary<(string, string), Fx>();
        foreach (var row in MainRows)
            foreach (var w in Four)
            {
                foreach (var v in Vers) res[(row, w.Name, v.Key)] = Measure(Swap(Row(row), v.D), w, Seeds, yoke: w == I.StrongWaves[2]);
                misa[(row, w.Name)] = Measure(Swap(Row(row), UnitCatalog.Tome), w);
            }
        foreach (var row in MainRows)
        {
            Console.WriteLine($"## {row}（{S307.Seats(Row(row))}）");
            Console.WriteLine();
            Console.WriteLine("| 波 | 版 | 勝率・倒しT | 規定との差 | ミサとの差 | 倒れた ／戦 | 倒れた駒 |");
            Console.WriteLine("|---|---|---|--:|--:|--:|---|");
            foreach (var w in Four)
            {
                foreach (var v in Vers)
                {
                    var a = res[(row, w.Name, v.Key)];
                    Console.WriteLine($"| {w.Name} | {v.Label} | {a.WT} | {(v.Key == "0" ? "—" : Delta(a.Win - res[(row, w.Name, "0")].Win))} | {Delta(a.Win - misa[(row, w.Name)].Win)} | {Per(a.Fell, a.N)} | {a.Fall} |");
                }
                var m = misa[(row, w.Name)];
                Console.WriteLine($"| {w.Name} | （ミサ）| {m.WT} | {Delta(m.Win - res[(row, w.Name, "0")].Win)} | — | {Per(m.Fell, m.N)} | {m.Fall} |");
            }
            Console.WriteLine();
            Console.WriteLine("ノミ: 与ダメ ／戦（ノミの名で入った分）・一振り ／戦・発 ／ 一振り・刻んだ傷 ／戦・相手の傷の最大（平均 ／ 最大）・傷の読み（回 ／ 量 ／戦）・再行動 ／戦・軛で切られた発の割合（強4 だけ）。");
            Console.WriteLine();
            Console.WriteLine("| 波 | 版 | 与ダメ | 一振り | 発 ／ 一振り | 刻んだ傷 | 傷の最大 | 傷の読み（回 ／ 量）| 再行動 | 軛で切られた |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|---|---|--:|--:|");
            foreach (var w in Four)
                foreach (var v in Vers)
                {
                    var a = res[(row, w.Name, v.Key)];
                    Console.WriteLine($"| {w.Name} | {v.Label} | {Per1(a.Dmg, a.N)} | {Per(a.Volleys, a.N)} | {Per1(a.Shots, a.Volleys)} | {Per1(a.Writes, a.N)} | {Per1(a.Peak, a.N)} ／ {a.PeakMax} | {Per1(a.Reads, a.N)} ／ {Per1(a.ReadAmt, a.N)} | {Per(a.Encore, a.N)} | {(w == I.StrongWaves[2] ? Pc(a.YokeCut, a.Hits) : "—")} |");
                }
            Console.WriteLine();
        }
        Console.WriteLine("## まとめ（4 波の平均勝率・規定との差 ／ ミサに替えた方が良い波の数（4 波中））");
        Console.WriteLine();
        Console.WriteLine("| 行 | " + string.Join(" | ", Vers.Select(v => v.Label)) + " | ミサ |");
        Console.WriteLine("|---|" + string.Concat(Vers.Select(_ => "---|")) + "--:|");
        var misaBetter = Vers.ToDictionary(v => v.Key, _ => 0);
        foreach (var row in MainRows)
        {
            double b0 = Four.Average(w => res[(row, w.Name, "0")].Win);
            double mm = Four.Average(w => misa[(row, w.Name)].Win);
            var cells = Vers.Select(v =>
            {
                double x = Four.Average(w => res[(row, w.Name, v.Key)].Win);
                int mb = Four.Count(w => misa[(row, w.Name)].Win > res[(row, w.Name, v.Key)].Win);
                if (mm > x) misaBetter[v.Key]++;
                return $"{F1(x)}{(v.Key == "0" ? "" : $"（{Delta(x - b0)}）")} ／ {mb}";
            });
            Console.WriteLine($"| {row} | " + string.Join(" | ", cells) + $" | {F1(mm)} |");
        }
        Console.WriteLine();
        Console.WriteLine("ミサに替えた方が良い行の数（5 行中・4 波平均で）: " + string.Join("・", Vers.Select(v => $"{v.Label} {misaBetter[v.Key]}")));
        Console.WriteLine();
        foreach (var v in Vers.Skip(1)) BossTrace(v.D, v.Label);
    }

    static void Mixed()
    {
        var waves = L.Mains.Append(I.Guard).Append(I.Bat).ToArray();
        Console.WriteLine($"# 第423期 §2-5 —— 残りの 4 行 × {{規定, NM-w}} × 本編 第1〜5波 ＋ 近衛 ／ 大隊・seed 0..199");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | " + string.Join(" | ", waves.Select(w => w.Name)) + " | 第2〜5波の平均 |");
        Console.WriteLine("|---|---|" + string.Concat(waves.Select(_ => "--:|")) + "--:|");
        foreach (var name in RestRows)
        {
            var f = Row(name);
            Fx[] b = waves.Select(w => Measure(f, w, 200)).ToArray();
            Fx[] x = waves.Select(w => Measure(Swap(f, UnitCatalog.NomiNMw), w, 200)).ToArray();
            double m0 = Enumerable.Range(1, 4).Average(i => b[i].Win), m1 = Enumerable.Range(1, 4).Average(i => x[i].Win);
            Console.WriteLine($"| {name} | 規定 | " + string.Join(" | ", b.Select(a => F1(a.Win))) + $" | {F1(m0)} |");
            Console.WriteLine($"| {name} | NM-w | " + string.Join(" | ", x.Select((a, i) => $"{F1(a.Win)}（{Delta(a.Win - b[i].Win)}）")) + $" | {F1(m1)}（{Delta(m1 - m0)}）|");
        }
        Console.WriteLine();
    }

    /// <summary>爆弾を置く席の候補: 味方の席ごと ／ 敵の前列（席 0 ／ 1）が 1 戦に何回被弾するか（台本の Damage・直の一撃・中継 ／ 刻みを除く）。</summary>
    static void Seats()
    {
        string[] boards = { "試遊・燃焼 ポンの台", "試遊・自傷" };
        string[] seat = { "前1", "前3", "中央", "後1", "後3" };
        Console.WriteLine($"# 第423期 §6 次の下見 —— 爆弾を置く席の候補: 席ごとの被弾 ／戦・seed 0..{Seeds - 1}（いまの規定）");
        Console.WriteLine();
        Console.WriteLine("味方 ＝ 敵の直の一撃で、その席の駒が HP か破片を減らされた回数（席は開戦時の席）。隣 ＝ その席の隣（`FormationRules.AreAdjacent`）の駒の被弾の和。敵の前列 ＝ 味方の直の一撃で、敵の席 0 ／ 1 の駒が受けた回数（範囲 ／ 単体の別）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | " + string.Join(" | ", seat.Select(s => $"{s}（駒）")) + " | " + string.Join(" | ", seat.Select(s => $"{s}の隣")) + " | 敵の前列（うち範囲）|");
        Console.WriteLine("|---|---|" + string.Concat(seat.Select(_ => "--:|")) + string.Concat(seat.Select(_ => "--:|")) + "--:|");
        foreach (var name in boards)
        {
            var f = Presets.Playtest.Concat(Presets.Versions).First(r => r.Name == name).F;
            var who = Enumerable.Range(0, 5).Select(i => f[i] is { } d ? Short(d) : "—").ToArray();
            foreach (var w in Four)
            {
                var hits = new long[5]; long front = 0, frontAoe = 0;
                object gate = new();
                Parallel.For(0, Seeds, sd =>
                {
                    var (r, p, e) = I.Raw(f, w, sd, verbose: true);
                    var seatOf = new Dictionary<int, int>();
                    foreach (var u in p) if (u.Slot is >= 0 and < 5 && !seatOf.ContainsKey(u.InstanceId)) seatOf[u.InstanceId] = u.Slot;
                    var pid = p.Select(u => u.InstanceId).ToHashSet(); var eid = e.Select(u => u.InstanceId).ToHashSet();
                    // 開戦時の席（敵の前列）: 最初に現れた席を使う
                    var eSeat = e.ToDictionary(u => u.InstanceId, u => u.Slot);
                    var h = new long[5]; long fr = 0, fa = 0;
                    foreach (var ev in r.Events)
                    {
                        if (ev.Kind != BattleEventKind.Damage || ev.Relayed || ev.Pattern is null || ev.TargetId is not int t || ev.ActorId is not int a) continue;
                        if (pid.Contains(t) && eid.Contains(a) && seatOf.TryGetValue(t, out int s0)) h[s0]++;
                        if (eid.Contains(t) && pid.Contains(a) && eSeat.TryGetValue(t, out int s1) && s1 is 0 or 1) { fr++; if (ev.Pattern != AttackPattern.Single) fa++; }
                    }
                    lock (gate) { for (int i = 0; i < 5; i++) hits[i] += h[i]; front += fr; frontAoe += fa; }
                });
                var adj = Enumerable.Range(0, 5).Select(s => Enumerable.Range(0, 5).Where(o => o != s && FormationRules.AreAdjacent(s, o)).Sum(o => hits[o])).ToArray();
                Console.WriteLine($"| {name} | {w.Name} | " + string.Join(" | ", Enumerable.Range(0, 5).Select(i => $"{Per1(hits[i], Seeds)}（{who[i]}）")) + " | "
                    + string.Join(" | ", adj.Select(x => Per1(x, Seeds))) + $" | {Per1(front, Seeds)}（{Pc(frontAoe, front)}）|");
            }
        }
        Console.WriteLine();
        Console.WriteLine("注意: 席は戦の終わりの `Slot`（動いた駒は動いた先）で数えている。台の駒は精鋭の波ではほとんど動かない。");
        Console.WriteLine();
    }

    static void Log(string vk, string head, string wk, int seed)
    {
        var name = CompareBuilds().Concat(CrossBuilds()).First(r => r.Name.StartsWith(head)).Name;
        var f = Swap(Row(name), Vers.First(v => v.Key == vk).D);
        var w = WaveKeys.First(x => x.Key == wk).W;
        var (r, _, _) = I.Raw(f, w, seed, verbose: true);
        Console.WriteLine($"# {name} × {w.Name} × {Vers.First(v => v.Key == vk).Label}・seed {seed}: PlayerWon={r.PlayerWon}・T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(new string(' ', l.Indent * 2) + l.Text);
    }

    static void Check()
    {
        int bad = 0;
        void Req(bool ok, string what) { Console.WriteLine($"- {what}: {(ok ? "○" : "**×**")}"); if (!ok) bad++; }
        Console.WriteLine("# 第423期 nomi423 check");
        Console.WriteLine();
        var nm = UnitCatalog.Nomi;
        Req(UnitCatalog.NomiNMw.Traits.SequenceEqual(nm.Traits.Where(t => t != TraitId.CarveOnce)), "NM-w ＝ 規定 − CarveOnce（連打 ／ 刻み ／ 執着はそのまま）");
        Req(UnitCatalog.NomiNMw3.Traits.SequenceEqual(UnitCatalog.NomiNMw.Traits.Append(TraitId.CarveDeep3)), "NM-w3 ＝ NM-w ＋ CarveDeep3");
        Req(new[] { UnitCatalog.NomiNMw, UnitCatalog.NomiNMw3 }.All(d => d.Id == "nomi" && d.MaxHp == nm.MaxHp && d.Attack == nm.Attack && d.Speed == nm.Speed && d.Name == nm.Name && d.Flavor == nm.Flavor && d.MinusText == nm.MinusText),
            "版の Id ／ 名前 ／ HP ／ 攻 ／ 速 ／ フレーバー ／ マイナスは規定のまま");
        Req(UnitCatalog.NomiNMw.PlusText.Contains("一発ごとに傷を刻み") && UnitCatalog.NomiNMw3.PlusText.Contains("3 を上乗せ"), "文面: 一発ごとに刻む ／ NM-w3 は 3 を上乗せ");
        Req(!UnitCatalog.All.Any(d => d.Traits.Contains(TraitId.CarveDeep3)), "`CarveDeep3` の保持者は `All` に 0 枚");
        var solo = Row("刻み (ノミ単騎)");
        var a0 = Measure(solo, I.Boss, 10); var aw = Measure(Swap(solo, UnitCatalog.NomiNMw), I.Boss, 10);
        Req(aw.Writes > a0.Writes * 5 && aw.Writes <= aw.Shots, $"NM-w は一発ごとに刻む（ボス 10 戦: 規定 {a0.Writes} → NM-w {aw.Writes}・撃った発 {aw.Shots}・その発で倒れた ／ 倒れていた相手には刻まない）");
        Req(a0.Writes == a0.Volleys, "規定は一振りに 1 つだけ刻む（刻んだ傷 ＝ 一振り）");
        bool same = true;
        foreach (var v in Vers.Skip(1))
            for (int s = 0; s < 6; s++)
            {
                var f = Swap(solo, v.D);
                var (r1, _, _) = I.Raw(f, I.Guard, s, verbose: false);
                var (r2, _, _) = I.Raw(f, I.Guard, s, verbose: true);
                if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns) same = false;
            }
        Req(same, "verbose の有無で勝敗 ／ T が変わらない（2 版 × ノミ単騎 × 近衛 × 6 seed）");
        bool cens = true;
        foreach (var s in Enumerable.Range(0, 8))
        {
            BattleContext.WoundTrace423 = false; var x = I.Raw(Swap(solo, UnitCatalog.NomiNMw), I.Bat, s).R;
            BattleContext.WoundTrace423 = true; var y = I.Raw(Swap(solo, UnitCatalog.NomiNMw), I.Bat, s).R;
            cens &= x.PlayerWon == y.PlayerWon && x.Turns == y.Turns && x.PlayerSurvivors == y.PlayerSurvivors;
        }
        Req(cens, "傷の計数（`WoundTrace423`）の有無で勝敗 ／ T ／ 生存が同じ（ノミ単騎 × 大隊 × 8 seed）");
        var row = Presets.Versions.FirstOrDefault(r => r.Name == "版・刻み ノミ");
        Req(row.Name is not null && Presets.Versions[^1].Name == "版・刻み ノミ" && row.F.Occupied().Any(o => o.Def.Id == "nomi" && o.Def != UnitCatalog.Nomi) && !Presets.Versions.Any(r => r.Name == "版・彫り ノミ"),
            "「版・刻み ノミ」が末尾で、ノミは版（第424期から NM-wc）・「版・彫り ノミ」は外した");
        Req(!VersionsH407().Any(r => r.Name == "版・刻み ノミ"), "鎖の終端（`VersionsH407`）は「版・刻み ノミ」を外す");
        Console.WriteLine();
        Console.WriteLine(bad == 0 ? "すべて ○。" : $"× が {bad} 件。");
        if (bad > 0) Environment.ExitCode = 1;
    }
}
