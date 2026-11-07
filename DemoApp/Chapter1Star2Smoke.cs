using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// ☆を金で直に重ねる門（第464期 §3・`--chapter1-star2-smoke`・指示書 design/PHASE464_STAR_DIRECT_SPEC.md）
//
// **画面を1つも作らない。** `Chapter1`（`ChapterRule.V464`）／ `StarRule.V464` ／ `DealRule.V464` ／ `Map11State` と器具の方針（`Map12ChangeSmoke.Greedy`）だけを回す。
//
//   (a) 呼ぶ候補に持っているキャラが出ない。呼んだキャラは列から外れ、売ったキャラは列に戻る
//   (b) 強化: 4 金で 1 枚。☆1 に 2 回で☆2、☆2 に 3 回で☆3（計 20 金）。☆3 では押せない
//   (c) 強化は拠点・取っている中間拠点でだけ。道の上・金が足りないときは押せない
//   (d) 倒れた駒も強化できる（倒れたまま☆が上がる）
//   (e) 1 章の敵の倍率: 1-1 ×0.6・1-2 ×0.9・1-3 ×1.3（勇者も）
//   (f) 1-1 の始めの 6 枚に、同じ軸が 3 枚以上（20 配りすべて）。今の 3 つの保証も満たす
//   (g) 単発のステージ・戦闘シミュ・第460〜463期の 1 章は今のまま
//
// 加えて（報告だけ・線は置かない）:
//   §5 1 章の通し × 100 seed × 方針 2 つ（第464期 ／ 第463期）・☆の分布・金の余り・強化の回数・1-1 の始めの軸の内訳
//   §5 ハギの表（第463期 §4 と同じ組み・ハギ☆1 ／ ☆2 ／ ☆3 × 作戦マップの勇者 ×0.9 ／ ×1.3）
// =====================================================================================

