using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using BS = BlazeSurgeDiag;
using FK = FireKindleDiag;

// foesurge の集計（1つのセル ＝ 台 × 版 × 波 × 倍率）。台本（verbose）と火勢の帳簿を読むだけで、盤面は1ビットも動かさない。
// 「爆炎の後」は戦の最初の爆炎の手番が閉じた後から、決着（的の波は 8 ターン）まで。
// 被弾の燃焼は台本の `Status`「燃焼」で `ActorId` がある件（第255期・一撃の主）、削った HP はその直後の同じ駒への出どころなしの `Damage`。
static partial class FoeSurgeDiag
{
    const int TM = FK.TM;

    internal sealed class XAgg
    {
        public long N, BlazeBattles, Blazes, FirstBlazeTurnSum, AftTurns, FoeDeathsAft, Wins;
        // 表B: 敵に入った被弾の燃焼（回 ／ 名目 ／ HP）——戦全体 ／ 爆炎の後。ターン頭の刻み（名目 ／ HP）も同じく。
        public long HitAll, HitAllNom, HitAllHp, HitAft, HitAftNom, HitAftHp, TickAll, TickAllHp, TickAft, TickAftHp;
        public long HitKillsAft;
        // 表C: 駒（味方の Def.Id）→ { 爆炎の後の直の与ダメ, 爆炎の後に起こした被弾の燃焼の HP, 爆炎の後の当てた回数, 爆炎の後に起こした被弾の燃焼の回数, 戦全体の直の与ダメ }
        public readonly Dictionary<string, long[]> ByUnit = new();
        // 表D: 周回の頭の敵（生きている）——火勢4 の数・燃えている数（T1..T8）・爆炎の次の周回の火勢4 の数
        public readonly long[] FoeLv4Sum = new long[TM + 1], FoeBurnSum = new long[TM + 1], FoeLvCnt = new long[TM + 1];
        public long AfterFoeLv4Sum, AfterFoeLv4N;
        // 表F（的・一）: 周回ごとの敵の火勢の和・敵への与ダメ（出どころを問わない）・被弾の燃焼の HP・その周回に入った戦の数・最初の爆炎の周回
        public readonly long[] TurnFoeLvSum = new long[TM + 1], TurnDmg = new long[TM + 1], TurnHitHp = new long[TM + 1], TurnReached = new long[TM + 1], BlazeTurnHist = new long[TM + 2];

        public void Merge(XAgg o)
        {
            N += o.N; BlazeBattles += o.BlazeBattles; Blazes += o.Blazes; FirstBlazeTurnSum += o.FirstBlazeTurnSum; AftTurns += o.AftTurns; FoeDeathsAft += o.FoeDeathsAft; Wins += o.Wins;
            HitAll += o.HitAll; HitAllNom += o.HitAllNom; HitAllHp += o.HitAllHp; HitAft += o.HitAft; HitAftNom += o.HitAftNom; HitAftHp += o.HitAftHp;
            TickAll += o.TickAll; TickAllHp += o.TickAllHp; TickAft += o.TickAft; TickAftHp += o.TickAftHp; HitKillsAft += o.HitKillsAft;
            foreach (var (k, v) in o.ByUnit)
            {
                if (!ByUnit.TryGetValue(k, out var r)) ByUnit[k] = r = new long[5];
                for (int i = 0; i < 5; i++) r[i] += v[i];
            }
            for (int t = 0; t <= TM; t++)
            {
                FoeLv4Sum[t] += o.FoeLv4Sum[t]; FoeBurnSum[t] += o.FoeBurnSum[t]; FoeLvCnt[t] += o.FoeLvCnt[t];
                TurnFoeLvSum[t] += o.TurnFoeLvSum[t]; TurnDmg[t] += o.TurnDmg[t]; TurnHitHp[t] += o.TurnHitHp[t]; TurnReached[t] += o.TurnReached[t];
            }
            for (int t = 0; t <= TM + 1; t++) BlazeTurnHist[t] += o.BlazeTurnHist[t];
            AfterFoeLv4Sum += o.AfterFoeLv4Sum; AfterFoeLv4N += o.AfterFoeLv4N;
        }

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e, bool target)
        {
            N++;
            if (r.PlayerWon) Wins++;
            var ev = r.Events;
            int n = ev.Count;
            if (target) while (n > 0 && ev[n - 1].Turn > TM) n--;
            var foes = new HashSet<int>(e.Select(u => u.InstanceId));
            foreach (var x in ev)
                if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid && x.Team is int tm && tm != BattleContext.PlayerTeam) foes.Add(sid);
            var mine = p.ToDictionary(u => u.InstanceId, u => u.Def.Id);
            int? borg = p.FirstOrDefault(u => u.Def.Id == "borg")?.InstanceId;
            int lastTurn = target ? Math.Min(r.Turns, TM) : r.Turns;

