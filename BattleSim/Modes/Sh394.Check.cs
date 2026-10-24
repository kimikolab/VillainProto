using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S390 = Sh390Diag;

// sh394 check —— 第394期の自己検査（指示書 §7 の「新しい器具に最低限入れる」）。
static partial class Sh394Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly FieldInfo TurnField = typeof(BattleContext).GetField("_turn", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly MethodInfo TallyOfM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic)!;
    internal const string HeadSha = "4ef2f20b";   // 第393期の HEAD
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;
    static UnitTally Tl(BattleContext c, UnitState u) => (UnitTally)TallyOfM.Invoke(c, new object[] { u })!;
    static readonly UnitDef Mate = new() { Id = "mate394", Name = "仲間", MaxHp = 50, Attack = 5, Speed = 8, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef Foe = new() { Id = "foe394", Name = "的", MaxHp = 1000, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };

    /// <summary>ドルガ（版・後1）・仲間 2 体（前1 ／ 前3）と的 1 体。ターン <paramref name="turn"/>。</summary>
    static (BattleContext C, UnitState D, UnitState M, UnitState M2, UnitState E) Bench(UnitDef dolga, int turn)
    {
        var ctx = new BattleContext(0, true);
        var f = new Formation(); f[3] = dolga; f[0] = Mate; f[1] = Mate;
        var g = new Formation(); g[0] = Foe;
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = BattleEngine.Materialize(g, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
        TurnField.SetValue(ctx, turn);
        var ms = p.Where(u => u.Def.Id == "mate394").ToList();
        return (ctx, p.First(u => u.Def.Id == "dolga"), ms[0], ms[1], e[0]);
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh394 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef t = UnitCatalog.Tomo, h = UnitCatalog.TomoH393, t3 = UnitCatalog.TomoT3;
        UnitDef d = UnitCatalog.Dolga, d0 = UnitCatalog.DolgaD0, d1 = UnitCatalog.DolgaD1, d2 = UnitCatalog.DolgaD2;

        // ---- 段0: 規定のトモ ----
        {
            bool cards = t.Traits.SequenceEqual(new[] { TraitId.Taillight, TraitId.TaillightKeep, TraitId.TaillightWake, TraitId.TaillightCarry }) && t.Traits.SequenceEqual(t3.Traits)
                && t.PlusText == t3.PlusText && t.MinusText == t3.MinusText && t.Id == "tomo" && t.MaxHp == h.MaxHp && t.Attack == h.Attack && t.Speed == h.Speed && t.Actions == h.Actions && t.Flavor == h.Flavor
                && UnitCatalog.All.Contains(t) && !UnitCatalog.All.Contains(h) && !UnitCatalog.All.Contains(t3) && h.Traits.SequenceEqual(new[] { TraitId.Taillight })
                && new[] { UnitCatalog.TomoT1, UnitCatalog.TomoT2, t3 }.All(v => v.Traits.Take(h.Traits.Count).SequenceEqual(h.Traits) && v.MaxHp == h.MaxHp && v.Speed == h.Speed && v.Actions == h.Actions);
            bool pins = PinT393(t) == h && PinT393(d) == d && PinT393(h) == h && Pin393(Presets.TempoSeek392()).Occupied().Any(o => o.Def == h) && !Pin393(Presets.TempoSeek392()).Occupied().Any(o => o.Def == t);
            Expect("段0: 規定のトモ ＝ 第393期の T3（札 ／ 文面 ／ 数値）・旧は `TomoH393`（`All` の外・尾灯 1 枚）・第393期の版の駒は旧から作ったまま・固定の口 `PinT393` ／ `Pin393`", cards && pins,
                $"規定 {string.Join("・", t.Traits)}・プラス「…{t.PlusText[^16..]}」");
        }
        {
            long cells = 0, diff = 0;
            foreach (var bd in BoardList().Take(5))
            {
                int ts = bd.F.Occupied().FirstOrDefault(o => o.Def.Id == "tomo").Slot;
                if (!Has(bd.F, "tomo")) continue;
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 10; sd++) { cells++; if (!Same(I.Raw(Put(bd.F, ts, t), w, sd).R, I.Raw(Put(bd.F, ts, t3), w, sd).R)) diff++; }
            }
            Expect("段0: 規定のトモは第393期の T3 と同じ動き（トモのいる台 × ボス ／ 近衛 ／ 大隊 ／ 第5波 × seed 0..9 で勝敗 ／ T ／ 生存数の差 0）", diff == 0 && cells > 0, $"{cells} 戦・差 {diff}");
        }
        {
            var v = VersionsH394().Select(r => r.Name).ToList(); var h3 = VersionsH393();   // 第395期: 第394期の一覧
            bool ok = v.Contains("試遊・手番 芯3枚") && v.Contains("試遊・手番 探索1位") && !v.Contains("版・灯 手番")
                && h3[^1].Name == "版・灯 手番" && h3[^1].F.Occupied().Any(o => o.Def == t3) && h3.Select(r => r.Name).Count(n => n == "試遊・手番 芯3枚") == 0
                && Presets.Versions.First(r => r.Name == "試遊・手番 芯3枚").F.Occupied().Any(o => o.Def == t)
                && h3.Where(r => r.Name != "版・灯 手番").All(r => !r.F.Occupied().Any(o => o.Def == t))
                && VersionsH392().Select(r => r.Name).SequenceEqual(h3.Select(r => r.Name).SkipLast(1));
            Expect("段0: 試遊の台「版・灯 手番」→「試遊・手番 芯3枚」（規定のトモ）・「試遊・手番 探索1位」は残す・第393期の一覧は `VersionsH393()`（名前と T3 を戻し、ほかのトモは旧）", ok, $"`Presets.Versions` {v.Count} 台");
        }

        // ---- D0: 休み番のずれ ----
        {
            var notes = new List<string>(); bool ok = true;
            foreach (var (name, def, odd) in new[] { ("規定", d, false), ("D0", d0, true), ("D1", d1, false), ("D2", d2, true) })
            {
                var (c, dl, _, _, _) = Bench(def, 1);
                var rest = new List<int>();
                for (int tt = 1; tt <= 6; tt++) { TurnField.SetValue(c, tt); if (SluggishTrait.Resting(c, dl)) rest.Add(tt); }
                bool a = rest.SequenceEqual(odd ? new[] { 1, 3, 5 } : new[] { 2, 4, 6 });
                TurnField.SetValue(c, odd ? 1 : 2);
                bool b = !dl.Traits.First(x => x.Id == TraitId.Sluggish).CanAct(c, dl, ActionKind.Attack) && !dl.Traits.First(x => x.Id == TraitId.Sluggish).CanReact(c, dl);
                c.TaillightWaking = dl; bool wk = dl.Traits.First(x => x.Id == TraitId.Sluggish).CanAct(c, dl, ActionKind.Attack); c.TaillightWaking = null;
                ok &= a && b && wk; notes.Add($"{name} 休み T{string.Join(",", rest)}{(a && b && wk ? "" : " ×")}");
            }
            // 実戦: 芯 3 枚の台で、ドルガが自分の手番で振ったターン ／ ガンの +8 ／ 叩き起こし ／ 灯の起こしのターンの偶奇
            var core = BoardList()[0];
            foreach (var (name, def, odd) in new[] { ("規定", d, false), ("D0", d0, true) })
            {
                long own = 0, ownBad = 0, rally = 0, rallyBad = 0, rev = 0, revBad = 0, yl = 0, ylBad = 0;
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 10; sd++)
                    {
                        var (r, p, _) = I.Raw(Put(core.F, core.Slot, def), w, sd, verbose: true);
                        int did = p.First(u => u.Def.Id == "dolga").InstanceId, tid = p.First(u => u.Def.Id == "tomo").InstanceId;
                        bool yp = false, gp = false;
                        foreach (var x in r.Events)
                        {
                            bool restT = odd ? x.Turn % 2 == 1 : x.Turn % 2 == 0;
                            switch (x.Kind)
                            {
                                case BattleEventKind.TurnStart: yp = gp = false; break;
                                case BattleEventKind.Highlight: if (x.ActorId == tid && x.Text?.Contains("道を譲る", StringComparison.Ordinal) == true) yp = true; break;
                                case BattleEventKind.Reveille: if (x.TargetId == did && x.Text == ReveilleLabels.Woken) { gp = true; rev++; if (restT) revBad++; } break;   // 叩き起こしは休み番の次のターン
                                case BattleEventKind.Whet: if (x.WhetRoute == WhetRoute.RallyTurn && x.TargetId == did) { rally++; if (restT) rallyBad++; } break;   // +8 は休み番の次のターン
                                case BattleEventKind.Attack:
                                    if (x.ActorId != did) { if (!x.Reaction && yp && x.ActorId != tid) yp = false; break; }
                                    if (x.Reaction && gp) { gp = false; break; }
                                    if (!x.Reaction && yp) { if (restT) yl++; else ylBad++; yp = false; break; }   // 休み番に譲られた振り ＝ 灯の起こし（休み番でない譲りは2振り目）
                                    if (!x.Reaction) { own++; if (restT) ownBad++; }
                                    break;
                            }
                        }
                    }
                bool g = own > 0 && ownBad == 0 && rally > 0 && rallyBad == 0 && rev > 0 && revBad == 0 && yl > 0;
                ok &= g; notes.Add($"{name}: 自分 {own}（休み番 {ownBad}）・+8 {rally}（休み番 {rallyBad}）・叩き起こし {rev}（休み番 {revBad}）・灯の起こし {yl}（休み番でない譲り {ylBad}）{(g ? "" : " ×")}");
            }
            Expect("D0: 奇数ターンに休み ／ 偶数ターンに振る（判定は `SluggishTrait.Resting` の1か所）・ガンの +8 ／ 叩き起こし ／ トモの起こしがずれた休み番に付いていく（芯 3 枚 × 4 物差し × seed 0..9）", ok, string.Join("・", notes));
        }

        // ---- D1: 壁 ----
        {
            var notes = new List<string>(); bool ok = true;
            (int Mate, int Dolga, long Hits) Hit(UnitDef def, int turn, int amount = 40, AttackPattern? pat = AttackPattern.Single, bool burn = false, bool waking = false, bool dead = false, bool onDolga = false, bool friendly = false)
            {
                var (c, dl, m, m2, e) = Bench(def, turn);
                if (waking) c.TaillightWaking = dl;
                if (dead) dl.Hp = 0;
                int mh = m.Hp, dh = dl.Hp;
                c.ApplyDamage(onDolga ? dl : m, amount, friendly ? m2 : e, burnTick: burn, pattern: pat);
                return (mh - m.Hp, dh - dl.Hp, Tl(c, dl).R394WallHits);
            }
            var a = Hit(d1, 2);
            bool g1 = a.Mate == 0 && a.Dolga == 20 && a.Hits == 1;
            var a2 = Hit(d1, 2, amount: 41);
            g1 &= a2.Mate == 0 && a2.Dolga == 20;
            ok &= g1; notes.Add($"休み番（T2）の単体 40 → 仲間 {a.Mate}・ドルガ {a.Dolga}（41 → {a2.Dolga}）{(g1 ? "" : " ×")}");
            var sw = Hit(d1, 2, pat: AttackPattern.Sweep); var pi = Hit(d1, 2, pat: AttackPattern.Pierce); var al = Hit(d1, 2, pat: AttackPattern.All); var bt = Hit(d1, 2, pat: null, burn: true); var np = Hit(d1, 2, pat: null);
            bool g2 = new[] { sw, pi, al, bt, np }.All(x => x.Mate == 40 && x.Dolga == 0 && x.Hits == 0);
            ok &= g2; notes.Add($"薙ぎ ／ 貫き ／ 全体 ／ 刻み ／ 型なしは受けない{(g2 ? "" : " ×")}");
            var ac = Hit(d1, 3);
            bool g3 = ac.Mate == 40 && ac.Dolga == 0;
            ok &= g3; notes.Add($"振る番（T3）は受けない{(g3 ? "" : " ×")}");
            var wk = Hit(d1, 2, waking: true);
            bool g4 = wk.Mate == 0 && wk.Dolga == 20;
            ok &= g4; notes.Add($"起こされたターンも壁は続く{(g4 ? "" : " ×")}");
            var dd = Hit(d1, 2, dead: true);
            bool g5 = dd.Mate == 40 && dd.Hits == 0;
            ok &= g5; notes.Add($"倒れたら止まる{(g5 ? "" : " ×")}");
            var od = Hit(d1, 2, onDolga: true); var ff = Hit(d1, 2, friendly: true);
            bool g6 = od.Dolga == 40 && od.Hits == 0 && ff.Mate == 40 && ff.Hits == 0;
            ok &= g6; notes.Add($"ドルガ自身 ／ 味方からの一撃は受けない{(g6 ? "" : " ×")}");
            var pl = Hit(d, 2); var z1 = Hit(d2, 1); var z2 = Hit(d2, 2); var z0 = Hit(d0, 1);
            bool g7 = pl.Mate == 40 && pl.Dolga == 0 && z1.Mate == 0 && z1.Dolga == 20 && z2.Mate == 40 && z2.Dolga == 0 && z0.Mate == 40;
            ok &= g7; notes.Add($"規定 ／ D0 は受けない・D2 は T1 に受け T2 は受けない{(g7 ? "" : " ×")}");
            Expect("D1: 休み番に味方への単体の攻撃を代わりに受け、量は半分・薙ぎ ／ 貫き ／ 全体 ／ 刻みは受けない・振る番は受けない・起こされたターンも壁は続く・ドルガが倒れたら止まる", ok, string.Join("・", notes));
        }

        // ---- 版の札と文面 ／ 規定のドルガは今どおり ----
        {
            bool ok = new[] { d0, d1, d2 }.All(v => v.Id == "dolga" && v.Name == d.Name && v.MaxHp == d.MaxHp && v.Attack == d.Attack && v.Speed == d.Speed && v.Pattern == d.Pattern && v.Traits.Take(2).SequenceEqual(d.Traits) && !UnitCatalog.Everyone.Contains(v))
                && d0.Traits.Skip(2).SequenceEqual(new[] { TraitId.SluggishShift }) && d1.Traits.Skip(2).SequenceEqual(new[] { TraitId.DolgaWall }) && d2.Traits.Skip(2).SequenceEqual(new[] { TraitId.SluggishShift, TraitId.DolgaWall })
                && d0.MinusText == "2ターンに1回しか動けない（開戦のターンは構えて動かない）" && d2.MinusText == d0.MinusText && d1.MinusText == d.MinusText && d0.PlusText == d.PlusText
                && d1.PlusText.EndsWith("休んでいるあいだは仲間の前に立ち、単体の攻撃を半分にして代わりに受ける", StringComparison.Ordinal) && d2.PlusText == d1.PlusText
                && d.Traits.SequenceEqual(new[] { TraitId.Sluggish, TraitId.GradeStep }) && d.MinusText == "2ターンに1回しか動けない" && UnitCatalog.All.Contains(d)
                && !UnitCatalog.Everyone.Any(u => u.Traits.Contains(TraitId.SluggishShift) || u.Traits.Contains(TraitId.DolgaWall));
            string units = File.Exists("docs/units.md") ? File.ReadAllText("docs/units.md") : "", head = S390.GitShow($"{HeadSha}:docs/units.md");
            string Row(string s) => s.Split('\n').FirstOrDefault(l => l.StartsWith("| **のろまの巨兵ドルガ** |", StringComparison.Ordinal))?.TrimEnd('\r') ?? "";
            bool same = Row(units) != "" && Row(units) == Row(head);
            Expect("版の札と文面: D0 ＝ 規定 ＋ 開戦で構える ／ D1 ＋ 壁 ／ D2 ＝ 両方・文面は叩き台・`All` ／ `Retired` の外・規定のドルガは今どおり（`docs/units.md` の行が第393期の HEAD と同じ）", ok && same, $"units.md の行 {(same ? "同じ" : "違う")}");
        }

        // ---- 規定の compare ----
        {
            var map = S390.BalanceMap(File.Exists("docs/balance.md") ? File.ReadAllText("docs/balance.md") : "");
            var rows = CompareBuilds(); var g = CGrid(rows, CompareSeeds);
            int diff = 0, miss = 0;
            for (int ri = 0; ri < rows.Length; ri++)
            {
                if (!map.TryGetValue(rows[ri].Name, out var cc)) { miss++; continue; }
                for (int w = 0; w < 5; w++) if (cc[w].TrimEnd('%') != F1(g[ri, w])) diff++;
            }
            Expect("規定の `compare` 全行 × 5 波 × seed 0..199 が `docs/balance.md` と一致（段0 のトモの規定化で動かない・版の札は保持者 0）", diff == 0 && miss == 0, $"{rows.Length} 行・ずれ {diff}・表に無い行 {miss}");
        }

        // ---- 替えた台 ／ 素体は器具の中だけ・試遊の台 ----
        {
            var vers = new[] { d0, d1, d2 };
            var everywhere = CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).Concat(Presets.Versions).SelectMany(r => r.F.Occupied()).Select(o => o.Def).ToList();
            bool inside = !everywhere.Any(x => x.Id.EndsWith("_plain392", StringComparison.Ordinal));
            var verRows = VersionsH394().Where(r => r.F.Occupied().Any(o => vers.Contains(o.Def))).Select(r => r.Name).ToList();   // 第395期: 第394期の一覧（「版・壁 E」を足す前）
            bool rows = verRows.SequenceEqual(VersionRows394) && VersionsH394()[^1].Name == "版・壁 ドルガ" && VersionsH394()[^1].F.Occupied().Count(o => vers.Contains(o.Def)) == 1
                && !CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).Any(r => r.F.Occupied().Any(o => vers.Contains(o.Def)))
                && VersionsH393().All(r => !r.F.Occupied().Any(o => vers.Contains(o.Def)));
            Expect("替えた台 ／ 素体は器具の中だけ・版のドルガが居る試遊の台は「版・壁 ドルガ」（`VersionsH394()` の末尾）だけ", inside && rows, string.Join("、", verRows));
        }

        // ---- checkup ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            bool Has2(TraitId id, string must) => System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{id}\]\s*=\s*\(Hc[A-Za-z]+L,\s*""[^""]*{must}""");
            var miss = new List<string>();
            foreach (var id in new[] { TraitId.TaillightKeep, TraitId.TaillightWake, TraitId.TaillightCarry }) if (!Has2(id, "第394期から規定（トモ）")) miss.Add(id.ToString());
            foreach (var id in new[] { TraitId.SluggishShift, TraitId.DolgaWall }) if (!Has2(id, "第394期[^\"]*保持者 0 枚")) miss.Add(id.ToString());
            Expect("`checkup` の札の分類表: トモの札 3 枚は「第394期から規定（トモ）」・ドルガの版の札 2 枚（開戦で構える ／ 壁）がある（ソースを読んで確かめる）", miss.Count == 0, miss.Count == 0 ? "" : "無い: " + string.Join("・", miss));
        }

        // ---- verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var b in BoardList().Take(5))
                foreach (var v in Vers)
                    foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                        for (int sd = 0; sd < 2; sd++)
                        {
                            cells++;
                            var f = Put(b.F, b.Slot, v.D);
                            var r0 = I.Raw(f, w, sd).R;
                            if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                            if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                        }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（台 5 × 版 4 × ボス ／ 近衛 ／ 大隊 ／ 第5波 × seed 0..1）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string he = S390.GitShow($"{HeadSha}:BattleCore/BattleEngine.cs"), ht = S390.GitShow($"{HeadSha}:BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, tr0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), tr1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect($"`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第393期の HEAD（{HeadSha}）と同じ）", e0 == e1 && tr0 == tr1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1}・traits {tr0} → {tr1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