public static class Chapter1Star2Smoke
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

    private static string UnitName(string id) => UnitCatalog.Everyone.First(d => d.Id == id).Name;
    private static UnitDef U(string name) => UnitCatalog.All.First(d => d.Name == name);

    public static bool Run(Action<string> write, bool bots = true)
    {
        bool ok = true;
        void Req(bool c, string what) { write($"{(c ? "○" : "×")} {what}"); ok &= c; }
        StarRule sr = StarRule.V464;
        ChapterRule rule = ChapterRule.V464;
        var ch = Chapter1.StagesFor(rule);
        write($"  1 章の規則 {rule.Name}・☆ {sr.Name}・配り {DealRule.V464.Name}・遊ぶ 1 章 {Chapter1.Default.Name}");
        write($"  倍率 {string.Join(" ／ ", ch.Select(s => $"{s.Id} {(s.FoeScale.Active ? s.FoeScale.Text : "×1.0")}"))}・呼ぶ {ch[0].Gold.Recruit} 金・強化 {sr.EnhancePrice} 金・呼び戻し {ch[0].Gold.Revive} 金（1-1）");

        // ---- (a) 呼ぶ候補 ----
        {
            Chapter1.Begin(4641, rule);
            Map11State st = Map11Session.State!;
            SetGold(st, 9999);
            int shownOwned = 0, n = 0;
            void Look() { foreach (UnitDef o in st.Offers) { n++; if (st.OnBoardOrHand(o) || st.Stars.ContainsKey(o.Id)) shownOwned++; } }
            for (int k = 0; k < 60; k++) { Look(); st.Reroll(); }
            // 呼んだキャラは列から外れる（その後 80 回の引き直しで候補に出ない）
            string x = st.Offers[0].Id;
            bool rec = st.Recruit(0);
            int again = 0;
            for (int k = 0; k < 80; k++) { Look(); again += st.Offers.Count(o => o.Id == x); st.Reroll(); }
            // 売ったキャラは列に戻る（80 回の引き直しのうちに出る）
            int bi = st.Bench.FindIndex(u => u.Def.Id == x);
            bool sold = bi >= 0 && st.Sell(bi);
            int back = -1;
            for (int k = 0; k < 80 && back < 0; k++) { if (st.Offers.Any(o => o.Id == x)) back = k; else st.Reroll(); }
            write($"  (a) 候補 {n} 枚のうち持っているキャラ {shownOwned} 枚（重ねの札 {st.StackOffersShown}）・呼んだ {UnitName(x)} はその後の引き直し 80 回で {again} 回・売ると {back} 回目の引き直しで候補に戻った");
            Req(rec && shownOwned == 0 && st.StackOffersShown == 0 && again == 0 && sold && back >= 0,
                "(a) 呼ぶ候補に持っているキャラが出ない。呼んだキャラは列から外れ、売ったキャラは列に戻る");
            Chapter1.End();
        }

        // ---- (b) 強化 4 金 × 5 回で☆3 ----
        {
            Chapter1.Begin(4642, rule);
            Map11State st = Map11Session.State!;
            SetGold(st, 100);
            UnitState u = st.Bench[0];
            string id = u.Def.Id;
            UnitDef b = sr.BaseOf(u.Def);
            u.Hp = Math.Max(1, u.MaxHp / 2 - 1);
            var steps = new List<string>();
            bool okB = true;
            int g0 = st.Gold, bench0 = st.Bench.Count;
            (int Star, int Pips)[] want = { (1, 1), (2, 0), (2, 1), (2, 2), (3, 0) };
            for (int k = 0; k < 5; k++)
            {
                UnitState before = Owned(st, id)!;
                string label = st.EnhanceText(before.Def);
                int hp0 = before.Hp, max0 = before.MaxHp, g = st.Gold;
                UnitState? after = st.Enhance(before);
                StarState s = st.StarOf(id);
                steps.Add($"「{label}」 → {st.StarText(after?.Def ?? b)}（HP {hp0}/{max0} → {after?.Hp}/{after?.MaxHp}・攻 {after?.Def.Attack}・金 {g} → {st.Gold}）");
                okB &= after is not null && g - st.Gold == 4 && s == new StarState(want[k].Star, want[k].Pips)
                       && after.MaxHp == MapFoeScale.Mul(b.MaxHp, sr.Percent[s.Star]) && after.Def.Attack == MapFoeScale.Mul(b.Attack, sr.Percent[s.Star])
                       && st.Bench.Count == bench0
                       && (after.MaxHp == max0 ? after.Hp == hp0 : after.Hp == Math.Max(1, (int)Math.Round((double)hp0 * after.MaxHp / max0, MidpointRounding.AwayFromZero)));
            }
            UnitState top = Owned(st, id)!;
            string? block3 = st.EnhanceBlock(top);
            bool none3 = st.Enhance(top) is null;
            foreach (string line in steps) write($"      {line}");
            write($"  (b) {b.Name}: 計 {g0 - st.Gold} 金で☆3（今の定義そのもの {ReferenceEquals(top.Def, b)}）・☆3 で強化「{block3}」・売値 ＋{st.SellPrice(top)}・強化 {st.Enhances} 回");
            Req(okB && g0 - st.Gold == 20 && ReferenceEquals(top.Def, b) && block3 is not null && none3 && st.Enhances == 5 && st.SellPrice(top) == 12,
                "(b) 強化: 4 金で 1 枚。☆1 に 2 回で☆2、☆2 に 3 回で☆3（計 20 金・今の定義そのもの）。☆3 では押せない。☆が上がると HP は割合を保つ");
            Chapter1.End();
        }

        // ---- (c) 強化できる場所・金 ----
        {
            Chapter1.Begin(4643, rule);
            Map11State st = Map11Session.State!;
            SetGold(st, 100);
            bool placed = st.SwapWithBench(0, 0, 0);
            UnitState road = st.UnitAt(0, 0)!;
            bool atHome = st.CanEnhance(road);
            st.Send(0, 0);
            string? onRoad = st.EnhanceBlock(road);
            UnitState bench = st.Bench[0];
            bool benchOk = st.CanReform(1) && st.CanEnhance(bench);
            SetGold(st, 3);
            string? poor = st.EnhanceBlock(bench);
            bool poorNo = st.Enhance(bench) is null && st.Gold == 3;
            write($"  (c) 拠点の隊の駒 {atHome}・道の上の第1隊の駒「{onRoad}」・拠点に第2隊がいる間の手札の駒 {benchOk}・金 3 で「{poor}」");
            write("      （取っている中間拠点は `CanReform` の第459期の口をそのまま読む——隊の駒の場所の判定は呼ぶ・組み直しと同じ 1 つ）");
            Req(placed && atHome && onRoad is not null && onRoad.Contains("道の上") && benchOk && poor == "金が足りない" && poorNo,
                "(c) 強化は拠点・取っている中間拠点でだけ。道の上・金が足りないときは押せない（理由を 1 行）");
            Chapter1.End();
        }

        // ---- (d) 倒れた駒も強化できる ----
        {
            Chapter1.Begin(4644, rule);
            Map11State st = Map11Session.State!;
            SetGold(st, 100);
            UnitState v = st.Bench[0];
            string id = v.Def.Id;
            v.Hp = 0;
            st.Bench.Remove(v);
            st.FallenUnits.Add(v);   // 器具だけの口: 戦闘の境界で倒れた駒と同じ置き場
            UnitState? a1 = st.Enhance(v);
            UnitState? a2 = a1 is null ? null : st.Enhance(a1);
            UnitDef b = sr.BaseOf(v.Def);
            UnitState? now = st.FallenUnits.FirstOrDefault(u => u.Def.Id == id);
            bool okD = a2 is not null && ReferenceEquals(now, a2) && !now!.IsAlive && now.Hp == 0 && st.StarOf(id) == new StarState(2, 0)
                       && now.MaxHp == MapFoeScale.Mul(b.MaxHp, 80) && !st.Bench.Any(u => u.Def.Id == id) && st.FallenUnits.Count(u => u.Def.Id == id) == 1;
            int revive = now is null ? -1 : st.FallenUnits.IndexOf(now);
            bool rv = revive >= 0 && st.Revive(revive);
            UnitState? back = st.Bench.FirstOrDefault(u => u.Def.Id == id);
            write($"  (d) 倒れた {b.Name} を 2 回強化: {st.StarText(b)}・HP {now?.Hp}/{now?.MaxHp}（☆2 の最大HP {MapFoeScale.Mul(b.MaxHp, 80)}）・呼び戻すと HP {back?.Hp}/{back?.MaxHp}");
            Req(okD && rv && back is not null && back.MaxHp == MapFoeScale.Mul(b.MaxHp, 80), "(d) 倒れた駒も強化できる（倒れたまま☆が上がる・呼び戻すと☆2 の最大HP で戻る）");
            Chapter1.End();
        }

        // ---- (e) 敵の倍率 ----
        {
            MapStage[] plain = { MapStage.Stage11Ch, MapStage.Stage12, MapStage.Stage13 };
            int[] pct = { 60, 90, 130 };
            bool okE = true;
            var parts = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                MapStage s = ch[i];
                var a = Foes(plain[i]); var b = Foes(s);
                okE &= a.Count == b.Count && s.FoeScale == new MapFoeScale(pct[i], pct[i]);
                int n = 0;
                for (int k = 0; k < Math.Min(a.Count, b.Count); k++)
                    foreach (var (x, y) in a[k].Occupied().Zip(b[k].Occupied()))
                    {
                        n++;
                        okE &= y.Def.MaxHp == MapFoeScale.Mul(x.Def.MaxHp, pct[i]) && y.Def.Attack == MapFoeScale.Mul(x.Def.Attack, pct[i]) && y.Def.Speed == x.Def.Speed
                               && ReferenceEquals(y.Def.Traits, x.Def.Traits);
                    }
                string hero = s.Hero.Unit is { } h ? $"・勇者 HP {h.MaxHp} 攻 {h.Attack} 回復 {s.FoeScale.HeroMend(BossMendTrait.Fixed300)}" : "";
                parts.Add($"{s.Id} {s.FoeScale.BarText} 敵 {n} 体{hero}");
                okE &= n > 0;
            }
            write($"  (e) {string.Join(" ／ ", parts)}");
            Req(okE, "(e) 1 章の敵の倍率: 1-1 ×0.6・1-2 ×0.9・1-3 ×1.3（勇者も・最大HP と攻撃だけ）");
        }

        // ---- (f) 1-1 の配り ----
        {
            bool okF = true;
            var axes = new Dictionary<string, int>();
            int outside = 0, tries = 0, maxTries = 0;
            var samples = new List<string>();
            var pool15 = MapDraw.Pool.Select(d => d.Id).ToHashSet();
            for (int c = 0; c < 20; c++)
            {
                Chapter1.Begin(4650 + c, rule);
                Map11State st = Map11Session.State!;
                var hand = st.Bench.Select(u => sr.BaseOf(u.Def)).ToList();
                var miss = MapDraw.Missing(hand, DealRule.V464);
                var full = MapDraw.FullAxes(hand, 3);
                okF &= ReferenceEquals(st.Stage, ch[0]) && st.Stage.Deal == DealRule.V464 && hand.Count == 6 && miss.Count == 0 && st.OpeningOk
                       && st.OpeningAxis is { } ax && full.Contains(ax) && hand.All(d => UnitCatalog.All.Contains(d)) && st.Bench.All(u => st.StarOf(u.Def.Id) == new StarState(1, 0));
                if (st.OpeningAxis is { } a) axes[a] = axes.GetValueOrDefault(a) + 1;
                outside += hand.Count(d => !pool15.Contains(d.Id));
                tries += st.OpeningRerolls;
                maxTries = Math.Max(maxTries, st.OpeningRerolls);
                if (c < 3) samples.Add($"{st.OpeningAxis}: {string.Join("・", hand.Select(d => d.Name))}");
                Chapter1.End();
            }
            write($"  (f) 20 配りの軸: {string.Join("・", UnitTags.Axes.Where(axes.ContainsKey).Select(a => $"{a} {axes[a]}"))}・引き直し 平均 {tries / 20.0:F1} 回（最大 {maxTries}）・既定の 3 隊の 15 枚の外から来た駒 {outside} ／ 120 枚");
            foreach (string s in samples) write($"      {s}");
            Req(okF, "(f) 1-1 の始めの 6 枚に、同じ軸が 3 枚以上（20 配りすべて・52 枚から）。回復役・前に立てる駒・芯の 3 つの保証も満たす");
        }

        // ---- (g) 単発・第460〜463期の 1 章 ----
        {
            var singles = new[] { MapStage.Stage11, MapStage.Stage12, MapStage.Stage13, MapStage.Stage11Ch };
            bool off = singles.All(s => !s.Stars.On && !s.Deal.On)
                       && new[] { ChapterRule.V460, ChapterRule.V461, ChapterRule.V462, ChapterRule.V463 }.All(r => Chapter1.StagesFor(r).All(s => !s.Deal.On && s.Stars.EnhancePrice == 0))
                       && Chapter1.StagesFor(ChapterRule.V463).All(s => s.Stars.StackOffers) && Chapter1.StagesFor(ChapterRule.V463)[1].FoeScale == new MapFoeScale(130, 130);
            // 第463期の 1 章: 呼ぶ候補に重ねの札が出る（今のまま）・強化は押せない
            Chapter1.Begin(4631, ChapterRule.V463);
            Map11State s3 = Map11Session.State!;
            SetGold(s3, 999);
            int stack = 0;
            for (int k = 0; k < 40; k++) { stack += s3.Offers.Count(s3.Stackable); s3.Reroll(); }
            bool noEnh = !s3.CanEnhance(s3.Bench[0]);
            Chapter1.End();
            bool sim = UnitCatalog.Everyone.All(d => d.ReapLineCapPermil is null);
            write($"  (g) 単発 ・第460〜463期の 1 章に配りの規則も強化も無い {off}・第463期の 1 章は候補に重ねの札 {stack} 枚（引き直し 40 回）・強化は押せない {noEnh}・戦闘シミュの駒は今のまま {sim}");
            write("      （単発の頭なしの門・画面の門・第460〜463期の 1 章の門・`compare` ／ `docs/` が 0 行かは回帰で見る）");
            Req(off && stack > 0 && noEnh && sim, "(g) 単発のステージ・戦闘シミュ・第460〜463期の 1 章は今のまま");
        }

        int crashed = 0;
        void Crash(string what, Exception ex) { crashed++; if (crashed <= 3) write($"  × {what}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
        try { DealTable(write); }
        catch (Exception ex) { Crash("§5 1-1 の配りの軸", ex); }
        try { ReapTable(write); }
        catch (Exception ex) { Crash("§5 ハギの表", ex); }
        if (bots)
        {
            try { BotTables(write, Crash); }
            catch (Exception ex) { Crash("§5 器具", ex); }
        }
        Req(crashed == 0, $"器具（1-1 の配りの軸・ハギの表{(bots ? $"・1 章の通し × 2 方針 × {Seeds} seed" : "")}）が落ちない（数字は報告のみ・線は置かない）");
        return ok;
    }

    /// <summary>§5: 1-1 の始め（1 章の seed 0〜99・やり直し 0 回目）の軸の内訳・そろった軸の数・役割。</summary>
    private static void DealTable(Action<string> write)
    {
        var picked = new Dictionary<string, int>();
        var full = new Dictionary<string, int>();
        int multi = 0, tries = 0, maxTries = 0, notOk = 0;
        var roles = new Dictionary<string, int>();
        for (int c = 0; c < Seeds; c++)
        {
            var st = new Map11State(Chapter1.StageSeed(c, 0, 0), Chapter1.StagesFor(ChapterRule.V464)[0].Time, Chapter1.StagesFor(ChapterRule.V464)[0].Portal,
                                    draft: true, stage: Chapter1.StagesFor(ChapterRule.V464)[0]);
            var hand = st.Bench.Select(u => StarRule.V464.BaseOf(u.Def)).ToList();
            if (st.OpeningAxis is { } a) picked[a] = picked.GetValueOrDefault(a) + 1;
            var f = MapDraw.FullAxes(hand, 3);
            foreach (string x in f) full[x] = full.GetValueOrDefault(x) + 1;
            if (f.Count > 1) multi++;
            tries += st.OpeningRerolls;
            maxTries = Math.Max(maxTries, st.OpeningRerolls);
            if (!st.OpeningOk) notOk++;
            foreach (UnitDef d in hand) if (UnitTags.EntryOf(d.Id) is { } e) roles[e.Role] = roles.GetValueOrDefault(e.Role) + 1;
        }
        write("");
        write($"  §5 1-1 の始めの軸（1 章の seed 0〜{Seeds - 1}・やり直し 0 回目の配り）");
        write("  | 軸 | 選ばれた軸（保証の 3 枚） | 3 枚以上そろった配り（選ばれた軸以外も） | 軸の駒の数（52 枚のうち） |");
        write("  |---|--:|--:|--:|");
        foreach (string a in UnitTags.Axes)
            write($"  | {a} | {picked.GetValueOrDefault(a)} | {full.GetValueOrDefault(a)} | {UnitCatalog.All.Count(d => MapDraw.AxesOf(d).Contains(a))} |");
        write($"  2 つ以上の軸が 3 枚そろった配り {multi} ／ {Seeds}・引き直し 平均 {(double)tries / Seeds:F1} 回（最大 {maxTries}）・保証を満たせなかった配り {notOk}");
        write($"  6 枚 × {Seeds} 配りの役割: {string.Join("・", UnitTags.Roles.Select(r => $"{r} {roles.GetValueOrDefault(r)}"))}");
    }

    /// <summary>§5: 第463期 §4 と同じ組み（ポンの第461期 1-3 の第2隊）・ハギ☆1 ／ ☆2 ／ ☆3（ほかの 4 枚は☆3）× 作戦マップの勇者 ×0.9 ／ ×1.3 × 20 seed。</summary>
    private static void ReapTable(Action<string> write)
    {
        StarRule sr = StarRule.V464;
        write("");
        write($"  §5 ハギの☆ × 作戦マップの勇者（満タンから・単発の戦闘・seed 0〜{ReapSeeds - 1}・レリックなし・ハギのほかの 4 枚は☆3）");
        write("  | ハギ | 勇者 | 勝ち | 決着のターン（中央値） | 刈り取りが決まった戦闘 ／ 回数 | 刈り取りのときの勇者の HP（中央値・範囲） | 刈り取りで落ちた戦闘 | 味方の失った枚数（平均） |");
        write("  |---|---|--:|--:|--:|--:|--:|--:|");
        for (int star = 1; star <= 3; star++)
        {
            UnitDef hagi = sr.Def(UnitCatalog.Hagi, star);
            Formation form = Formation.Build(front1: U("砕け盾のヒビ"), front3: U("錯乱のササ"), center: U("継ぎ当てのツギ"), back1: U("礫のガレ"), back3: hagi);
            foreach (int pct in new[] { 90, 130 })
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
                string hp = reapHp.Count == 0 ? "—" : $"{Med(reapHp):F0}%（{reapHp.Min():F0}〜{reapHp.Max():F0}%）";
                write($"  | ☆{star}（HP {hagi.MaxHp}・攻 {hagi.Attack}・線の上限 {HagiDepthTrait.LineCapOf(BattleEngine.Materialize(Formation.Build(front1: hagi), BattleContext.PlayerTeam)[0]) / 10.0:0.#}%） | ×{pct / 100.0:0.0#}（HP {hero.MaxHp}・回復 {sc.HeroMend(BossMendTrait.Fixed300)}） | {wins} ／ {ReapSeeds} | {Med(turns):0.#} | {reapBattles} ／ {reaps} | {hp} | {reapKills} | {(double)lost / ReapSeeds:F2} |");
            }
        }
    }

    private static double Med(IEnumerable<double> xs) { var o = xs.OrderBy(x => x).ToList(); return o.Count == 0 ? double.NaN : o.Count % 2 == 1 ? o[o.Count / 2] : (o[o.Count / 2 - 1] + o[o.Count / 2]) / 2; }

    private enum Cause { Fallen, TimeUp, Wiped, Stalled, Other }
    private static string CauseName(Cause c) => c switch
    {
        Cause.Fallen => "陥落", Cause.TimeUp => "時間切れ", Cause.Wiped => "全滅", Cause.Stalled => "勇者の打ち切り", _ => "その他",
    };

    /// <summary>§5: 1 章の通し（第464期 ／ 第463期）× 方針 2 つ × 100 seed（第463期 §5 と同じ組み・第464期の器具は強化を足す）。</summary>
    private static void BotTables(Action<string> write, Action<string, Exception> crash)
    {
        string curKey = "";
        int curStage = 0;
        var battles = new List<(string Key, int Stage, bool Won, bool Hero, double Hp)>();
        var heroLoss = new HashSet<Map11State>(ReferenceEqualityComparer.Instance);
        var heroWins = new Dictionary<string, int[]>();
        Map12ChangeSmoke.FightHook = (s, squad, node, intercept) =>
        {
            var prep = intercept ? s.PrepareIntercept(squad, node) : s.Prepare(squad, node);
            if (prep is not { } q) return;
            BattleResult r = BattleEngine.Run(q.Players, q.Enemies, q.Seed, verbose: false);
            int max = q.Players.Sum(u => u.MaxHp);
            double hp = max == 0 ? 0 : 100.0 * q.Players.Where(u => u.IsAlive).Sum(u => u.Hp) / max;
            battles.Add((curKey, curStage, r.PlayerWon, node.IsHero, hp));
            if (!r.PlayerWon && node.IsHero) heroLoss.Add(s);
            if (r.PlayerWon && node.IsHero && heroWins.TryGetValue(curKey, out int[]? hw)) hw[curStage]++;
            s.Resolve(squad, q.Node, r.PlayerWon, intercept);
        };
        var causes = new Dictionary<string, int[,]>();
        var heroCost = new Dictionary<string, int[]>();
        var stuckCause = new Dictionary<string, List<string>>();
        var ends = new Dictionary<string, List<(int Stage, int S1, int S2, int S3, int Gold, int Units)>>();
        var spend = new Dictionary<string, (long Recruits, long Enhances, long Rerolls, long Plays)[]>();
        try
        {
            write("");
            write($"  §5 1 章の通し（1 章の seed 0〜{Seeds - 1}・負ければ同じステージをやり直す・1 ステージ {RetryCap} 回まで）");
            write("  器具の買い方: 拠点の隊の穴（倒れた駒・空席）を、呼び戻し → 候補の先頭を呼ぶ → 穴が無くなったら次の隊の枠を買う → また穴を呼ぶ（今のまま）。");
            write("  **第464期だけ**、その後に金が残っていれば強化——隊の前の席から順に（第1隊 前1 → … → 第4隊 後3・1 体を☆3 まで上げてから次・生きている駒だけ）。第463期は今のまま（候補の先頭が重ねの札でも呼ぶ）");
            write($"  | 1 章 | 方針 | 通せた | 1-1 のやり直し（平均） | 1-2 のやり直し（平均） | 1-3 のやり直し（平均） | 止まったステージ（やり直し {RetryCap} 回を超えた） |");
            write("  |---|---|--:|--:|--:|--:|---|");
            foreach (ChapterRule rule in new[] { ChapterRule.V464, ChapterRule.V463 })
                foreach (var (cname, pol) in new[] { ("1 隊残す（3 ステージとも）", new[] { Keep1, Keep1, Keep1 }), ("分担＋戻り番（1-3 は逃げる）", new[] { Split, Split, Flee }) })
                {
                    var stages = Chapter1.StagesFor(rule);
                    string key = $"{(rule.Stars?.EnhancePrice > 0 ? "**第464期（強化）**" : "第463期（重ね）")}|{cname}";
                    curKey = key;
                    causes[key] = new int[3, 5];
                    heroCost[key] = new int[3];
                    heroWins[key] = new int[3];
                    stuckCause[key] = new();
                    ends[key] = new();
                    spend[key] = new (long, long, long, long)[3];
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
                                    var o = spend[key][i];
                                    spend[key][i] = (o.Recruits + st.Recruits, o.Enhances + st.Enhances, o.Rerolls + st.Rerolls, o.Plays + 1);
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

        string Id(int i) => Chapter1.StagesFor(ChapterRule.V464)[i].Id;

        write("");
        write("  負けた配りの切り分け（やり直したぶんも止まったぶんも数える・ステージごと）。「勇者に隊を落とされた」はその配りで勇者に負けた戦闘があった配り（陥落 ／ 時間切れ ／ 全滅と重なる）");
        write("  | 1 章 | 方針 | ステージ | 負けた配り | 陥落 | 時間切れ | 全滅 | 勇者の打ち切り | その他 | うち勇者に隊を落とされた | 勇者を倒した戦闘 | 止まった通しの終わり |");
        write("  |---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach (var (key, arr) in causes)
        {
            var parts = key.Split('|');
            for (int i = 0; i < 3; i++)
            {
                int total = Enumerable.Range(0, 5).Sum(k => arr[i, k]);
                string id = Id(i);
                var st = stuckCause[key].Where(x => x.StartsWith(id + " ", StringComparison.Ordinal)).GroupBy(x => x[(id.Length + 1)..]).Select(g => $"{g.Key} {g.Count()}");
                write($"  | {parts[0]} | {parts[1]} | {id} | {total} | {arr[i, 0]} | {arr[i, 1]} | {arr[i, 2]} | {arr[i, 3]} | {arr[i, 4]} | {heroCost[key][i]} | {heroWins[key][i]} | {(st.Any() ? string.Join("・", st) : "—")} |");
            }
        }

        write("");
        write("  ステージの終わり（勝った配り・持ち越しの写し）: 駒の枚数と☆ごとの枚数の平均・金の余りの平均 ／ 金の使い道（すべての配り・1 配りの平均）");
        write("  | 1 章 | 方針 | ステージ | 勝った配り | 駒 | ☆1 | ☆2 | ☆3 | 金の余り | 呼んだ（1 配り） | 強化した（1 配り） | リロール（1 配り） |");
        write("  |---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (string key in causes.Keys)
        {
            var parts = key.Split('|');
            for (int i = 0; i < 3; i++)
            {
                var xs = ends[key].Where(e => e.Stage == i).ToList();
                string Am(Func<(int Stage, int S1, int S2, int S3, int Gold, int Units), int> f) => xs.Count == 0 ? "—" : $"{xs.Average(f):F1}";
                var o = spend[key][i];
                string P(long v) => o.Plays == 0 ? "—" : $"{(double)v / o.Plays:F2}";
                write($"  | {parts[0]} | {parts[1]} | {Id(i)} | {xs.Count} | {Am(x => x.Units)} | {Am(x => x.S1)} | {Am(x => x.S2)} | {Am(x => x.S3)} | {Am(x => x.Gold)} | {P(o.Recruits)} | {P(o.Enhances)} | {P(o.Rerolls)} |");
            }
        }

        write("");
        write("  戦闘（すべての配り・やり直しを含む）: 終わりの HP は隊の駒の残り HP の合計 ÷ 最大HP の合計（倒れた駒は 0）");
        write("  | 1 章 | 方針 | ステージ | 戦闘 | 終わりの HP（中央値） | 勝った戦闘の HP（中央値） | 隊を失った戦闘 | うち勇者 | 勇者との戦闘 |");
        write("  |---|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (string key in causes.Keys)
            for (int i = 0; i < 3; i++)
            {
                var bs = battles.Where(b => b.Key == key && b.Stage == i).ToList();
                var parts = key.Split('|');
                write($"  | {parts[0]} | {parts[1]} | {Id(i)} | {bs.Count} | {Med(bs.Select(b => b.Hp)):F0}% | {Med(bs.Where(b => b.Won).Select(b => b.Hp)):F0}% | {bs.Count(b => !b.Won)} | {bs.Count(b => !b.Won && b.Hero)} | {bs.Count(b => b.Hero)} |");
            }
    }
}
