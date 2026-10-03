using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;
using FK = FireKindleDiag;

// giftorder の集計（表C・D）——2体のギフトの並びと、爆炎の後の焼き尽くす。台本（verbose）を読むだけで盤面は1ビットも動かさない。
// 的の波は台本を 8 ターンで切る（第252期と同じ）。
static partial class GiftOrderDiag
{
    internal sealed class OAgg
    {
        public long N;
        // 表C: ギフト（ヒヨが火を渡した回）
        public long Gifts, Gifts1, Gifts2, Gifts2Borg, BorgFirst, Swapped, Gift1Borg;
        public long Turns2BorgBoth, Turns2BorgOnly1, EndedInBorgTurn;   // ボルグを含む2体のギフト: 2体とも動いた ／ 1体だけ（2体目の前に終わった・倒れた）／ ボルグの手番で戦が終わった
        public long BorgTurnBlaze;   // ボルグを含む2体のギフトで、ボルグの手番に爆炎を撃った
        // 表D: 焼き尽くす（ホタ）——爆炎の直後（同じギフトで先に動いたボルグが爆炎）か、それ以外
        public long BurnAfter, DmgAfter, BrittleAfter, KillsAfter;
        public long BurnOther, DmgOther, BrittleOther, KillsOther;
        // ギフトの手番に倒れた敵（ボルグを含む2体のギフト・1体目 ／ 2体目の手番）
        public long Deaths1, Deaths2;
        public long Pair2Groups;   // 2体のギフトで GiftTurn が出た回（死者の分母）

        public void Merge(OAgg o)
        {
            N += o.N; Gifts += o.Gifts; Gifts1 += o.Gifts1; Gifts2 += o.Gifts2; Gifts2Borg += o.Gifts2Borg; BorgFirst += o.BorgFirst; Swapped += o.Swapped; Gift1Borg += o.Gift1Borg;
            Turns2BorgBoth += o.Turns2BorgBoth; Turns2BorgOnly1 += o.Turns2BorgOnly1; EndedInBorgTurn += o.EndedInBorgTurn; BorgTurnBlaze += o.BorgTurnBlaze;
            BurnAfter += o.BurnAfter; DmgAfter += o.DmgAfter; BrittleAfter += o.BrittleAfter; KillsAfter += o.KillsAfter;
            BurnOther += o.BurnOther; DmgOther += o.DmgOther; BrittleOther += o.BrittleOther; KillsOther += o.KillsOther;
            Deaths1 += o.Deaths1; Deaths2 += o.Deaths2; Pair2Groups += o.Pair2Groups;
        }

        static bool FL(BattleEvent x, string label) => x.Kind == BattleEventKind.FireLevel && x.Text == label;

        sealed class Group
        {
            public int Turn;
            public readonly List<int> Recips = new();
            public bool Swapped;
            public readonly List<(int Id, int Start, int End)> Turns = new();
        }

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e, bool target)
        {
            N++;
            var ev = r.Events;
            int n = ev.Count;
            if (target) while (n > 0 && ev[n - 1].Turn > FK.TM) n--;
            var foes = new HashSet<int>(e.Select(u => u.InstanceId));
            foreach (var x in ev)
                if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid && x.Team is int tm && tm != BattleContext.PlayerTeam) foes.Add(sid);
            int? Id(string s) => p.FirstOrDefault(u => u.Def.Id == s)?.InstanceId;
            int? borg = Id("borg"), hota = Id("hota"), hiyo = Id("hiyo");
            if (hiyo is not int hy) return;

            // ---- ギフトのまとまり（「火を渡す」Slot 1 で始まり、続く「ターンギフト」の手番を持つ）----
            var groups = new List<Group>();
            Group? cur = null;
            for (int i = 0; i < n; i++)
            {
                var x = ev[i];
                if (FL(x, FireLevelLabels.GiftOrder) && x.ActorId == hy) { cur = new Group { Turn = x.Turn, Swapped = true }; groups.Add(cur); continue; }
                if (FL(x, FireLevelLabels.Gift) && x.ActorId == hy)
                {
                    if (x.Slot == 1 && !(cur is { Swapped: true } && cur.Recips.Count == 0)) { cur = new Group { Turn = x.Turn }; groups.Add(cur); }
                    cur!.Recips.Add(x.TargetId ?? -1);
                    continue;
                }
                if (FL(x, FireLevelLabels.GiftTurn) && x.ActorId == hy && cur is not null && x.TargetId is int to)
                {
                    int end = n;
                    foreach (var h in r.Hands) if (h.EventStart == i + 1 && h.ActorId == to) { end = Math.Min(h.EventEnd, n); break; }
                    cur.Turns.Add((to, i + 1, end));
                }
            }

