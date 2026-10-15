using System.Reflection;
using BattleCore;
using static Common;
using G339 = Grow339Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using T323 = Tou323Diag;

// =====================================================================================
// settle340 —— 第340期【軽い期・規定化だけ】ミサの羽は標が無いと増えていく（FG-b）を規定にする。
// 指示書は design/PHASE340_MISA_GROW_SETTLE_SPEC.md ／ 報告は design/PHASE340_MISA_GROW_SETTLE.md。
//
//     dotnet run --project BattleSim -c Release 0 settle340 boards                     # §4 代表台 5 × 5 波 × 旧 ／ 新（seed 0..99）・書き手なしのターンごとの乱射
//     dotnet run --project BattleSim -c Release 0 settle340 log [台] [波] [seed] [old|new] # 1戦のログ（台 guard ／ nowriter か台の名前）
//     dotnet run --project BattleSim -c Release 0 settle340 check                      # §7 自己検査
//
// 旧 ＝ 第334〜339期の規定のミサ（`TomeH339`・`Common.Pin339`）。新 ＝ いまの規定（FG-b）。台と波と1戦の計数は `grow339` のものをそのまま使う。
// **予測のファイルが無ければ本測定（boards）を走らせない**（R397・`.tmp/p340/predict.md`）。
// =====================================================================================
static partial class Settle340Diag
{
    internal const string PredictFile = ".tmp/p340/predict.md";
    const int Seeds = 100;   // 軽い期
    const string Tag = "第340期";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"settle340: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "log": Log(A(3, "nowriter"), A(4, "guard"), int.Parse(A(5, "0")), A(6, "new") == "old"); return;
            case "boards": BoardsAll(); return;
            case "check": Check(); return;
            default: Console.WriteLine("settle340: モードは log / boards / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");

    /// <summary>§4 の代表台（`grow339` と同じ5台・駒はいまの規定）。旧は <see cref="Pin339"/> を通す。</summary>
    internal static (string Name, Formation F)[] Boards() => G339.Boards();
    internal static readonly S287.Wave[] Waves = G339.Waves;
    static readonly int[] TurnsShown = { 1, 3, 5 };

    static string Cell(G339.St z) => $"{F1(z.Win)}%・{Per(z.WinT, z.Wins)}T・倒 {Per2(z.AllyDeaths, z.N)}";

    static void BoardsAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        Console.WriteLine($"# {Tag} §4 代表台 {boards.Length} × {Waves.Length} 波 × 旧（`TomeH339`）／ 新（規定 ＝ FG-b）・seed 0..{Seeds - 1}（軽い期）");
        Console.WriteLine();
        foreach (var (bn, bf) in boards) Console.WriteLine($"- {bn}: {S307.Seats(bf)}");
        Console.WriteLine();
        var res = new Dictionary<(string, string, bool), G339.St>();
        foreach (var (bn, bf) in boards) foreach (var w in Waves) { res[(bn, w.Name, false)] = G339.Measure(Pin339(bf), w, Seeds); res[(bn, w.Name, true)] = G339.Measure(Pin344(bf), w, Seeds); }   // 第345期: ゴルムは第1〜344期の規定