            // 最初の爆炎（その手番の枠の終わり）
            int he = int.MaxValue, bt = 0;
            for (int i = 0; i < n; i++)
            {
                var x = ev[i];
                if (x.Kind != BattleEventKind.FireLevel || x.Text != FireLevelLabels.Blaze || x.ActorId != borg) continue;
                Blazes++;
                if (bt != 0) continue;
                bt = x.Turn;
                he = n;
                foreach (var h in r.Hands) if (h.ActorId == borg && h.EventStart <= i && i < h.EventEnd) he = Math.Min(he, h.EventEnd);
            }
            if (bt != 0)
            {
                BlazeBattles++; FirstBlazeTurnSum += bt; AftTurns += Math.Max(0, lastTurn - bt + 1);
                BlazeTurnHist[Math.Min(bt, TM + 1)]++;
            }
            else BlazeTurnHist[0]++;
            for (int t = 1; t <= Math.Min(lastTurn, TM); t++) TurnReached[t]++;

            long[] U(string id) { if (!ByUnit.TryGetValue(id, out var a)) ByUnit[id] = a = new long[5]; return a; }
            for (int j = 0; j < n; j++)
            {
                var x = ev[j];
                bool aft = j >= he;
                if (x.Kind == BattleEventKind.Status && x.Text == "燃焼" && x.TargetId is int tg && foes.Contains(tg))
                {
                    long hp = 0; bool died = false;
                    for (int k = j + 1; k < Math.Min(n, j + 8); k++)
                    {
                        var y = ev[k];
                        if (y.Kind == BattleEventKind.Damage && y.TargetId == tg && y.ActorId is null) { hp = y.Amount; died = y.HpAfter <= 0; break; }
                        if (y.Kind == BattleEventKind.Status) break;
                    }
                    if (x.ActorId is int ac)
                    {
                        HitAll++; HitAllNom += x.Amount; HitAllHp += hp;
                        if (x.Turn <= TM) TurnHitHp[x.Turn] += hp;
                        if (aft)
                        {
                            HitAft++; HitAftNom += x.Amount; HitAftHp += hp; if (died) HitKillsAft++;
                            if (mine.TryGetValue(ac, out var aid)) { var a = U(aid); a[1] += hp; a[3]++; }
                        }
                    }
                    else { TickAll++; TickAllHp += hp; if (aft) { TickAft++; TickAftHp += hp; } }
                }
                if (x.Kind == BattleEventKind.Damage && x.TargetId is int dt && foes.Contains(dt))
                {
                    if (x.Turn <= TM) TurnDmg[x.Turn] += x.Amount;
                    if (x.ActorId is int ac && mine.TryGetValue(ac, out var aid) && !x.FriendlyFire)
                    {
                        var a = U(aid); a[4] += x.Amount;
                        if (aft) { a[0] += x.Amount; a[2]++; }
                    }
                }
                if (aft && x.Kind == BattleEventKind.Death && x.TargetId is int dd && foes.Contains(dd)) FoeDeathsAft++;
            }