            foreach (var g in groups)
            {
                Gifts++;
                if (g.Recips.Count == 1) { Gifts1++; if (g.Recips[0] == borg) Gift1Borg++; }
                if (g.Recips.Count != 2) continue;
                Gifts2++;
                if (g.Swapped) Swapped++;
                bool hasBorg = borg is int bb && g.Recips.Contains(bb);
                if (g.Turns.Count > 0) Pair2Groups++;
                for (int k = 0; k < g.Turns.Count; k++)
                {
                    var (_, s0, e0) = g.Turns[k];
                    long d = 0;
                    for (int j = s0; j < e0; j++) if (ev[j].Kind == BattleEventKind.Death && ev[j].TargetId is int dt && foes.Contains(dt)) d++;
                    if (k == 0) Deaths1 += d; else Deaths2 += d;
                }
                if (!hasBorg) continue;
                Gifts2Borg++;
                if (g.Recips[0] == borg) BorgFirst++;
                if (g.Turns.Count == 2) Turns2BorgBoth++; else Turns2BorgOnly1++;
                int bi = g.Turns.FindIndex(t => t.Id == borg);
                if (bi >= 0)
                {
                    var (_, s0, e0) = g.Turns[bi];
                    bool blz = false;
                    for (int j = s0; j < e0; j++) if (FL(ev[j], FireLevelLabels.Blaze) && ev[j].ActorId == borg) { blz = true; break; }
                    if (blz) BorgTurnBlaze++;
                    if (bi == g.Turns.Count - 1 && g.Recips.Count == 2 && g.Turns.Count == 1 && g.Recips[0] == borg) EndedInBorgTurn++;
                }
            }

