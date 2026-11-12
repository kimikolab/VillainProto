using BattleCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

// =====================================================================================
// 弱い 4 軸の診断（第472期・`--axis-diag`・指示書 design/PHASE472_AXIS_DIAG_SPEC.md）
//
// 10 軸 × 代表の隊 1 つ（＋ ポンの写しのその軸の隊）を、1 章の 1-2 ・1-3 の敵部隊の全部（今の版・倍率込み）と勇者・
// 本陣の守り → 勇者の連戦に当て、☆1 ／ ☆2 ／ ☆3 で表にする（`design/weights/axis_diag.md`）。
//   決着のターン ／ 看板の効果が初めて働いたターン ／ 与えたダメージの内訳 ／ 受けたダメージと減らした分 ／
//   続けて 2 戦目・3 戦目（HP と倒れた駒を持ち越す・道中の回復 50%）／ 敵の最大HP だけ ×2 ・×3 にした雑兵での順位。
// 戦闘は `verbose: true` で回して台本（`BattleEvent`）と帳簿（`UnitTally`）を読む——**台本を積んでも盤面は動かない**（CLAUDE.md の約束）。
// **規則は1つも持たない・盤は1つも動かさない。ここには Godot の型を1つも使わない。**
// =====================================================================================

public static class AxisDiag
{
    /// <summary>弱い 4 軸（第471期のポン・問い 3）。</summary>
    public static readonly string[] Weak = { "毒", "生贄", "呪い", "破片" };

    public static string SeatName(int s) => s switch { 0 => "前1", 1 => "前3", 2 => "中央", 3 => "後1", 4 => "後3", _ => $"席{s}" };

    // ---------------- 隊 ----------------

    /// <summary>軸の隊 1 つ（代表 ＝ 道具が組んだ隊 ／ 写し ＝ ポンの写しの隊）。<c>How</c> は駒ごとの置き方の説明。</summary>
    public sealed record AxisSquad(string Axis, bool Representative, FoeWeight.Squad Squad, IReadOnlyList<string> How);

    private static (string Name, Formation F)[]? _compare;
    private static (string Name, Formation F)[] CompareRows => _compare ??= Presets.Compare;

    /// <summary>
    /// 軸の代表の隊（指示書 §1-1）: その軸の駒を 33 体から全部入れ、足りない席は `compare` の行で<b>その軸の駒と同じ行に多く載る駒</b>で埋める
    /// （点 ＝ 行ごとの「その行に載るその軸の駒の数」の和・同点は載る行の数 → 表の並び）。
    /// 席は `compare` の行の席に合わせる（隊と重なる駒の多い行から順に、空いている席へ）。どの行でも置けなかった駒は役割で置く。
    /// </summary>
    public static AxisSquad Representative(string axis, IReadOnlyList<UnitDef> units)
    {
        var rows = CompareRows.Select(c => (c.Name, Seats: c.F.Occupied().Where(o => o.Slot < 5).Select(o => (o.Slot, o.Def.Id)).ToList())).ToList();
        var core = units.Where(d => d.Tags.Contains(axis)).Take(5).ToList();
        var ids = core.Select(d => d.Id).ToHashSet();
        var how = new Dictionary<string, string>();
        foreach (UnitDef d in core) how[d.Id] = "軸の駒";
        while (ids.Count < 5)
        {
            var best = units.Where(d => !ids.Contains(d.Id))
                .Select((d, i) => (d, i,
                    score: rows.Where(r => r.Seats.Any(s => s.Id == d.Id)).Sum(r => r.Seats.Count(s => core.Any(c => c.Id == s.Id))),
                    count: rows.Count(r => r.Seats.Any(s => s.Id == d.Id))))
                .OrderByDescending(x => x.score).ThenByDescending(x => x.count).ThenBy(x => x.i).First();
            ids.Add(best.d.Id);
            how[best.d.Id] = $"埋め（軸の駒と同じ行に {best.score} 回）";
        }
        var defs = units.Where(d => ids.Contains(d.Id)).ToList();
        var seat = new Dictionary<string, int>();
        var taken = new HashSet<int>();
        foreach (var r in rows.Select((r, i) => (r, i, n: r.Seats.Count(s => ids.Contains(s.Id)))).Where(x => x.n > 0).OrderByDescending(x => x.n).ThenBy(x => x.i))
            foreach (var (slot, id) in r.r.Seats)
                if (ids.Contains(id) && !seat.ContainsKey(id) && !taken.Contains(slot))
                {
                    seat[id] = slot;
                    taken.Add(slot);
                    how[id] += $"・席は「{r.r.Name}」";
                }
        foreach (UnitDef d in defs.Where(d => !seat.ContainsKey(d.Id)))
        {
            string role = d.Tags.FirstOrDefault(t => UnitTags.Roles.Contains(t)) ?? "";
            int[] pref = role == "タンク" ? new[] { 0, 1, 2, 3, 4 } : role is "ヒーラー" or "サポート" ? new[] { 2, 3, 4, 1, 0 } : new[] { 3, 4, 1, 0, 2 };
            int s = pref.First(x => !taken.Contains(x));
            seat[d.Id] = s;
            taken.Add(s);
            how[d.Id] += $"・席は役割（{role}）で";
        }
        var pieces = defs.Select(d => new FoeWeight.Piece(d, 1, seat[d.Id], null)).OrderBy(p => p.Slot).ToList();
        return new AxisSquad(axis, true, new FoeWeight.Squad($"代表・{axis}", 1, pieces),
            pieces.Select(p => $"{SeatName(p.Slot)} {p.Base.Name}（{how[p.Base.Id]}）").ToList());
    }

