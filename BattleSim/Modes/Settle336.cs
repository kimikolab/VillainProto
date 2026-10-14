using BattleCore;
using static Common;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using T323 = Tou323Diag;

// =====================================================================================
// settle336 —— 第336期【軽い期・規定化だけ】ドハの「等分」（DM-a）を規定にする。
// 指示書は design/PHASE336_DOHA_SPLIT_SETTLE_SPEC.md ／ 報告は design/PHASE336_DOHA_SPLIT_SETTLE.md。
//
//     dotnet run --project BattleSim -c Release 0 settle336 boards                     # §4 代表台 4 × 5 波 × 旧 ／ 新（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 settle336 log [台] [波] [seed] [old|new] # 1戦のログ（台の名前）
//     dotnet run --project BattleSim -c Release 0 settle336 check                      # §7 自己検査
//
// 旧 ＝ 第317〜335期の規定のドハ（`DohaH335`・`Common.Pin335`）。新 ＝ いまの規定（DM-a）。
// **予測のファイルが無ければ本測定（boards）を走らせない**（R397・`.tmp/p336/predict.md`）。
// =====================================================================================
static partial class Settle336Diag
{
    internal const string PredictFile = ".tmp/p336/predict.md";
    const int Seeds = 100;   // 軽い期
    const string Tag = "第336期";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"settle336: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "log": Log(A(3, "守り型"), A(4, "boss"), int.Parse(A(5, "0")), A(6, "new") == "old"); return;
            case "boards": BoardsAll(); return;
            case "check": Check(); return;
            default: Console.WriteLine("settle336: モードは log / boards / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");

    /// <summary>§4 の代表台（駒はいまの規定）。旧は <see cref="Pin335"/> を通す。</summary>
    internal static (string Name, Formation F)[] Boards() => new (string, Formation)[]
    {
        ("守り型", Presets.Playtest.First(r => r.Name == "試遊・標 守り型").F),
        ("雷の型", Presets.Playtest.First(r => r.Name == "試遊・感電 雷の型").F),
        ("反撃改 (ドハ×カド)", CompareBuilds().First(r => r.Name == "反撃改 (ドハ×カド)").F),
        ("反撃改2 (ガン×カド)", CompareBuilds().First(r => r.Name == "反撃改2 (ガン×カド)").F),
    };

    internal static readonly S287.Wave[] Waves = new[] { "boss", "guard", "bat", "2", "5" }.Select(T323.WaveOf).ToArray();

    internal sealed class St
    {
        public long N, Wins, WinT, AllyDeaths, Splits, SplitAmt, SplitKills;
        public readonly Dictionary<string, long> KilledBy = new();
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public void Add(St o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; AllyDeaths += o.AllyDeaths; Splits += o.Splits; SplitAmt += o.SplitAmt; SplitKills += o.SplitKills;
            foreach (var (k, v) in o.KilledBy) KilledBy[k] = KilledBy.GetValueOrDefault(k) + v;
        }
    }

