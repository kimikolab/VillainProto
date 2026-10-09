using BattleCore;
using static Common;
using static Hush304Diag;

// =====================================================================================
// hush305 —— 第305期「粛の『騎士の怒りでひびが入る』中間の版（HC ＋ HD15 ／ HC ＋ HD20）」。
// 指示書は design/PHASE305_HUSH_CRACK_SPEC.md ／ 報告は design/PHASE305_HUSH_CRACK.md。
// 版の作り方・台・1戦の集計は `hush304` のもの（`Hush304Diag` の internal）をそのまま使う。版は**第2波の敵だけ**を差し替える。
//
//     dotnet run --project BattleSim -c Release 0 hush305 p0 [seeds]      # §3 Phase 0（HC の戦で、止めた数 ＋ 止められた騎士の斬り返しの累計 → HCD15 ／ HCD20 の砕けるT の見込み）
//     dotnet run --project BattleSim -c Release 0 hush305 ver [seeds]     # §4-2 代表台 × 本編 第1〜5波 × 5列（S ／ HD20 ／ HCD ／ HCD15 ／ HCD20）
//     dotnet run --project BattleSim -c Release 0 hush305 wave2 [seeds]   # §4-3 標軸の行 × 第2波 × 5列と、砕けた戦 ／ T ／ ひびの内訳 ／ 砕けた後に通った行動
//     dotnet run --project BattleSim -c Release 0 hush305 cmp [seeds]     # §4-3 `compare` 64 行 × 第2波 × 5列・分布・帯（50〜80%）に入った行・(G2)
//     dotnet run --project BattleSim -c Release 0 hush305 check           # 自己検査
// =====================================================================================
static class Hush305Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        switch (mode)
        {
            case "p0": Phase0(int.Parse(A(3, "200"))); return;
            case "ver": Ver(int.Parse(A(3, "200"))); return;
            case "wave2": Wave2(int.Parse(A(3, "200"))); return;
            case "cmp": Cmp(int.Parse(A(3, "200"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("hush305: モードは p0 / ver / wave2 / cmp / check。"); return;
        }
    }

    // HCD は第304期の HCD（HC ＋ HD10・§7 の線で選ばれた方）を固定で書く（`PickHd` を回し直さない）。
    static readonly V VHCD = new("HCD", "HC ＋ HD10", EnemyCatalog.HusherHD10, EnemyCatalog.KnightGR);
    static readonly V VHCD15 = new("HCD15", "HC ＋ HD15", EnemyCatalog.HusherHD15, EnemyCatalog.KnightGR);
    static readonly V VHCD20 = new("HCD20", "HC ＋ HD20", EnemyCatalog.HusherHD20, EnemyCatalog.KnightGR);
    static V[] Vers => new[] { VS, VHD20, VHCD, VHCD15, VHCD20 };

    /// <summary>§0 の狙いの帯（第2波の勝率）。</summary>
    const double BandLo = 50, BandHi = 80;
    static readonly string[] Targets = { "試遊・標 ボス台", "標経済 (ヒサ×ザン×ミサ)", "反撃改2 (ガン×カド)" };

    // ---------------------------------------------------------------------------------
    // §3 Phase 0
    // ---------------------------------------------------------------------------------
    static void Phase0(int seeds)
    {
        Console.WriteLine($"# 第305期 Phase 0（seed 0..{seeds - 1}・第2波・HC ＝ 騎士の斬り返し ＋ 既定の粛）");
        Console.WriteLine();
        Console.WriteLine("HC の戦は粛が生きている間は S と同じ戦（騎士の斬り返しは全部止まる）なので、そこで数えた「粛が単独の原因で止めた数」の累計が、そのまま HCD の版でひびが入る数になる（砕けるまで）。");
        Console.WriteLine("見込み ＝ 累計が 10 ／ 15 ／ 20 に達したT（中央・平均・届かない ＝ 粛が倒れるか決着が先）。止めた ＝ 味方の行動 ／ 騎士の斬り返し（1戦）。");
        Console.WriteLine();
        var w = Wave2Of(VHC);
        var cmpRows = CompareBuilds().Select(r => (r.Name, r.F, A: Many(r.F, Wave2Of(VS), seeds, verbose: true))).ToList();
        var top = cmpRows.OrderByDescending(x => x.A.BlockOpp + x.A.BlockOwn).Take(10).Select(x => (x.Name, x.F)).ToList();
        var rows = Boards().Concat(top.Where(t => !Boards().Any(b => b.Name == t.Name)))
            .Concat(new[] { "反撃改3 (カド×ハギ)" }.Where(n => !top.Any(t => t.Name == n)).Select(n => (n, CompareBuilds().First(r => r.Name == n).F))).ToList();
        Console.WriteLine("| 行 | 止めた（味方 ／ 騎士） | 騎士の割合 | T1 | T2 | T3 | T4 | T5 | T6+ | HCD（10） | HCD15 | HCD20 | 粛が倒れた戦（T） |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|---|---|---|---|");
        foreach (var (n, f) in rows)
        {
            var a = Many(f, w, seeds, verbose: true);
            long all = a.BlockOpp + a.BlockOwn;
            var bt = Enumerable.Range(1, 5).Select(t => $"{a.P(a.BlocksByTurn[t]):F2}").Append($"{a.P(a.BlocksByTurn.Skip(6).Sum()):F2}");
            Console.WriteLine($"| {n} | {a.P(a.BlockOpp):F2} ／ {a.P(a.BlockOwn):F2} | {(all == 0 ? "—" : $"{100.0 * a.BlockOwn / all:F0}%")} | " + string.Join(" | ", bt)
                + $" | {Est(a.Est10)} | {Est(a.Est15)} | {Est(a.Est20)} | {Pct(a.HeraldDeadN, a.N)}（{Avg(a.HeraldDeadT, a.HeraldDeadN)}） |");
        }
    }

    // ---------------------------------------------------------------------------------
    // §4-2 代表台 × 本編 第1〜5波
    // ---------------------------------------------------------------------------------
    static void Ver(int seeds)
    {
        Console.WriteLine($"# 第305期 代表台 × 本編 第1〜5波 × 版（seed 0..{seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("版は第2波の敵だけを差し替える。S から ±10pt 以上動いたセルは太字。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        foreach (var (n, f) in Boards())
        {
            var s0 = Enumerable.Range(0, StagesH305.Count).Select(i => Many(f, WaveOf((i + 1).ToString()).Make, seeds)).ToArray();
            foreach (var v in Vers)
            {
                var a = Enumerable.Range(0, StagesH305.Count).Select(i => i == 1 ? Many(f, Wave2Of(v), seeds) : v == VS ? s0[i] : Many(f, WaveOf((i + 1).ToString()).Make, seeds)).ToArray();
                Console.WriteLine($"| {n} | {v.Key} | " + string.Join(" | ", a.Select((x, i) => v != VS && Math.Abs(x.Win - s0[i].Win) >= 10 ? $"**{Cell(x)}**" : Cell(x))) + " |");
            }
        }
    }

    // ---------------------------------------------------------------------------------
    // §4-3 第2波の表と中身
    // ---------------------------------------------------------------------------------
    static void Wave2(int seeds)
    {
        var vers = Vers;
        var rows = MarkRows();
        var res = new Dictionary<(string, string), Agg>();
        foreach (var (n, f) in rows) foreach (var v in vers) res[(n, v.Key)] = Many(f, Wave2Of(v), seeds, verbose: true);
        Console.WriteLine($"# 第305期 標軸の行 × 第2波（seed 0..{seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine($"勝率（倒しT）／ 粛が倒れた戦（倒れたT）。S から ±10pt 以上は太字・帯（{BandLo:F0}〜{BandHi:F0}%）に入ったセルは「◎」。");
        Console.WriteLine();
        Console.WriteLine("| 行 | " + string.Join(" | ", vers.Select(v => v.Key)) + " |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("---|", vers.Length)));
        foreach (var (n, _) in rows)
            Console.WriteLine($"| {n} | " + string.Join(" | ", vers.Select(v =>
            {
                var a = res[(n, v.Key)];
                string c = $"{Cell(a)} ／ {Pct(a.HeraldDeadN, a.N)}（T{Avg(a.HeraldDeadT, a.HeraldDeadN)}）";
                if (v != VS && Math.Abs(a.Win - res[(n, "S")].Win) >= 10) c = $"**{c}**";
                return a.Win >= BandLo && a.Win <= BandHi ? "◎ " + c : c;
            })) + " |");
        foreach (var v in vers.Where(v => v != VS))
        {
            Console.WriteLine();
            Console.WriteLine($"## {v.Key}（{v.Name}）—— 砕けた戦 ／ 砕けたT（砕けなかった戦）・ひびの内訳 ・砕けた後に通った行動（1戦）");
            Console.WriteLine();
            Console.WriteLine("ひび ＝ 粛が単独の原因で止めた行動（味方の行動 ／ 騎士の斬り返し・砕けるまで）。通った ＝ 砕けた後、粛が生きている間に通ったターン外の行動（味方 ／ 騎士の斬り返し）。斬り返し ＝ 騎士の斬り返し（出た ／ 粛で止まった）。");
            Console.WriteLine();
            Console.WriteLine("| 行 | 勝率 S → 版 | 砕けた戦 | 砕けたT | ひび（味方 ／ 騎士） | 騎士の割合 | 通った（味方 ／ 騎士） | 斬り返し（出た ／ 止まった） |");
            Console.WriteLine("|---|---|--:|---|---|--:|---|---|");
            foreach (var (n, _) in rows)
            {
                var a = res[(n, v.Key)]; var s = res[(n, "S")];
                long all = a.BlockOpp + a.BlockOwn;
                Console.WriteLine($"| {n} | {s.Win:F1} → {a.Win:F1} | {Pct(a.ShatterN, a.N)} | T{Avg(a.ShatterT, a.ShatterN)}（{Pct(a.N - a.ShatterN, a.N)}） | {a.P(a.BlockOpp):F2} ／ {a.P(a.BlockOwn):F2} | {(all == 0 ? "—" : $"{100.0 * a.BlockOwn / all:F0}%")} | {a.P(a.ShatterPassOpp):F2} ／ {a.P(a.PassKnight):F2} | {a.P(a.KnightRip):F2} ／ {a.P(a.KnightHushed):F2} |");
            }
        }
    }

    // ---------------------------------------------------------------------------------
    // §4-3 `compare` 64 行 × 第2波
    // ---------------------------------------------------------------------------------
    static void Cmp(int seeds)
    {
        var rows = CompareBuilds();
        var vers = Vers;
        var area = rows.Select(r => HasArea(r.F)).ToArray();
        var all = vers.ToDictionary(v => v.Key, v => rows.Select(r => Rate2(r.F, v, seeds)).ToArray());
        var s0 = all["S"];
        Console.WriteLine($"# 第305期 `compare` {rows.Length} 行 × 第2波 × 版（seed 0..{seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine($"第1・3〜5波は版で動かない。S から ±10pt 以上動いた行だけ（−10 以上は太字・+10 以上は斜体）。範囲 ／ 貫き ＝ 手番の攻撃型が単体でない駒がいる行（{area.Count(x => x)} 行）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 範囲 ／ 貫き | " + string.Join(" | ", vers.Select(v => v.Key)) + " |");
        Console.WriteLine("|---|:-:|" + string.Concat(Enumerable.Repeat("--:|", vers.Length)));
        for (int i = 0; i < rows.Length; i++)
        {
            if (!vers.Any(v => Math.Abs(all[v.Key][i] - s0[i]) >= 10.0)) continue;
            Console.WriteLine($"| {rows[i].Name} | {(area[i] ? "○" : "")} | " + string.Join(" | ", vers.Select(v =>
            {
                double x = all[v.Key][i], d = x - s0[i];
                return v != VS && d <= -10.0 ? $"**{x:F1}**" : v != VS && d >= 10.0 ? $"*{x:F1}*" : $"{x:F1}";
            })) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("## 分布（第304期 §5-4 と同じ形）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 0〜20 | 20〜50 | 50〜80 | 80〜100 | 64 行の平均 | 範囲 ／ 貫きの行の平均（S からの上げ） | ない行の平均（同） | 20% 未満の行 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|---|---|---|");
        foreach (var v in vers)
        {
            var a = all[v.Key];
            double ar = Enumerable.Range(0, rows.Length).Where(i => area[i]).Average(i => a[i]), arg = Enumerable.Range(0, rows.Length).Where(i => area[i]).Average(i => a[i] - s0[i]);
            double nr = Enumerable.Range(0, rows.Length).Where(i => !area[i]).Average(i => a[i]), nrg = Enumerable.Range(0, rows.Length).Where(i => !area[i]).Average(i => a[i] - s0[i]);
            var low = Enumerable.Range(0, rows.Length).Where(i => a[i] < 20).Select(i => rows[i].Name);
            Console.WriteLine($"| {v.Key} | {a.Count(x => x < 20)} | {a.Count(x => x >= 20 && x < 50)} | {a.Count(x => x >= 50 && x < 80)} | {a.Count(x => x >= 80)} | {a.Average():F1} | {ar:F1}（{arg:+0.0;-0.0;0.0}） | {nr:F1}（{nrg:+0.0;-0.0;0.0}） | {(low.Any() ? string.Join("・", low) : "—")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"## 帯（第2波 {BandLo:F0}〜{BandHi:F0}%）に入った行");
        Console.WriteLine();
        foreach (var v in vers)
        {
            var band = Enumerable.Range(0, rows.Length).Where(i => all[v.Key][i] >= BandLo && all[v.Key][i] <= BandHi).Select(i => $"{rows[i].Name} {all[v.Key][i]:F1}");
            Console.WriteLine($"- {v.Key}: " + (band.Any() ? string.Join("・", band) : "—"));
        }
        Console.WriteLine();
        Console.WriteLine("狙いの3台（ボス台は代表台なので `hush305 wave2` で読む）: " + string.Join(" ／ ", vers.Select(v => $"{v.Key} " + string.Join("・",
            Targets.Where(t => rows.Any(r => r.Name == t)).Select(t => { int i = Array.FindIndex(rows, r => r.Name == t); return $"{all[v.Key][i]:F1}"; })))));
        Console.WriteLine();
        Console.WriteLine("## (G2)（第2波で −10.0pt 以上落ちた行）");
        Console.WriteLine();
        bool any = false;
        foreach (var v in vers.Where(v => v != VS))
            for (int i = 0; i < rows.Length; i++)
            {
                if (all[v.Key][i] - s0[i] > -10.0) continue;
                any = true;
                var parts = new List<string>(); bool broken = false;
                foreach (var d in rows[i].F.Occupied().Select(o => o.Def).Distinct())
                {
                    var others = Enumerable.Range(0, rows.Length).Where(j => j != i && rows[j].F.Occupied().Any(o => ReferenceEquals(o.Def, d))).ToArray();
                    if (others.Length == 0) { parts.Add($"{Short(d)}: 他の行 0（分解が成立しない）"); continue; }
                    double delta = others.Average(j => (all[v.Key][j] - s0[j]) / 5.0);
                    if (delta <= -3.0) broken = true;
                    parts.Add($"{Short(d)} {delta:+0.0;-0.0;0.0}pt（{others.Length} 行）");
                }
                Console.WriteLine($"- {v.Key} {rows[i].Name}（{s0[i]:F1} → {all[v.Key][i]:F1}）: " + string.Join(" ／ ", parts) + (broken ? " → **壊れ**" : " → 編成上の制約"));
            }
        if (!any) Console.WriteLine("どの版も −10.0pt 以上落ちた行は無い（(G2) の分解の対象なし）。");
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
    // ---------------------------------------------------------------------------------
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static int Count(string path, string needle)
    {
        string s = File.ReadAllText(path);
        int c = 0, i = 0;
        while ((i = s.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { c++; i += needle.Length; }
        return c;
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# hush305 check —— 第305期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        Expect("(a) 定義: HD15 は粛に札を1枚足しただけ（数値・Id は既定のまま）・`Stages` に無い・HCD15 ／ HCD20 の第2波は粛の伝令と巡礼騎士の席だけを差し替える",
            EnemyCatalog.HusherHD15.Traits.SequenceEqual(new[] { TraitId.Hush, TraitId.HushShatter15 }) && EnemyCatalog.HusherHD15.Id == EnemyCatalog.Husher.Id
            && EnemyCatalog.HusherHD15.MaxHp == EnemyCatalog.Husher.MaxHp && EnemyCatalog.HusherHD15.Attack == EnemyCatalog.Husher.Attack && EnemyCatalog.HusherHD15.Speed == EnemyCatalog.Husher.Speed
            && !StagesH305.Any(s => s.Enemy.Occupied().Any(o => o.Def == EnemyCatalog.HusherHD15))
            && EnemyOf(VHCD15).Occupied().Count(o => o.Def == EnemyCatalog.KnightGR) == 2 && EnemyOf(VHCD15).Occupied().Count(o => o.Def == EnemyCatalog.HusherHD15) == 1
            && EnemyOf(VHCD20).Occupied().Count(o => o.Def == EnemyCatalog.HusherHD20) == 1 && EnemyOf(VHCD15).Occupied().Count() == StagesH305[1].Enemy.Occupied().Count());

        (bool Before, bool Last, bool After, int Cracks, bool Alive) Shatter(UnitDef herald, int n)
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Golm), Formation.Build(front1: EnemyCatalog.KnightGR, center: herald), out var p, out var e);
            foreach (var u in p.Concat(e)) { u.MaxHp = 1000; u.Hp = 1000; }
            bool before = true;
            for (int i = 0; i < n - 1; i++) before &= !ctx.CanActOutOfTurn(i % 2 == 0 ? p[0] : e[0], OutOfTurnRoute.Avenge);
            bool last = ctx.CanActOutOfTurn(p[0], OutOfTurnRoute.Avenge);
            bool after = ctx.CanActOutOfTurn(p[0], OutOfTurnRoute.Avenge) && ctx.CanActOutOfTurn(e[0], OutOfTurnRoute.Avenge);
            return (before, last, after, ctx.HushCracks, e.First(u => u.Def.Id == "husher").IsAlive);
        }
        var h15 = Shatter(EnemyCatalog.HusherHD15, 15); var h20 = Shatter(EnemyCatalog.HusherHD20, 20);
        Expect("(b) HD15 ／ HD20: 14 ／ 19 回は止まる ／ 15 ／ 20 回目も止まり、その後は両陣営とも通る ／ 保持者は生きている",
            h15.Before && !h15.Last && h15.After && h15.Cracks == 15 && h15.Alive && h20.Before && !h20.Last && h20.After && h20.Cracks == 20 && h20.Alive,
            $"HD15 ひび {h15.Cracks}・後 {h15.After}・HD20 ひび {h20.Cracks}・後 {h20.After}");

        // (c) 止められた騎士の斬り返しがひびに入る（HCD15 ／ HCD20）・14 本の後の騎士の1本で砕け、次のターンの斬り返しは出る
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Golm), Formation.Build(front1: EnemyCatalog.KnightGR, center: EnemyCatalog.HusherHD15), out var p, out var e);
            foreach (var u in p.Concat(e)) { u.MaxHp = 1000; u.Hp = 1000; }
            var k = e.First(u => u.Def.Id == "knight_g");
            for (int i = 0; i < 14; i++) ctx.CanActOutOfTurn(p[0], OutOfTurnRoute.Avenge);
            ctx.ApplyDamage(k, 5, p[0], pattern: AttackPattern.Single);
            int c15 = ctx.HushCracks; bool sh = ctx.HushShattered; long rip0 = Tal(ctx, "knight_g").KnightRipostes;
            TurnProp.SetValue(ctx, 2);
            ctx.ApplyDamage(k, 5, p[0], pattern: AttackPattern.Single);
            long rip1 = Tal(ctx, "knight_g").KnightRipostes;
            var ctx2 = Ctx(Formation.Build(front1: UnitCatalog.Golm), Formation.Build(front1: EnemyCatalog.KnightGR, center: EnemyCatalog.HusherHD20), out var p2, out var e2);
            foreach (var u in p2.Concat(e2)) { u.MaxHp = 1000; u.Hp = 1000; }
            ctx2.ApplyDamage(e2.First(u => u.Def.Id == "knight_g"), 5, p2[0], pattern: AttackPattern.Single);
            Expect("(c) HCD15: 14 本の後、止められた騎士の斬り返しが 15 本目のひびになって砕ける ／ 次のターンの斬り返しは出る ／ HCD20 でも止められた斬り返しはひび1つ",
                c15 == 15 && sh && rip0 == 0 && rip1 == 1 && ctx2.HushCracks == 1, $"ひび {c15}・砕けた {sh}・砕けた後の斬り返し {rip1}・HCD20 ひび {ctx2.HushCracks}");
        }

        // (d) HCD（＝ HC ＋ HD10）の数字が第304期と同じ: 代表台 × 第2波 × seed 0..199（第304期 §5-2）
        {
            var expect = new Dictionary<string, double> { ["試遊・標 ボス台"] = 98.5, ["試遊・標 循環"] = 100.0, ["試遊・標 三人組"] = 96.5, ["試遊・標 守り型"] = 100.0, ["標経済 (ヒサ×ザン×ミサ)"] = 86.5 };
            var got = Boards().Where(b => expect.ContainsKey(b.Name)).Select(b => (b.Name, W: Many(b.F, Wave2Of(VHCD), 200).Win)).ToList();
            var hd20 = Many(Boards().First(b => b.Name == "試遊・標 ボス台").F, Wave2Of(VHD20), 200).Win;
            Expect("(d) 第304期の版を測り直すと同じ数字（HCD × 代表台 5 ／ HD20 × ボス台 65.5・第304期 §5-2）",
                got.All(g => Math.Abs(g.W - expect[g.Name]) < 1e-9) && Math.Abs(hd20 - 65.5) < 1e-9, string.Join("・", got.Select(g => $"{g.W:F1}")) + $"・HD20 {hd20:F1}");
        }

        int pick = Directory.GetFiles("BattleCore", "*.cs").Sum(f => Count(f, "PickOne("));
        Expect("(e) `PickOne(` の出現数が第304期と同じ（BattleCore）", pick == 36, $"{pick}");
        {
            int diff = 0, n2 = 0;
            foreach (var v in new[] { VHCD15, VHCD20 })
                foreach (var (_, f) in Boards()) for (int s = 0; s < 10; s++)
                    {
                        var a = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), Wave2Of(v)(), s, verbose: false);
                        var b = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), Wave2Of(v)(), s, verbose: true);
                        n2++; if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns) diff++;
                    }
            Expect("(f) verbose の有無で勝敗・決着T が同じ（HCD15 ／ HCD20 × 代表台 × 第2波 × seed 0..9）", diff == 0, $"{n2} 戦・違い {diff}");
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "すべて ○" : $"**× {_fail} 件**");
    }
}
