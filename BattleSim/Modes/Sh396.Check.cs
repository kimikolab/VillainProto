using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S390 = Sh390Diag;

// sh396 check —— 第396期の自己検査（指示書 §7 の「新しい器具に最低限入れる」）。
static partial class Sh396Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly FieldInfo TurnField = typeof(BattleContext).GetField("_turn", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly MethodInfo TallyOfM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic)!;
    internal const string HeadSha = "ff751426";   // 第395期の HEAD
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;
    static UnitTally Tl(BattleContext c, UnitState u) => (UnitTally)TallyOfM.Invoke(c, new object[] { u })!;
    static readonly UnitDef Mate = new() { Id = "mate396", Name = "仲間", MaxHp = 50, Attack = 5, Speed = 8, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef Foe = new() { Id = "foe396", Name = "的", MaxHp = 1000, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };

    /// <summary>セッキ（版・後1）・仲間 2 体（前1 ／ 前3）・（あれば）ガン（中央）と的 2 体。ターン <paramref name="turn"/>。</summary>
    static (BattleContext C, UnitState S, UnitState M, UnitState M2, List<UnitState> E, List<UnitState> P) Bench(UnitDef sekki, int turn, bool gan = false)
    {
        var ctx = new BattleContext(0, true);
        var f = new Formation(); f[3] = sekki; f[0] = Mate; f[1] = Mate;
        if (gan) f[2] = UnitCatalog.Gan;
        var g = new Formation(); g[0] = Foe; g[1] = Foe;
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = BattleEngine.Materialize(g, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
        TurnField.SetValue(ctx, turn);
        var ms = p.Where(u => u.Def.Id == "mate396").ToList();
        return (ctx, p.First(u => u.Def.Id == "sekki"), ms[0], ms[1], e, p);
    }
    /// <summary>1発: 戻り値は（仲間が失った HP・セッキが失った HP・帳簿・包んだ回数）。</summary>
    static (int Mate, int Sekki, int Store, long Wraps) Hit(UnitDef def, AttackPattern? pat = AttackPattern.Sweep, int amount = 40, bool burn = false, bool onSekki = false, bool dead = false, bool friendly = false)
    {
        var (c, s, m, m2, e, _) = Bench(def, 1);
        if (dead) s.Hp = 0;
        int mh = m.Hp, sh = s.Hp;
        c.ApplyDamage(onSekki ? s : m, amount, friendly ? m2 : e[0], burnTick: burn, pattern: pat);
        return (mh - m.Hp, sh - s.Hp, s.RawCounter(SlimeTrait.StoreKey), Tl(c, s).R396WrapHits);
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh396 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef d = UnitCatalog.Dolga, h = UnitCatalog.DolgaH395, e1 = UnitCatalog.DolgaE1;
        UnitDef s0 = UnitCatalog.Sekki, s1 = UnitCatalog.SekkiS1, s2 = UnitCatalog.SekkiS2, s2h = UnitCatalog.SekkiS2h;

        // ---- 段0: 規定のドルガ ----
        {
            bool cards = d.Traits.SequenceEqual(e1.Traits) && d.Traits.SequenceEqual(new[] { TraitId.Sluggish, TraitId.GradeStep, TraitId.SluggishShift, TraitId.DolgaWall, TraitId.DolgaWallRetreat })
                && d.PlusText == e1.PlusText && d.MinusText == e1.MinusText && d.Id == "dolga" && d.MaxHp == h.MaxHp && d.Attack == h.Attack && d.Speed == h.Speed && d.Pattern == h.Pattern && d.Flavor == h.Flavor
                && UnitCatalog.All.Contains(d) && !UnitCatalog.All.Contains(h) && !UnitCatalog.All.Contains(e1) && h.Traits.SequenceEqual(new[] { TraitId.Sluggish, TraitId.GradeStep })
                && h.MinusText == "2ターンに1回しか動けない"
                && new[] { UnitCatalog.DolgaD0, UnitCatalog.DolgaD1, UnitCatalog.DolgaD2, e1, UnitCatalog.DolgaE2, UnitCatalog.DolgaE12 }.All(v => v.Traits.Take(2).SequenceEqual(h.Traits) && v.MaxHp == h.MaxHp && v.Flavor == h.Flavor);
            bool pins = PinD395(d) == h && PinD395(e1) == e1 && PinT393(d) == h && PinT393(UnitCatalog.Tomo) == UnitCatalog.TomoH393 && Pin395(Presets.BindTempo395()).Occupied().Any(o => o.Def == h) && Pin393(Presets.TempoSeek392()).Occupied().Any(o => o.Def == h);
            Expect("段0: 規定のドルガ ＝ 第395期の E1（札 ／ 文面 ／ 数値）・旧は `DolgaH395`（`All` の外・のろま ＋ 段違い）・第394〜395期の版の駒は旧から作ったまま・固定の口 `PinD395` ／ `Pin395`（`PinT393` の終端）", cards && pins,
                $"規定 {string.Join("・", d.Traits)}");
        }
        {
            long cells = 0, diff = 0;
            foreach (var bd in TempoBoards())
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 10; sd++) { cells++; int ds = SlotOf(bd.F, "dolga"); if (!Same(I.Raw(Put(bd.F, ds, d), w, sd).R, I.Raw(Put(bd.F, ds, e1), w, sd).R)) diff++; }
            Expect("段0: 規定のドルガは第395期の E1 と同じ動き（手番の軸の 3 台 × ボス ／ 近衛 ／ 大隊 ／ 第5波 × seed 0..9 で勝敗 ／ T ／ 生存数の差 0）", diff == 0 && cells > 0, $"{cells} 戦・差 {diff}");
        }
        {
            var v = Presets.Versions.Select(r => r.Name).ToList(); var h5 = VersionsH395();
            bool ok = v.Contains("試遊・手番 縛め") && v.Contains("試遊・手番 芯3枚") && v.Contains("試遊・手番 探索1位") && !v.Contains("版・壁 ドルガ") && !v.Contains("版・壁 E")
                && Presets.Versions.First(r => r.Name == "試遊・手番 縛め").F.Occupied().Any(o => o.Def == d)
                && h5.Select(r => r.Name).TakeLast(2).SequenceEqual(new[] { "版・壁 ドルガ", "版・壁 E" }) && h5[^1].F.Occupied().Any(o => o.Def == e1) && h5[^2].F.Occupied().Any(o => o.Def == UnitCatalog.DolgaD2)
                && !h5.Any(r => r.F.Occupied().Any(o => o.Def == d)) && !h5.Any(r => VersionRows396.Contains(r.Name))
                && VersionsH394().Select(r => r.Name).SequenceEqual(h5.Select(r => r.Name).SkipLast(1));
            Expect("段0: 試遊の台「版・壁 ドルガ」を外し、「版・壁 E」→「試遊・手番 縛め」（規定のドルガ）・芯3枚 ／ 探索1位は残す・第395期の一覧は `VersionsH395()`（2 台と名前を戻し、ドルガは旧）", ok, $"`Presets.Versions` {v.Count} 台");
        }

        // ---- 版の札と文面 ----
        {
            bool ok = new[] { s1, s2, s2h }.All(x => x.Id == "sekki" && x.Name == "包みのセッキ" && x.MaxHp == 70 && x.Attack == 2 && x.Speed == 2 && x.MaxHp == s0.MaxHp && !UnitCatalog.Everyone.Contains(x)
                    && x.MinusText == "単体の攻撃は包まない。自分の手番はぷるぷるしているだけで動かない" && x.Flavor.StartsWith("何を食べてここまで大きくなったのか", StringComparison.Ordinal))
                && s1.Traits.SequenceEqual(new[] { TraitId.SlimeWrap }) && s2.Traits.SequenceEqual(new[] { TraitId.SlimeWrap, TraitId.SlimeOverflow }) && s2h.Traits.SequenceEqual(new[] { TraitId.SlimeWrap, TraitId.SlimeOverflow, TraitId.SlimeDeep })
                && s1.PlusText.Contains("衝撃の 3 割を吸い取る", StringComparison.Ordinal) && s2.PlusText.EndsWith("溜まりすぎると自分から弾き返す", StringComparison.Ordinal) && s2h.PlusText.Contains("衝撃の 5 割", StringComparison.Ordinal)
                && s0.Traits.SequenceEqual(new[] { TraitId.RearGuard, TraitId.Rage }) && UnitCatalog.All.Contains(s0)
                && !UnitCatalog.Everyone.Any(u => u.Traits.Contains(TraitId.SlimeWrap) || u.Traits.Contains(TraitId.SlimeOverflow) || u.Traits.Contains(TraitId.SlimeDeep));
            string Row(string s) => s.Split('\n').FirstOrDefault(l => l.StartsWith("| **後備えのセッキ** |", StringComparison.Ordinal))?.TrimEnd('\r') ?? "";
            string units = File.Exists("docs/units.md") ? File.ReadAllText("docs/units.md") : "", head = S390.GitShow($"{HeadSha}:docs/units.md");
            bool same = Row(units) != "" && Row(units) == Row(head);
            Expect("版の札と文面: S1 ＝ 包む ／ S2 ＋ 溢れたら弾く ／ S2h ＋ 深く包む・後備え ／ 憤怒は外す・数値は今のセッキ（HP 70 ／ 攻 2 ／ 速 2）・`All` ／ `Retired` の外・規定のセッキは今どおり（`docs/units.md` の行が第395期の HEAD と同じ）", ok && same,
                $"units.md の行 {(same ? "同じ" : "違う")}");
        }

        // ---- 包む ----
        {
            var notes = new List<string>(); bool ok = true;
            var a = Hit(s1); var b = Hit(s1, amount: 41); var dh = Hit(s2h);
            bool g1 = a.Mate == 28 && a.Store == 12 && a.Sekki == 6 && a.Wraps == 1 && b.Mate == 29 && b.Store == 12 && dh.Mate == 20 && dh.Store == 20 && dh.Sekki == 10;
            ok &= g1; notes.Add($"薙ぎ 40 → 仲間 {a.Mate}・帳簿 {a.Store}・セッキ HP {a.Sekki}（41 → {b.Mate} ／ {b.Store}・S2h {dh.Mate} ／ {dh.Store} ／ {dh.Sekki}）{(g1 ? "" : " ×")}");
            var pi = Hit(s1, AttackPattern.Pierce); var al = Hit(s1, AttackPattern.All);
            bool g2 = pi.Store == 12 && pi.Mate == 28 && al.Store == 12 && al.Mate == 28;
            ok &= g2; notes.Add($"貫き ／ 全体も包む{(g2 ? "" : " ×")}");
            var sg = Hit(s1, AttackPattern.Single); var bt = Hit(s1, null, burn: true); var np = Hit(s1, null); var on = Hit(s1, onSekki: true); var dd = Hit(s1, dead: true); var ff = Hit(s1, friendly: true); var old = Hit(s0);
            bool g3 = new[] { sg, bt, np, dd, ff, old }.All(x => x.Mate == 40 && x.Store == 0 && x.Wraps == 0) && on.Sekki == 40 && on.Store == 0;
            ok &= g3; notes.Add($"単体 ／ 刻み ／ 型なし ／ セッキ自身 ／ 倒れたセッキ ／ 味方から ／ 旧セッキ は包まない{(g3 ? "" : " ×")}");
            Expect("包む: 範囲の一撃だけを 3 割（S2h 5 割）吸い、半分を HP で受け、吸った量の全部を帳簿に積む・単体 ／ 刻み ／ 中継などは包まない・セッキ自身への一撃は包まない・倒れたら包まない", ok, string.Join("・", notes));
        }

        // ---- 溜める ／ 弾き返す ----
        {
            var notes = new List<string>(); bool ok = true;
            var (c, s, _, _, e, _) = Bench(s1, 1);
            s.SetCounter(SlimeTrait.StoreKey, 30);
            int h0 = e[0].Hp, h1 = e[1].Hp;
            c.PerformAttack(s);
            bool g1 = h0 - e[0].Hp == 30 && h1 - e[1].Hp == 30 && s.RawCounter(SlimeTrait.StoreKey) == 0 && Tl(c, s).R396Bursts == 1 && Tl(c, s).R396BurstDealt == 60;
            ok &= g1; notes.Add($"帳簿 30 → 敵 2 体に 30 ずつ・帳簿 0{(g1 ? "" : $" × {h0 - e[0].Hp} {h1 - e[1].Hp}")}");
            int k0 = e[0].Hp + e[1].Hp;
            c.PerformAttack(s);
            bool g2 = Tl(c, s).R396Bursts == 1 && k0 - (e[0].Hp + e[1].Hp) is > 0 and < 30;
            ok &= g2; notes.Add($"帳簿 0 ならふつうに殴る（{k0 - (e[0].Hp + e[1].Hp)}）{(g2 ? "" : " ×")}");
            // 手番: S1 は自分の手番では動かない・譲られた手番では動く ／ S2 は帳簿 ≧ 最大 HP のとき自分の手番でも動く
            Trait Sl(UnitState u) => u.Traits.First(t => t.Id == TraitId.SlimeWrap);
            var (c1, a1, _, _, _, _) = Bench(s1, 1); a1.SetCounter(SlimeTrait.StoreKey, 100);
            bool own1 = Sl(a1).CanAct(c1, a1, ActionKind.Attack); c1.Yielding = true; bool given1 = Sl(a1).CanAct(c1, a1, ActionKind.Attack); c1.Yielding = false;
            var (c2, a2, _, _, _, _) = Bench(s2, 1); a2.SetCounter(SlimeTrait.StoreKey, 69); bool lo2 = Sl(a2).CanAct(c2, a2, ActionKind.Attack); a2.SetCounter(SlimeTrait.StoreKey, 70); bool hi2 = Sl(a2).CanAct(c2, a2, ActionKind.Attack);
            bool g3 = !own1 && given1 && !lo2 && hi2;
            ok &= g3; notes.Add($"S1 自分の手番 {(own1 ? "動く" : "動かない")} ／ 譲られた手番 {(given1 ? "動く" : "動かない")}・S2 帳簿 69 {(lo2 ? "動く" : "動かない")} ／ 70（最大 HP）{(hi2 ? "動く" : "動かない")}{(g3 ? "" : " ×")}");
            // 自分の手番で溢れて弾く（S2）: きっかけは「溢れ」
            var (c3, a3, _, _, e3, _) = Bench(s2, 1); a3.SetCounter(SlimeTrait.StoreKey, 70);
            int e30 = e3[0].Hp; c3.TakeTurn(a3);
            bool g4 = e30 - e3[0].Hp == 70 && Tl(c3, a3).R396BurstOverflow == 1;
            ok &= g4; notes.Add($"S2 の自分の手番で帳簿 70 を弾く（きっかけ ＝ 溢れ）{(g4 ? "" : " ×")}");
            Expect("溜める ／ 弾き返す: 敵全体に帳簿の量・帳簿が 0 に・S1 は手番の外 ／ 譲られた手番でだけ・S2 は自分の手番で帳簿 ≧ 最大 HP のとき・帳簿 0 で動かされたらふつうに殴る", ok, string.Join("・", notes));
        }

        // ---- IdleTurn ／ 号令 ----
        {
            var (c, s, _, _, _, p) = Bench(s1, 1, gan: true);
            var o = c.TakeTurn(s);
            bool idle = o == TurnOutcome.Stalled && s.RawCounter(StatusKeys.IdleTurn) == 1 && Trait.SurrenderedTurn(c, s);
            TurnField.SetValue(c, 2);
            var gan = p.First(u => u.Def.Id == "gan"); int a0 = s.AtkBonus;
            gan.Traits.First(t => t.Id == TraitId.Rally).OnTurnStart(c, gan);
            bool rally = s.AtkBonus - a0 == RallyTrait.Gain;
            // 実戦: 試遊・手番 縛め に S1 で、号令の +8 がセッキに入る ／ 叩き起こしでセッキが選ばれる戦がある
            long r8 = 0, rev = 0;
            var bd = Presets.BindTempo395(); int seat = 3;
            foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                for (int sd = 0; sd < 10; sd++)
                {
                    var (r, pp, _) = I.Raw(Put(bd, seat, s1), w, sd, verbose: true);
                    int sid = pp.First(u => u.Def.Id == "sekki").InstanceId;
                    r8 += r.Events.Count(x => x.Kind == BattleEventKind.Whet && x.WhetRoute == WhetRoute.RallyTurn && x.TargetId == sid);
                    rev += r.Events.Count(x => x.Kind == BattleEventKind.Reveille && x.Text == ReveilleLabels.Woken && x.TargetId == sid);
                }
            Expect("自分の手番で動かないとき `IdleTurn` が立ち（差し出した手番）、ガンの +8 の対象になる（台本でも +8 が入る）", idle && rally && r8 > 0, $"潰れた手番 {o}・+8 {s.AtkBonus - a0}・試遊・手番 縛め（後1 をセッキ S1）× 4 物差し × seed 0..9 で +8 {r8} 回 ／ 叩き起こし {rev} 回");
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
            Expect("規定の `compare` 全行 × 5 波 × seed 0..199 が `docs/balance.md`（段0 で再生成）と一致", diff == 0 && miss == 0, $"{rows.Length} 行・ずれ {diff}・表に無い行 {miss}");
        }

        // ---- 替えた台 ／ 素体は器具の中だけ・試遊の台 ----
        {
            var vers = new[] { s1, s2, s2h };
            var everywhere = CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).Concat(Presets.Versions).SelectMany(r => r.F.Occupied()).Select(o => o.Def).ToList();
            bool inside = !everywhere.Any(x => x.Id.EndsWith("_plain392", StringComparison.Ordinal));
            var verRows = Presets.Versions.Where(r => r.F.Occupied().Any(o => vers.Contains(o.Def))).Select(r => r.Name).ToList();
            bool rows = verRows.SequenceEqual(VersionRows396) && Presets.Versions[^1].Name == "版・包み セッキ" && Presets.Versions[^1].F.Occupied().Count(o => vers.Contains(o.Def)) == 1
                && !CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).Any(r => r.F.Occupied().Any(o => vers.Contains(o.Def)));
            Expect("替えた台 ／ 素体は器具の中だけ・版のセッキが居る試遊の台は「版・包み セッキ」（`Presets.Versions` の末尾）だけ", inside && rows, string.Join("、", verRows));
        }

        // ---- checkup ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            bool Has2(TraitId id, string must) => System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{id}\]\s*=\s*\(Hc[A-Za-z]+L,\s*""[^""]*{must}""");
            var miss = new List<string>();
            foreach (var id in new[] { TraitId.SluggishShift, TraitId.DolgaWall, TraitId.DolgaWallRetreat }) if (!Has2(id, "第396期から規定（ドルガ）")) miss.Add(id.ToString());
            foreach (var id in new[] { TraitId.SlimeWrap, TraitId.SlimeOverflow, TraitId.SlimeDeep }) if (!Has2(id, "第396期[^\"]*保持者 0 枚")) miss.Add(id.ToString());
            Expect("`checkup` の札の分類表: 規定のドルガの札 3 枚は「第396期から規定（ドルガ）」・セッキの版の札 3 枚がある（ソースを読んで確かめる）", miss.Count == 0, miss.Count == 0 ? "" : "無い: " + string.Join("・", miss));
        }

        // ---- verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var b in TempoBoards())
                foreach (var v in Vers)
                    foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                        for (int sd = 0; sd < 2; sd++)
                        {
                            cells++;
                            var f = Put(b.F, 2, v.D);
                            var r0 = I.Raw(f, w, sd).R;
                            if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                            if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                        }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（手番の軸の 3 台の中央 × 版 4 × ボス ／ 近衛 ／ 大隊 ／ 第5波 × seed 0..1）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string he = S390.GitShow($"{HeadSha}:BattleCore/BattleEngine.cs"), ht = S390.GitShow($"{HeadSha}:BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, tr0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1n = Now("BattleCore/BattleEngine.cs", "PickOne("), tr1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect($"`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第395期の HEAD（{HeadSha}）と同じ）", e0 == e1n && tr0 == tr1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1n}・traits {tr0} → {tr1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