    internal static St Fight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, w.Make(), seed, verbose: false);
        var a = new St { N = 1, AllyDeaths = p.Count(u => u.LastDeathTurn > 0) };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        var names = p.GroupBy(u => u.Def.Id).ToDictionary(g => g.Key, g => S307.Short(g.First().Def));
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (!names.TryGetValue(id, out var nm)) continue;
            if (t.Dm336PieceDeaths > 0) a.KilledBy[nm] = a.KilledBy.GetValueOrDefault(nm) + t.Dm336PieceDeaths;
            if (id != "doha") continue;
            a.Splits += t.Dm335Splits; a.SplitAmt += t.Dm335SplitAmt; a.SplitKills += t.Dm335SplitKills;
        }
        return a;
    }

    internal static St Measure(Formation f, S287.Wave w, int n = Seeds)
    {
        var parts = new St[n];
        Parallel.For(0, n, i => parts[i] = Fight(f, w, i));
        var all = new St();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    static string Cell(St z) => $"{F1(z.Win)}%・{Per(z.WinT, z.Wins)}T・倒 {Per2(z.AllyDeaths, z.N)}";

    static void BoardsAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        Console.WriteLine($"# {Tag} §4 代表台 {boards.Length} × {Waves.Length} 波 × 旧（`DohaH335`）／ 新（規定 ＝ DM-a）・seed 0..{Seeds - 1}（軽い期）");
        Console.WriteLine();
        foreach (var (bn, bf) in boards) Console.WriteLine($"- {bn}: {S307.Seats(bf)}");
        Console.WriteLine();
        var res = new Dictionary<(string, string, bool), St>();
        foreach (var (bn, bf) in boards) foreach (var w in Waves) { res[(bn, w.Name, false)] = Measure(Pin335(bf), w); res[(bn, w.Name, true)] = Measure(bf, w); }

        Console.WriteLine("## 1. 勝率 ／ 決着T（ボスは倒しT）／ 倒れた味方 ／戦（旧 → 新・動いたセルだけ太字）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Waves.Select(_ => "---|")));
        foreach (var (bn, _) in boards)
            Console.WriteLine($"| {bn} | " + string.Join(" | ", Waves.Select(w =>
            {
                var o = res[(bn, w.Name, false)]; var z = res[(bn, w.Name, true)];
                bool moved = o.Wins != z.Wins || o.WinT != z.WinT || o.AllyDeaths != z.AllyDeaths;
                return moved ? $"{Cell(o)} → **{Cell(z)}**" : $"{Cell(z)}（同じ）";
            })) + " |");
        Console.WriteLine();

        Console.WriteLine("## 2. 等分（新・1戦あたり）: 回 ／ 量 ／ 欠片で倒れた味方");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Waves.Select(_ => "---|")));
        foreach (var (bn, _) in boards)
            Console.WriteLine($"| {bn} | " + string.Join(" | ", Waves.Select(w => { var z = res[(bn, w.Name, true)]; return $"{Per(z.Splits, z.N)} ／ {Per(z.SplitAmt, z.N)} ／ {Per2(z.SplitKills, z.N)}"; })) + " |");
        Console.WriteLine();

        Console.WriteLine("## 3. 等分の欠片で倒れた駒（新・全 5 波の合計・`反撃改` の2行と全台）");
        Console.WriteLine();
        foreach (var (bn, _) in boards)
        {
            var all = new Dictionary<string, long>();
            foreach (var w in Waves) foreach (var (k, v) in res[(bn, w.Name, true)].KilledBy) all[k] = all.GetValueOrDefault(k) + v;
            Console.WriteLine($"- {bn}: {(all.Count == 0 ? "なし" : string.Join("・", all.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{x.Key} {x.Value}")))}（{Waves.Length * Seeds} 戦）");
        }
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    static void Log(string board, string wave, int seed, bool old)
    {
        var (name, f0) = Boards().First(b => b.Name == board);
        var w = T323.WaveOf(wave);
        var r = BattleEngine.Run(BattleEngine.Materialize(old ? Pin335(f0) : f0, BattleContext.PlayerTeam), w.Make(), seed, verbose: true);
        Console.WriteLine($"# {name} × {w.Name} × seed {seed} × {(old ? "旧" : "新")}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }

    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# settle336 check —— 第336期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef D = UnitCatalog.Doha, O = UnitCatalog.DohaH335;
        {
            bool def = D.Traits.SequenceEqual(O.Traits.Append(TraitId.DohaSplit)) && D.Traits.SequenceEqual(UnitCatalog.DohaDMa.Traits) && D.MinusText == UnitCatalog.DohaDMa.MinusText
                && D.MinusText == "自分は強くならず、自分が受ける痛みは、仲間に等しく分かれる" && D.PlusText == O.PlusText && O.MinusText == "自分は強くならず、味方が多いほど早く尽きる"
                && UnitCatalog.DohaDMc.Traits.SequenceEqual(O.Traits.Append(TraitId.DohaOverflow)) && UnitCatalog.DohaH316.MinusText == O.MinusText
                && UnitCatalog.All.Contains(D) && !UnitCatalog.Everyone.Contains(O) && UnitCatalog.All.Count == 52;
            Expect("定義: 規定のドハ ＝ 第317〜335期の規定（`DohaH335`）＋ `DohaSplit`（札と文面は第335期の `DohaDMa` と同じ）・DM-c は旧から作り直した・旧は `Everyone` の外・`DohaH316` の文面は第335期まで", def);
            var guard = Presets.Playtest.First(r => r.Name == "試遊・標 守り型").F;
            bool pin = ReferenceEquals(PinD335(D), O) && ReferenceEquals(Pin335(guard)[2], O)
                && new Func<UnitDef, UnitDef>[] { PinZ334, PinM333, PinS332, PinTK331, PinT328, PinTK327, PinT326, PinTKK324 }.All(p => ReferenceEquals(p(D), O))
                && ReferenceEquals(PinDS316(D), UnitCatalog.DohaH316) && ReferenceEquals(PinDS316(O), UnitCatalog.DohaH316) && ReferenceEquals(Pin334(guard)[2], O)
                && Doha335Diag.Boards().SelectMany(b => b.F.Occupied()).All(o => !ReferenceEquals(o.Def, D))
                && CompareBuildsH335().Concat(PlaytestH335).SelectMany(r => r.F.Occupied()).All(o => !ReferenceEquals(o.Def, D));
            Expect("固定の口: `PinD335` ／ `Pin335` は規定のドハを `DohaH335` へ・古い口の終端（`PinZ334` ← `PinM333` ← ほかの口）も・`PinDS316` は `DohaH335` も旧へ・`doha335` の代表台は `DohaH335`", pin);
        }
        {
            var v = Presets.Versions;
            Expect("試遊の台: `Presets.Versions` ＝ 「試遊・感電 ポンの台」1台（第335期の「版・」の4台は外した）・`Playtest` は 17 台のまま",
                v.Length == 1 && v[0].Name == "試遊・感電 ポンの台" && Presets.Playtest.Length == 17);
        }
        {
            long nNew = 0, nOld = 0; int diff = 0, n = 0;
            foreach (var (_, f) in Boards())
                foreach (var w in new[] { Waves[0], Waves[1], Waves[2], Waves[3] })
                    for (int sd = 0; sd < 10; sd++)
                    {
                        var rn = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                        var r0 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        var ro = BattleEngine.Run(BattleEngine.Materialize(Pin335(f), BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        n++;
                        nNew += rn.TallyByUnit.GetValueOrDefault("doha")?.Dm335Splits ?? 0;
                        nOld += ro.TallyByUnit.GetValueOrDefault("doha")?.Dm335Splits ?? 0;
                        if (rn.PlayerWon != r0.PlayerWon || rn.Turns != r0.Turns) diff++;
                    }
            Expect($"代表台 4 × ボス ／ 近衛 ／ 大隊 ／ 第2波 × seed 0..9（{n} 戦）: 規定のドハでは等分が起き、旧（`DohaH335`）では起きない", nNew > 0 && nOld == 0, $"等分 規定 {nNew} ／ 旧 {nOld}");
            Expect($"同じ {n} 戦: verbose の有無で勝敗 ／ 決着T が同じ（規定）", diff == 0, $"違い {diff}");
        }
        {
            // 再入の止め（第336期・決めたこと）: 等分の欠片がカドに当たる → 棘の巻き込みがドハに当たる → また等分…の連鎖。欠片の連鎖の中でドハに戻ってきた一撃は等分せずに受ける。
            long re = 0, splits = 0; int n = 0;
            foreach (var (_, f) in Boards().Where(b => b.Name.StartsWith("反撃改", StringComparison.Ordinal)))
                foreach (var w in Waves)
                    for (int sd = 0; sd < 10; sd++)
                    {
                        var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        n++;
                        var t = r.TallyByUnit.GetValueOrDefault("doha");
                        re += t?.Dm336Reentry ?? 0; splits += t?.Dm335Splits ?? 0;
                    }
            Expect($"再入の止め: `反撃改` の2行 × 5 波 × seed 0..9（{n} 戦）で、欠片の連鎖の中でドハに戻ってきた一撃（カドの棘の巻き込み）は等分せずに受ける（戦が終わる＝無限の連鎖にならない）", re > 0 && splits > 0, $"等分 {splits}・止めた再入 {re}");
        }
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            int engine = Now("BattleCore/BattleEngine.cs"), traits = Now("BattleCore/Traits.cs");
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show da5fda6:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int he = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, ht = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第335期（da5fda6）と同じ）", engine == he && traits == ht && he > 0, $"engine {he} → {engine}・traits {ht} → {traits}");
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