    /// <summary>写しの隊のうち、その軸が「いちばん多い軸（2 枚以上）」の隊（同じ顔ぶれは 1 つ・軸ごとに <paramref name="cap"/> 隊まで・その軸の駒の多い順）。</summary>
    public static List<AxisSquad> FromPlays(string axis, IReadOnlyList<FoeWeight.Squad> plays, int cap)
    {
        static int Count(FoeWeight.Squad q, string a) => q.Pieces.Count(p => p.Base.Tags.Contains(a));
        var seen = new HashSet<string>();
        var list = new List<AxisSquad>();
        foreach (FoeWeight.Squad q in plays.Where(q => q.Pieces.Count >= 3)
                     .Where(q => { int n = Count(q, axis); return n >= 2 && UnitTags.Axes.All(a => Count(q, a) <= n); })
                     .OrderByDescending(q => Count(q, axis)).ThenByDescending(q => q.Pieces.Count).ThenBy(q => q.Name, StringComparer.Ordinal))
        {
            string key = string.Join(",", q.Pieces.Select(p => p.Base.Id).OrderBy(x => x, StringComparer.Ordinal));
            if (!seen.Add(key)) continue;
            list.Add(new AxisSquad(axis, false, q, q.Pieces.OrderBy(p => p.Slot).Select(p => $"{SeatName(p.Slot)} {p.Base.Name}（写しでは☆{p.Star}）").ToList()));
            if (list.Count >= cap) break;
        }
        return list;
    }

    // ---------------- 1 戦 ----------------

    /// <summary>1 戦ぶんの数（指示書 §1-3 の列）。ダメージは HP が実際に減った量（`BattleEventKind.Damage`）。</summary>
    public sealed class Fight
    {
        public bool Won; public double Hp; public int Lost; public int Turns;
        /// <summary>看板の効果が初めて働いたターン（ターン頭の写しで読む・届かなければ null・その軸に読み方が無ければ <c>Readable</c> が偽）。</summary>
        public int? Sign; public bool Readable;
        /// <summary>与えた: 素の殴り ／ 殴りのうち攻撃力の上乗せぶん ／ 呼んだ駒の殴り ／ 殴り以外（刻み・爆ぜ・撃ち・反撃・共有・敵どうし）。</summary>
        public double DealBase, DealBoost, DealSummon, DealOther;
        /// <summary>受けた: 敵から ／ 味方から（巻き込み・漏れ・自傷）。</summary>
        public double RecvFoe, RecvAlly;
        /// <summary>味方の破片が減った延べ（受け止めた量 ＋ ガレが砕いた量）・ガレが撃った回数。</summary>
        public double ArmorLost; public int Shots;
        /// <summary>ターン頭の攻撃力の下がり（素の値 − 今の値の和 ／ 素の値の和）: 敵 ／ 味方。</summary>
        public double FoeAtkBase, FoeAtkDrop, AllyAtkBase, AllyAtkDrop;
    }

    /// <summary>看板の効果（指示書 §1-3）。毒 ＝ 敵 1 体の毒が 3 層 ／ 呪い ＝ 呪われた（呪の層 か 攻撃力が素の値より下）敵が 2 体 ／ 生贄 ＝ リィカの層が 3 ／ 破片 ＝ ガレが初めて撃った（振り以外のダメージ）。</summary>
    public static string SignText(string axis) => axis switch
    {
        "毒" => "敵 1 体の毒が 3 層（ターン頭）",
        "呪い" => "呪われた敵が 2 体（ターン頭・呪の層か攻撃力が素の値より下）",
        "生贄" => "リィカの層が 3（ターン頭）",
        "破片" => "ガレが初めて撃った（ガレの、振り以外で敵の HP を減らした最初のターン）",
        _ => "取らない（この期は弱い 4 軸だけ）",
    };

    private sealed record Info(int Team, int BaseAtk, bool Summon, bool Gare);

