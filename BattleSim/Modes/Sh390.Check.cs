using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S388 = Sh388Diag;

// sh390 check —— 第390期の自己検査（指示書 §8 の「新しい器具に最低限入れる」）。
static partial class Sh390Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly FieldInfo TurnField = typeof(BattleContext).GetField("_turn", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly MethodInfo TallyOfM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;
    static bool SameFull(BattleResult a, BattleResult b) => Same(a, b) && a.Events.Count == b.Events.Count && a.Log.Count == b.Log.Count
        && a.Events.Select(e => (e.Kind, e.Turn, e.ActorId, e.TargetId, e.Amount)).SequenceEqual(b.Events.Select(e => (e.Kind, e.Turn, e.ActorId, e.TargetId, e.Amount)));
    static readonly UnitDef Mate = new() { Id = "mate390", Name = "仲間", MaxHp = 50, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static UnitDef FoeDef(string id, int hp = 1000) => new() { Id = id, Name = "的" + id[^1], MaxHp = hp, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static UnitTally Tl(BattleContext c, UnitState u) => (UnitTally)TallyOfM.Invoke(c, new object[] { u })!;

    static (BattleContext Ctx, List<UnitState> P, List<UnitState> E) Bench((int Slot, UnitDef D)[] mine, (int Slot, UnitDef D, int Hp)[] foes)
    {
        var ctx = new BattleContext(0, true);
        var f = new Formation(); foreach (var (s, d) in mine) f[s] = d;
        var g = new Formation(); foreach (var (s, d, _) in foes) g[s] = d;
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = BattleEngine.Materialize(g, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var (s, _, hp) in foes) e.First(u => u.Slot == s).Hp = hp;
        foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
        TurnField.SetValue(ctx, 1);
        return (ctx, p, e);
    }
    static UnitState Hg(List<UnitState> p) => p.First(u => u.Def.Id == "hagi");
    static UnitState Mt(List<UnitState> p) => p.First(u => u.Def.Id == "mate390");
    static UnitState At(List<UnitState> e, int slot) => e.First(u => u.Slot == slot);

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh390 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef hagi = UnitCatalog.HagiH391, old = UnitCatalog.HagiH389, r5m = UnitCatalog.HagiR5m;   // 第392期: 第390〜391期の規定（`HagiH391`）に固定
        UnitDef v1 = UnitCatalog.HagiV1, v2 = UnitCatalog.HagiV2, v3 = UnitCatalog.HagiV3, va = UnitCatalog.HagiVA;
        UnitDef fa = FoeDef("foe390a"), fb = FoeDef("foe390b"), fc = FoeDef("foe390c"), fd = FoeDef("foe390d"), small = FoeDef("foe390s", 400);

        // ---- 段0: 規定のハギの札と文面 ／ 旧 ／ 固定の口 ----
        {
            bool def = hagi.Id == "hagi" && hagi.Name == "追い打ちのハギ" && hagi.MaxHp == 62 && hagi.Attack == 16 && hagi.Speed == 7 && hagi.Pattern == AttackPattern.Single && hagi.Actions is null
                && hagi.Traits.SequenceEqual(r5m.Traits) && UnitCatalog.All.Contains(UnitCatalog.Hagi) && !UnitCatalog.Retired.Contains(hagi) && UnitCatalog.All.Count == 52
                && hagi.PlusText.StartsWith("大鎌で、弱った敵を刈り取る（HP が線を切った敵は一振りで倒す）。倒せば、もう一度振る。敵が半分まで弱ると", StringComparison.Ordinal)
                && hagi.PlusText.Contains("ついには一振りで列ごと刈る。斬るたび、その敵の命に印を刻み、鎌の届く線を深くする（印1つ +5%・合わせて最大 6 割）", StringComparison.Ordinal)
                && hagi.MinusText == "大鎌の振りは、隣にいる仲間も巻き込む。刈る相手がいなければ、魂は薄れていく" && hagi.Flavor == r5m.Flavor;
            bool o = old.Id == "hagi" && old.Traits.SequenceEqual(new[] { TraitId.Pursuer }) && old.Pattern == AttackPattern.Sweep && old.MaxHp == 62 && old.Attack == 16 && old.Speed == 7
                && old.MinusText == "自分の手番では決して動かない。味方が誰も倒せなければ置物" && !UnitCatalog.Everyone.Contains(old);
            bool holders = !UnitCatalog.All.Any(d => d.Traits.Contains(TraitId.Pursuer))
                && new[] { TraitId.HagiReap, TraitId.HagiHalfPursuit, TraitId.HagiDepthSoul, TraitId.HagiSoulMoves, TraitId.HagiReapMark, TraitId.HagiSpillFixed }.All(t => UnitCatalog.All.Count(d => d.Traits.Contains(t)) == 1)
                && new[] { TraitId.HagiAimLine, TraitId.HagiColumnEarly, TraitId.HagiLineEvery }.All(t => !UnitCatalog.Everyone.Any(d => d.Traits.Contains(t)));
            bool pins = PinH389(hagi) == old && PinH389(r5m) == r5m && PinV381(hagi) == old && PinM379(hagi) == old && PinMug386(hagi) == old && PinE385(hagi) == old
                && PinR378(hagi) == old && PinN355(hagi) == old && Pin389(Presets.HagiPlay389(hagi))[3] == old;
            Expect("段0: 規定のハギ ＝ 第389期の R5m の札（刈り取り ／ 追撃 ／ 魂の深さ ／ 段の動き ／ 刈りの印 ／ 固定の巻き込み）・体 62 ／ 16 ／ 7・単体・名前「追い打ちのハギ」・文面は叩き台 ＋ 印の句・`All` の 52 枚 ／ 旧 `HagiH389`（追い打ち・`All` ／ `Retired` の外）／ 固定の口（`PinH389` ／ `Pin389`・古い口の終端と `PinMug386` ／ `PinE385` からも通る）／ `Pursuer` の保持者 0 枚",
                def && o && holders && pins, $"規定 {string.Join("・", hagi.Traits)}");
        }

        // ---- 段0: 試遊の台の一覧と過去の一覧（第391期: 第390期の一覧 `VersionsH390` で読む）----
        {
            var names = VersionsH390().Select(r => r.Name).ToList();
            var h389 = VersionsH389();
            var want389 = names.Where(n => !VersionRows390.Contains(n)).Select(n => n switch { "試遊・ハギ 台B" => "版・R5 ハギ", "試遊・死神と墓守" => "版・R5 死神と墓守", _ => n }).ToList();
            bool rename = names.Contains("試遊・ハギ 台B") && names.Contains("試遊・死神と墓守") && !names.Contains("版・R5 ハギ") && !names.Contains("版・R5 死神と墓守")
                && VersionsH390().First(r => r.Name == "試遊・ハギ 台B").F[3] == hagi && VersionsH390().First(r => r.Name == "試遊・死神と墓守").F[0] == hagi;
            bool past = h389.Select(r => r.Name).SequenceEqual(want389) && h389.First(r => r.Name == "版・R5 ハギ").F[3] == r5m && h389.First(r => r.Name == "版・R5 死神と墓守").F[0] == r5m
                && !h389.SelectMany(r => r.F.Occupied()).Any(o => o.Def == hagi)
                && VersionsH388().Count(r => r.Name is "版・H3 ハギ" or "版・H3 死神と墓守") == 2 && !VersionsH388().Any(r => r.Name.StartsWith("版・R5", StringComparison.Ordinal))
                && Sh387Diag.Play().Select(r => r.Name).SequenceEqual(h389.Where(r => r.Name.StartsWith("試遊・", StringComparison.Ordinal)).Select(r => r.Name));
            var added = VersionsH390().Where(r => VersionRows390.Contains(r.Name)).ToList();
            bool tail = added.Count == 2 && names.TakeLast(2).SequenceEqual(VersionRows390)
                && added.First(r => r.Name == "版・VA ハギ").F.Occupied().Count(o => o.Def == va) == 1 && added.First(r => r.Name == "版・移動 ハギ").F.Occupied().Count(o => o.Def == hagi) == 1
                && Som307Diag.Seats(added.First(r => r.Name == "版・VA ハギ").F) == Som307Diag.Seats(Presets.HagiDeath389(va))
                && Som307Diag.Seats(added.First(r => r.Name == "版・移動 ハギ").F) == Som307Diag.Seats(Put(CompareBuilds().First(r => r.Name == MoveRows[0]).F, 1, hagi));
            var vers = new[] { v1, v2, v3, va };
            bool inside = !CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).Concat(VersionsH390().Where(r => r.Name != "版・VA ハギ")).SelectMany(r => r.F.Occupied()).Any(o => vers.Contains(o.Def) || o.Def.Id.EndsWith("_plain388", StringComparison.Ordinal));
            Expect("試遊の台: 「版・R5 ハギ」「版・R5 死神と墓守」→「試遊・ハギ 台B」「試遊・死神と墓守」（規定のハギ）・末尾に「版・VA ハギ」「版・移動 ハギ」・第389期の一覧 `VersionsH389`（名前と R5m を戻し、ハギは旧）／ `VersionsH388` ／ `sh387` の試遊の台は第389期の並び・替えた台 ／ 素体 ／ V1〜V3 は器具の中だけ",
                rename && past && tail && inside, string.Join("、", names.TakeLast(4)));
        }

        // ---- 段0: 規定のハギは第389期の R5m と同じ動き ----
        {
            long cells = 0, diff = 0;
            var boards = MainBoards().ToList();
            foreach (var b in boards)
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 5; sd++)
                    {
                        cells++;
                        var a = I.Raw(ColBoard(b, hagi), w, sd, verbose: true).R; var z = I.Raw(ColBoard(b, r5m), w, sd, verbose: true).R;
                        if (!SameFull(a, z)) diff++;
                    }
            var rows = CompareBuilds().Where(r => Has(r.F, "hagi")).ToArray();
            var gA = CGrid(rows, 20); var gB = CGrid(rows.Select(r => (r.Name, I.Swap(r.F, "hagi", r5m))).ToArray(), 20);
            int cd = 0; for (int ri = 0; ri < rows.Length; ri++) for (int w = 0; w < gA.GetLength(1); w++) if (gA[ri, w] != gB[ri, w]) cd++;
            Expect("規定のハギは第389期の R5m と同じ動き（3 台 × ボス ／ 近衛 ／ 大隊 ／ 第5波 × seed 0..4 の勝敗 ／ T ／ 生存数 ／ 出来事の並び ／ ログの数・`compare` のハギの 5 行 × 5 波 × seed 0..19）",
                diff == 0 && cd == 0, $"{cells} 戦・差 {diff}・compare のセルの差 {cd}");
        }

        // ---- 段0: 第388 ／ 389期の版の駒の結果が変わらない ----
        {
            var got = new List<string>(); bool ok = true;
            var bs = S388.RicaBoards();
            var want = new (double A4, double Str)[] { (97.5, 50.0), (100.0, 73.5) };   // 第388期の報告（`rica.md` §1・H2）
            for (int i = 0; i < bs.Length; i++)
            {
                var g = M8(ColBoard(bs[i], UnitCatalog.HagiH2));
                ok &= F1(A4(g)) == F1(want[i].A4) && F1(g[WStrong].Win) == F1(want[i].Str);
                got.Add($"H2 {bs[i].Name} {F1(A4(g))} ／ {F1(g[WStrong].Win)}");
            }
            var b5 = M8(Presets.HagiPlay389(r5m)); var d5 = M8(Presets.HagiDeath389(r5m));   // 第389期の報告（§3-1・R5m: 台B 77.2 ／ 強 8.2・死の連鎖 98.2 ／ 49.5）
            ok &= F1(A4(b5)) == "77.2" && F1(b5[WStrong].Win) == "8.2" && F1(A4(d5)) == "98.2" && F1(d5[WStrong].Win) == "49.5";
            got.Add($"R5m 台B {F1(A4(b5))} ／ {F1(b5[WStrong].Win)}・死の連鎖 {F1(A4(d5))} ／ {F1(d5[WStrong].Win)}");
            Expect("第388 ／ 389期の版の駒の結果が変わらない（H2 のリィカと並べる台 2・R5m の「版・R5」の 2 台 × 8 物差し × seed 0..99 の 4 波の平均 ／ 強い条件が第388 ／ 389期の報告の値と一致）", ok, string.Join("・", got));
        }

        // ---- V1 ----
        {
            var notes = new List<string>(); bool ok = true;
            // 割合の低い A（30%・印なし・線 2 割の外）と、印 3 で線 35% の内の B（34%）
            (int, UnitDef, int)[] foes = { (0, fa, 300), (1, fb, 340) };
            var (c0, p0, e0) = Bench(new[] { (2, hagi) }, foes); At(e0, 1).SetCounter(HagiDepthTrait.MarkKey, 3); c0.TakeTurn(Hg(p0));
            var (c1, p1, e1) = Bench(new[] { (2, v1) }, foes); At(e1, 1).SetCounter(HagiDepthTrait.MarkKey, 3); c1.TakeTurn(Hg(p1));
            var t0 = Tl(c0, Hg(p0)); var t1 = Tl(c1, Hg(p1));
            bool g1 = At(e0, 1).IsAlive && At(e0, 0).Hp < 300 && t0.R388Reaps == 0 && t0.R390AimSwitch == 0
                   && !At(e1, 1).IsAlive && t1.R388Reaps == 1 && t1.R390AimSwitch == 1 && t1.R390AimMark == 1 && t1.R389MarkReaps == 1;
            ok &= g1; notes.Add($"印で線を切った B を刈る（規定は割合の低い A を斬る）{(g1 ? "" : $" × 規定 reaps {t0.R388Reaps} ／ V1 reaps {t1.R388Reaps} sw {t1.R390AimSwitch} mk {t1.R390AimMark}")}");
            // 線を切っている敵がいなければ今どおり
            var (c2, p2, e2) = Bench(new[] { (2, v1) }, foes); c2.TakeTurn(Hg(p2));
            var (c3, p3, e3) = Bench(new[] { (2, hagi) }, foes); c3.TakeTurn(Hg(p3));
            bool g2 = e2.Select(u => u.Hp).SequenceEqual(e3.Select(u => u.Hp)) && Tl(c2, Hg(p2)).R390AimSwitch == 0 && At(e2, 0).Hp == 300 - 16;
            ok &= g2; notes.Add($"線を切った敵がいなければ今どおり（割合の低い A を斬る）{(g2 ? "" : " ×")}");
            // 線を切った敵が2体: HP のいちばん低い敵（割合ではなく）
            (int, UnitDef, int)[] two = { (0, fa, 150), (1, small, 70) };   // A 15%（150）／ 小 17.5%（70）
            var (c4, p4, e4) = Bench(new[] { (2, v1) }, two); c4.TakeTurn(Hg(p4));
            var (c5, p5, e5) = Bench(new[] { (2, hagi) }, two); c5.TakeTurn(Hg(p5));
            var t4 = Tl(c4, Hg(p4)); var t5 = Tl(c5, Hg(p5));
            bool g3 = t4.R390AimSwitch == 1 && t4.R390AimMark == 0 && t4.R388Reaps == 2 && t4.R389LastReapHp == 150 && t5.R390AimSwitch == 0 && t5.R388Reaps == 2 && t5.R389LastReapHp == 70;
            ok &= g3; notes.Add($"線の内に 2 体なら HP の低い方（70）から刈る（規定は割合の低い 150 から）{(g3 ? "" : $" × last {t4.R389LastReapHp} ／ {t5.R389LastReapHp}")}");
            Expect("V1: 線を切っている敵がいればその中で HP の最も低い敵を刈る ／ いなければ今どおり", ok, string.Join("・", notes));
        }

        // ---- V2 ----
        {
            var notes = new List<string>(); bool ok = true;
            (int, UnitDef, int)[] foes = { (0, fa, 300), (1, fb, 450), (2, fc, 400), (3, fd, 600) };
            // 魂 6（線 4 割）: V2 は的 ＋ 中列の 40% を一振りで刈り、再行動しない ／ 規定は列ごと刈らず、再行動で 1 体ずつ
            var (c0, p0, e0) = Bench(new[] { (2, v2) }, foes); var h0 = Hg(p0); HagiDepthTrait.Gain(c0, h0, 6, 0); c0.TakeTurn(h0);
            var (c1, p1, e1) = Bench(new[] { (2, hagi) }, foes); var h1 = Hg(p1); HagiDepthTrait.Gain(c1, h1, 6, 0); c1.TakeTurn(h1);
            var t0 = Tl(c0, h0); var t1 = Tl(c1, h1);
            bool g1 = !At(e0, 0).IsAlive && !At(e0, 2).IsAlive && At(e0, 1).Hp == 450 && At(e0, 3).Hp == 600 && t0.R389ColumnReaps == 1 && t0.R390ColumnEarly == 1 && t0.R390ColumnEarlyUnits == 1 && t0.R390ColumnStops == 1 && t0.R388Encores == 0
                   && t1.R389ColumnReaps == 0 && t1.R388Encores == 1 && t1.R390ColumnStops == 0 && At(e1, 2).IsAlive && At(e1, 1).Hp < 450;   // 規定の再行動は薙ぎ（魂 3 から）なので中列にも当たる
            ok &= g1; notes.Add($"魂 6 で列ごと刈り（的 ＋ 中列の 40%）・その振りで手番を終える（規定は列ごと刈らず、再行動で前列の 45% を薙ぐ——前列が残る間は中列を的にできない）{(g1 ? "" : $" × col {t0.R389ColumnReaps}/{t0.R390ColumnEarly} stop {t0.R390ColumnStops} enc {t0.R388Encores} ／ 規定 col {t1.R389ColumnReaps} enc {t1.R388Encores}")}");
            // 魂 9（線 5 割）: 列ごと刈った後も再行動
            var (c2, p2, e2) = Bench(new[] { (2, v2) }, foes); var h2 = Hg(p2); HagiDepthTrait.Gain(c2, h2, 9, 0); c2.TakeTurn(h2);
            var t2 = Tl(c2, h2);
            bool g2 = e2.Count(u => !u.IsAlive) == 3 && At(e2, 3).IsAlive && At(e2, 3).Hp < 600 && t2.R389ColumnReaps == 1 && t2.R389ColumnUnits == 2 && t2.R390ColumnEarly == 0 && t2.R390ColumnStops == 0 && t2.R388Encores == 1;
            ok &= g2; notes.Add($"魂 9 で列ごと刈った後も再行動（残りの 60% を斬る）{(g2 ? "" : $" × enc {t2.R388Encores} stop {t2.R390ColumnStops}")}");
            // 魂 5 では列ごと刈らない
            var (c3, p3, _) = Bench(new[] { (2, v2) }, foes); var h3 = Hg(p3); HagiDepthTrait.Gain(c3, h3, 5, 0);
            bool g3 = !HagiDepthTrait.Column(h3) && HagiDepthTrait.Column(h0) is var _ && HagiDepthTrait.ColumnEarlyAt == 6;
            ok &= g3; notes.Add("魂 5 では列ごと刈らない");
            Expect("V2: 魂 6 で列ごと刈る（その振りで手番を終える）／ 魂 9 で列ごと刈った後も再行動（2 回まで）", ok, string.Join("・", notes));
        }

        // ---- V3 ----
        {
            var notes = new List<string>(); bool ok = true;
            var (c0, p0, e0) = Bench(new[] { (3, v3), (2, Mate) }, new[] { (0, fa, 900) });
            var (c1, p1, e1) = Bench(new[] { (3, hagi), (2, Mate) }, new[] { (0, fa, 900) });
            foreach (var (c, p, e) in new[] { (c0, p0, e0), (c1, p1, e1) })
            {
                c.ApplyDamage(e[0], 200, Mt(p));      // 900 → 700: 8割を割った
                c.Heal(e[0], 200); c.ApplyDamage(e[0], 200, Mt(p));   // 同じターンにもう一度 8割を割る
            }
            int s0 = HagiDepthTrait.Soul(Hg(p0)), s1 = HagiDepthTrait.Soul(Hg(p1));
            bool g1 = s0 == 2 && s1 == 1 && Tl(c0, Mt(p0)).R390LinesCrossed == 2 && Tl(c0, Mt(p0)).R390LinesRepeat == 1 && Tl(c1, Mt(p1)).R390LinesCrossed == 1 && Tl(c1, Mt(p1)).R390LinesRepeat == 0;
            ok &= g1; notes.Add($"同じ敵の同じ線を1ターンに2回割れば V3 は +2（規定は +1）・割った一撃の主に数える{(g1 ? "" : $" × V3 {s0} ／ 規定 {s1}")}");
            Expect("V3: 同じ敵の同じ線を1ターンに2回割れば +2", ok, string.Join("・", notes));
        }

        // ---- 刈り取りの撃破の数え ----
        {
            (int, UnitDef, int)[] foes = { (0, fa, 150), (1, small, 70) };
            var (c, p, e) = Bench(new[] { (2, va) }, foes); c.TakeTurn(Hg(p));
            var t = Tl(c, Hg(p));
            bool ok = t.R390ReapKills == 2 && t.Kills == 2 && t.R388Reaps == 2;
            Expect("刈り取りで倒した数（`R390ReapKills`）が刈り取りの撃破と一致（VA・2 体）", ok, $"刈り取り {t.R388Reaps} ／ 撃破 {t.Kills} ／ 刈り取りで倒した {t.R390ReapKills}");
        }

        // ---- 版の札と文面 ----
        {
            bool ok = new[] { v1, v2, v3, va }.All(d => d.Id == "hagi" && d.MaxHp == 62 && d.Attack == 16 && d.Speed == 7 && d.Pattern == AttackPattern.Single && d.Traits.Take(5).SequenceEqual(hagi.Traits.Take(5)) && d.Traits[^1] == TraitId.HagiSpillFixed
                    && d.PlusText.StartsWith(hagi.PlusText, StringComparison.Ordinal) && d.MinusText == hagi.MinusText && !UnitCatalog.Everyone.Contains(d))
                && v1.Traits.Contains(TraitId.HagiAimLine) && !v1.Traits.Contains(TraitId.HagiColumnEarly) && v2.Traits.Contains(TraitId.HagiColumnEarly) && v3.Traits.Contains(TraitId.HagiLineEvery)
                && new[] { TraitId.HagiAimLine, TraitId.HagiColumnEarly, TraitId.HagiLineEvery }.All(va.Traits.Contains)
                && va.PlusText == hagi.PlusText + v1.PlusText[hagi.PlusText.Length..] + v2.PlusText[hagi.PlusText.Length..] + v3.PlusText[hagi.PlusText.Length..];
            Expect("版の札と文面: V1 ／ V2 ／ V3 ／ VA ＝ 規定のハギ ＋ 札（刈りの印の後ろ）・文面は規定に1句ずつ・`All` ／ `Retired` の外", ok, string.Join(" ／ ", new[] { ("V1", v1), ("V2", v2), ("V3", v3), ("VA", va) }.Select(x => $"{x.Item1} {string.Join("・", x.Item2.Traits.Skip(5).SkipLast(1))}")));
        }

        // ---- docs: 規定の compare ／ units ----
        {
            var map = BalanceMap(File.Exists("docs/balance.md") ? File.ReadAllText("docs/balance.md") : "");
            var rows = CompareBuilds(); var g = CGrid(rows, CompareSeeds);
            int diff = 0, miss = 0;
            for (int ri = 0; ri < rows.Length; ri++)
            {
                if (!map.TryGetValue(rows[ri].Name, out var c)) { miss++; continue; }
                for (int w = 0; w < 5; w++) if (c[w].TrimEnd('%') != F1(g[ri, w])) diff++;
            }
            string units = File.Exists("docs/units.md") ? File.ReadAllText("docs/units.md") : "";
            string row = units.Split('\n').FirstOrDefault(l => l.StartsWith($"| **{hagi.Name}** |", StringComparison.Ordinal)) ?? "";
            bool urow = row.Contains("斬るたび、その敵の命に印を刻み", StringComparison.Ordinal);
            Expect("`docs/` は規定のハギで再生成した（`compare` 全行 × 5 波 × seed 0..199 が `docs/balance.md` と一致・`docs/units.md` のハギの行が規定の文面）", diff == 0 && miss == 0 && urow, $"{rows.Length} 行・ずれ {diff}・表に無い行 {miss}・units の行 {(urow ? "規定" : "×")}");
        }

        // ---- checkup の札の表 ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            var nowCards = new[] { TraitId.HagiReap, TraitId.HagiHalfPursuit, TraitId.HagiDepthSoul, TraitId.HagiSoulMoves, TraitId.HagiReapMark, TraitId.HagiSpillFixed };
            var newCards = new[] { TraitId.HagiAimLine, TraitId.HagiColumnEarly, TraitId.HagiLineEvery };
            var m1 = nowCards.Where(t => !System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{t}\]\s*=\s*\(Hc[A-Za-z]+L,\s*""[^""]*第390期から規定（ハギ）""")).ToList();
            var m2 = newCards.Where(t => !System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{t}\]\s*=\s*\(HcPlusL,\s*""[^""]*第390期[^""]*保持者 0 枚""")).ToList();
            bool pursuer = System.Text.RegularExpressions.Regex.IsMatch(src, @"\[TraitId\.Pursuer\]\s*=\s*\(HcBothL,\s*""[^""]*第390期に規定のハギから外した（保持者 0 枚）""");
            Expect("`checkup` の札の分類表: 規定のハギの札 6 枚は「第390期から規定（ハギ）」・`Pursuer` は保持者 0 枚・第390期の札 3 枚（V1 ／ V2 ／ V3）がある（ソースを読んで確かめる）", m1.Count == 0 && m2.Count == 0 && pursuer,
                m1.Count + m2.Count == 0 ? "" : "無い: " + string.Join("・", m1.Concat(m2)));
        }

        // ---- verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var b in MainBoards())
                foreach (var v in Vers)
                    foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                        for (int sd = 0; sd < 2; sd++)
                        {
                            cells++;
                            var f = ColBoard(b, v.D);
                            var r0 = I.Raw(f, w, sd).R;
                            if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                            if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                        }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（3 台 × 版 5 × ボス ／ 近衛 ／ 大隊 ／ 第5波 × seed 0..1）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string he = GitShow($"{HeadSha}:BattleCore/BattleEngine.cs"), ht = GitShow($"{HeadSha}:BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect($"`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第389期の HEAD（{HeadSha}）と同じ）", e0 == e1 && t0 == t1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
