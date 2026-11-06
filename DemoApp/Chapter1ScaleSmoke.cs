using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 1 章の敵の倍率の門（第462期 §4・`--chapter1-scale-smoke`・指示書 design/PHASE462_CHAPTER_SCALE_SPEC.md）
//
// **画面を1つも作らない。** `Chapter1`（`ChapterRule.V462`）／ `MapStage.Scaled` ／ `Map11State` と器具の方針（`Map12ChangeSmoke.Greedy`）だけを回す。
//
//   (a) 1 章の 1-1 の敵の数値は今のまま（×1.0）
//   (b) 1 章の 1-2: 道の部隊・中間拠点・湧き・予備隊・本陣の守り・勇者の最大HP と攻撃が ×1.3。勇者の固定回復も ×1.3
//   (c) 1 章の 1-3: 同じく ×1.6（大群も）
//   (d) 単発の 1-1 ／ 1-2 ／ 1-3 の敵の数値は今のまま
//   (e) ステージの頭の札と上の帯に「敵の強さ ×n」が出る（1-1 は出ない）
//   (f) 速さ・札・金は掛けていない
//
// 加えて（報告だけ・線は置かない）:
//   §3 作戦マップの勇者 ×1.0 ／ ×1.3 ／ ×1.6 とポンの 1-3 の第2隊（ハギあり ／ ハギ抜き）の単発の戦闘 × 20 seed（刈り取りの回数）
//   §6 1 章の通し × 100 seed × 方針 2 つ（第462期 ／ 第461期）・止まった通しの切り分け・1-2 ／ 1-3 の戦闘の終わりの HP と隊を失った戦闘
// =====================================================================================