    /// <summary>1 戦（敵は満タンから・`verbose: true` で台本と帳簿を読む）。</summary>
    public static Fight Run(string axis, List<UnitState> players, List<UnitState> enemies, int seed)
    {
        int max = players.Sum(u => u.MaxHp);
        BattleResult r = BattleEngine.Run(players, enemies, seed, verbose: true);
        var f = new Fight
        {
            Won = r.PlayerWon, Turns = r.Turns, Lost = players.Count(u => !u.IsAlive),
            Hp = !r.PlayerWon || max == 0 ? 0 : 100.0 * players.Where(u => u.IsAlive).Sum(u => u.Hp) / max,
            Readable = axis is "毒" or "呪い" or "生贄" or "破片",
        };
        var info = new Dictionary<int, Info>();
        foreach (UnitState u in players) info[u.InstanceId] = new Info(BattleContext.PlayerTeam, u.Def.Attack, false, u.Def.Id == "gare");   // 規定のガレは拾い集め（PebbleGather）＋ 集まる全体（第365期）——札ではなく駒で見分ける
        foreach (UnitState u in enemies) info[u.InstanceId] = new Info(BattleContext.EnemyTeam, u.Def.Attack, false, false);
        bool Mine(int? id) => id is { } i && info.TryGetValue(i, out Info? x) && x.Team == BattleContext.PlayerTeam;
        bool Theirs(int? id) => id is { } i && info.TryGetValue(i, out Info? x) && x.Team != BattleContext.PlayerTeam;
        var atkNow = new Dictionary<int, int>();
        int? swing = null;
        var shotTurns = new HashSet<int>();
        var poison = new Dictionary<int, int>();   // 毒（ターン頭・敵ごと）
        var cursed = new HashSet<int>();           // 呪われた敵（そのターン頭）
        int curTurn = -1;
        void CloseTurn()
        {
            if (curTurn < 0 || f.Sign is not null) return;
            if (axis == "毒" && poison.Values.Any(v => v >= 3)) f.Sign = curTurn;
            if (axis == "呪い" && cursed.Count >= 2) f.Sign = curTurn;
        }
        foreach (BattleEvent e in r.Events)
        {
            switch (e.Kind)
            {
                case BattleEventKind.TurnStart:
                    CloseTurn();
                    curTurn = e.Turn; poison.Clear(); cursed.Clear(); swing = null;
                    break;
                case BattleEventKind.Summon:
                    if (e.TargetId is { } sid) info[sid] = new Info(e.Team ?? (Mine(e.ActorId) ? BattleContext.PlayerTeam : BattleContext.EnemyTeam), 0, true, false);
                    break;
                case BattleEventKind.Attack:
                    swing = e.ActorId;
                    break;
                case BattleEventKind.Skill:
                case BattleEventKind.Charge:
                    swing = null;
                    break;
                case BattleEventKind.StatusSnapshot when e.TargetId is { } t && Theirs(t):
                    if (e.Text == StatusKeys.LabelOf(StatusKeys.Poison)) poison[t] = e.Amount;
                    if (e.Text == StatusKeys.LabelOf(StatusKeys.Curse) && e.Amount > 0) cursed.Add(t);
                    break;
                case BattleEventKind.StatSnapshot when e.TargetId is { } t && info.TryGetValue(t, out Info? ui):
                    atkNow[t] = e.Amount;
                    if (ui.Summon) break;
                    if (ui.Team == BattleContext.PlayerTeam) { f.AllyAtkBase += ui.BaseAtk; f.AllyAtkDrop += Math.Max(0, ui.BaseAtk - e.Amount); }
                    else
                    {
                        f.FoeAtkBase += ui.BaseAtk; f.FoeAtkDrop += Math.Max(0, ui.BaseAtk - e.Amount);
                        if (e.Amount < ui.BaseAtk) cursed.Add(t);
                    }
                    break;
                case BattleEventKind.Damage when e.TargetId is { } t && e.Amount > 0:
                    if (Theirs(t))
                    {
                        bool hit = e.ActorId is { } a && a == swing && Mine(a) && e.Pattern is not null && !e.Relayed && e.ShareFromId is null;
                        if (hit && info[e.ActorId!.Value].Summon) f.DealSummon += e.Amount;
                        else if (hit)
                        {
                            Info ai = info[e.ActorId!.Value];
                            int now = atkNow.TryGetValue(e.ActorId.Value, out int n) ? n : ai.BaseAtk;
                            double boost = now > ai.BaseAtk && now > 0 ? (double)(now - ai.BaseAtk) / now : 0;
                            f.DealBoost += e.Amount * boost;
                            f.DealBase += e.Amount * (1 - boost);
                        }
                        else
                        {
                            f.DealOther += e.Amount;
                            if (e.ActorId is { } g && info.TryGetValue(g, out Info? gi) && gi.Gare && g != swing) shotTurns.Add(e.Turn);
                        }
                    }
                    else if (Mine(t))
                    {
                        if (Mine(e.ActorId) || e.FriendlyFire) f.RecvAlly += e.Amount; else f.RecvFoe += e.Amount;
                    }
                    break;
            }
        }
        CloseTurn();
        f.Shots = shotTurns.Count;
        if (axis == "破片" && shotTurns.Count > 0) f.Sign = shotTurns.Min();
        if (axis == "生贄" && r.TallyByUnit.TryGetValue("rica", out UnitTally? rt) && rt.R377LayerByTurn is { } lay && rt.R377AliveByTurn is { } al)
            for (int t = 0; t < lay.Length; t++)
                if (al[t] > 0 && lay[t] >= 3 * al[t]) { f.Sign = t; break; }
        foreach (UnitState u in players)
            if (r.TallyByUnit.TryGetValue(u.Def.Id, out UnitTally? ut)) f.ArmorLost += ut.Lv332ArmorLost;
        return f;
    }

