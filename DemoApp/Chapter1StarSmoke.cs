using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// ☆の重ねの門（第463期 §3・`--chapter1-star-smoke`・指示書 design/PHASE463_STAR_SPEC.md）
//
// **画面を1つも作らない。** `Chapter1`（`ChapterRule.V463`）／ `StarRule` ／ `Map11State` と器具の方針（`Map12ChangeSmoke.Greedy`）だけを回す。
//
//   (a) 1 章の 1-1 の 6 枚はすべて☆1（最大HP・攻撃が今の ×0.6・四捨五入）
//   (b) 呼ぶ候補に持っている駒と同じキャラが出る。☆3 のキャラは出ない
//   (c) ☆1 に 2 枚重ねると☆2（×0.8）、☆2 に 3 枚重ねると☆3（×1.0）。途中の数が札に出る
//   (d) ☆が上がると今の HP は割合を保つ
//   (e) 倒れた駒と同じキャラを呼ぶと、倒れた駒に重なる
//   (f) 売ると ＋2 × 重ねた枚数
//   (g) ☆と途中の数が次のステージへ持ち越される。やり直すと入った時点の☆に戻る
//   (h) ハギの刈り取りの線の上限: ☆1 25% ・☆2 35% ・☆3 は今のまま。単発の戦闘のハギは今のまま
//   (i) 1 章の 1-1 の敵は ×0.6
//   (j) 単発の 1-1 ／ 1-2 ／ 1-3 と戦闘シミュの駒は今のまま（☆の表示も無し）
//
// 加えて（報告だけ・線は置かない）:
//   §5 1 章の通し × 100 seed × 方針 2 つ（第463期 ／ 第462期）・☆の分布・金の余り・重ねの札の割合
//   §5 ハギの表（第462期 §3 と同じ組み・ハギ☆1 ／ ☆2 ／ ☆3 × 作戦マップの勇者 ×1.0 ／ ×1.3 ／ ×1.6）
// =====================================================================================

public static class Chapter1StarSmoke
{
    public const int Seeds = 100;
    public const int RetryCap = 20;
    public const int ReapSeeds = 20;

    private readonly record struct Policy(string Name, bool Guard, bool Split, Map12ChangeSmoke.HeroPlan Plan);
    private static readonly Policy Keep1 = new("1 隊残す", true, false, Map12ChangeSmoke.HeroPlan.None);
    private static readonly Policy Split = new("分担＋戻り番", true, true, Map12ChangeSmoke.HeroPlan.None);
    private static readonly Policy Flee = new("逃げる", true, true, Map12ChangeSmoke.HeroPlan.Flee);

    private static void Play(Map11State st, Policy p)
        => Map12ChangeSmoke.Greedy(st.Seed, st.Stage, p.Guard, p.Split, false, state: st, siegeRest: true, heroPlan: p.Plan);

    /// <summary>器具だけの口: 金を置く（`Gold` は private set）。</summary>
    private static void SetGold(Map11State st, int g) => typeof(Map11State).GetProperty(nameof(Map11State.Gold))!.SetValue(st, g);

    /// <summary>そのキャラの駒（隊・手札・倒れた駒）。</summary>
    private static UnitState? Owned(Map11State st, string id)
        => st.Squads.Where(q => q.Units is not null).SelectMany(q => q.Units!).Concat(st.Bench).Concat(st.FallenUnits).FirstOrDefault(u => u.Def.Id == id);

    /// <summary>そのキャラが候補に出るまで引き直す（最大 <paramref name="cap"/> 回）。出た位置（無ければ -1）。</summary>
    private static int RerollUntil(Map11State st, string id, int cap = 80)
    {
        for (int k = 0; k <= cap; k++)
        {
            int i = st.Offers.FindIndex(o => o.Id == id);
            if (i >= 0) return i;
            if (!st.Reroll()) return -1;
        }
        return -1;
    }

    /// <summary>盤に出る敵の部隊の一覧（経路の区画・湧き・予備隊・勇者）。</summary>
    private static List<Formation> Foes(MapStage s)
    {
        var list = new List<Formation>();
        foreach (var lane in s.Lanes) foreach (Map11.RoadNode n in lane) list.Add(n.Enemy);
        for (int r = 0; r < s.LaneCount; r++) list.Add(s.Spawn(r).Enemy);
        if (s.Reserve.Enemy is { } re) list.Add(re);
        if (s.Hero.Unit is { } hu) list.Add(Formation.Build(center: hu));
        return list;
    }