            // ---- 表D: ホタの焼き尽くす（爆炎の直後かどうか）----
            if (hota is int ho)
            {
                for (int i = 0; i < n; i++)
                {
                    if (!FL(ev[i], FireLevelLabels.Burnout) || ev[i].ActorId != ho) continue;
                    int end = n;
                    foreach (var h in r.Hands) if (h.ActorId == ho && h.EventStart <= i && i < h.EventEnd) end = Math.Min(end, h.EventEnd);
                    long dmg = 0, brit = 0, kills = 0;
                    for (int j = i + 1; j < Math.Min(end, n); j++)
                    {
                        var y = ev[j];
                        if (y.Kind == BattleEventKind.Damage && y.ActorId == ho && y.TargetId is int t && foes.Contains(t) && !y.Relayed) { dmg += y.Amount; brit += y.BrittleExtra ?? 0; }
                        if (y.Kind == BattleEventKind.Death && y.TargetId is int dt && foes.Contains(dt)) kills++;
                    }
                    // 同じギフトで先に動いたボルグが爆炎を撃ったか
                    bool after = false;
                    foreach (var g in groups)
                    {
                        int hi = g.Turns.FindIndex(t => t.Id == ho && t.Start <= i && i < t.End);
                        if (hi <= 0) continue;
                        for (int k = 0; k < hi && !after; k++)
                        {
                            if (g.Turns[k].Id != borg) continue;
                            for (int j = g.Turns[k].Start; j < g.Turns[k].End; j++) if (FL(ev[j], FireLevelLabels.Blaze) && ev[j].ActorId == borg) { after = true; break; }
                        }
                    }
                    if (after) { BurnAfter++; DmgAfter += dmg; BrittleAfter += brit; KillsAfter += kills; }
                    else { BurnOther++; DmgOther += dmg; BrittleOther += brit; KillsOther += kills; }
                }
            }
        }
    }

    static Dictionary<(string B, string V, int W, int S), OAgg> _order = new();

    internal static OAgg MeasureOrder(Formation f, int w, EnemyScaleRule sc, int seed0 = 0, int seeds = BA.Seeds)
    {
        var os = new OAgg[seeds];
        bool target = FK.IsTarget(w);
        Parallel.For(0, seeds, i =>
        {
            var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var e = FC.WaveOf(w, sc)();
            var r = BattleEngine.Run(p, e, seed0 + i, verbose: true);
            var o = new OAgg(); o.Take(r, p, e, target); os[i] = o;
        });
        var O = new OAgg();
        foreach (var o in os) O.Merge(o);
        return O;
    }

    static OAgg OGrp(string b, string v, (int W, int S)[] cells)
    {
        var m = new OAgg();
        foreach (var (w, s) in cells) m.Merge(_order[(b, v, w, FK.IsTarget(w) ? 0 : s)]);
        return m;
    }

    static void TableC()
    {
        Console.WriteLine("## 表C —— ギフトの回数と並び（/戦）");
        Console.WriteLine();
        Console.WriteLine("「2体」はヒヨが2体に渡した回。「ボルグ込み」はそのうち相手にボルグがいた回、「ボルグ先」はそのうちボルグが1体目だった割合（札で並べ替えた回を含む）。「2体目が来ない」はボルグ込みの2体で1体目の手番の後に戦が終わったか2体目が倒れた回。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | ギフト/戦（1体 ／ 2体） | 1体のギフトの相手がボルグ % | 2体のうちボルグ込み/戦 | ボルグ先 %（並べ替え/戦） | 2体目が来ない/戦 | ボルグの手番で爆炎/戦 |");
        Console.WriteLine("|---|---|---|---|--:|--:|---|--:|--:|");
        foreach (string b in FK.BoardNames)
            foreach (var v in FK._vers)
                foreach (var (gn, cells) in Groups)
                {
                    var m = OGrp(b, v.Name, cells);
                    Console.WriteLine($"| {b} | {v.Name} | {gn} | {FK.Per(m.Gifts, m.N)}（{FK.Per(m.Gifts1, m.N)} ／ {FK.Per(m.Gifts2, m.N)}） | {FK.Pct(m.Gift1Borg, m.Gifts1)} | {FK.Per(m.Gifts2Borg, m.N)} | {FK.Pct(m.BorgFirst, m.Gifts2Borg)}（{FK.Per(m.Swapped, m.N)}） | {FK.Per(m.Turns2BorgOnly1, m.N)} | {FK.Per(m.BorgTurnBlaze, m.N)} |");
                }
        Console.WriteLine();
    }

    static void TableD()
    {
        Console.WriteLine("## 表D —— ホタの焼き尽くす: 爆炎の直後（同じギフトで先に動いたボルグが爆炎）と、それ以外");
        Console.WriteLine();
        Console.WriteLine("与ダメはホタの手番の中で敵に入った分（中継を除く・脆さの上乗せ込み）、括弧は脆さの上乗せの名目（`BrittleExtra`）。「倒れた敵」は2体のギフトの1体目 ／ 2体目の手番に倒れた敵/回。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 爆炎の直後: 回/戦 ／ 与ダメ/回（脆さ） ／ 倒した/回 | それ以外: 回/戦 ／ 与ダメ/回（脆さ） ／ 倒した/回 | 2体のギフトで倒れた敵 1体目 ／ 2体目 /回 |");
        Console.WriteLine("|---|---|---|---|---|---|");
        foreach (string b in FK.BoardNames)
            foreach (var v in FK._vers)
                foreach (var (gn, cells) in Groups)
                {
                    var m = OGrp(b, v.Name, cells);
                    Console.WriteLine($"| {b} | {v.Name} | {gn} | {FK.Per(m.BurnAfter, m.N)} ／ {FK.Per(m.DmgAfter, m.BurnAfter)}（{FK.Per(m.BrittleAfter, m.BurnAfter)}） ／ {FK.Per(m.KillsAfter, m.BurnAfter)} | {FK.Per(m.BurnOther, m.N)} ／ {FK.Per(m.DmgOther, m.BurnOther)}（{FK.Per(m.BrittleOther, m.BurnOther)}） ／ {FK.Per(m.KillsOther, m.BurnOther)} | {FK.Per(m.Deaths1, m.Pair2Groups)} ／ {FK.Per(m.Deaths2, m.Pair2Groups)} |");
                }
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // digest（`firekindle digest` と同じ形式・同じ台と波。版の駒を当てるだけ）
    // ---------------------------------------------------------------------------------
    static void Digest(string ver, string outPath)
    {
        var v = VerOf(ver);
        var boards = new (string Name, Formation F)[]
        {
            ("T3-244", FK.Apply(FC.T3244, v)), ("T3-238", FK.Apply(FC.T3238, v)), ("雷＋ボルグ", FK.Apply(FC.ThunderBorg, v)),
        };
        var scales = new[] { new EnemyScaleRule(200, 200), new EnemyScaleRule(400, 300), new EnemyScaleRule(115, 115) };
        var lines = new List<string>();
        foreach (var (bn, f) in boards)
            for (int w = 0; w < 9; w++)
                for (int s = 0; s < (w >= 7 ? 1 : scales.Length); s++)
                    for (int seed = 0; seed < 6; seed++)
                    {
                        var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), FC.WaveOf(w, w >= 7 ? EnemyScaleRule.None : scales[s])(), seed, verbose: true);
                        lines.Add($"## {bn} 波{w} 倍率{s} seed{seed} won={r.PlayerWon} T={r.Turns}");
                        foreach (var l in r.Log) lines.Add(l.Text);
                        foreach (var e in r.Events)
                            lines.Add($"E {e.Kind} {e.Turn} {e.ActorId} {e.TargetId} {e.Amount} {e.HpAfter} {e.Slot} {e.Text} {e.TickIndex} {e.TickCount} {e.BrittleExtra}");
                    }
        File.WriteAllLines(outPath, lines);
        Console.WriteLine($"digest {ver}: {lines.Count} 行を {outPath} に書いた");
    }
}
