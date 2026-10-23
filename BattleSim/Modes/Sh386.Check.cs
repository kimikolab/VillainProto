using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;

// sh386 check —— 第386期の自己検査（指示書 §6 の「新しい器具に最低限入れる」）。
static partial class Sh386Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly FieldInfo TurnField = typeof(BattleContext).GetField("_turn", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly MethodInfo TallyM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static UnitTally Tal(BattleContext ctx, UnitState u) => (UnitTally)TallyM.Invoke(ctx, new object[] { u })!;
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;
    static readonly UnitDef Mate = new() { Id = "mate386", Name = "仲間", MaxHp = 50, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef Foe = new() { Id = "foe386", Name = "的", MaxHp = 999, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };

    static (BattleContext Ctx, List<UnitState> P, List<UnitState> E) Board(params (int Slot, UnitDef D)[] seats)
    {
        var ctx = new BattleContext(0, true);
        var f = new Formation();
        foreach (var (s, d) in seats) f[s] = d;
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = BattleEngine.Materialize(Formation.Build(center: Foe), BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
        TurnField.SetValue(ctx, 1);
        return (ctx, p, e);
    }
    static void Kill(BattleContext ctx, UnitState u, UnitState? by) => ctx.ApplyDamage(u, 9999, by);
    static List<UnitState> Living(BattleContext ctx, string id) => ctx.LivingMembers(BattleContext.PlayerTeam).Where(u => u.Def.Id == id).ToList();
    static void Act(BattleContext ctx, UnitState u) { foreach (var t in u.Traits) t.OnAction(ctx, u, u.Def.Actions![0]); }
    static readonly UnitDef[] SporeOf = { UnitCatalog.SporeH386, UnitCatalog.SporeS0, UnitCatalog.SporeP, UnitCatalog.SporeQ, UnitCatalog.SporePQ };

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh386 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 版の定義 ----
        {
            bool spores = SporeOf.Skip(1).All(d => d.Id == "spore" && d.Name == "胞子" && d.MaxHp == 14 && d.Attack == 0 && d.Speed == 10 && d.Traits[0] == TraitId.Ephemeral)
                && UnitCatalog.SporeS0.Traits.SequenceEqual(new[] { TraitId.Ephemeral, TraitId.SporeIdle }) && UnitCatalog.SporeS0.Actions is null
                && UnitCatalog.SporeP.Traits.SequenceEqual(new[] { TraitId.Ephemeral, TraitId.SporeIdle, TraitId.SporeBurst }) && UnitCatalog.SporeP.Actions is null
                && UnitCatalog.SporeQ.Traits.SequenceEqual(new[] { TraitId.Ephemeral, TraitId.SporeParasite }) && UnitCatalog.SporeQ.Actions is { Count: 1 } q && q[0].Kind == ActionKind.Skill
                && UnitCatalog.SporePQ.Traits.SequenceEqual(new[] { TraitId.Ephemeral, TraitId.SporeParasite, TraitId.SporeBurst }) && UnitCatalog.SporePQ.Actions is { Count: 1 } pq && pq[0].Kind == ActionKind.Skill
                && TraitCatalog.Get(TraitId.SporeIdle).NeverAttacksOwnTurn && !TraitCatalog.Get(TraitId.SporeIdle).SurrendersTurn;
            var m = UnitCatalog.MugH386;   // 第387期: 第386期の規定に固定
            bool mugs = MugOf.Skip(1).All(d => d.Id == "mug" && d.Name == m.Name && d.MaxHp == m.MaxHp && d.Attack == m.Attack && d.Speed == m.Speed && d.Traits.Take(m.Traits.Count).SequenceEqual(m.Traits) && d.Actions == m.Actions && d.MinusText == m.MinusText)
                && UnitCatalog.MugS0.Traits.Skip(m.Traits.Count).SequenceEqual(new[] { TraitId.MugSporeS0 }) && UnitCatalog.MugS0.PlusText == m.PlusText
                && UnitCatalog.MugP.Traits.Skip(m.Traits.Count).SequenceEqual(new[] { TraitId.MugSporeP }) && UnitCatalog.MugP.PlusText == m.PlusText + "／胞子は殴られて弾けると、殴った敵に毒を撒く"
                && UnitCatalog.MugQ.Traits.Skip(m.Traits.Count).SequenceEqual(new[] { TraitId.MugSporeQ, TraitId.ParasiteFeed, TraitId.ParasiteBloom }) && UnitCatalog.MugQ.PlusText == m.PlusText + "／胞子は敵に取り付いて中から食い、宿主が倒れると、その亡骸から胞子が湧き出す"
                && UnitCatalog.MugPQ.Traits.Skip(m.Traits.Count).SequenceEqual(new[] { TraitId.MugSporePQ, TraitId.ParasiteFeed, TraitId.ParasiteBloom }) && UnitCatalog.MugPQ.PlusText == m.PlusText + "／" + UnitCatalog.MugBurstClause386 + "／" + UnitCatalog.MugParasiteClause386;
            bool outside = MugOf.Skip(1).Concat(SporeOf.Skip(1)).All(d => !UnitCatalog.Everyone.Contains(d))
                && !UnitCatalog.Everyone.Any(d => !ReferenceEquals(d, UnitCatalog.Mug) && d.Traits.Any(t => t is >= TraitId.SporeIdle and <= TraitId.MugSporePQ))   /* 第387期: 規定のムグは印 P を持つ */
                && !CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).Any(r => r.F.Occupied().Any(o => MugOf.Skip(1).Contains(o.Def)));
            bool def = m.Traits.SequenceEqual(UnitCatalog.MugSowAll.Traits) && UnitCatalog.SporeH386.Attack == 4 && UnitCatalog.SporeH386.Traits.SequenceEqual(new[] { TraitId.Ephemeral }) && UnitCatalog.SporeH386.Actions is null;   // 第387期: 第386期の規定（旧）で見る
            Expect("版の定義: 版の胞子 4 体は攻撃力 0（HP 14 ／ 速 10 ／ 儚い・`Id` ／ 名前は胞子）・S0 ／ P は殴らない札（差し出した手番にならない）・Q ／ PQ は手番 `[Skill]` の寄生・P ／ PQ は弾けて毒・版のムグ ＝ 規定のムグの写し ＋ 印（文面は §2-3 の叩き台）・`All` ／ `Retired` ／ `compare` ／ 交差帯 ／ `Playtest` の外・規定のムグ ／ 胞子は今どおり",
                spores && mugs && outside && def);
        }

        // ---- 版のムグは版の胞子を撒く ／ 分裂で出す ----
        {
            var notes = new List<string>(); bool ok = true;
            for (int vi = 0; vi < MugOf.Length; vi++)
            {
                var (ctx, p, e) = Board((2, MugOf[vi]), (4, Mate));
                var mug = p.First(u => u.Def.Id == "mug");
                Act(ctx, mug);
                var sown = Living(ctx, "spore");
                var (c2, p2, e2) = Board((2, MugOf[vi]), (4, Mate));   // 分裂は既定の走査（倒れた駒の枠も塞ぐ）なので、撒く前の盤で倒す
                Kill(c2, p2.First(u => u.Def.Id == "mug"), e2[0]);
                var split = Living(c2, "spore");
                bool good = sown.Count == 4 && sown.All(u => u.Def == SporeOf[vi]) && split.Count == 2 && split.All(u => u.Def == SporeOf[vi]);
                ok &= good;
                notes.Add($"{VerNames[vi]} 撒いた {sown.Count}・分裂 {split.Count} 体（{(sown.Concat(split).All(u => u.Def == SporeOf[vi]) ? "すべて" : "混ざる")} {(vi == 0 ? "今の胞子" : "版の胞子")}）");
            }
            Expect("版のムグは手番で版の胞子を撒き（空いた枠をすべて）・倒れると分裂で版の胞子を2体出す（規定の胞子は出さない）・規定のムグは今の胞子", ok, string.Join("・", notes));
        }

        // ---- 版の胞子は殴らない（戦闘）----
        {
            long cells = 0, atk = 0, dmg = 0, mugDeaths = 0, sporeTurns = 0;
            for (int vi = 1; vi < MugOf.Length; vi++)
                foreach (var f in new[] { Apply(Presets.DeathChain377(), vi), Apply(Presets.DeathBard382(UnitCatalog.Ere), vi) })
                    foreach (var w in new[] { I.Boss, I.Guard, I.Bat })
                        for (int sd = 0; sd < 5; sd++)
                        {
                            cells++;
                            var (r, p, _) = I.Raw(f, w, sd, verbose: true);
                            var spores = r.Events.Where(x => x.Kind == BattleEventKind.Summon && x.Team == BattleContext.PlayerTeam && x.Text == "胞子").Select(x => x.TargetId!.Value).ToHashSet();
                            atk += r.Events.Count(x => x.Kind == BattleEventKind.Attack && x.ActorId is int a && spores.Contains(a));
                            dmg += r.Events.Count(x => x.Kind == BattleEventKind.Damage && x.ActorId is int a && spores.Contains(a) && x.Amount > 0);
                            mugDeaths += r.Events.Count(x => x.Kind == BattleEventKind.Death && x.TargetId == p.First(u => u.Def.Id == "mug").InstanceId);
                            if (r.TallyByUnit.TryGetValue("spore", out var st)) sporeTurns += st.TurnStalls + st.TurnSkills;
                        }
            Expect("版の胞子は殴らない: 版 4 つ × 死の連鎖 ／ 死 エレ × ボス ／ 近衛 ／ 大隊 × seed 0..4 で、胞子の `Attack` ／ 胞子が撃ち手の `Damage` が 0（分裂の胞子も出る戦を含む）", cells > 0 && atk == 0 && dmg == 0 && mugDeaths > 0 && sporeTurns > 0,
                $"{cells} 戦・胞子の Attack {atk}・Damage {dmg}・ムグが倒れた {mugDeaths}・胞子の手番（潰れ ／ 術）{sporeTurns}");
        }

        // ---- P: 弾けて毒 ----
        {
            var (ctx, p, e) = Board((2, UnitCatalog.MugP), (4, Mate));
            var mug = p.First(u => u.Def.Id == "mug"); var mate = p.First(u => u.Def.Id == "mate386"); var foe = e[0];
            Act(ctx, mug);
            var s = Living(ctx, "spore");
            Kill(ctx, s[0], foe); int p1 = foe.RawCounter(StatusKeys.Poison);
            ctx.LoseHp(s[1], 99, TraitId.Rot); int p2 = foe.RawCounter(StatusKeys.Poison);   // 撃破者のいない死
            Kill(ctx, s[2], mate); int p3 = foe.RawCounter(StatusKeys.Poison);              // 味方が倒した
            bool route = ctx.Events.Count(x => x.Kind == BattleEventKind.StatusGain && x.PoisonRoute == PoisonRoute.SporeBurst) == 1
                && ctx.Events.Single(x => x.Kind == BattleEventKind.StatusGain && x.PoisonRoute == PoisonRoute.SporeBurst) is var g && g.TargetId == foe.InstanceId && g.ActorId == s[0].InstanceId && g.Amount == 6;
            var (c2, p2s, e2) = Board((2, UnitCatalog.MugS0), (4, Mate));
            Act(c2, p2s.First(u => u.Def.Id == "mug")); Kill(c2, Living(c2, "spore")[0], e2[0]);
            Expect("P: 倒した者が敵ならその敵に毒 +6（経路 `SporeBurst`・書き手は胞子）／ 撃破者のいない死 ／ 味方が倒した死では撒かない ／ S0 の胞子は撒かない", p1 == 6 && p2 == 6 && p3 == 6 && route && e2[0].RawCounter(StatusKeys.Poison) == 0 && Tal(ctx, s[0]).R386BurstPoison == 6,
                $"敵の毒 {p1} → {p2} → {p3}");
        }

        // ---- Q: 寄生 ----
        {
            var (ctx, p, e) = Board((0, UnitCatalog.MugQ), (2, UnitCatalog.Rica), (4, UnitCatalog.Ere));
            var mug = p.First(u => u.Def.Id == "mug"); var rica = p.First(u => u.Def.Id == "rica"); var ere = p.First(u => u.Def.Id == "ere"); var foe = e[0];
            Act(ctx, mug);
            var s = Living(ctx, "spore");
            int deaths0 = ctx.Events.Count(x => x.Kind == BattleEventKind.Death), layer0 = rica.RawCounter("necro"), snd0 = ere.RawCounter(DirgeSoundTrait.SoundKey);
            Act(ctx, s[0]); Act(ctx, s[1]);
            bool gone = !s[0].IsAlive && !s[1].IsAlive && foe.RawCounter(SporeParasiteTrait.CountKey) == 2 && ctx.Events.Count(x => x.Kind == BattleEventKind.Death) == deaths0
                && rica.RawCounter("necro") == layer0 && ere.RawCounter(DirgeSoundTrait.SoundKey) == snd0
                && ctx.Events.Count(x => x.Kind == BattleEventKind.Parasite && x.TargetId == foe.InstanceId) == 2 && Tal(ctx, s[0]).R386Parasite == 2;   // 帳簿は `Id`（胞子）ごと
            Act(ctx, mug);
            bool reseat = Living(ctx, "spore").Count == 4 && Living(ctx, "spore").Select(u => u.Slot).Contains(s[0].Slot);
            Expect("Q: 寄生で胞子が盤から消える（死の出来事が出ない・層 ／ エレの音が動かない・表示専用の出来事 `Parasite`）・宿主の寄生の数が重なる・枠が空いてムグがまた撒ける", gone && reseat,
                $"寄生の数 {foe.RawCounter(SporeParasiteTrait.CountKey)}・層 {layer0} → {rica.RawCounter("necro")}・音 {snd0} → {ere.RawCounter(DirgeSoundTrait.SoundKey)}・撒き直し {Living(ctx, "spore").Count} 体");

            TurnField.SetValue(ctx, 2);
            int hp0 = foe.Hp; int ev0 = ctx.Events.Count;
            ctx.TickStatuses();
            var feed = ctx.Events.Skip(ev0).Where(x => x.Kind == BattleEventKind.Damage && x.TargetId == foe.InstanceId).ToList();
            bool fed = hp0 - foe.Hp == 2 * SporeParasiteTrait.FeedPer && feed.Count == 1 && feed[0].ActorId == mug.InstanceId;
            Kill(ctx, mug, foe);   // ムグが倒れる（分裂で胞子 → 枠は埋まっている）
            int hp1 = foe.Hp; ev0 = ctx.Events.Count;
            TurnField.SetValue(ctx, 3);
            ctx.TickStatuses();
            var feed2 = ctx.Events.Skip(ev0).Where(x => x.Kind == BattleEventKind.Damage && x.TargetId == foe.InstanceId).ToList();
            bool fed2 = hp1 - foe.Hp == 2 * SporeParasiteTrait.FeedPer && feed2.Count == 1 && feed2[0].ActorId is null;
            Expect("Q: ターンの頭に寄生の数 × 4 のダメージ（撃ち手はムグ・ムグが倒れていれば撃ち手なし）", fed && fed2, $"HP {hp0} → {hp1}（撃ち手 ムグ）→ {foe.Hp}（撃ち手なし）");
        }
        {
            // 宿主が倒れると寄生の数だけ湧く（空いた枠まで・残りは捨てる）
            var (ctx, p, e) = Board((2, UnitCatalog.MugPQ), (4, Mate));
            var mug = p.First(u => u.Def.Id == "mug"); var mate = p.First(u => u.Def.Id == "mate386"); var foe = e[0];
            Act(ctx, mug);
            foreach (var s in Living(ctx, "spore")) Act(ctx, s);   // 4 体寄生
            Act(ctx, mug);                                         // 4 枠を埋め直す
            Kill(ctx, Living(ctx, "spore")[0], mate);              // 1 枠だけ空ける（味方が倒した・毒は撒かない）
            int before = Living(ctx, "spore").Count;
            Kill(ctx, foe, mate);
            var bloom = Living(ctx, "spore").Except(Living(ctx, "spore").Take(before)).ToList();
            int now = Living(ctx, "spore").Count;
            bool ok = foe.RawCounter(SporeParasiteTrait.CountKey) == 0 && now == 4 && before == 3 && Tal(ctx, foe).R386Bloom == 1 && Tal(ctx, foe).R386BloomLost == 3
                && ctx.LivingMembers(BattleContext.PlayerTeam).Where(u => u.Def.Id == "spore").All(u => u.Def == UnitCatalog.SporePQ);
            var (c2, p2, e2) = Board((2, UnitCatalog.MugQ), (4, Mate));
            var m2 = p2.First(u => u.Def.Id == "mug");
            Act(c2, m2);
            var s2 = Living(c2, "spore"); Act(c2, s2[0]); Act(c2, s2[1]); Act(c2, s2[2]);   // 3 体寄生 → 3 枠空く
            Kill(c2, e2[0], p2.First(u => u.Def.Id == "mate386"));
            bool ok2 = Living(c2, "spore").Count == 4 && Tal(c2, e2[0]).R386Bloom == 3 && Tal(c2, e2[0]).R386BloomLost == 0 && Living(c2, "spore").All(u => u.Def == UnitCatalog.SporeQ);
            Expect("Q ／ PQ: 宿主が倒れると寄生の数だけ版の胞子が召喚枠に湧く（空いた枠まで・湧かなかった分は捨てる・Q は Q の胞子 ／ PQ は PQ の胞子）", ok && ok2,
                $"PQ: 寄生 4・空き 1 → 湧いた {Tal(ctx, foe).R386Bloom}・捨てた {Tal(ctx, foe).R386BloomLost} ／ Q: 寄生 3・空き 3 → 湧いた {Tal(c2, e2[0]).R386Bloom}");
        }

        // ---- 規定のムグ ／ 胞子は今どおり・ムグのいない行 ----
        {
            var bal = File.ReadAllLines("docs/balance.md").Where(l => l.StartsWith("| ")).Select(l => l.Split('|').Select(c => c.Trim()).ToArray()).Where(c => c.Length > 7).ToDictionary(c => c[1], c => c[2..7]);
            var rows = CompareBuilds();
            int nw = EnemyCatalog.Stages.Count;
            var g = new string[rows.Length, nw];
            Parallel.For(0, rows.Length * nw, k =>
            {
                int ri = k / nw, wi = k % nw, wins = 0;
                for (int sd = 0; sd < CompareSeeds; sd++) if (BattleEngine.Run(rows[ri].F, EnemyCatalog.Stages[wi].Enemy, sd, verbose: false).PlayerWon) wins++;
                g[ri, wi] = S307.F1(100.0 * wins / CompareSeeds);
            });
            int diff = 0, miss = 0;
            for (int ri = 0; ri < rows.Length; ri++)
            {
                if (!bal.TryGetValue(rows[ri].Name, out var c)) { miss++; continue; }
                for (int wi = 0; wi < nw; wi++) if (c[wi].TrimEnd('%') != g[ri, wi]) diff++;
            }
            bool others = rows.Where(r => !S377.HasMug(r.F)).All(r => Enumerable.Range(1, 4).All(vi => Apply(r.F, vi).Occupied().Select(o => o.Def).SequenceEqual(r.F.Occupied().Select(o => o.Def))));
            Expect("規定は動かない: `compare` 全行 × 5 波 × seed 0..199 が `docs/balance.md` と一致（ムグのいる行も・規定のムグ ／ 胞子は今どおり）・ムグのいない行は版の写像で1枚も変わらない", diff == 0 && miss == 0 && others,
                $"{rows.Length} 行・{rows.Length * nw} セル・ずれ {diff}・表に無い行 {miss}");
        }

        // ---- 試遊の台 ----
        {
            var vr = VersionsH386();   // 第387期: 第386期の一覧（「版・弾け」を足し戻し・ムグは旧）
            var a = vr.FirstOrDefault(r => r.Name == "版・弾け 死の連鎖"); var b = vr.FirstOrDefault(r => r.Name == "版・寄生 死の連鎖");
            bool two = a.F is not null && b.F is not null && vr[^2].Name == "版・弾け 死の連鎖" && vr[^1].Name == "版・寄生 死の連鎖"
                && a.F.Occupied().Select(o => o.Def).SequenceEqual(Presets.DeathChain377(mug: UnitCatalog.MugP).Occupied().Select(o => o.Def))
                && b.F.Occupied().Select(o => o.Def).SequenceEqual(Presets.DeathChain377(mug: UnitCatalog.MugQ).Occupied().Select(o => o.Def));
            bool only = vr.Count(r => r.F.Occupied().Any(o => MugOf.Skip(1).Contains(o.Def))) == 2
                && VersionsH385().Select(r => r.Name).SequenceEqual(vr.Take(vr.Length - 2).Select(r => r.Name));
            Expect("試遊の台: `Presets.Versions` の末尾に「版・弾け 死の連鎖」（ムグ P）／「版・寄生 死の連鎖」（ムグ Q）・ほかの台は第385期のまま（`VersionsH385`）・版のムグのいる台はこの2台だけ（ほかの版の台は器具の中だけ）", two && only,
                string.Join("、", vr.Select(r => r.Name)));
        }

        // ---- verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var (_, _, f0) in Boards().Take(4))
                for (int vi = 1; vi < MugOf.Length; vi++)
                    foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                        for (int sd = 0; sd < 3; sd++)
                        {
                            var f = Apply(f0, vi);
                            cells++;
                            var r0 = I.Raw(f, w, sd).R;
                            if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                            if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                        }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（死の台 4 × 版 4 × ボス ／ 近衛 ／ 大隊 ／ 第5波 × seed 0..2）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- 毒の経路 ----
        {
            int nr = Enum.GetValues<PoisonRoute>().Length;
            Expect("毒の経路 `SporeBurst` は末尾（`CloakOpening` の後）・燃焼の添字は後ろ（`SoakRouteCount` ＝ 経路の数 ＋ 1）", (int)PoisonRoute.SporeBurst == nr - 1 && (int)PoisonRoute.CloakOpening == nr - 2 && BattleContext.SoakBurnRouteIx == nr && BattleContext.SoakRouteCount == nr + 1,
                $"経路 {nr} 本・`SoakRouteCount` {BattleContext.SoakRouteCount}・燃焼の添字 {BattleContext.SoakBurnRouteIx}");
        }

        // ---- checkup の札の表 ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            var news = new[] { TraitId.SporeIdle, TraitId.SporeBurst, TraitId.SporeParasite, TraitId.ParasiteFeed, TraitId.ParasiteBloom, TraitId.MugSporeS0, TraitId.MugSporeP, TraitId.MugSporeQ, TraitId.MugSporePQ };
            bool ok = news.All(t => System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{t}\]\s*=\s*\(Hc[A-Za-z]+L,\s*""[^""]*第386期"));
            Expect("`checkup` の札の分類表に新しい札（殴らない胞子 ／ 弾けて毒 ／ 寄生 ／ 寄生の食い ／ 亡骸から湧く ／ 版の胞子を出す 4 枚）の第386期の行がある（ソースを読んで確かめる）", ok);
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 4774c8e:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            string he = Head("BattleCore/BattleEngine.cs"), ht = Head("BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect("`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第385期の HEAD（4774c8e）と同じ・寄生の的は `SelectTarget`）", e0 == e1 && t0 == t1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
