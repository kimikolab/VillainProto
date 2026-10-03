using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FC = FireCycleDiag;
using BH = BurnHitDiag;

// burnadopt doha —— §3 T3-238 で H-分担 のときドハが落ちやすくなる理由。
// 版 H0 ／ H-分担（規定）／ H-敵だけ × 本編 第2〜5波 × 400/300・200/200 × seed 0..199（verbose・害の帳簿つき）。
static partial class BurnAdoptDiag
{
    sealed class DAgg
    {
        public long N, Wins, AllSurv, DohaFall, DohaFallTurn, DohaHealed, Turns;
        public readonly long[] Amt = new long[DamageRoutes.Count], Fatal = new long[DamageRoutes.Count];
        public long BurnRelayed, BurnRelayedFatal;
        public readonly Dictionary<string, long> Fall = new();
        public readonly Dictionary<string, long> RelayFrom = new(), BurnRelayFrom = new();   // ドハが肩代わりした相手（量）
        public long DohaBurning;   // ドハ自身に燃焼が付いた戦
        public long HitFires, HitHeal, HitDmg, RelayChances, RelayChanceLv;   // ドハの被弾の燃焼 ／ 中継の一撃で燃えていた機会
        public void Add(DAgg o)
        {
            N += o.N; Wins += o.Wins; AllSurv += o.AllSurv; DohaFall += o.DohaFall; DohaFallTurn += o.DohaFallTurn; DohaHealed += o.DohaHealed; Turns += o.Turns;
            for (int i = 0; i < Amt.Length; i++) { Amt[i] += o.Amt[i]; Fatal[i] += o.Fatal[i]; }
            BurnRelayed += o.BurnRelayed; BurnRelayedFatal += o.BurnRelayedFatal; DohaBurning += o.DohaBurning;
            HitFires += o.HitFires; HitHeal += o.HitHeal; HitDmg += o.HitDmg; RelayChances += o.RelayChances; RelayChanceLv += o.RelayChanceLv;
            foreach (var (k, v) in o.Fall) Fall[k] = Fall.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.RelayFrom) RelayFrom[k] = RelayFrom.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.BurnRelayFrom) BurnRelayFrom[k] = BurnRelayFrom.GetValueOrDefault(k) + v;
        }
    }

    static readonly string[] DohaWaves = { "本編 第2〜5波" };

    static DAgg DohaCell(AVer v, Formation f, int s)
    {
        var parts = new DAgg[4 * Seeds];
        Parallel.For(0, 4 * Seeds, k =>
        {
            int w = k / Seeds, seed = k % Seeds;
            var p = BattleEngine.Materialize(BH.Mark(f, v.Card), BattleContext.PlayerTeam);
            var r = BattleEngine.Run(p, FC.WaveOf(w, BA.Scales[s].Sc)(), seed, verbose: true, ember: v.Ember, harm: new HarmRule(true));
            var a = new DAgg { N = 1, Turns = r.Turns };
            if (r.PlayerWon) a.Wins++;
            if (r.PlayerWon && p.All(u => u.IsAlive)) a.AllSurv++;
            foreach (var u in p) if (!u.IsAlive) a.Fall[u.Def.Name] = a.Fall.GetValueOrDefault(u.Def.Name) + 1;
            var doha = p.First(u => u.Def.Id == "doha");
            if (!doha.IsAlive)
            {
                a.DohaFall++;
                var dt = r.Events.LastOrDefault(e => e.Kind == BattleEventKind.Death && e.TargetId == doha.InstanceId);
                a.DohaFallTurn += dt?.Turn ?? r.Turns;
            }
            if (r.TallyByUnit.TryGetValue("doha", out var t))
            {
                a.DohaHealed += t.Healed;
                if (t.HarmAmount is { } am) for (int i = 0; i < am.Length; i++) a.Amt[i] += am[i];
                if (t.HarmFatal is { } fa) for (int i = 0; i < fa.Length; i++) a.Fatal[i] += fa[i];
                a.BurnRelayed += t.HarmBurnRelayed; a.BurnRelayedFatal += t.HarmBurnRelayedFatal;
            }
            if (r.BurnHit is { } bh)
            {
                if (bh.ByTarget.TryGetValue("0:doha", out var tv)) { a.HitFires += tv[0]; a.HitDmg += tv[1]; a.HitHeal += tv[2]; }
                if (bh.RelayChanceBy.TryGetValue("0:doha", out var rc)) { a.RelayChances += rc[0]; a.RelayChanceLv += rc[1]; }
            }
            if (r.Events.Any(e => e.Kind == BattleEventKind.StatusGain && e.TargetId == doha.InstanceId && e.Text is "burn")) a.DohaBurning++;
            // ドハが肩代わりした相手（ログの「ドハ が X の痛みを引き受けた」の回数）
            foreach (var l in r.Log)
            {
                const string mark = "が ";
                if (l.Text.Contains(doha.Def.Name + " が ") && l.Text.Contains("の痛みを引き受けた"))
                {
                    int i0 = l.Text.IndexOf(mark) + mark.Length, i1 = l.Text.IndexOf(" の痛み");
                    if (i1 > i0) { string who = l.Text[i0..i1]; a.RelayFrom["（ログ）" + who] = a.RelayFrom.GetValueOrDefault("（ログ）" + who) + 1; }
                }
            }
            parts[k] = a;
        });
        var tot = new DAgg();
        foreach (var a in parts) tot.Add(a);
        return tot;
    }

    static void Doha()
    {
        var t0 = DateTime.Now;
        Console.WriteLine("# 第256期 §3 —— T3-238 でドハが落ちやすくなる理由");
        Console.WriteLine();
        Console.WriteLine($"台 T3-238（{BA.SeatsNamed(Board)}）× 本編 第2〜5波 × seed 0..{Seeds - 1}（verbose・害の帳簿 `HarmRule(true)`）。");
        Console.WriteLine("版: " + string.Join(" ／ ", Versions.Select(v => $"{v.Name} ＝ {v.What}")));
        Console.WriteLine();
        foreach (int s in new[] { 1, 0 })
        {
            Console.WriteLine($"## {BA.Scales[s].Name}");
            Console.WriteLine();
            var cells = Versions.Select(v => (v, a: DohaCell(v, Board, s))).ToList();
            Console.WriteLine("| 版 | 勝率 | 全員生存 | 決着T | ドハ落ち % | 落ちたT（平均） | ドハの回復/戦 | ドハに燃焼が付いた戦 % |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
            foreach (var (v, a) in cells)
                Console.WriteLine($"| {v.Name} | {Pct(a.Wins, a.N)} | {Pct(a.AllSurv, a.N)} | {Per(a.Turns, a.N)} | {Pct(a.DohaFall, a.N)} | {Per(a.DohaFallTurn, a.DohaFall)} | {Per(a.DohaHealed, a.N)} | {Pct(a.DohaBurning, a.N)} |");
            Console.WriteLine();
            Console.WriteLine("ドハの被弾の燃焼（起きた回数 ／ 回復になった HP ／ 削った HP・/戦）と、**中継の一撃で燃えていた機会**（被弾の燃焼の対象外・回数 ／ 火勢 × 6 の見込み・/戦）。");
            Console.WriteLine();
            Console.WriteLine("| 版 | 被弾の燃焼 回数 | 回復 | 削った | 中継で燃えていた機会 | 見込み（火勢 × 6） |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|");
            foreach (var (v, a) in cells)
                Console.WriteLine($"| {v.Name} | {Per(a.HitFires, a.N)} | {Per(a.HitHeal, a.N)} | {Per(a.HitDmg, a.N)} | {Per(a.RelayChances, a.N)} | {Per(6 * a.RelayChanceLv, a.N)} |");
            Console.WriteLine();
            Console.WriteLine("ドハが受けたダメージ（HP・/戦）。燃焼は「自分の燃焼」と「肩代わりで受けた燃焼」（`HarmBurnRelayed`）に分ける。");
            Console.WriteLine();
            Console.WriteLine("| 版 | 単体 | 薙ぎ | 貫き | 全体 | その他(敵) | 肩代わり（燃焼以外） | 燃焼・自分 | 燃焼・肩代わり | 巻き込み | 毒 | 合計 |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            foreach (var (v, a) in cells)
            {
                long R(DamageRoute r) => a.Amt[(int)r];
                long own = R(DamageRoute.Burn) - a.BurnRelayed;
                Console.WriteLine($"| {v.Name} | {Per(R(DamageRoute.Single), a.N)} | {Per(R(DamageRoute.Sweep), a.N)} | {Per(R(DamageRoute.Pierce), a.N)} | {Per(R(DamageRoute.All), a.N)} | {Per(R(DamageRoute.Other), a.N)} | {Per(R(DamageRoute.Relay), a.N)} | {Per(own, a.N)} | {Per(a.BurnRelayed, a.N)} | {Per(R(DamageRoute.Friendly), a.N)} | {Per(R(DamageRoute.Poison), a.N)} | {Per(a.Amt.Sum(), a.N)} |");
            }
            Console.WriteLine();
            Console.WriteLine("ドハが倒れた一撃の経路（件）。");
            Console.WriteLine();
            Console.WriteLine("| 版 | " + string.Join(" | ", DamageRoutes.Names) + " | うち燃焼・肩代わり |");
            Console.WriteLine("|---|" + string.Concat(DamageRoutes.Names.Select(_ => "--:|")) + "--:|");
            foreach (var (v, a) in cells)
                Console.WriteLine($"| {v.Name} | " + string.Join(" | ", a.Fatal) + $" | {a.BurnRelayedFatal} |");
            Console.WriteLine();
            Console.WriteLine("ドハが肩代わりした相手（ログの「痛みを引き受けた」の回数/戦）。");
            Console.WriteLine();
            foreach (var (v, a) in cells)
            {
                Console.WriteLine($"- {v.Name}: " + string.Join("・", a.RelayFrom.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key.Replace("（ログ）", "")} {Per(kv.Value, a.N)}")));
            }
            Console.WriteLine();
            Console.WriteLine("落ちた駒（% ＝ 戦あたり）: ");
            foreach (var (v, a) in cells)
                Console.WriteLine($"- {v.Name}: " + string.Join("・", a.Fall.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {Pct(kv.Value, a.N)}")));
            Console.WriteLine();
        }
        Console.WriteLine($"（{(DateTime.Now - t0).TotalSeconds:F0} 秒）");
    }
}