    public static bool Run(Action<string> write, bool bots = true)
    {
        bool ok = true;
        void Req(bool c, string what) { write($"{(c ? "○" : "×")} {what}"); ok &= c; }
        StarRule sr = StarRule.V463;
        var ch = Chapter1.StagesFor(ChapterRule.V463);
        write($"  1 章の規則 {ChapterRule.V463.Name}・☆ {sr.Name}");
        write($"  倍率 {string.Join(" ／ ", ch.Select(s => $"{s.Id} {(s.FoeScale.Active ? s.FoeScale.Text : "×1.0")}"))}・呼び戻し {ch[0].Gold.Revive} 金（1-1）");

        // ---- (a) 1-1 の 6 枚は☆1 ----
        {
            bool all = true;
            int n = 0;
            string sample = "";
            for (int c = 0; c < 20; c++)
            {
                Chapter1.Begin(4630 + c, ChapterRule.V463);
                Map11State st = Map11Session.State!;
                all &= ReferenceEquals(st.Stage, ch[0]) && st.Stage.Stars.On && st.Bench.Count == 6 && st.Stars.Count == 6;
                foreach (UnitState u in st.Bench)
                {
                    UnitDef b = sr.BaseOf(u.Def);
                    n++;
                    all &= st.StarOf(u.Def.Id) == new StarState(1, 0) && !ReferenceEquals(b, u.Def)
                           && u.MaxHp == MapFoeScale.Mul(b.MaxHp, 60) && u.Hp == u.MaxHp && u.Def.Attack == MapFoeScale.Mul(b.Attack, 60) && u.Def.Speed == b.Speed
                           && ReferenceEquals(u.Def.Traits, b.Traits) && st.StarText(u.Def) == "☆1";
                    if (c == 0 && sample.Length < 200) sample += $"{b.Name} HP {b.MaxHp}→{u.MaxHp}・攻 {b.Attack}→{u.Def.Attack} ／ ";
                }
                Chapter1.End();
            }
            write($"  (a) 1 章の 1-1 × 20 配り・{n} 枚: {sample}");
            Req(all, "(a) 1 章の 1-1 の 6 枚はすべて☆1（最大HP・攻撃が今の ×0.6・四捨五入・速さと札は今のまま）");
        }

        // ---- (b)(c)(d)(f) 1 つの通し（1-1・金を置く）で同じキャラを重ねて☆3 まで ----
        {
            Chapter1.Begin(4631, ChapterRule.V463);
            Map11State st = Map11Session.State!;
            SetGold(st, 999);
            // (b) 持っている駒と同じキャラが候補に出る（20 回の引き直しのうち・最初の 1 枚が出るまで）
            int stackShown = 0, rolls = 0;
            string? x = null;
            for (; rolls < 40 && x is null; rolls++)
            {
                stackShown += st.Offers.Count(st.Stackable);
                x = st.Offers.FirstOrDefault(st.Stackable)?.Id;
                if (x is null) st.Reroll();
            }
            bool bShown = x is not null;
            string xName = x is null ? "—" : UnitCatalog.Everyone.First(d => d.Id == x).Name;
            UnitDef xb = x is null ? UnitCatalog.Hagi : UnitCatalog.Everyone.First(d => d.Id == x);
            string offerText = x is null ? "" : st.OfferStarText(st.Offers.First(o => o.Id == x));
            write($"  (b) 候補に持っている駒（{xName}）が出た: 引き直し {rolls - 1} 回目・候補の札「{offerText}」");

            // (c)(d) 5 枚重ねて☆3（途中の札の文と HP）
            var steps = new List<string>();
            bool cOk = x is not null, dOk = x is not null;
            int bench0 = st.Bench.Count;
            for (int k = 0; k < 5 && x is not null; k++)
            {
                UnitState before = Owned(st, x)!;
                if (k == 1) before.Hp = Math.Max(1, before.MaxHp / 2 - 1);   // (d) ☆2 に上がる 1 枚の前に HP を半分弱に
                int hp0 = before.Hp, max0 = before.MaxHp;
                int at = RerollUntil(st, x);
                string card = at >= 0 ? st.OfferStarText(st.Offers[at]) : "（出ない）";
                bool rec = at >= 0 && st.Recruit(at);
                UnitState after = Owned(st, x)!;
                StarState s = st.StarOf(x);
                steps.Add($"{card} → {st.StarText(after.Def)}（HP {hp0}/{max0} → {after.Hp}/{after.MaxHp}・攻 {after.Def.Attack}）");
                int wantStar = k switch { 0 => 1, 1 => 2, 2 or 3 => 2, _ => 3 };
                int wantPips = k switch { 0 => 1, 1 => 0, 2 => 1, 3 => 2, _ => 0 };
                cOk &= rec && s == new StarState(wantStar, wantPips) && after.MaxHp == MapFoeScale.Mul(xb.MaxHp, sr.Percent[wantStar])
                       && after.Def.Attack == MapFoeScale.Mul(xb.Attack, sr.Percent[wantStar]) && st.Bench.Count == bench0;
                if (wantStar > (k == 0 ? 1 : st.StarOf(x).Star - 1) && s.Pips == 0 && s.Star > 1)
                    dOk &= after.Hp == Math.Max(1, (int)Math.Round((double)hp0 * after.MaxHp / max0, MidpointRounding.AwayFromZero));
                else dOk &= after.Hp == hp0 || (k == 0 && after.Hp == after.MaxHp);
            }
            bool max3 = x is not null && ReferenceEquals(Owned(st, x)!.Def, xb) && st.StarText(xb) == "☆3";
            foreach (string line in steps) write($"      {line}");
            // (b) ☆3 のキャラは出ない（列を 2 周ぶん引き直す）
            int shown3 = 0;
            for (int k = 0; k < 40 && x is not null; k++) { shown3 += st.Offers.Count(o => o.Id == x); st.Reroll(); }
            write($"  (b) ☆3 の {xName} は、その後の引き直し 40 回で候補に {shown3} 回出た");
            Req(bShown && shown3 == 0 && stackShown > 0, "(b) 呼ぶ候補に持っている駒と同じキャラが出る。☆3 のキャラは出ない");
            Req(cOk && max3, "(c) ☆1 に 2 枚重ねると☆2（×0.8）、☆2 に 3 枚重ねると☆3（×1.0・今の定義そのもの）。手札に 2 体目は来ない。途中の数が札に出る");
            Req(dOk, "(d) ☆が上がると今の HP は割合を保つ（満タンなら満タン）");

            // (f) 売る: ☆3（6 枚）は ＋12、☆1（1 枚）は ＋2、☆1（1／2・2 枚）は ＋4
            int Sell(string id)
            {
                int i = st.Bench.FindIndex(u => u.Def.Id == id);
                int g0 = st.Gold;
                return i >= 0 && st.Sell(i) ? st.Gold - g0 : -1;
            }
            int s3 = x is null ? -1 : Sell(x);
            string? y = st.Bench.FirstOrDefault(u => st.StarOf(u.Def.Id) == new StarState(1, 0))?.Def.Id;
            int s1 = y is null ? -1 : Sell(y);
            string? z = st.Bench.FirstOrDefault(u => st.StarOf(u.Def.Id) == new StarState(1, 0))?.Def.Id;
            int s2 = -1;
            if (z is not null && RerollUntil(st, z) is int zi && zi >= 0 && st.Recruit(zi)) s2 = Sell(z);
            bool back = x is not null && RerollUntil(st, x) >= 0;   // 売った☆3 のキャラは列に戻る
            write($"  (f) 売る: ☆3（6 枚）＋{s3} ／ ☆1（1 枚）＋{s1} ／ ☆1（1／2・2 枚）＋{s2}・売った☆3 のキャラが候補に戻る {back}・売ったキャラの☆が消える {x is null || !st.Stars.ContainsKey(x)}");
            Req(s3 == 12 && s1 == 2 && s2 == 4 && back && x is not null && !st.Stars.ContainsKey(x), "(f) 売ると ＋2 × 重ねた枚数（☆3 まで重ねた駒は 12）");
            Chapter1.End();
        }

        // ---- (e) 倒れた駒と同じキャラを呼ぶ ----
        {
            Chapter1.Begin(4632, ChapterRule.V463);
            Map11State st = Map11Session.State!;
            SetGold(st, 999);
            UnitState v = st.Bench[0];
            string id = v.Def.Id;
            v.Hp = 0;
            st.Bench.Remove(v);
            st.FallenUnits.Add(v);   // 器具だけの口: 戦闘の境界で倒れた駒と同じ置き場
            int a1 = RerollUntil(st, id);
            bool r1 = a1 >= 0 && st.Recruit(a1);
            int a2 = RerollUntil(st, id);
            bool r2 = a2 >= 0 && st.Recruit(a2);
            UnitState now = st.FallenUnits.FirstOrDefault(u => u.Def.Id == id)!;
            UnitDef b = sr.BaseOf(v.Def);
            bool okE = r1 && r2 && now is not null && !now.IsAlive && now.Hp == 0 && st.StarOf(id) == new StarState(2, 0)
                       && now.MaxHp == MapFoeScale.Mul(b.MaxHp, 80) && !st.Bench.Any(u => u.Def.Id == id) && st.FallenUnits.Count(u => u.Def.Id == id) == 1;
            int revive = st.FallenUnits.IndexOf(now!);
            bool rv = revive >= 0 && st.Revive(revive);
            UnitState? back = st.Bench.FirstOrDefault(u => u.Def.Id == id);
            write($"  (e) 倒れた {b.Name} に 2 枚: ☆ {st.StarText(b)}・HP {now?.Hp}/{now?.MaxHp}（☆2 の最大HP {MapFoeScale.Mul(b.MaxHp, 80)}）・手札に 2 体目 {st.Bench.Count(u => u.Def.Id == id) - (back is null ? 0 : 1)} 枚・呼び戻すと HP {back?.Hp}/{back?.MaxHp}");
            Req(okE && rv && back is not null && back.MaxHp == MapFoeScale.Mul(b.MaxHp, 80), "(e) 倒れた駒と同じキャラを呼ぶと、倒れた駒に重なる（☆は上がるが倒れたまま・呼び戻すと☆2 の最大HP で戻る）");
            Chapter1.End();
        }

        // ---- (g) 持ち越しとやり直し ----
        {
            bool done = false, okG = false;
            string what = "1-1 に勝てる配りが無かった";
            for (int c = 0; c < 30 && !done; c++)
            {
                Chapter1.Begin(4640 + c, ChapterRule.V463);
                Map11State st = Map11Session.State!;
                SetGold(st, 999);
                // 手札の 1 枚目を☆2、2 枚目を☆1（1／2）まで重ねる
                string p = st.Bench[0].Def.Id, q = st.Bench[1].Def.Id;
                for (int k = 0; k < 2; k++) { int i = RerollUntil(st, p); if (i >= 0) st.Recruit(i); }
                { int i = RerollUntil(st, q); if (i >= 0) st.Recruit(i); }
                if (st.StarOf(p) != new StarState(2, 0) || st.StarOf(q) != new StarState(1, 1)) { Chapter1.End(); continue; }
                SetGold(st, 8);
                Play(st, Keep1);
                if (!st.Won) { Chapter1.End(); continue; }
                Chapter1.Settle(st, new Dictionary<(string, TraitId), int>(), new List<Map11Session.Line>());
                var end = Chapter1.Records[0].EndPack!;
                bool packed = end.Stars is { } ps && ps.Count == end.Units && ps.GetValueOrDefault(p) == st.StarOf(p) && ps.GetValueOrDefault(q) == st.StarOf(q);
                Chapter1.Next();
                Map11State s2 = Map11Session.State!;
                UnitState? up = Owned(s2, p);
                bool carried = s2.StarOf(p) == end.Stars![p] && s2.StarOf(q) == end.Stars[q] && s2.Stars.Count == end.Stars.Count
                               && (up is null || up.MaxHp == MapFoeScale.Mul(sr.BaseOf(up.Def).MaxHp, sr.Percent[s2.StarOf(p).Star]));
                // 1-2 で☆を上げてからやり直す → 入った時点の☆に戻る
                SetGold(s2, 999);
                string w = s2.Bench.Concat(s2.Squads.Where(x => x.Units is not null).SelectMany(x => x.Units!)).First(u => s2.StarOf(u.Def.Id).Star < 3).Def.Id;
                StarState w0 = s2.StarOf(w);
                { int i = RerollUntil(s2, w); if (i >= 0) s2.Recruit(i); }
                StarState w1 = s2.StarOf(w);
                Chapter1.Retry();
                Map11State s3 = Map11Session.State!;
                bool rolled = !ReferenceEquals(s2, s3) && s3.StarOf(w) == w0 && w1 != w0 && s3.Stars.Count == end.Stars.Count;
                string T(StarState x) => sr.Text(x);
                what = $"1 章の seed {4640 + c}: 1-1 の終わり {UnitName(p)} {T(st.StarOf(p))}・{UnitName(q)} {T(st.StarOf(q))}（器具が 1-1 の中でさらに重ねた分を含む）"
                       + $" → 1-2 の始め {T(s2.StarOf(p))} ／ {T(s2.StarOf(q))}（持ち越しの写しに☆ {end.Stars.Count} キャラ）・1-2 で {UnitName(w)} {T(w0)} → {T(w1)} → やり直すと {T(s3.StarOf(w))}";
                okG = packed && carried && rolled;
                done = true;
                Chapter1.End();
            }
            write($"  (g) {what}");
            Req(done && okG, "(g) ☆と途中の数が次のステージへ持ち越される。やり直すと入った時点の☆に戻る");
        }

        // ---- (h) ハギの刈り取りの線の上限 ----
        {
            UnitState Make(UnitDef d) => BattleEngine.Materialize(Formation.Build(front1: d), BattleContext.PlayerTeam)[0];
            int c1 = HagiDepthTrait.LineCapOf(Make(sr.Def(UnitCatalog.Hagi, 1)));
            int c2 = HagiDepthTrait.LineCapOf(Make(sr.Def(UnitCatalog.Hagi, 2)));
            int c3 = HagiDepthTrait.LineCapOf(Make(sr.Def(UnitCatalog.Hagi, 3)));
            int cs = HagiDepthTrait.LineCapOf(Make(UnitCatalog.Hagi));
            bool none = UnitCatalog.Everyone.All(d => d.ReapLineCapPermil is null) && EnemyCatalog.HeroMap.ReapLineCapPermil is null;
            bool same3 = ReferenceEquals(sr.Def(UnitCatalog.Hagi, 3), UnitCatalog.Hagi);
            write($"  (h) ハギの刈り取りの線の上限（‰）: ☆1 {c1} ・☆2 {c2} ・☆3 {c3}（今の定義そのもの {same3}）・単発 {cs}・戦闘シミュの駒はどれも上限を渡さない {none}・札の文「{sr.SignText(UnitCatalog.Hagi.Id, 1)}」");
            Req(c1 == 250 && c2 == 350 && c3 == 500 && cs == 500 && same3 && none, "(h) ハギの刈り取りの線の上限: ☆1 25% ・☆2 35% ・☆3 は今のまま（50%）。単発の戦闘のハギは今のまま");
        }

        // ---- (i) 1-1 の敵 ×0.6 ----
        {
            MapStage plain = MapStage.Stage11Ch, s = ch[0];
            var a = Foes(plain); var b = Foes(s);
            bool okI = a.Count == b.Count && s.FoeScale == new MapFoeScale(60, 60);
            int n = 0;
            for (int i = 0; i < Math.Min(a.Count, b.Count); i++)
                foreach (var (x, y) in a[i].Occupied().Zip(b[i].Occupied()))
                {
                    n++;
                    okI &= y.Def.MaxHp == MapFoeScale.Mul(x.Def.MaxHp, 60) && y.Def.Attack == MapFoeScale.Mul(x.Def.Attack, 60) && y.Def.Speed == x.Def.Speed
                           && ReferenceEquals(y.Def.Traits, x.Def.Traits);
                }
            string intro = Chapter1.Intro(s).Body.Split('\n')[^1];
            write($"  (i) 1 章の 1-1 の敵 {n} 体（{string.Join("・", a.SelectMany(f => f.Occupied()).Zip(b.SelectMany(f => f.Occupied())).Select(p => $"{p.First.Def.Name} {p.First.Def.MaxHp}→{p.Second.Def.MaxHp}").Distinct().Take(6))}）・頭の札「{intro}」・上の帯「{s.FoeScale.BarText}」・始め 手札 {s.Arrival.Start} ・枠 {s.Gold.StartSquads} ・金 {s.Gold.Start}");
            Req(okI && n > 0 && s.Arrival.Start == 6 && s.Gold.Start == 8 && s.Gold.StartSquads == 2, "(i) 1 章の 1-1 の敵は ×0.6（勇者の列も 0.6・始めの手札 6 ・枠 2 ・金 8 は今のまま）");
        }

        // ---- (j) 単発と戦闘シミュ ----
        {
            var singles = new[] { MapStage.Stage11, MapStage.Stage12, MapStage.Stage13, MapStage.Stage11Ch };
            bool off = singles.All(s => !s.Stars.On) && Chapter1.StagesFor(ChapterRule.V462).All(s => !s.Stars.On) && Chapter1.StagesFor(ChapterRule.V461).All(s => !s.Stars.On);
            bool plain = true;
            foreach (MapStage s in new[] { MapStage.Stage12, MapStage.Stage13 })
                for (int c = 0; c < 10; c++)
                {
                    var st = new Map11State(c, s.Time, s.Portal, draft: true, stage: s);
                    SetGold(st, 99);
                    for (int k = 0; k < 6; k++) { st.Recruit(0); st.Reroll(); }
                    plain &= st.Stars.Count == 0 && st.Offers.All(o => !st.OnBoardOrHand(o) && st.OfferStarText(o) == "")
                             && st.Bench.All(u => ReferenceEquals(u.Def, UnitCatalog.Everyone.First(d => d.Id == u.Def.Id)) && st.StarText(u.Def) == "")
                             && st.SellPrice(st.Bench[0]) == s.Gold.Sell;
                }
            write($"  (j) 単発 ・第460〜462期の 1 章に☆の規則なし {off}・単発の 1-2 ／ 1-3 × 10 配りで呼んだ駒は今の定義・候補に持っている駒が出ない・☆の文なし・売値 ＋{MapStage.Stage12.Gold.Sell} {plain}");
            Req(off && plain, "(j) 単発の 1-1 ／ 1-2 ／ 1-3 と第462期までの 1 章の駒は今のまま（☆の表示も無し）");
        }

        int crashed = 0;
        void Crash(string what, Exception ex) { crashed++; if (crashed <= 3) write($"  × {what}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
        try { CostTable(write); }
        catch (Exception ex) { Crash("§5 重ねの値段", ex); }
        try { ReapTable(write); }
        catch (Exception ex) { Crash("§5 ハギの表", ex); }
        if (bots)
        {
            try { BotTables(write, Crash); }
            catch (Exception ex) { Crash("§5 器具", ex); }
        }
        Req(crashed == 0, $"器具（ハギの表{(bots ? $"・1 章の通し × 2 方針 × {Seeds} seed" : "")}）が落ちない（数字は報告のみ・線は置かない）");
        return ok;
    }

    private static string UnitName(string id) => UnitCatalog.Everyone.First(d => d.Id == id).Name;
    private static UnitDef U(string name) => UnitCatalog.All.First(d => d.Name == name);

    /// <summary>§5: 第462期 §3 と同じ組み（ポンの第461期 1-3 の第2隊）・ハギ☆1 ／ ☆2 ／ ☆3（ほかの 4 枚は☆3）× 作戦マップの勇者 ×1.0 ／ ×1.3 ／ ×1.6 × 20 seed。</summary>
    private static void ReapTable(Action<string> write)
    {
        StarRule sr = StarRule.V463;
        write("");
        write($"  §5 ハギの☆ × 作戦マップの勇者（満タンから・単発の戦闘・seed 0〜{ReapSeeds - 1}・レリックなし・ハギのほかの 4 枚は☆3）");
        write("  | ハギ | 勇者 | 勝ち | 決着のターン（中央値） | 刈り取りが決まった戦闘 ／ 回数 | 刈り取りのときの勇者の HP（中央値・範囲） | 刈り取りで落ちた戦闘 | 味方の失った枚数（平均） |");
        write("  |---|---|--:|--:|--:|--:|--:|--:|");
        for (int star = 1; star <= 3; star++)
        {
            UnitDef hagi = sr.Def(UnitCatalog.Hagi, star);
            Formation form = Formation.Build(front1: U("砕け盾のヒビ"), front3: U("錯乱のササ"), center: U("継ぎ当てのツギ"), back1: U("礫のガレ"), back3: hagi);
            foreach (int pct in new[] { 100, 130, 160 })
            {
                var sc = new MapFoeScale(pct, pct);
                UnitDef hero = sc.HeroDef(EnemyCatalog.HeroMap);
                int wins = 0, reapBattles = 0, reaps = 0, reapKills = 0, lost = 0;
                var turns = new List<double>();
                var reapHp = new List<double>();
                for (int seed = 0; seed < ReapSeeds; seed++)
                {
                    var players = BattleEngine.Materialize(form, BattleContext.PlayerTeam);
                    var enemies = sc.MendHero(BattleEngine.Materialize(Formation.Build(center: hero), BattleContext.EnemyTeam, EnemyScaleRule.None));
                    UnitState h = enemies[0];
                    BattleResult r = BattleEngine.Run(players, enemies, seed, verbose: true);
                    if (r.PlayerWon) wins++;
                    turns.Add(r.Turns);
                    lost += players.Count(u => !u.IsAlive);
                    var rs = r.Events.Where(e => e.Kind == BattleEventKind.Reap && e.Text == ReapLabels.Reap && e.TargetId == h.InstanceId).ToList();
                    if (rs.Count > 0) reapBattles++;
                    reaps += rs.Count;
                    foreach (var e in rs) reapHp.Add(100.0 * e.Amount / h.MaxHp);
                    if (rs.Count > 0 && !h.IsAlive) reapKills++;
                }
                static double Med(List<double> xs) { if (xs.Count == 0) return double.NaN; var o = xs.OrderBy(x => x).ToList(); return o.Count % 2 == 1 ? o[o.Count / 2] : (o[o.Count / 2 - 1] + o[o.Count / 2]) / 2; }
                string hp = reapHp.Count == 0 ? "—" : $"{Med(reapHp):F0}%（{reapHp.Min():F0}〜{reapHp.Max():F0}%）";
                write($"  | ☆{star}（HP {hagi.MaxHp}・攻 {hagi.Attack}・線の上限 {HagiDepthTrait.LineCapOf(BattleEngine.Materialize(Formation.Build(front1: hagi), BattleContext.PlayerTeam)[0]) / 10.0:0.#}%） | ×{pct / 100.0:0.0#}（HP {hero.MaxHp}・回復 {sc.HeroMend(BossMendTrait.Fixed300)}） | {wins} ／ {ReapSeeds} | {Med(turns):0.#} | {reapBattles} ／ {reaps} | {hp} | {reapKills} | {(double)lost / ReapSeeds:F2} |");
            }
        }
    }

    /// <summary>
    /// §5 重ねの値段: 1 章の 1-1 の始め（☆1 の 6 枚）から、手札の 1 枚を狙って☆2 ／ ☆3 まで重ねるのに要る金（呼ぶ 4 × 枚数 ＋ リロール 2 × 回数）。
    /// 狙うキャラが候補に出るまでリロールし、出たら呼ぶ（ほかの札は呼ばない）。金は置く（器具だけの口）。1 章の seed 0〜39。
    /// </summary>
    private static void CostTable(Action<string> write)
    {
        var to2 = new List<int>(); var to3 = new List<int>(); var rolls = new List<int>();
        for (int c = 0; c < 40; c++)
        {
            Chapter1.Begin(c, ChapterRule.V463);
            Map11State st = Map11Session.State!;
            SetGold(st, 9999);
            string id = st.Bench[0].Def.Id;
            int g0 = st.Gold, n = 0;
            while (st.StarOf(id).Star < 3 && n < 400)
            {
                int i = st.Offers.FindIndex(o => o.Id == id);
                if (i >= 0) { st.Recruit(i); if (st.StarOf(id) == new StarState(2, 0)) to2.Add(g0 - st.Gold); continue; }
                st.Reroll(); n++;
            }
            if (st.StarOf(id).Star == 3) { to3.Add(g0 - st.Gold); rolls.Add(n); }
            Chapter1.End();
        }
        static string M(List<int> xs) { if (xs.Count == 0) return "—"; var o = xs.OrderBy(x => x).ToList(); return $"{o[o.Count / 2]}（{o[0]}〜{o[^1]}）"; }
        write("");
        write("  §5 重ねの値段（1 章の 1-1 の始めから手札の 1 枚を狙って重ねる・狙うキャラが候補に出るまでリロール・1 章の seed 0〜39）");
        write($"  ☆2 まで（2 枚）の金 中央値 {M(to2)} ／ ☆3 まで（5 枚）の金 中央値 {M(to3)}・そのうちリロール {M(rolls)} 回（☆3 に届いた {to3.Count} ／ 40）");
        write($"  （候補の列は現役 {UnitCatalog.All.Count} 枚を 1 周してから同じキャラに戻る——呼んだキャラは列の最後へ・リロールで捨てた 3 枚も列の最後へ）");
    }

    private enum Cause { Fallen, TimeUp, Wiped, Stalled, Other }
    private static string CauseName(Cause c) => c switch
    {
        Cause.Fallen => "陥落", Cause.TimeUp => "時間切れ", Cause.Wiped => "全滅", Cause.Stalled => "勇者の打ち切り", _ => "その他",
    };

    /// <summary>§5: 1 章の通し（第463期 ／ 第462期）× 方針 2 つ × 100 seed。</summary>
    private static void BotTables(Action<string> write, Action<string, Exception> crash)
    {
        string curKey = "";
        int curStage = 0;
        var battles = new List<(string Key, int Stage, bool Won, bool Hero, double Hp)>();
        var heroLoss = new HashSet<Map11State>(ReferenceEqualityComparer.Instance);
        Map12ChangeSmoke.FightHook = (s, squad, node, intercept) =>
        {
            var prep = intercept ? s.PrepareIntercept(squad, node) : s.Prepare(squad, node);
            if (prep is not { } q) return;
            BattleResult r = BattleEngine.Run(q.Players, q.Enemies, q.Seed, verbose: false);
            int max = q.Players.Sum(u => u.MaxHp);
            double hp = max == 0 ? 0 : 100.0 * q.Players.Where(u => u.IsAlive).Sum(u => u.Hp) / max;
            battles.Add((curKey, curStage, r.PlayerWon, node.IsHero, hp));
            if (!r.PlayerWon && node.IsHero) heroLoss.Add(s);
            s.Resolve(squad, q.Node, r.PlayerWon, intercept);
        };
        var causes = new Dictionary<string, int[,]>();
        var heroCost = new Dictionary<string, int[]>();
        var stuckCause = new Dictionary<string, List<string>>();
        // ステージの終わり（勝った配り）の☆の分布・金の余り・重ねの札の割合
        var ends = new Dictionary<string, List<(int Stage, int S1, int S2, int S3, int Gold, int Units)>>();
        var offers = new Dictionary<string, (long Shown, long Stack, long Stacks, long Ups)[]>();
        try
        {
            write("");
            write($"  §5 1 章の通し（1 章の seed 0〜{Seeds - 1}・負ければ同じステージをやり直す・1 ステージ {RetryCap} 回まで・器具の買い方は今のまま——穴があれば候補の先頭を呼ぶ・重ねの札でも）");
            write($"  | 1 章 | 方針 | 通せた | 1-1 のやり直し（平均） | 1-2 のやり直し（平均） | 1-3 のやり直し（平均） | 止まったステージ（やり直し {RetryCap} 回を超えた） |");
            write("  |---|---|--:|--:|--:|--:|---|");
            foreach (ChapterRule rule in new[] { ChapterRule.V463, ChapterRule.V462 })
                foreach (var (cname, pol) in new[] { ("1 隊残す（3 ステージとも）", new[] { Keep1, Keep1, Keep1 }), ("分担＋戻り番（1-3 は逃げる）", new[] { Split, Split, Flee }) })
                {
                    var stages = Chapter1.StagesFor(rule);
                    string key = $"{(rule.Stars is null ? "第462期（☆なし）" : "**第463期（☆）**")}|{cname}";
                    curKey = key;
                    causes[key] = new int[3, 5];
                    heroCost[key] = new int[3];
                    stuckCause[key] = new();
                    ends[key] = new();
                    offers[key] = new (long, long, long, long)[3];
                    int through = 0;
                    var retries = new int[3];
                    var stuck = new int[3];
                    for (int c = 0; c < Seeds; c++)
                    {
                        try
                        {
                            Map11State.CarryPack? pack = null;
                            bool all = true;
                            for (int i = 0; i < 3 && all; i++)
                            {
                                curStage = i;
                                bool won = false;
                                Cause last = Cause.Other;
                                for (int a = 0; a <= RetryCap && !won; a++)
                                {
                                    MapStage stage = stages[i];
                                    var st = new Map11State(Chapter1.StageSeed(c, i, a), stage.Time, stage.Portal, draft: true, stage: stage);
                                    if (pack is not null) st.ApplyCarry(pack);
                                    Play(st, pol[i]);
                                    var o = offers[key][i];
                                    offers[key][i] = (o.Shown + st.OffersShown, o.Stack + st.StackOffersShown, o.Stacks + st.Stacks, o.Ups + st.StarUps);
                                    won = st.Won;
                                    if (won)
                                    {
                                        pack = st.TakeCarry();
                                        var units = pack.Bench.Concat(pack.Squads.SelectMany(q => q)).ToList();
                                        int Count(int s) => units.Count(u => (pack.Stars is { } ps && ps.TryGetValue(u.Def.Id, out StarState x) ? x.Star : 3) == s);
                                        ends[key].Add((i, Count(1), Count(2), Count(3), pack.Gold, units.Count));
                                        continue;
                                    }
                                    last = st.Fallen ? Cause.Fallen : st.TimeUp ? Cause.TimeUp : st.HeroStalled ? Cause.Stalled
                                         : st.Squads.All(q => q.Lost || q.Units is null || !q.Units.Any(u => u.IsAlive)) ? Cause.Wiped : Cause.Other;
                                    causes[key][i, (int)last]++;
                                    if (heroLoss.Contains(st)) heroCost[key][i]++;
                                    if (a < RetryCap) retries[i]++;
                                }
                                if (!won) { stuck[i]++; all = false; stuckCause[key].Add($"{stages[i].Id} {CauseName(last)}"); }
                            }
                            if (all) through++;
                        }
                        catch (Exception ex) { crash($"{key} seed {c}", ex); }
                    }
                    var parts = key.Split('|');
                    write($"  | {parts[0]} | {parts[1]} | {through}% | {(double)retries[0] / Seeds:F2} | {(double)retries[1] / Seeds:F2} | {(double)retries[2] / Seeds:F2} | 1-1 {stuck[0]}・1-2 {stuck[1]}・1-3 {stuck[2]} |");
                }
        }
        finally { Map12ChangeSmoke.FightHook = null; }

        string Id(int i) => Chapter1.StagesFor(ChapterRule.V462)[i].Id;

        write("");
        write("  負けた配りの切り分け（やり直したぶんも止まったぶんも数える・ステージごと）。「勇者に隊を落とされた」はその配りで勇者に負けた戦闘があった配り（陥落 ／ 時間切れ ／ 全滅と重なる）");
        write("  | 1 章 | 方針 | ステージ | 負けた配り | 陥落 | 時間切れ | 全滅 | 勇者の打ち切り | その他 | うち勇者に隊を落とされた | 止まった通しの終わり |");
        write("  |---|---|---|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach (var (key, arr) in causes)
        {
            var parts = key.Split('|');
            for (int i = 0; i < 3; i++)
            {
                int total = Enumerable.Range(0, 5).Sum(k => arr[i, k]);
                string id = Id(i);
                var st = stuckCause[key].Where(x => x.StartsWith(id + " ", StringComparison.Ordinal)).GroupBy(x => x[(id.Length + 1)..]).Select(g => $"{g.Key} {g.Count()}");
                write($"  | {parts[0]} | {parts[1]} | {id} | {total} | {arr[i, 0]} | {arr[i, 1]} | {arr[i, 2]} | {arr[i, 3]} | {arr[i, 4]} | {heroCost[key][i]} | {(st.Any() ? string.Join("・", st) : "—")} |");
            }
        }

        write("");
        write("  ステージの終わり（勝った配り・持ち越しの写し）: 駒の枚数と☆ごとの枚数の平均・金の余りの平均 ／ 呼ぶ候補（すべての配り）のうち重ねの札だった割合・重ねた枚数と☆が上がった回数（1 配りの平均）");
        write("  | 1 章 | 方針 | ステージ | 勝った配り | 駒 | ☆1 | ☆2 | ☆3 | 金の余り | 候補に出た札 | うち重ねの札 | 重ねた（1 配り） | ☆が上がった（1 配り） |");
        write("  |---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (string key in causes.Keys)
        {
            var parts = key.Split('|');
            for (int i = 0; i < 3; i++)
            {
                var xs = ends[key].Where(e => e.Stage == i).ToList();
                string Am(Func<(int Stage, int S1, int S2, int S3, int Gold, int Units), int> f) => xs.Count == 0 ? "—" : $"{xs.Average(f):F1}";
                var o = offers[key][i];
                int plays = Enumerable.Range(0, 5).Sum(k => causes[key][i, k]) + xs.Count;
                write($"  | {parts[0]} | {parts[1]} | {Id(i)} | {xs.Count} | {Am(x => x.Units)} | {Am(x => x.S1)} | {Am(x => x.S2)} | {Am(x => x.S3)} | {Am(x => x.Gold)} | {o.Shown} | {(o.Shown == 0 ? 0 : 100.0 * o.Stack / o.Shown):F0}% | {(plays == 0 ? 0 : (double)o.Stacks / plays):F2} | {(plays == 0 ? 0 : (double)o.Ups / plays):F2} |");
            }
        }

        write("");
        write("  1-2 ／ 1-3 の戦闘（すべての配り・やり直しを含む）: 終わりの HP は隊の駒の残り HP の合計 ÷ 最大HP の合計（倒れた駒は 0）");
        write("  | 1 章 | 方針 | ステージ | 戦闘 | 終わりの HP（中央値） | 勝った戦闘の HP（中央値） | 隊を失った戦闘 | うち勇者 | 勇者との戦闘 |");
        write("  |---|---|---|--:|--:|--:|--:|--:|--:|");
        static double Med(IEnumerable<double> xs) { var o = xs.OrderBy(x => x).ToList(); return o.Count == 0 ? double.NaN : o.Count % 2 == 1 ? o[o.Count / 2] : (o[o.Count / 2 - 1] + o[o.Count / 2]) / 2; }
        foreach (string key in causes.Keys)
            for (int i = 0; i < 3; i++)
            {
                var bs = battles.Where(b => b.Key == key && b.Stage == i).ToList();
                var parts = key.Split('|');
                write($"  | {parts[0]} | {parts[1]} | {Id(i)} | {bs.Count} | {Med(bs.Select(b => b.Hp)):F0}% | {Med(bs.Where(b => b.Won).Select(b => b.Hp)):F0}% | {bs.Count(b => !b.Won)} | {bs.Count(b => !b.Won && b.Hero)} | {bs.Count(b => b.Hero)} |");
            }
    }
}