    // ---------------- 相手 ----------------

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Formation, int), Formation> _hp = new();

    /// <summary>敵の最大HP だけ ×<paramref name="k"/> にした写し（攻撃・札はそのまま・`MapFoeScale.Foe` と同じ写しの口——盤は動かさない）。</summary>
    public static Formation HpOnly(Formation f, int k) => k == 1 ? f : _hp.GetOrAdd((f, k), key =>
    {
        Formation c = key.Item1.Clone();
        foreach ((int slot, UnitDef d) in key.Item1.Occupied()) c[slot] = d.WithStats(d.MaxHp * key.Item2, d.Attack);
        return c;
    });

    /// <summary>連戦の相手（勇者と本陣の守り以外の部隊）。</summary>
    public static bool Grunt(FoeWeight.Foe f) => f.Kind is not FoeWeight.Kind.Hero and not FoeWeight.Kind.Garrison;

    /// <summary>
    /// 続けて 3 戦（指示書 §1-3 H3）。seed <paramref name="seed"/> の通しは雑兵の列の <c>seed, seed+1, seed+2</c> 番目（巡る）に順に当たる。
    /// 勝った隊は生きている駒だけで次へ（境界は作戦マップと同じ `EngagementEngine.CrossBoundary`・道中の回復 50%・同じ席の直し `Map11State.RepairSeats` の写し）。
    /// 返すのは各戦の（勝ち・その戦に入った枚数・その戦の後に倒れている枚数）。負けたらそこで止まる。
    /// </summary>
    public static List<(bool Won, int In, int Down)> Chain3(FoeWeight.Squad q, int star, StarRule stars, IReadOnlyList<FoeWeight.Foe> grunts, MapStage s, int seed)
    {
        var res = new List<(bool, int, int)>();
        List<UnitState> p = FoeWeight.Players(q, star, stars, relics: false);
        int full = p.Count;
        for (int k = 0; k < 3; k++)
        {
            FoeWeight.Foe foe = grunts[(seed + k) % grunts.Count];
            int inCount = p.Count(u => u.IsAlive);
            BattleResult r = BattleEngine.Run(p, FoeWeight.Enemies(foe, s), Map11.DeriveSeed(seed, k), verbose: false);
            var alive = p.Where(u => u.IsAlive).ToList();
            res.Add((r.PlayerWon, inCount, full - alive.Count));
            if (!r.PlayerWon || alive.Count == 0) break;
            p = EngagementEngine.CrossBoundary(alive, p, new RecoverRule(Map11.RecoverPercent, false));
            RepairSeats(p);
        }
        return res;
    }

    /// <summary>`Map11State.RepairSeats` の写し（同じ席に 2 体立ったときに空いている席へ・測るためだけ）。</summary>
    private static void RepairSeats(List<UnitState> units)
    {
        var taken = new HashSet<int>();
        foreach (UnitState u in units.Where(x => x.IsAlive && !x.Def.Traits.Contains(TraitId.Ephemeral)))
        {
            if (taken.Add(u.Slot)) continue;
            int to = FormationRules.PlayableSlotsOfRow(u.Row).Concat(FormationRules.PlayableSlots).FirstOrDefault(sl => !taken.Contains(sl), -1);
            if (to < 0) continue;
            u.Slot = to;
            taken.Add(to);
        }
    }

    // ---------------- 集計 ----------------

    /// <summary>1 組（隊 × ☆ × 敵部隊 × 硬さ）。</summary>
    public sealed record Cell(AxisSquad Q, int Star, string Stage, FoeWeight.Foe Foe, int Hard, List<Fight> Fights);

    private static double Med(IEnumerable<double> xs)
    {
        var o = xs.OrderBy(x => x).ToList();
        return o.Count == 0 ? double.NaN : o.Count % 2 == 1 ? o[o.Count / 2] : (o[o.Count / 2 - 1] + o[o.Count / 2]) / 2;
    }

    private static string P(double x) => double.IsNaN(x) ? "—" : $"{x:F0}%";
    private static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("0.0", CultureInfo.InvariantCulture);
    private static string Sh(double part, double all) => all <= 0 ? "—" : $"{100 * part / all:F0}%";

    /// <summary>1 群（同じ隊 × ☆ の雑兵の全部など）の 1 行ぶん。</summary>
    private sealed record Agg(int N, double Win, double Hp, double Lost, double Turns, int Capped, double SignReach, double SignMed, bool Readable,
                              double Base, double Boost, double Summon, double Other, double RecvFoe, double RecvAlly, double Armor, double Shots,
                              double FoeDrop, double AllyDrop)
    {
        public double Deal => Base + Boost + Summon + Other;
    }

    private static Agg Sum(IEnumerable<Fight> fs)
    {
        var l = fs.ToList();
        if (l.Count == 0) return new Agg(0, double.NaN, double.NaN, double.NaN, double.NaN, 0, double.NaN, double.NaN, false, 0, 0, 0, 0, 0, 0, 0, double.NaN, double.NaN, double.NaN);
        int n = l.Count;
        var signs = l.Where(f => f.Sign is not null).Select(f => (double)f.Sign!.Value).ToList();
        return new Agg(l.Count, 100.0 * l.Count(f => f.Won) / n, l.Average(f => f.Hp), l.Average(f => f.Lost), Med(l.Select(f => (double)f.Turns)),
            l.Count(f => f.Turns >= BattleEngine.MaxTurns), 100.0 * signs.Count / n, Med(signs), l.Any(f => f.Readable),
            l.Sum(f => f.DealBase) / n, l.Sum(f => f.DealBoost) / n, l.Sum(f => f.DealSummon) / n, l.Sum(f => f.DealOther) / n,
            l.Sum(f => f.RecvFoe) / n, l.Sum(f => f.RecvAlly) / n, l.Sum(f => f.ArmorLost) / n, l.Average(f => (double)f.Shots),
            l.Sum(f => f.FoeAtkBase) <= 0 ? double.NaN : 100 * l.Sum(f => f.FoeAtkDrop) / l.Sum(f => f.FoeAtkBase),
            l.Sum(f => f.AllyAtkBase) <= 0 ? double.NaN : 100 * l.Sum(f => f.AllyAtkDrop) / l.Sum(f => f.AllyAtkBase));
    }

    // ---------------- 口（`--axis-diag`） ----------------

    /// <summary>
    /// `--axis-diag [--quick] [--seeds=20] [--axes=毒,生贄] [--stars=1,2,3] [--rule=V471] [--plays=<dir>] [--cap=2] [--out=<path>]`。
    /// `--quick` は seed 2・軸 2 つ（毒・手番）・写しの隊なし（1 分以内が門）。
    /// </summary>
    public static bool Main(Action<string> write, IReadOnlyList<string> args, string demoAppDir)
    {
        string? Opt(string name) => args.FirstOrDefault(a => a.StartsWith($"--{name}=", StringComparison.Ordinal))?[(name.Length + 3)..];
        bool quick = args.Contains("--quick") || args.Contains("--axis-diag-quick");
        string repo = Path.GetFullPath(Path.Combine(demoAppDir, ".."));
        string root = Path.GetFullPath(Opt("plays") ?? Path.Combine(repo, "design", "plays"), repo);
        string outPath = Path.GetFullPath(Opt("out") ?? (quick ? Path.Combine(repo, ".tmp", "p472", "axis_diag_quick.md") : Path.Combine(repo, "design", "weights", "axis_diag.md")), repo);
        ChapterRule rule = Opt("rule") is { } rk ? ChapterRule.ByKey(rk) ?? throw new ArgumentException($"版が無い: {rk}") : Chapter1.Default;
        int seeds = Opt("seeds") is { } sd ? int.Parse(sd, CultureInfo.InvariantCulture) : quick ? 2 : 20;
        int cap = Opt("cap") is { } cp ? int.Parse(cp, CultureInfo.InvariantCulture) : quick ? 0 : 2;
        var axes = (Opt("axes")?.Split(',').Select(x => x.Trim()).ToArray()) ?? (quick ? new[] { "毒", "手番" } : UnitTags.Axes);
        var stars = (Opt("stars") ?? "1,2,3").Split(',').Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToList();
        StarRule starRule = rule.Stars is { On: true } sr ? sr : StarRule.V464;
        var t0 = DateTime.Now;

        // ---- 隊 ----
        var units = Roster33Audit.Units(RosterRule.V471);
        var snaps = PlayLog.List(root).Where(s => s.Pack is not null).ToList();
        var plays = new[] { "1-1", "1-2", "1-3" }.SelectMany(id => FoeWeight.SquadsOf(snaps, id, root)).ToList();
        var squads = new List<AxisSquad>();
        foreach (string a in axes)
        {
            squads.Add(Representative(a, units));
            if (cap > 0) squads.AddRange(FromPlays(a, plays, cap));
        }
        write($"AXIS_DIAG 版 {rule.Name}・☆の表 {starRule.Name}・軸 {axes.Length}・隊 {squads.Count}（代表 {squads.Count(q => q.Representative)} ・写し {squads.Count(q => !q.Representative)}）・seed {seeds}・☆ {string.Join("/", stars)}");

        // ---- 相手 ----
        var chapter = Chapter1.StagesFor(rule);
        var stages = new[] { (Id: "1-2", S: chapter[1]), (Id: "1-3", S: chapter[2]) };
        // 同じ名前の部隊（道の「第一波 ／ 物見の兵」と湧きの同じ部隊など・同じ編成）は 1 つにまとめる（雑兵の和で 2 回数えないため）。
        var foes = stages.Select(x => (x.Id, x.S, Foes: FoeWeight.FoesOf(x.S).GroupBy(f => f.Name).Select(g => g.First()).ToList())).ToList();
        foreach (var (id, s, fs) in foes)
            write($"AXIS_DIAG {id}（{s.FoeScale.Text}{(s.FoeScale.HeroMends ? "" : "・勇者の回復なし")}）: {string.Join(" ／ ", fs.Select(f => $"{f.Name}（{FoeWeight.KindName(f.Kind)}）"))}");

        // ---- 戦闘 ----
        var cells = new List<Cell>();
        int[] hards = { 1, 2, 3 };
        int fights = 0;
        foreach (AxisSquad q in squads)
            foreach (int star in stars)
                foreach (var (id, s, fs) in foes)
                    foreach (FoeWeight.Foe f in fs)
                        foreach (int k in (q.Representative ? hards : new[] { 1 }))
                        {
                            if (k > 1 && f.Kind is FoeWeight.Kind.Hero or FoeWeight.Kind.Garrison) continue;
                            Formation ef = HpOnly(f.Enemy, k);
                            var list = new List<Fight>();
                            for (int seed = 0; seed < seeds; seed++)
                            {
                                List<UnitState> en = FoeWeight.Enemies(f with { Enemy = ef }, s);
                                list.Add(Run(q.Axis, FoeWeight.Players(q.Squad, star, starRule, relics: false), en, seed));
                                fights++;
                            }
                            cells.Add(new Cell(q, star, id, f, k, list));
                        }
        write($"AXIS_DIAG 単発 {fights} 戦（{(DateTime.Now - t0).TotalSeconds:F0} 秒）");

        // ---- 本陣の守り → 勇者 ・ 連戦 ----
        var siege = new Dictionary<(AxisSquad, int), int>();
        var chains = new Dictionary<(AxisSquad, int), List<List<(bool Won, int In, int Down)>>>();
        foreach (AxisSquad q in squads)
            foreach (int star in stars)
            {
                foreach (var (id, s, fs) in foes)
                {
                    var guards = fs.Where(f => f.Kind == FoeWeight.Kind.Garrison).ToList();
                    if (fs.FirstOrDefault(f => f.Kind == FoeWeight.Kind.Hero) is { } hero && guards.Count > 0 && !s.Hero.Chaser)
                    {
                        var line = guards.Append(hero).ToList();
                        siege[(q, star)] = Enumerable.Range(0, seeds).Count(seed => FoeWeight.SiegeChain(new[] { q.Squad }, line, s, star, starRule, seed, relics: false) == 1);
                    }
                    var grunts = fs.Where(Grunt).ToList();
                    if (!chains.TryGetValue((q, star), out var cl)) chains[(q, star)] = cl = new();
                    for (int seed = 0; seed < seeds; seed++) cl.Add(Chain3(q.Squad, star, starRule, grunts, s, seed));
                }
            }
        write($"AXIS_DIAG 連戦・守り → 勇者 済み（{(DateTime.Now - t0).TotalSeconds:F0} 秒）");

        // ---- 胞子・亡者は隊の列に残るか（§2 の 1 段落のための確かめ・生贄の代表 ☆1 ・1-2 の最初の雑兵 ・seed 0） ----
        string summonNote = "";
        if (squads.FirstOrDefault(q => q.Representative && q.Axis == "生贄") is { } sac)
        {
            var (id, s, fs) = foes[0];
            var p = FoeWeight.Players(sac.Squad, 1, starRule, relics: false);
            int before = p.Count;
            BattleResult r = BattleEngine.Run(p, FoeWeight.Enemies(fs.First(Grunt), s), 0, verbose: true);
            int summoned = r.Events.Count(e => e.Kind == BattleEventKind.Summon && e.Team == BattleContext.PlayerTeam);
            summonNote = $"生贄の代表・☆1・{id} の {fs.First(Grunt).Name}・seed 0: 戦闘で湧いた味方 {summoned} 体・戦闘の前の隊の列 {before} 枚 → 後 {p.Count} 枚（湧いた駒は隊の列に入らない＝次の戦闘へ持ち越さない）・倒れた {p.Count(u => !u.IsAlive)} 枚";
            write($"AXIS_DIAG {summonNote}");
        }

        var lines = Lines(rule, starRule, seeds, stars, squads, foes.Select(x => (x.Id, x.S, x.Foes)).ToList(), cells, siege, chains, summonNote, fights, DateTime.Now - t0);
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        File.WriteAllText(outPath, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
        write($"AXIS_DIAG {lines.Count} 行 → {outPath}（{(DateTime.Now - t0).TotalSeconds:F0} 秒）");
        return cells.Count > 0;
    }

    // ---------------- 表 ----------------

    private static List<string> Lines(ChapterRule rule, StarRule starRule, int seeds, List<int> stars, List<AxisSquad> squads,
                                      List<(string Id, MapStage S, List<FoeWeight.Foe> Foes)> foes, List<Cell> cells,
                                      Dictionary<(AxisSquad, int), int> siege, Dictionary<(AxisSquad, int), List<List<(bool Won, int In, int Down)>>> chains,
                                      string summonNote, int fights, TimeSpan took)
    {
        var L = new List<string>
        {
            "# 軸の診断（第472期）",
            "",
            $"`--axis-diag` の出力（{DateTime.Now:yyyy-MM-dd HH:mm}・{fights} 戦 ・{took.TotalSeconds:F0} 秒）。手で編集しない（次の実行で上書きされる）。指示書 `design/PHASE472_AXIS_DIAG_SPEC.md`。**調べるだけ——規則は動かしていない。**",
            "",
            $"- 版: {rule.Name}（☆の表 {starRule.Name}）・相手は 1 章の 1-2 ／ 1-3 の敵部隊の全部（倍率込み）・seed 0〜{seeds - 1}・レリックなし・全員☆{string.Join(" ／ ☆", stars)}",
            $"- 相手: " + string.Join(" ／ ", foes.Select(x => $"{x.Id}（{x.S.FoeScale.Text}{(x.S.FoeScale.HeroMends ? "" : "・勇者の回復なし")}）{string.Join("・", x.Foes.Select(f => $"{f.Name}〔{FoeWeight.KindName(f.Kind)}〕"))}")),
            "- 「雑兵」＝勇者と本陣の守り以外の部隊。勝ち ・終わりの HP（隊の最大 HP に対する %・負けは 0）・失った枚数は `--foe-weight` と同じ",
            "- 与えたダメージ（1 戦あたり・HP が実際に減った量）: **素の殴り**（手番の振りで当てた分のうち素の攻撃力ぶん）／ **上乗せ**（同じ振りのうちターン頭の攻撃力が素の値を超えたぶん・層や号令）／ **呼んだ駒**（胞子・亡者などの振り）／ **殴り以外**（毒 ・燃焼の刻み・爆ぜ・撃ち・反撃・呪いの共有・敵どうし）",
            "- 受けた: 敵から ／ 味方から（巻き込み ・漏れ・自傷）。**破片の減り** は味方の破片が減った延べ（受け止めた量に、ガレが味方の破片を砕いた量が混ざる——分けられない）",
            "- 攻撃の下がり: ターン頭の攻撃力が素の値より下がっていた量の和 ÷ 素の値の和（敵 ／ 味方・呼んだ駒は除く）",
            "",
        };

        // ---- 隊の中身 ----
        L.Add("## 隊の中身（ポンが「その組み方はしない」と直せるように）");
        L.Add("");
        L.Add("代表 ＝ その軸の駒を 33 体から全部 ＋ 足りない席は `compare` の行でその軸の駒と同じ行に多く載る駒。席は `compare` の行の席（重なる駒の多い行から）。写し ＝ ポンの写しのうちその軸がいちばん多い隊（同じ顔ぶれは 1 つ・軸ごとに 2 隊まで）。");
        L.Add("");
        L.Add("| 軸 | 隊 | 中身（席・駒・置き方） |");
        L.Add("|---|---|---|");
        foreach (AxisSquad q in squads) L.Add($"| {q.Axis}{(Weak.Contains(q.Axis) ? "（弱い）" : "")} | {(q.Representative ? "代表" : $"写し {q.Squad.Name}")} | {string.Join(" ／ ", q.How)} |");

        // ---- いつ手に入るか（加入の表） ----
        RosterRule roster = RosterRule.V471;
        string[] order = { "始め", "1-1", "1-2", "1-3" };
        string When(UnitDef d) => roster.Start.Contains(d.Name) ? "始め" : roster.Joins.FirstOrDefault(j => j.Units.Contains(d.Name)).Stage ?? "—";
        int Idx(string w) => Array.IndexOf(order, w) is int i && i >= 0 ? i : 99;
        string By(IEnumerable<UnitDef> ds, int n) { var ws = ds.Select(d => Idx(When(d))).OrderBy(x => x).ToList(); return ws.Count < n || ws[n - 1] >= 99 ? "—" : order[ws[n - 1]]; }
        var units33 = Roster33Audit.Units(roster);
        L.Add("");
        L.Add($"## 軸の駒がいつ手に入るか（加入の表 `{roster.Name}`）");
        L.Add("");
        L.Add("「始め」＝始めの 6 枚・ほかはそのステージから呼べる（呼べるだけで、配られるかは引き次第）。「そろう」＝そのステージまでに呼べる軸の駒の数が n 枚に届く最初のステージ。");
        L.Add("");
        L.Add("| 軸 | 駒（加わるステージ） | 2 枚そろう | 3 枚そろう | 代表の隊（5 枚）がそろう |");
        L.Add("|---|---|---|---|---|");
        foreach (string a in UnitTags.Axes)
        {
            var ds = units33.Where(d => d.Tags.Contains(a)).ToList();
            var rep = squads.FirstOrDefault(q => q.Representative && q.Axis == a);
            L.Add($"| {a}{(Weak.Contains(a) ? "（弱い）" : "")} | {string.Join("・", ds.Select(d => $"{d.Name}（{When(d)}）"))} | {By(ds, 2)} | {By(ds, 3)} | {(rep is null ? "—" : By(rep.Squad.Pieces.Select(p => p.Base), rep.Squad.Pieces.Count))} |");
        }

        // ---- 順位表 ----
        var reps = squads.Where(q => q.Representative).ToList();
        foreach (int star in stars)
        {
            L.Add("");
            L.Add($"## 10 軸の順位表 —— 全員☆{star}（1 章の今の敵・代表の隊）");
            L.Add("");
            L.Add("順は雑兵の勝ち → 終わりの HP。勇者は 1 隊で当てた勝ち（門番 ＝ 1-2・追う勇者 ＝ 1-3）。守り → 勇者 ＝ 1-2 の本陣の守りから勇者まで同じ隊で続けて戦って勇者を倒した seed の数。");
            L.Add("");
            L.Add("| 順 | 軸 | 雑兵の勝ち | 終わりの HP | 失った枚数 | 決着T（中央値） | 30T 打ち切り | 勇者 1-2 | 勇者 1-3 | 守り → 勇者 |");
            L.Add("|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|");
            int k = 0;
            foreach (var (q, g) in reps.Select(q => (q, g: Sum(cells.Where(c => c.Q == q && c.Star == star && c.Hard == 1 && Grunt(c.Foe)).SelectMany(c => c.Fights))))
                         .OrderByDescending(x => x.g.Win).ThenByDescending(x => x.g.Hp))
            {
                string Hero(string id) { var hc = cells.Where(c => c.Q == q && c.Star == star && c.Stage == id && c.Foe.Kind == FoeWeight.Kind.Hero).SelectMany(c => c.Fights).ToList(); return hc.Count == 0 ? "—" : $"{hc.Count(f => f.Won)}／{hc.Count}"; }
                L.Add($"| {++k} | {q.Axis}{(Weak.Contains(q.Axis) ? "（弱い）" : "")} | {P(g.Win)} | {P(g.Hp)} | {F1(g.Lost)} | {F1(g.Turns)} | {g.Capped}／{g.N} | {Hero("1-2")} | {Hero("1-3")} | {(siege.TryGetValue((q, star), out int sg) ? $"{sg}／{seeds}" : "—")} |");
            }
        }

        // ---- 軸 × ☆ の詳しい表 ----
        L.Add("");
        L.Add("## 軸 × ☆（雑兵の全部・1 戦あたり）");
        L.Add("");
        L.Add("看板 ＝ " + string.Join(" ／ ", Weak.Select(a => $"{a}: {SignText(a)}")) + "。「届いた」は届いた戦の割合、「T」は届いた戦の中央値（ターン頭の写しで読むので、そのターンの中で働き始めたものは次のターンに出る）。");
        L.Add("");
        L.Add("| 軸 | 隊 | ☆ | 勝ち | HP | 決着T | 看板 届いた ／ T | 与えた | 素の殴り | 上乗せ | 呼んだ駒 | 殴り以外 | 受けた（敵から） | 味方から | 破片の減り | ガレの撃ち（回 ／ 戦） | 敵の攻撃の下がり | 味方の攻撃の下がり |");
        L.Add("|---|---|--:|--:|--:|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (AxisSquad q in squads)
            foreach (int star in stars)
            {
                Agg g = Sum(cells.Where(c => c.Q == q && c.Star == star && c.Hard == 1 && Grunt(c.Foe)).SelectMany(c => c.Fights));
                string sign = !g.Readable ? "—" : $"{P(g.SignReach)} ／ {F1(g.SignMed)}";
                L.Add($"| {q.Axis} | {(q.Representative ? "代表" : q.Squad.Name)} | ☆{star} | {P(g.Win)} | {P(g.Hp)} | {F1(g.Turns)} | {sign} | {g.Deal:F0} | {Sh(g.Base, g.Deal)} | {Sh(g.Boost, g.Deal)} | {Sh(g.Summon, g.Deal)} | {Sh(g.Other, g.Deal)} | {g.RecvFoe:F0} | {g.RecvAlly:F0} | {g.Armor:F0} | {F1(g.Shots)} | {P(g.FoeDrop)} | {P(g.AllyDrop)} |");
            }

        // ---- 勇者（1 隊で当てた） ----
        L.Add("");
        L.Add("## 勇者に 1 隊で当てた（代表の隊・1 戦あたり）");
        L.Add("");
        L.Add("| 軸 | ☆ | 勇者 | 勝ち | 決着T | 看板 届いた ／ T | 与えた | 素の殴り | 上乗せ | 呼んだ駒 | 殴り以外 | 受けた（敵から） | 敵の攻撃の下がり | 味方の攻撃の下がり |");
        L.Add("|---|--:|---|--:|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (AxisSquad q in reps)
            foreach (int star in stars)
                foreach (Cell c in cells.Where(c => c.Q == q && c.Star == star && c.Foe.Kind == FoeWeight.Kind.Hero))
                {
                    Agg g = Sum(c.Fights);
                    L.Add($"| {q.Axis} | ☆{star} | {c.Stage} {c.Foe.Name} | {c.Fights.Count(f => f.Won)}／{c.Fights.Count} | {F1(g.Turns)} | {(!g.Readable ? "—" : $"{P(g.SignReach)} ／ {F1(g.SignMed)}")} | {g.Deal:F0} | {Sh(g.Base, g.Deal)} | {Sh(g.Boost, g.Deal)} | {Sh(g.Summon, g.Deal)} | {Sh(g.Other, g.Deal)} | {g.RecvFoe:F0} | {P(g.FoeDrop)} | {P(g.AllyDrop)} |");
                }

        // ---- 連戦 ----
        L.Add("");
        L.Add("## 続けて 3 戦（H3・HP と倒れた駒を持ち越す・勝った後に道中の回復 50%）");
        L.Add("");
        L.Add("通し ＝ 1-2 ・1-3 それぞれの雑兵の列を seed ずつずらして 3 部隊（seed × 2 ステージ）。「n 戦目まで勝った」は通しの割合。「倒れている」は n 戦目の後に倒れている駒の平均（勝った通しだけ・呼んだ駒は数えない）。");
        L.Add("");
        L.Add("| 軸 | 隊 | ☆ | 1 戦目まで勝った | 2 戦目まで | 3 戦目まで | 1 戦目の後に倒れている | 2 戦目の後 | 3 戦目の後 |");
        L.Add("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (AxisSquad q in squads)
            foreach (int star in stars)
            {
                var cl = chains[(q, star)];
                string Upto(int n) => P(100.0 * cl.Count(c => c.Count >= n && c.Take(n).All(x => x.Won)) / Math.Max(1, cl.Count));
                string Down(int n) { var xs = cl.Where(c => c.Count >= n && c.Take(n).All(x => x.Won)).Select(c => (double)c[n - 1].Down).ToList(); return xs.Count == 0 ? "—" : F1(xs.Average()); }
                L.Add($"| {q.Axis} | {(q.Representative ? "代表" : q.Squad.Name)} | ☆{star} | {Upto(1)} | {Upto(2)} | {Upto(3)} | {Down(1)} | {Down(2)} | {Down(3)} |");
            }

        // ---- 長い戦闘 ----
        foreach (int star in stars)
        {
            L.Add("");
            L.Add($"## 長い戦闘での順位（H1・全員☆{star}・代表の隊）");
            L.Add("");
            L.Add("雑兵の最大HP だけ ×1 ／ ×2 ／ ×3（攻撃・札はそのまま）。各列は 雑兵の勝ち ・終わりの HP ・決着T（中央値）・（その列の順位）。勇者は素のまま（1-2 ・1-3 の 1 隊の勝ちの和）。");
            L.Add("");
            L.Add("| 軸 | ×1 | ×2 | ×3 | 順位 ×1 → ×3 | 勇者 |");
            L.Add("|---|---|---|---|---|--:|");
            var rank = new Dictionary<(AxisSquad, int), int>();
            foreach (int hard in new[] { 1, 2, 3 })
            {
                int i = 0;
                foreach (var x in reps.Select(q => (q, g: Sum(cells.Where(c => c.Q == q && c.Star == star && c.Hard == hard && Grunt(c.Foe)).SelectMany(c => c.Fights))))
                             .OrderByDescending(x => x.g.Win).ThenByDescending(x => x.g.Hp))
                    rank[(x.q, hard)] = ++i;
            }
            foreach (AxisSquad q in reps.OrderBy(q => rank[(q, 3)]))
            {
                string Col(int hard) { Agg g = Sum(cells.Where(c => c.Q == q && c.Star == star && c.Hard == hard && Grunt(c.Foe)).SelectMany(c => c.Fights)); return $"{P(g.Win)} ・{P(g.Hp)} ・{F1(g.Turns)}T（{rank[(q, hard)]} 位）"; }
                var hc = cells.Where(c => c.Q == q && c.Star == star && c.Foe.Kind == FoeWeight.Kind.Hero).SelectMany(c => c.Fights).ToList();
                int d = rank[(q, 1)] - rank[(q, 3)];
                L.Add($"| {q.Axis}{(Weak.Contains(q.Axis) ? "（弱い）" : "")} | {Col(1)} | {Col(2)} | {Col(3)} | {rank[(q, 1)]} → {rank[(q, 3)]}（{(d > 0 ? "+" : "")}{d}） | {hc.Count(f => f.Won)}／{hc.Count} |");
            }
        }

        // ---- 敵部隊ごと（☆1 だけ） ----
        L.Add("");
        L.Add($"## 敵部隊ごとの勝ち（代表の隊・全員☆{stars[0]}・素の硬さ）");
        L.Add("");
        var fl = foes.SelectMany(x => x.Foes.Select(f => (x.Id, f))).ToList();
        L.Add("| 軸 | " + string.Join(" | ", fl.Select(x => $"{x.Id} {x.f.Name}")) + " |");
        L.Add("|---|" + string.Concat(fl.Select(_ => "--:|")));
        foreach (AxisSquad q in reps)
            L.Add($"| {q.Axis} | " + string.Join(" | ", fl.Select(x =>
            {
                var c = cells.FirstOrDefault(c => c.Q == q && c.Star == stars[0] && c.Hard == 1 && c.Stage == x.Id && c.Foe == x.f);
                return c is null ? "—" : $"{c.Fights.Count(f => f.Won)}／{c.Fights.Count}";
            })) + " |");

        if (summonNote.Length > 0) { L.Add(""); L.Add($"- {summonNote}"); }
        return L;
    }
}