        Console.WriteLine("## 1. 勝率 ／ 決着T（ボスは倒しT）／ 倒れた味方 ／戦（旧 → 新・表示の桁で動いたセルだけ太字）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Waves.Select(_ => "---|")));
        foreach (var (bn, _) in boards)
            Console.WriteLine($"| {bn} | " + string.Join(" | ", Waves.Select(w =>
            {
                var o = res[(bn, w.Name, false)]; var z = res[(bn, w.Name, true)];
                return Cell(o) != Cell(z) ? $"{Cell(o)} → **{Cell(z)}**" : $"{Cell(z)}（同じ）";
            })) + " |");
        Console.WriteLine();

        string nw = G339.NoWriter;
        Console.WriteLine($"## 2. {nw}: ターンごとの乱射 ／ 味方に当たった（そのターンまで続いた戦あたり）・乱射で倒れた味方 ／戦（平均のT）・羽（戦の終わり）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 列 | 勝率・決着T | " + string.Join(" | ", TurnsShown.Select(t => $"T{t}")) + " | 乱射で倒れた味方 | 羽（終わり） |");
        Console.WriteLine("|---|---|---|" + string.Concat(TurnsShown.Select(_ => "---|")) + "--:|--:|");
        foreach (var w in Waves)
            foreach (bool now in new[] { false, true })
            {
                var z = res[(nw, w.Name, now)];
                string C(int t) => z.Alive[t] == 0 ? "—" : $"{Per(z.Spray[t], z.Alive[t])} ／ {Per(z.AllyHits[t], z.Alive[t])}";
                Console.WriteLine($"| {w.Name} | {(now ? "新" : "旧")} | {F1(z.Win)}%・{Per(z.WinT, z.Wins)}T | " + string.Join(" | ", TurnsShown.Select(C))
                    + $" | {Per2(z.SprayKills, z.N)}（{Per(z.SprayKillTurnSum, z.SprayKills)}） | {Per(z.FeathersEnd, z.N)} |");
            }
        Console.WriteLine();
        Console.WriteLine("セル ＝ 乱射 ／ 味方に当たった。— ＝ そのターンまで続いた戦が無い。");
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    static void Log(string board, string wave, int seed, bool old)
    {
        var (name, f0) = board switch { "guard" => Boards()[0], "nowriter" => Boards()[4], _ => Boards().First(b => b.Name == board) };
        var w = T323.WaveOf(wave);
        var r = BattleEngine.Run(BattleEngine.Materialize(old ? Pin339(f0) : f0, BattleContext.PlayerTeam), w.Make(), seed, verbose: true);
        Console.WriteLine($"# {name} × {w.Name} × seed {seed} × {(old ? "旧" : "新")}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }

    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static readonly UnitDef Dummy = new()
    {
        Id = "dummy340", Name = "木偶", MaxHp = 5000, Attack = 1, Speed = 1, Pattern = AttackPattern.Single, Traits = Array.Empty<TraitId>(),
        PlusText = "", MinusText = "", Flavor = "",
    };
    static readonly MethodInfo VolleyM = typeof(BattleContext).GetMethod("FeatherVolley", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("FeatherVolley が見つからない");

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# settle340 check —— 第340期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef M = UnitCatalog.Tome, O = UnitCatalog.TomeH339;
        {
            bool def = M.Traits.SequenceEqual(O.Traits.Where(t => t != TraitId.FeatherLoss).Append(TraitId.FeatherGrowUnmarked)) && M.Traits.SequenceEqual(UnitCatalog.TomeFGb.Traits)
                && M.MinusText == UnitCatalog.TomeFGb.MinusText && M.MinusText == "標を追えない間、羽は毎ターン3枚ずつ増えていく。追う標が無い羽は、敵味方構わず飛ぶ。味方が指差されても、撃つ"
                && M.PlusText == O.PlusText && O.Traits.Contains(TraitId.FeatherLoss) && !M.Traits.Contains(TraitId.FeatherLoss)
                && UnitCatalog.TomeFGa.Traits.SequenceEqual(O.Traits.Where(t => t != TraitId.FeatherLoss).Append(TraitId.FeatherGrow))
                && UnitCatalog.MisaMIf.Traits.SequenceEqual(O.Traits.Append(TraitId.MarkInheritFeathers))
                && UnitCatalog.All.Contains(M) && !UnitCatalog.Everyone.Contains(O) && UnitCatalog.All.Count == 52
                && UnitCatalog.Everyone.Where(d => d.Traits.Contains(TraitId.FeatherGrowUnmarked)).SequenceEqual(new[] { M })
                && UnitCatalog.Everyone.All(d => !d.Traits.Contains(TraitId.FeatherGrow));
            Expect("定義: 規定のミサ ＝ 第334〜339期の規定（`TomeH339`）− `FeatherLoss` ＋ `FeatherGrowUnmarked`（札と文面は第339期の `TomeFGb` と同じ）・FG-a ／ ＋羽 は旧から作り直した・旧は `Everyone` の外・FG-b の保持者は規定のミサ1枚", def);
            var guard = Presets.Playtest.First(r => r.Name == "試遊・標 守り型").F;
            bool pin = ReferenceEquals(PinM339(M), O) && ReferenceEquals(Pin339(guard)[3], O)
                && new Func<UnitDef, UnitDef>[] { PinH337, PinD335, PinZ334 }.All(p => ReferenceEquals(p(M), O))
                && new Func<UnitDef, UnitDef>[] { PinM333, PinS332, PinTK331, PinT328, PinTK327, PinT326, PinTKK324 }.All(p => ReferenceEquals(p(M), UnitCatalog.TomeH333))
                && ReferenceEquals(Pin299(guard)[3], UnitCatalog.TomeMb) && ReferenceEquals(Pin299(Pin337(guard))[3], UnitCatalog.TomeMb) && IsMisaNow(O)
                && ReferenceEquals(Pin337(guard)[3], O) && ReferenceEquals(Pin334(guard)[3], O)
                && G339.Apply(guard, G339.Vers[0]).Occupied().All(o => !ReferenceEquals(o.Def, M))
                && CompareBuildsH339().Concat(PlaytestH339).Concat(CompareBuildsH337()).SelectMany(r => r.F.Occupied()).All(o => !ReferenceEquals(o.Def, M));
            Expect("固定の口: `PinM339` ／ `Pin339` は規定のミサを `TomeH339` へ・古い口の終端（`PinH337` ← `PinD335` ← `PinZ334` ← ほかの口）も・第333期までの口（`PinM333` ／ `PinS332` ／ … ／ `PinTKK324`）は `TomeH333` のまま・`Pin299` は `TomeH339` も `TomeMb` へ（`IsMisaNow`）・`grow339` の「規定」は `TomeH339`", pin);
        }
        {
            var v = Presets.Versions; var nw = G339.NoWriterBoard();
            bool rows = v.Length >= 2 && v[0].Name == "試遊・感電 ポンの台" && v[1].Name == "試遊・ミサ 書き手なし" && v.Skip(2).All(r => r.Name.StartsWith("版・", StringComparison.Ordinal))   /* 第342期: 後ろの「版・」の台は期ごとに入れ替わる */
                && v[1].F.Occupied().Select(o => o.Def).SequenceEqual(nw.Occupied().Select(o => o.Def)) && v[1].F.Occupied().Select(o => o.Slot).SequenceEqual(nw.Occupied().Select(o => o.Slot))
                && v[1].F.Occupied().Any(o => ReferenceEquals(o.Def, M)) && Presets.Playtest.Length == 17
                && Presets.Compare.Concat(Presets.Cross).Concat(Presets.Playtest).Concat(v).First(r => r.Name.Contains("試遊・ミサ 書き手なし", StringComparison.Ordinal)).Name == "試遊・ミサ 書き手なし";
            Expect("試遊の台: `Presets.Versions` ＝ 「試遊・感電 ポンの台」＋「試遊・ミサ 書き手なし」（第339期の書き手なしの台・規定のミサ）・第339期の「版・FG」の3台は外した・`Playtest` は 17 台のまま・`--demo-preset=` の部分一致で自分に当たる", rows);
        }
        {
            // 規定のミサ: 標が無いとき羽が増え、乱射で減らない ／ 旧: 増えず乱射で減る（盤を直に組んで一振りを呼ぶ）
            var foes = Formation.Build(front1: Dummy, front3: Dummy, center: Dummy, back1: Dummy, back3: Dummy);
            (int, int, int) Go(UnitDef d)
            {
                var ctx = T323.Ctx(Formation.Build(front1: T323.Wall, back1: d), foes, out var p, out var e);
                var m = T323.Seat(p, 3);
                m.SetCounter(FeathersTrait.ExtraKey, 3);
                T323.TurnProp.SetValue(ctx, 1); VolleyM.Invoke(ctx, new object[] { m }); int a = FeathersTrait.Count(m);
                T323.Seat(e, 0).SetCounter(StatusKeys.Marked, 2);
                T323.TurnProp.SetValue(ctx, 2); VolleyM.Invoke(ctx, new object[] { m }); int b = FeathersTrait.Count(m);
                T323.Seat(e, 0).SetCounter(StatusKeys.Marked, 0);
                T323.TurnProp.SetValue(ctx, 3); VolleyM.Invoke(ctx, new object[] { m }); int c = FeathersTrait.Count(m);
                return (a, b, c);
            }
            var n = Go(M); var o = Go(O);
            Expect("規定のミサ: 標が無い一振りの頭で羽が 3 枚増え、乱射で減らない（4 → 7 → 7（標あり）→ 10）・旧（`TomeH339`）は増えず乱射で減る（4 → 1 → 1 → 1）", n == (7, 7, 10) && o == (1, 1, 1),
                $"新 4 → {n.Item1} → {n.Item2} → {n.Item3}・旧 4 → {o.Item1} → {o.Item2} → {o.Item3}");
        }
        {
            long gNew = 0, gOld = 0; int diff = 0, same = 0, n = 0;
            foreach (var f in Boards().Select(b => Pin344(b.F)))   // 第345期: ゴルムは第1〜344期の規定
                foreach (var w in Waves)
                    for (int sd = 0; sd < 10; sd++)
                    {
                        var rn = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                        var r0 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        var ro = BattleEngine.Run(BattleEngine.Materialize(Pin339(f), BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        var rv = BattleEngine.Run(BattleEngine.Materialize(G339.Apply(f, G339.Vers[2]), BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        n++;
                        gNew += r0.TallyByUnit.GetValueOrDefault("tome")?.Fg339Grows ?? 0;
                        gOld += ro.TallyByUnit.GetValueOrDefault("tome")?.Fg339Grows ?? 0;
                        if (rn.PlayerWon != r0.PlayerWon || rn.Turns != r0.Turns) diff++;
                        if (r0.PlayerWon == rv.PlayerWon && r0.Turns == rv.Turns && (r0.TallyByUnit.GetValueOrDefault("tome")?.Fg339Grows ?? 0) == (rv.TallyByUnit.GetValueOrDefault("tome")?.Fg339Grows ?? 0)) same++;
                    }
            Expect($"代表台 5 × 5 波 × seed 0..9（{n} 戦）: 規定のミサでは羽が増え、旧（`TomeH339`）では増えない", gNew > 0 && gOld == 0, $"増えた 規定 {gNew} ／ 旧 {gOld}");
            Expect($"同じ {n} 戦: verbose の有無で勝敗 ／ 決着T が同じ（規定）", diff == 0, $"違い {diff}");
            Expect($"同じ {n} 戦: 規定のミサは第339期の版 FG-b（`grow339` の b）と勝敗 ／ 決着T ／ 増えた回数が一致", same == n, $"一致 {same} ／ {n}");
        }
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            int engine = Now("BattleCore/BattleEngine.cs"), traits = Now("BattleCore/Traits.cs");
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 54f32382:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int he = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, ht = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第339期（54f32382）と同じ）", engine == he && traits == ht && he > 0, $"engine {he} → {engine}・traits {ht} → {traits}");
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
