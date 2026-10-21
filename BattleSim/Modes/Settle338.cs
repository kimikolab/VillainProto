using BattleCore;
using static Common;
using M337 = Mark337Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using T323 = Tou323Diag;

// =====================================================================================
// settle338 —— 第338期【軽い期・規定化だけ】ヒサの「標の引き継ぎ」（MI-b 周りへ散る）を規定にする。
// 指示書は design/PHASE338_MARK_INHERIT_SETTLE_SPEC.md ／ 報告は design/PHASE338_MARK_INHERIT_SETTLE.md。
//
//     dotnet run --project BattleSim -c Release 0 settle338 boards                     # §4 代表台 5 × 5 波 × 旧 ／ 新（seed 0..99）・守り型の引き継ぎ ／ 連鎖 ／ 標を追えない羽
//     dotnet run --project BattleSim -c Release 0 settle338 log [台] [波] [seed] [old|new] # 1戦のログ（台の名前）
//     dotnet run --project BattleSim -c Release 0 settle338 check                      # §7 自己検査
//
// 旧 ＝ 第322〜337期の規定のヒサ（`HisaH337`・`Common.Pin337`）。新 ＝ いまの規定（MI-b）。台と波と1戦の計数は `mark337` のものをそのまま使う。
// **予測のファイルが無ければ本測定（boards）を走らせない**（R397・`.tmp/p338/predict.md`）。
// =====================================================================================
static partial class Settle338Diag
{
    internal const string PredictFile = ".tmp/p338/predict.md";
    const int Seeds = 100;   // 軽い期
    const string Tag = "第338期";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"settle338: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "log": Log(A(3, "守り型"), A(4, "bat"), int.Parse(A(5, "0")), A(6, "new") == "old"); return;
            case "boards": BoardsAll(); return;
            case "check": Check(); return;
            default: Console.WriteLine("settle338: モードは log / boards / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");

    /// <summary>§4 の代表台（`mark337` と同じ5台・駒はいまの規定）。旧は <see cref="Pin337"/> を通す。</summary>
    internal static (string Name, Formation F)[] Boards() => M337.Boards().Select(b => (b.Name, Pin339(b.F))).ToArray();   // 第340期: ミサは第334〜339期の規定（`TomeH339`）に固定
    internal static readonly S287.Wave[] Waves = M337.Waves;

    static string Cell(M337.St z) => $"{F1(z.Win)}%・{Per(z.WinT, z.Wins)}T・倒 {Per2(z.AllyDeaths, z.N)}";

    static void BoardsAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        Console.WriteLine($"# {Tag} §4 代表台 {boards.Length} × {Waves.Length} 波 × 旧（`HisaH337`）／ 新（規定 ＝ MI-b）・seed 0..{Seeds - 1}（軽い期）");
        Console.WriteLine();
        foreach (var (bn, bf) in boards) Console.WriteLine($"- {bn}: {S307.Seats(bf)}");
        Console.WriteLine();
        var res = new Dictionary<(string, string, bool), M337.St>();
        foreach (var (bn, bf) in boards) foreach (var w in Waves) { res[(bn, w.Name, false)] = M337.Measure(Pin337(bf), w, Seeds); res[(bn, w.Name, true)] = M337.Measure(bf, w, Seeds); }