            var led = r.FireLevels;
            if (led is null) return;
            var lv4 = new Dictionary<int, int>();
            foreach (var s in led.Snaps)
            {
                if (s.Team == BattleContext.PlayerTeam || s.Turn < 1 || s.Turn > TM) continue;
                if (target && s.Turn > lastTurn) continue;
                FoeLvCnt[s.Turn]++;
                TurnFoeLvSum[s.Turn] += s.Level;
                if (s.Burning) FoeBurnSum[s.Turn]++;
                if (s.Level >= 4) { FoeLv4Sum[s.Turn]++; lv4[s.Turn] = lv4.GetValueOrDefault(s.Turn) + 1; }
            }
            if (bt != 0 && bt + 1 <= lastTurn) { AfterFoeLv4Sum += lv4.GetValueOrDefault(bt + 1); AfterFoeLv4N++; }
        }
    }

    // ---------------------------------------------------------------------------------
    // 表
    // ---------------------------------------------------------------------------------
    static XAgg XG(string b, string v, (int W, int S)[] cells) { var m = new XAgg(); foreach (var (w, s) in cells) m.Merge(XAt(b, v, w, s)); return m; }
    static BS.SAgg SG(string b, string v, (int W, int S)[] cells) { var m = new BS.SAgg(); foreach (var (w, s) in cells) m.Merge(BS.SAt(b, v, w, s)); return m; }
    static IEnumerable<(string B, string V)> Rows() { foreach (string b in Boards) foreach (var v in _vers) yield return (b, v.Name); }
    static string Per(long a, long n) => FK.Per(a, n);
    static string Pct(long a, long n) => FK.Pct(a, n);
    static string D1(double x) => double.IsNaN(x) ? "—" : (x >= 0 ? "+" : "") + x.ToString("F1");
    static double PerD(long a, long n) => n == 0 ? double.NaN : (double)a / n;
    static string UnitName(string id) => UnitCatalog.Everyone.FirstOrDefault(u => u.Id == id)?.Name ?? id;
    static bool HasW0 => _vers.Any(v => v.Name == "W0");

    static void TableQ2()
    {
        Console.WriteLine("## 表Q —— 爆炎の後に残る敵（Q0-2）");
        Console.WriteLine();
        Console.WriteLine("爆炎/戦 ／ 爆炎のあった戦 % ／ 最初の爆炎の周回 ／ 爆炎の一撃の後に生きていた敵/爆炎（うち燃えていた %）／ 最初の爆炎から決着までの周回（的は 8 ターンまで）／ 爆炎の後に倒れた敵/戦。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 爆炎/戦 | 爆炎のあった戦 % | 最初の爆炎 T | 生き残った敵/爆炎（燃えていた %） | 爆炎の後の周回 | 爆炎の後に倒れた敵/戦 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|---|--:|--:|");
        foreach (var (b, v) in Rows())
            foreach (var (gn, cells) in BS.Groups)
            {
                var x = XG(b, v, cells); var s = SG(b, v, cells);
                long alive = s.FoePre.Sum();
                Console.WriteLine($"| {b} | {v} | {gn} | {Per(x.Blazes, x.N)} | {Pct(x.BlazeBattles, x.N)} | {Per(x.FirstBlazeTurnSum, x.BlazeBattles)} | {Per(alive, s.Blazes)}（{Pct(alive - s.FoePre[0], alive)}） | {Per(x.AftTurns, x.BlazeBattles)} | {Per(x.FoeDeathsAft, x.N)} |");
            }
        Console.WriteLine();
    }

    static void TableB()
    {
        Console.WriteLine("## 表B —— 爆炎の後の被弾の燃焼（敵に入った分）");
        Console.WriteLine();
        Console.WriteLine("/戦（爆炎の無い戦も分母）。被弾 ＝ 回 ／ 削った HP（名目）。Δ は同じ台・同じ波の W0 との HP の差。刻み ＝ ターン頭の燃焼の刻みで削った HP。倒した ＝ 被弾の燃焼でとどめを刺した敵。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 爆炎の後: 被弾 回 ／ HP（名目） | Δ HP（W0 比） | 爆炎の後: 刻み HP | 爆炎の後: 被弾で倒した/戦 | 戦全体: 被弾 回 ／ HP | 戦全体: 刻み HP |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|---|--:|");
        foreach (var (b, v) in Rows())
            foreach (var (gn, cells) in BS.Groups)
            {
                var x = XG(b, v, cells);
                string d = HasW0 && v != "W0" ? D1(PerD(x.HitAftHp, x.N) - PerD(XG(b, "W0", cells).HitAftHp, XG(b, "W0", cells).N)) : "—";
                Console.WriteLine($"| {b} | {v} | {gn} | {Per(x.HitAft, x.N)} ／ {Per(x.HitAftHp, x.N)}（{Per(x.HitAftNom, x.N)}） | {d} | {Per(x.TickAftHp, x.N)} | {Per(x.HitKillsAft, x.N)} | {Per(x.HitAll, x.N)} ／ {Per(x.HitAllHp, x.N)} | {Per(x.TickAllHp, x.N)} |");
            }
        Console.WriteLine();
    }

    static readonly string[] CGroups = { "九/新兵 400/300", "本編 400/300", "重い波 400/300", "的・一（8T）" };
    static void TableC()
    {
        Console.WriteLine("## 表C —— 爆炎の後の駒ごとの与ダメ（/戦）");
        Console.WriteLine();
        Console.WriteLine("駒ごとに「直 ＋ 燃」——直 ＝ 爆炎の後にその駒の一撃が敵に与えた HP、燃 ＝ その駒の一撃が起こした被弾の燃焼で敵が失った HP。括弧は W0 との差（直＋燃）。ボルグの爆炎そのものは爆炎の手番の中なので入らない。");
        Console.WriteLine();
        foreach (string b in Boards)
        {
            var ids = BoardOf(b, Versions[0]).Occupied().Select(o => o.Def.Id).ToList();
            Console.WriteLine($"### {b}");
            Console.WriteLine();
            Console.WriteLine("| 版 | 波 | " + string.Join(" | ", ids.Select(UnitName)) + " |");
            Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", ids.Count)));
            foreach (var v in _vers)
                foreach (var (gn, cells) in BS.Groups.Where(g => CGroups.Contains(g.Name)))
                {
                    var x = XG(b, v.Name, cells);
                    var w0 = HasW0 ? XG(b, "W0", cells) : null;
                    string Cell(string id)
                    {
                        var a = x.ByUnit.GetValueOrDefault(id) ?? new long[5];
                        string s = $"{Per(a[0], x.N)} ＋ {Per(a[1], x.N)}";
                        if (w0 is not null && v.Name != "W0")
                        {
                            var z = w0.ByUnit.GetValueOrDefault(id) ?? new long[5];
                            s += $"（{D1(PerD(a[0] + a[1], x.N) - PerD(z[0] + z[1], w0.N))}）";
                        }
                        return s;
                    }
                    Console.WriteLine($"| {v.Name} | {gn} | " + string.Join(" | ", ids.Select(Cell)) + " |");
                }
            Console.WriteLine();
        }
    }

    static void TableD()
    {
        Console.WriteLine("## 表D —— 周回の頭に火勢4 の敵の数（生きている敵・/戦）と、燃えている敵の数（括弧）・爆炎の次の周回の火勢4 の敵");
        Console.WriteLine();
        Console.WriteLine("周回の頭（刻みの後）の写し。その周回に入った戦の平均（戦の数で割る）。爆炎の前後の火勢の分布は下の第254期の表F（敵 前 → 後）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | T1 | T2 | T3 | T4 | T5 | T6 | T7 | T8 | 爆炎の次の周回 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (b, v) in Rows())
            foreach (var (gn, cells) in BS.Groups)
            {
                var x = XG(b, v, cells);
                string lv = string.Join(" | ", Enumerable.Range(1, TM).Select(t => x.TurnReached[t] == 0 ? "—" : $"{(double)x.FoeLv4Sum[t] / x.TurnReached[t]:F2}（{(double)x.FoeBurnSum[t] / x.TurnReached[t]:F2}）"));
                Console.WriteLine($"| {b} | {v} | {gn} | {lv} | {Per(x.AfterFoeLv4Sum, x.AfterFoeLv4N)} |");
            }
        Console.WriteLine();
    }

    static void TableF()
    {
        Console.WriteLine("## 表F —— 的・一（ボス戦の代わり）: 周回ごとの的の火勢と与ダメ");
        Console.WriteLine();
        Console.WriteLine("最初の爆炎の周回の分布（爆炎なし ／ T1..T8）、周回ごとの「的の火勢（頭）／ その周回に的が失った HP ／ うち被弾の燃焼」（その周回に入った戦の平均）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 最初の爆炎（なし ／ T1..T8 の %） | T1 | T2 | T3 | T4 | T5 | T6 | T7 | T8 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var (b, v) in Rows())
        {
            var x = XAt(b, v, FireCycleDiag.WaveTarget1, 0);
            string hist = string.Join(" ／ ", Enumerable.Range(0, TM + 1).Select(t => Pct(x.BlazeTurnHist[t], x.N)));
            string turns = string.Join(" | ", Enumerable.Range(1, TM).Select(t => x.TurnReached[t] == 0 ? "—"
                : $"{(double)x.TurnFoeLvSum[t] / x.TurnReached[t]:F2} ／ {(double)x.TurnDmg[t] / x.TurnReached[t]:F0} ／ {(double)x.TurnHitHp[t] / x.TurnReached[t]:F0}"));
            Console.WriteLine($"| {b} | {v} | {hist} | {turns} |");
        }
        Console.WriteLine();
    }
}