public static class Chapter1ScaleSmoke
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

    /// <summary>盤に出る敵の部隊の一覧（種類・編成）。経路の区画・湧き（経路ごと）・予備隊・勇者。</summary>
    private static List<(string Kind, string Name, Formation F)> Foes(MapStage s)
    {
        var list = new List<(string, string, Formation)>();
        foreach (var lane in s.Lanes)
            foreach (Map11.RoadNode n in lane)
                list.Add((n.Stationary ? "本陣の守り" : n.Hold ? "大群" : "道の部隊（中間拠点を含む）", n.Name, n.Enemy));
        for (int r = 0; r < s.LaneCount; r++) { var (e, name, _) = s.Spawn(r); list.Add(("湧き", name, e)); }
        if (s.Reserve.Enemy is { } re) list.Add(("予備隊", s.Reserve.Name, re));
        if (s.Hero.Unit is { } hu) list.Add(("勇者", hu.Name, Formation.Build(center: hu)));
        return list;
    }

    /// <summary>倍率の写し <paramref name="scaled"/> が素の <paramref name="plain"/> に <paramref name="pct"/>% を掛けたものか（最大HP・攻撃は四捨五入・ほかは同じ）。</summary>
    private static bool ScaledOk(MapStage plain, MapStage scaled, int pct, Action<string> write, string label, out int pieces, out int kinds)
    {
        var a = Foes(plain);
        var b = Foes(scaled);
        bool ok = a.Count == b.Count;
        pieces = 0;
        var seen = new HashSet<string>();
        bool keep = true;   // (f) 速さ・札・攻撃型・行動・名前は同じ
        for (int i = 0; i < Math.Min(a.Count, b.Count); i++)
        {
            var pa = a[i].F.Occupied().ToList();
            var pb = b[i].F.Occupied().ToList();
            ok &= pa.Count == pb.Count && a[i].Kind == b[i].Kind && a[i].F.Shape == b[i].F.Shape;
            for (int k = 0; k < Math.Min(pa.Count, pb.Count); k++)
            {
                UnitDef x = pa[k].Def, y = pb[k].Def;
                pieces++;
                ok &= pa[k].Slot == pb[k].Slot && y.MaxHp == MapFoeScale.Mul(x.MaxHp, pct) && y.Attack == MapFoeScale.Mul(x.Attack, pct);
                keep &= y.Id == x.Id && y.Name == x.Name && y.Speed == x.Speed && ReferenceEquals(y.Traits, x.Traits) && y.Pattern == x.Pattern
                        && y.Advances == x.Advances && ReferenceEquals(y.Actions, x.Actions);
                if (seen.Add(a[i].Kind + "|" + x.Id))
                    write($"      {label} {a[i].Kind}「{a[i].Name}」{x.Name}: HP {x.MaxHp} → {y.MaxHp}・攻 {x.Attack} → {y.Attack}・速 {x.Speed} → {y.Speed}");
            }
        }
        kinds = b.Select(x => x.Kind).Distinct().Count();
        return ok && keep;
    }

    /// <summary>勇者の駒（盤の口 `Map11State` が作ったもの）の最大HP と固定回復。</summary>
    private static (int MaxHp, int Mend) HeroUnit(List<UnitState> us)
    {
        UnitState h = us[0];
        int mend = h.Traits.OfType<BossMendTrait>().Select(m => m.Fixed).DefaultIfEmpty(0).First();
        return (h.MaxHp, mend);
    }

    public static bool Run(Action<string> write, bool bots = true)
    {
        bool ok = true;
        void Req(bool c, string what) { write($"{(c ? "○" : "×")} {what}"); ok &= c; }

        var ch = Chapter1.StagesFor(ChapterRule.V462);
        var plainCh = Chapter1.StagesFor(ChapterRule.V461);
        write($"  1 章の規則 {ChapterRule.V462.Name}: 倍率 {string.Join(" ／ ", ch.Select(s => $"{s.Id} {(s.FoeScale.Active ? s.FoeScale.Text : "×1.0")}（それ以外 {s.FoeScale.FoePercent}% ・ 勇者 {s.FoeScale.HeroPercent}%）"))}");

        // ---- (a) 1-1 ----
        {
            bool same = ReferenceEquals(ch[0], MapStage.Stage11Ch) && !ch[0].FoeScale.Active;
            Chapter1.Begin(4620, ChapterRule.V462);
            Map11State st = Map11Session.State!;
            bool live = ReferenceEquals(st.Stage, MapStage.Stage11Ch) && ReferenceEquals(Chapter1.CurrentStage, st.Stage);
            var defs = Foes(ch[0]).SelectMany(f => f.F.Occupied().Select(o => o.Def)).ToList();
            write($"  (a) 1 章の 1-1: ステージは Stage11Ch そのもの {same}・遊ぶ盤も同じ {live}・敵の駒 {defs.Count} 体（HP {string.Join("/", defs.Select(d => d.MaxHp).Distinct())}）");
            Req(same && live, "(a) 1 章の 1-1 の敵の数値は今のまま（×1.0・ステージは第461期の 1-1 そのもの）");
            Chapter1.End();
        }

        // ---- (b)(c) 1-2 ×1.3 ／ 1-3 ×1.6（定義）＋ 盤の口で勇者の駒（最大HP・固定回復）と区画の HP ----
        var heroSeen = new Dictionary<string, List<(int MaxHp, int Mend)>>();
        var nodeHp = new Dictionary<string, (bool Ok, int Nodes)>();
        foreach ((int i, int pct, string tag) in new[] { (1, 130, "(b)"), (2, 160, "(c)") })
        {
            MapStage plain = plainCh[i], scaled = ch[i];
            write($"  {tag} 1 章の {scaled.Id}（{scaled.FoeScale.Text}）:");
            bool defsOk = ScaledOk(plain, scaled, pct, write, "   ", out int pieces, out int kinds);
            bool cached = ReferenceEquals(Chapter1.StagesFor(ChapterRule.V462)[i], scaled) && ReferenceEquals(plain.Scaled(scaled.FoeScale), scaled);

            // 盤の口: 区画の HP（DefMaxHp）は掛けた後の値の合計・勇者の駒の最大HP と固定回復
            var st = new Map11State(Chapter1.StageSeed(4620, i, 0), scaled.Time, scaled.Portal, draft: true, stage: scaled);
            bool hpOk = st.Nodes.SelectMany(r => r).All(n => n.DefMaxHp == n.Def.Enemy.Occupied().Sum(o => o.Def.MaxHp))
                        && (st.Reserve is null || st.Reserve.DefMaxHp == st.Reserve.Def.Enemy.Occupied().Sum(o => o.Def.MaxHp));
            int plainSum = new Map11State(0, plain.Time, plain.Portal, draft: true, stage: plain).Nodes.SelectMany(r => r).Sum(n => n.DefMaxHp);
            int scaledSum = st.Nodes.SelectMany(r => r).Sum(n => n.DefMaxHp);
            write($"      盤の区画の HP の合計 {plainSum} → {scaledSum}（区画ごとの HP が掛けた後の駒の合計 {hpOk}）");
            nodeHp[scaled.Id] = (hpOk, st.Nodes.Sum(r => r.Length));

            // 勇者: 盤の口（`Map11State.Prepare`）で作った勇者の駒を拾う（器具の通しを数本・勇者に当たった戦闘だけ）
            var hs = heroSeen[scaled.Id] = new();
            Map12ChangeSmoke.FightHook = (s, squad, node, intercept) =>
            {
                bool fresh = node.Units is null;   // 盤の口がこの戦闘で勇者の駒を作る（2 戦目からは削れた最大HP を持ち越すので数えない）
                var prep = intercept ? s.PrepareIntercept(squad, node) : s.Prepare(squad, node);
                if (prep is not { } q) return;
                if (node.IsHero && fresh) hs.Add(HeroUnit(q.Enemies));
                BattleResult r = BattleEngine.Run(q.Players, q.Enemies, q.Seed, verbose: false);
                s.Resolve(squad, q.Node, r.PlayerWon, intercept);
            };
            try
            {
                for (int c = 0; c < 40 && hs.Count < 3; c++)
                {
                    var g = new Map11State(Chapter1.StageSeed(c, i, 0), scaled.Time, scaled.Portal, draft: true, stage: scaled);
                    Play(g, c % 2 == 0 ? Keep1 : Split);
                }
            }
            finally { Map12ChangeSmoke.FightHook = null; }
            int wantHp = MapFoeScale.Mul(EnemyCatalog.HeroMap.MaxHp, pct), wantMend = MapFoeScale.Mul(BossMendTrait.Fixed300, pct);
            bool heroOk = hs.Count > 0 && hs.All(h => h.MaxHp == wantHp && h.Mend == wantMend);
            write($"      勇者の駒（盤の口で作ったもの・{hs.Count} 戦）: 最大HP {string.Join("/", hs.Select(h => h.MaxHp).Distinct())}（{EnemyCatalog.HeroMap.MaxHp} × {pct}% ＝ {wantHp}）"
                + $"・固定回復 {string.Join("/", hs.Select(h => h.Mend).Distinct())}（{BossMendTrait.Fixed300} × {pct}% ＝ {wantMend}）"
                + $"・説明文「{scaled.Hero.Unit?.PlusText}」");
            string kindsText = string.Join("・", Foes(scaled).Select(f => f.Kind).Distinct());
            Req(defsOk && cached && hpOk && heroOk && scaled.FoeScale == new MapFoeScale(pct, pct),
                $"{tag} 1 章の {scaled.Id}: 敵の駒 {pieces} 体（{kindsText}）の最大HP と攻撃が ×{pct / 100.0:0.0#}（四捨五入）・区画の HP も掛けた後の値・勇者の固定回復も ×{pct / 100.0:0.0#}");
        }

        // ---- (d) 単発 ----
        {
            var singles = new[] { MapStage.Stage11, MapStage.Stage12, MapStage.Stage13 };
            bool none = singles.All(s => !s.FoeScale.Active) && !MapStage.Stage11Ch.FoeScale.Active;
            bool heroPlain = ReferenceEquals(MapStage.Stage12.Hero.Unit, EnemyCatalog.HeroMap) && ReferenceEquals(MapStage.Stage13.Hero.Unit, EnemyCatalog.HeroMap);
            // 単発の盤の敵の駒は、倍率の写しのどれでもない（素の定義のまま）
            var scaledDefs = new HashSet<UnitDef>(ReferenceEqualityComparer.Instance);
            foreach (MapStage s in ch.Skip(1)) foreach (var f in Foes(s)) foreach (var o in f.F.Occupied()) scaledDefs.Add(o.Def);
            int leaked = singles.Sum(s => Foes(s).Sum(f => f.F.Occupied().Count(o => scaledDefs.Contains(o.Def))));
            // 単発の 1-2 の勇者の駒は固定回復 300・最大HP 2400（盤の口で作る）
            var heroSingle = new List<(int, int)>();
            Map12ChangeSmoke.FightHook = (s, squad, node, intercept) =>
            {
                bool fresh = node.Units is null;
                var prep = intercept ? s.PrepareIntercept(squad, node) : s.Prepare(squad, node);
                if (prep is not { } q) return;
                if (node.IsHero && fresh) heroSingle.Add(HeroUnit(q.Enemies));
                BattleResult r = BattleEngine.Run(q.Players, q.Enemies, q.Seed, verbose: false);
                s.Resolve(squad, q.Node, r.PlayerWon, intercept);
            };
            try
            {
                for (int c = 0; c < 40 && heroSingle.Count < 3; c++)
                    foreach (MapStage s in new[] { MapStage.Stage12, MapStage.Stage13 })
                        Play(new Map11State(c, s.Time, s.Portal, draft: true, stage: s), c % 2 == 0 ? Keep1 : Split);
            }
            finally { Map12ChangeSmoke.FightHook = null; }
            bool heroUnitOk = heroSingle.Count > 0 && heroSingle.All(h => h == (EnemyCatalog.HeroMap.MaxHp, BossMendTrait.Fixed300));
            write($"  (d) 単発: 倍率なし {none}・勇者の定義は素のまま {heroPlain}・単発の盤に倍率の写しの駒 {leaked} 体・単発の 1-2 の勇者の駒（{heroSingle.Count} 戦）{string.Join("/", heroSingle.Distinct().Select(h => $"HP {h.Item1}・回復 {h.Item2}"))}");
            Req(none && heroPlain && leaked == 0 && heroUnitOk, "(d) 単発の 1-1 ／ 1-2 ／ 1-3 の敵の数値は今のまま（勇者 HP 2400・固定回復 300）");
        }

        // ---- (e) 表示 ----
        {
            var intros = ch.Select(s => Chapter1.Intro(s).Body).ToList();
            foreach ((MapStage s, string body) in ch.Zip(intros)) write($"  (e) {s.Id} の頭の札の最後の行「{body.Split('\n')[^1]}」・上の帯「{s.FoeScale.BarText}」");
            bool okE = !intros[0].Contains("敵の強さ", StringComparison.Ordinal) && ch[0].FoeScale.BarText == ""
                       && intros[1].EndsWith("\n敵の強さ ×1.3", StringComparison.Ordinal) && ch[1].FoeScale.BarText == "敵 ×1.3"
                       && intros[2].EndsWith("\n敵の強さ ×1.6", StringComparison.Ordinal) && ch[2].FoeScale.BarText == "敵 ×1.6"
                       && plainCh.All(s => !Chapter1.Intro(s).Body.Contains("敵の強さ", StringComparison.Ordinal));
            Req(okE, "(e) ステージの頭の札と上の帯に「敵の強さ ×n」が出る（1-1 と単発・第461期の 1 章は出ない）");
        }

        // ---- (f) 速さ・札・金 ----
        {
            bool gold = ch.Zip(plainCh).All(p => p.First.Gold == p.Second.Gold && p.First.Hero.Gold == p.Second.Hero.Gold);
            bool keep = true;
            foreach ((int i, int pct) in new[] { (1, 130), (2, 160) })
                foreach (var (f, g) in Foes(plainCh[i]).Zip(Foes(ch[i])))
                    foreach (var (x, y) in f.F.Occupied().Zip(g.F.Occupied()))
                        keep &= x.Def.Speed == y.Def.Speed && ReferenceEquals(x.Def.Traits, y.Def.Traits);
            // 金: 区画ごとの抜いたときの金（`Map11State.KillGold`・画面の札も同じ値）が倍率の有無で同じ
            bool goldOnce = true;
            var amounts = new List<string>();
            foreach (int i in new[] { 1, 2 })
            {
                var a = new Map11State(1, plainCh[i].Time, plainCh[i].Portal, draft: true, stage: plainCh[i]);
                var b = new Map11State(1, ch[i].Time, ch[i].Portal, draft: true, stage: ch[i]);
                var na = a.Nodes.SelectMany(r => r).Append(a.Reserve).OfType<Map11State.Node>().Select(a.KillGold).ToList();
                var nb = b.Nodes.SelectMany(r => r).Append(b.Reserve).OfType<Map11State.Node>().Select(b.KillGold).ToList();
                goldOnce &= na.SequenceEqual(nb);
                amounts.Add($"{ch[i].Id} {string.Join("/", nb.Select(v => "＋" + v))}");
            }
            write($"  (f) 速さ・札は同じ {keep}・金の規則（勇者を倒した金を含む）は同じ {gold}・区画ごとの抜いたときの金は同じ {goldOnce}（{string.Join(" ／ ", amounts)}）");
            Req(keep && gold && goldOnce, "(f) 速さ・札の係数・金は掛けていない");
        }

        // ---- 報告のみ §3: 作戦マップの勇者 × ポンの 1-3 の第2隊 ----
        int crashed = 0;
        void Crash(string what, Exception ex) { crashed++; if (crashed <= 3) write($"  × {what}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
        try { ReapTable(write); }
        catch (Exception ex) { Crash("§3 刈り取りの表", ex); }

        if (bots)
        {
            try { BotTables(write, Crash); }
            catch (Exception ex) { Crash("§6 器具", ex); }
        }
        Req(crashed == 0, $"器具（§3 の単発の戦闘{(bots ? $"・§6 の 1 章の通し × 2 方針 × {Seeds} seed" : "")}）が落ちない（数字は報告のみ・線は置かない）");
        return ok;
    }

    private static UnitDef U(string name) => UnitCatalog.All.First(d => d.Name == name);

    /// <summary>§3: ポンの 1-3 の第2隊（ハギあり ／ ハギ抜き）× 作戦マップの勇者 ×1.0 ／ ×1.3 ／ ×1.6 × 20 seed。</summary>
    private static void ReapTable(Action<string> write)
    {
        var squads = new[]
        {
            ("ハギあり（ポンの 1-3 の第2隊）", Formation.Build(front1: U("砕け盾のヒビ"), front3: U("錯乱のササ"), center: U("継ぎ当てのツギ"), back1: U("礫のガレ"), back3: U("彼岸花のハギ"))),
            ("ハギ抜き（後3 は空席）", Formation.Build(front1: U("砕け盾のヒビ"), front3: U("錯乱のササ"), center: U("継ぎ当てのツギ"), back1: U("礫のガレ"))),
        };
        write("");
        write($"  §3 作戦マップの勇者（満タンから・単発の戦闘・seed 0〜{ReapSeeds - 1}・レリックなし）");
        write("  | 隊 | 勇者 | 勝ち | 決着のターン（中央値） | 刈り取りが決まった戦闘 ／ 回数 | 刈り取りのときの勇者の HP（中央値・範囲） | 刈り取りで落ちた戦闘 | 味方の失った枚数（平均） |");
        write("  |---|---|--:|--:|--:|--:|--:|--:|");
        foreach ((string sname, Formation form) in squads)
            foreach (int pct in new[] { 100, 130, 160 })
            {
                var sc = new MapFoeScale(pct, pct);
                UnitDef hero = sc.HeroDef(EnemyCatalog.HeroMap);
                int wins = 0, reapBattles = 0, reaps = 0, reapKills = 0, lost = 0;
                var turns = new List<int>();
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
                write($"  | {sname} | ×{pct / 100.0:0.0#}（HP {hero.MaxHp}・攻 {hero.Attack}・回復 {sc.HeroMend(BossMendTrait.Fixed300)}） | {wins} ／ {ReapSeeds} | {Med(turns.Select(t => (double)t).ToList()):0.#} | {reapBattles} ／ {reaps} | {hp} | {reapKills} | {(double)lost / ReapSeeds:F2} |");
            }
    }

    private enum Cause { Fallen, TimeUp, Wiped, Stalled, Other }
    private static string CauseName(Cause c) => c switch
    {
        Cause.Fallen => "陥落", Cause.TimeUp => "時間切れ", Cause.Wiped => "全滅", Cause.Stalled => "勇者の打ち切り", _ => "その他",
    };

    /// <summary>§6: 1 章の通し（第462期 ／ 第461期）× 方針 2 つ × 100 seed。</summary>
    private static void BotTables(Action<string> write, Action<string, Exception> crash)
    {
        // 戦闘ごとの記録（いま回している 1 章の規則・方針・ステージ）
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
        var causes = new Dictionary<string, int[,]>();   // key → [stage, cause]
        var heroCost = new Dictionary<string, int[]>();  // key → 勇者に隊を落とされた負け（ステージごと）
        var stuckCause = new Dictionary<string, List<string>>();
        try
        {
            write("");
            write($"  §6 1 章の通し（1 章の seed 0〜{Seeds - 1}・負ければ同じステージをやり直す・1 ステージ {RetryCap} 回まで）");
            write($"  | 1 章 | 方針 | 通せた | 1-1 のやり直し（平均） | 1-2 のやり直し（平均） | 1-3 のやり直し（平均） | 止まったステージ（やり直し {RetryCap} 回を超えた） |");
            write("  |---|---|--:|--:|--:|--:|---|");
            foreach (ChapterRule rule in new[] { ChapterRule.V462, ChapterRule.V461 })
                foreach (var (cname, pol) in new[] { ("1 隊残す（3 ステージとも）", new[] { Keep1, Keep1, Keep1 }), ("分担＋戻り番（1-3 は逃げる）", new[] { Split, Split, Flee }) })
                {
                    var stages = Chapter1.StagesFor(rule);
                    string key = $"{(rule.Scales is null ? "第461期（倍率なし）" : "**第462期（倍率）**")}|{cname}";
                    curKey = key;
                    causes[key] = new int[3, 5];
                    heroCost[key] = new int[3];
                    stuckCause[key] = new();
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
                                    won = st.Won;
                                    if (won) { pack = st.TakeCarry(); continue; }
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
                string id = Chapter1.StagesFor(ChapterRule.V461)[i].Id;
                var st = stuckCause[key].Where(x => x.StartsWith(id + " ", StringComparison.Ordinal)).GroupBy(x => x[(id.Length + 1)..]).Select(g => $"{g.Key} {g.Count()}");
                write($"  | {parts[0]} | {parts[1]} | {id} | {total} | {arr[i, 0]} | {arr[i, 1]} | {arr[i, 2]} | {arr[i, 3]} | {arr[i, 4]} | {heroCost[key][i]} | {(st.Any() ? string.Join("・", st) : "—")} |");
            }
        }

        write("");
        write("  1-2 ／ 1-3 の戦闘（すべての配り・やり直しを含む）: 終わりの HP は隊の駒の残り HP の合計 ÷ 最大HP の合計（倒れた駒は 0）");
        write("  | 1 章 | 方針 | ステージ | 戦闘 | 終わりの HP（中央値） | 勝った戦闘の HP（中央値） | 隊を失った戦闘 | うち勇者 | 勇者との戦闘 |");
        write("  |---|---|---|--:|--:|--:|--:|--:|--:|");
        static double Med(IEnumerable<double> xs) { var o = xs.OrderBy(x => x).ToList(); return o.Count == 0 ? double.NaN : o.Count % 2 == 1 ? o[o.Count / 2] : (o[o.Count / 2 - 1] + o[o.Count / 2]) / 2; }
        foreach (string key in causes.Keys)
            for (int i = 1; i < 3; i++)
            {
                var bs = battles.Where(b => b.Key == key && b.Stage == i).ToList();
                var parts = key.Split('|');
                write($"  | {parts[0]} | {parts[1]} | {Chapter1.StagesFor(ChapterRule.V461)[i].Id} | {bs.Count} | {Med(bs.Select(b => b.Hp)):F0}% | {Med(bs.Where(b => b.Won).Select(b => b.Hp)):F0}% | {bs.Count(b => !b.Won)} | {bs.Count(b => !b.Won && b.Hero)} | {bs.Count(b => b.Hero)} |");
            }
    }
}