        Console.WriteLine("## 1. 勝率 ／ 決着T（ボスは倒しT）／ 倒れた味方 ／戦（旧 → 新・動いたセルだけ太字）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Waves.Select(_ => "---|")));
        foreach (var (bn, _) in boards)
            Console.WriteLine($"| {bn} | " + string.Join(" | ", Waves.Select(w =>
            {
                var o = res[(bn, w.Name, false)]; var z = res[(bn, w.Name, true)];
                bool moved = Cell(o) != Cell(z);   // 表示の桁で動いたセルだけ（合計が1戦ぶん違っても丸めて同じなら「同じ」）
                return moved ? $"{Cell(o)} → **{Cell(z)}**" : $"{Cell(z)}（同じ）";
            })) + " |");
        Console.WriteLine();

        string g0 = boards[0].Name;
        Console.WriteLine($"## 2. {g0}（1戦あたり）: 引き継ぎの回数・連鎖の長さ（戦ごとの最大の平均 ／ 全戦の最大）・標を追えない羽（一振りの中で標の敵がいなくて敵味方構わず飛んだ羽・うち味方に当たった）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 引き継ぎ（新） | 連鎖の長さ（新） | 移った層（新） | 標を追えない羽 旧 → 新（味方） | 羽 計 旧 → 新 |");
        Console.WriteLine("|---|--:|---|--:|---|---|");
        foreach (var w in Waves)
        {
            var o = res[(g0, w.Name, false)]; var z = res[(g0, w.Name, true)];
            Console.WriteLine($"| {w.Name} | {Per(z.Fires, z.N)} | {Per(z.ChainSum, z.N)} ／ {z.ChainPeak} | {Per(z.Layers, z.N)} | {Per(o.Sprayed, o.N)}（{Per(o.SprayAlly, o.N)}）→ {Per(z.Sprayed, z.N)}（{Per(z.SprayAlly, z.N)}） | {Per(o.Feathers, o.N)} → {Per(z.Feathers, z.N)} |");
        }
        Console.WriteLine();
        Console.WriteLine("旧では引き継ぎは起きない（計数は 0）。連鎖の長さ ＝ 引き継いだ標の世代の最大（第337期の定義）。");
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    static void Log(string board, string wave, int seed, bool old)
    {
        var (name, f0) = Boards().First(b => b.Name == board);
        var w = T323.WaveOf(wave);
        var r = BattleEngine.Run(BattleEngine.Materialize(old ? Pin337(f0) : f0, BattleContext.PlayerTeam), w.Make(), seed, verbose: true);
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
        Console.WriteLine("# settle338 check —— 第338期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef H = UnitCatalog.Hisa, O = UnitCatalog.HisaH337;
        {
            bool def = H.Traits.SequenceEqual(O.Traits.Append(TraitId.MarkInheritSpread)) && H.Traits.SequenceEqual(UnitCatalog.HisaMIb.Traits) && H.PlusText == UnitCatalog.HisaMIb.PlusText
                && H.PlusText.EndsWith("。標を付けられた敵が倒れると、その恨みが周りの敵に散る", StringComparison.Ordinal) && H.MinusText == O.MinusText
                && UnitCatalog.HisaMIa.Traits.SequenceEqual(O.Traits.Append(TraitId.MarkInheritOne)) && UnitCatalog.MisaMIf.Traits.SequenceEqual(UnitCatalog.TomeH339.Traits.Append(TraitId.MarkInheritFeathers))
                && UnitCatalog.All.Contains(H) && !UnitCatalog.Everyone.Contains(O) && UnitCatalog.All.Count == 52
                && UnitCatalog.Everyone.Where(d => d.Traits.Contains(TraitId.MarkInheritSpread)).SequenceEqual(new[] { H });
            Expect("定義: 規定のヒサ ＝ 第322〜337期の規定（`HisaH337`）＋ `MarkInheritSpread`（札と文面は第337期の `HisaMIb` と同じ）・MI-a は旧から作り直した・旧は `Everyone` の外・札の保持者は規定のヒサ1枚", def);
            var guard = Presets.Playtest.First(r => r.Name == "試遊・標 守り型").F;
            bool pin = ReferenceEquals(PinH337(H), O) && ReferenceEquals(Pin337(guard)[4], O)
                && new Func<UnitDef, UnitDef>[] { PinD335, PinZ334, PinM333, PinS332, PinTK331, PinT328, PinTK327, PinT326, PinTKK324 }.All(p => ReferenceEquals(p(H), O))
                && new Func<UnitDef, UnitDef>[] { PinS317, PinSS318, PinSH321 }.All(p => ReferenceEquals(p(H), UnitCatalog.HisaH321) && ReferenceEquals(p(O), UnitCatalog.HisaH321))
                && ReferenceEquals(Pin302(Pin334(guard))[4], UnitCatalog.HisaH301) && ReferenceEquals(Pin299(Pin334(guard))[4], UnitCatalog.HisaHKb)
                && ReferenceEquals(Pin334(guard)[4], O) && ReferenceEquals(Pin335(guard)[4], O)
                && M337.Apply(guard, M337.Vers[0]).Occupied().All(o => !ReferenceEquals(o.Def, H))
                && CompareBuildsH337().Concat(PlaytestH337).Concat(CompareBuildsH334()).SelectMany(r => r.F.Occupied()).All(o => !ReferenceEquals(o.Def, H));
            Expect("固定の口: `PinH337` ／ `Pin337` は規定のヒサを `HisaH337` へ・古い口の終端（`PinD335` ← `PinZ334` ← `PinM333` ← ほかの口）も・もっと古い規定へ替える口（`PinS317` ／ `Pin302` ／ `Pin299` ほか）は `HisaH337` も受ける・`mark337` の「規定」は `HisaH337`", pin);
        }
        {
            var v = Presets.Versions;
            Expect("試遊の台: `Presets.Versions` ＝ 「試遊・感電 ポンの台」1台（第337期の「版・MI」の3台は外した）・`Playtest` は 17 台のまま",
                v.Length >= 1 && v[0].Name == "試遊・感電 ポンの台" && v.Skip(1).All(r => r.Name.StartsWith("版・", StringComparison.Ordinal) || r.Name.StartsWith("試遊・", StringComparison.Ordinal))   /* 第340期: 「試遊・ミサ 書き手なし」も */
                && v.Where(r => !r.Name.StartsWith("版・", StringComparison.Ordinal)).SelectMany(r => r.F.Occupied()).All(o => o.Def.Id != "hisa" || ReferenceEquals(o.Def, H)) && Presets.Playtest.Length == 17);   // 第366期: 「版・」の台のヒサは版（P-t ／ P-60 ／ P-180）
        }
        {
            long nNew = 0, nOld = 0; int diff = 0, n = 0;
            foreach (var (_, f) in Boards())
                foreach (var w in new[] { Waves[1], Waves[2], Waves[3], Waves[4] })
                    for (int sd = 0; sd < 10; sd++)
                    {
                        var rn = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                        var r0 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        var ro = BattleEngine.Run(BattleEngine.Materialize(Pin337(f), BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        n++;
                        nNew += rn.TallyByUnit.GetValueOrDefault("hisa")?.Mi337Fires ?? 0;
                        nOld += ro.TallyByUnit.GetValueOrDefault("hisa")?.Mi337Fires ?? 0;
                        if (rn.PlayerWon != r0.PlayerWon || rn.Turns != r0.Turns) diff++;
                    }
            Expect($"代表台 5 × 近衛 ／ 大隊 ／ 第2波 ／ 第5波 × seed 0..9（{n} 戦）: 規定のヒサでは引き継ぎが起き、旧（`HisaH337`）では起きない", nNew > 0 && nOld == 0, $"引き継ぎ 規定 {nNew} ／ 旧 {nOld}");
            Expect($"同じ {n} 戦: verbose の有無で勝敗 ／ 決着T が同じ（規定）", diff == 0, $"違い {diff}");
        }
        {
            // 規定のヒサ ＝ 第337期の版 MI-b: 同じ台・同じ seed で勝敗 ／ 決着T ／ 引き継ぎの回数がすべて一致する
            int diff = 0, n = 0;
            foreach (var (_, f) in Boards())
                foreach (var w in Waves)
                    for (int sd = 0; sd < 10; sd++)
                    {
                        var rn = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        var rv = BattleEngine.Run(BattleEngine.Materialize(M337.Apply(f, M337.Vers[1]), BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        n++;
                        if (rn.PlayerWon != rv.PlayerWon || rn.Turns != rv.Turns
                            || (rn.TallyByUnit.GetValueOrDefault("hisa")?.Mi337Fires ?? 0) != (rv.TallyByUnit.GetValueOrDefault("hisa")?.Mi337Fires ?? 0)) diff++;
                    }
            Expect($"代表台 5 × 5 波 × seed 0..9（{n} 戦）: 規定のヒサは第337期の版 MI-b（`mark337` の b）と勝敗 ／ 決着T ／ 引き継ぎの回数が一致", diff == 0, $"違い {diff}");
        }
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            int engine = Now("BattleCore/BattleEngine.cs"), traits = Now("BattleCore/Traits.cs");
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 7e8143f:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int he = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, ht = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第337期（7e8143f）と同じ）", engine == he && traits == ht && he > 0, $"engine {he} → {engine}・traits {ht} → {traits}");
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
