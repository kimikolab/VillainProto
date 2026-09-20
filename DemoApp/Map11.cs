using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 検証用マップ 1-1（第169期）
//
// **第168期 部B で3つの線を同時に通した1点（`S4/S4` × 道中の回復 50%）を、そのまま写しただけ。**
// 数値・敵・隊は第168期から1つも変えていない。**ここには Godot の型を1つも使わない**
// ——同じ規則を頭なしの自己検査（`--map11-verify`）からも回すため。
//
//   北の道: 第二波・先遣（後1 施しの司祭長を抜く） → 第三波（5体）
//   南の道: 第四波・先遣（後1 詠唱兵を抜く）       → 第五波（5体）
//
// 先遣隊の定義は `EnemyCatalog.Vanguards`（`Stages` / `Columns` には載っていない）。
// =====================================================================================

/// <summary>マップ 1-1 の定義。<b>静的な内容だけ</b>で、進行の状態は <see cref="Map11State"/> が持つ。</summary>
public static class Map11
{
    /// <summary>道中の回復（%）。<b>勝った隊だけが、次の戦闘へ入る前に戻す。</b> 死者は戻らない。</summary>
    public const int RecoverPercent = 50;

    /// <summary>1 マップの戦闘回数の上限（第168期 <c>BandCap</c> と同じ）。</summary>
    public const int BattleCap = 24;

    public const int RoadCount = 2;
    public static readonly string[] RoadNames = { "北の道", "南の道" };

    /// <summary>
    /// 道の 1 区画。<c>Index</c> は 0 が手前（先に当たる側）。
    /// <c>StageIndex</c> は<b>背景の選択にしか使わない</b>（先遣も元の波と同じ背景で描く）。
    /// </summary>
    public sealed record RoadNode(int Road, int Index, string Name, Formation Enemy, int StageIndex);

    /// <summary>道ごとの敵部隊。<b>手前を抜かないと奥に当たれない。</b></summary>
    public static IReadOnlyList<RoadNode>[] Roads { get; } =
    {
        new[]
        {
            new RoadNode(0, 0, EnemyCatalog.Vanguards[0].Name, EnemyCatalog.Vanguards[0].Enemy, 1),
            new RoadNode(0, 1, EnemyCatalog.Stages[2].Name,    EnemyCatalog.Stages[2].Enemy,    2),
        },
        new[]
        {
            new RoadNode(1, 0, EnemyCatalog.Vanguards[1].Name, EnemyCatalog.Vanguards[1].Enemy, 3),
            new RoadNode(1, 1, EnemyCatalog.Stages[4].Name,    EnemyCatalog.Stages[4].Enemy,    4),
        },
    };

    public static int TotalNodes => Roads.Sum(r => r.Count);

    // ---------------- 隊 ----------------

    public sealed record SquadDef(string Id, string Name, string Role, Formation F);

    private const string RowKado = "反撃 (ヒサ×カド)";
    private const string RowHane = "突き返し (ハネ×ウツ)";
    private const string RowHold = "死軸×ヒヨ (ゾト×火選り)";

    private static Formation RowOf(string name) =>
        Presets.Compare.FirstOrDefault(r => r.Name == name).F
        ?? throw new InvalidOperationException("`Presets.Compare` にその行が無い: " + name);

    /// <summary>
    /// ハネ隊。<b>席はその行のまま</b>で、カド隊と重なる 前3 ガルドだけを<b>軋みのヨミ</b>に替える
    /// ——第168期 §2-2 が「どこでも同じ」群の上位5枚を測って選んだ1枚（ハネ隊の第2〜5波の
    /// 単発勝率の平均が最も高い）。<b>ここでは選び直さず、その結果を固定で写す。</b>
    /// </summary>
    private static Formation HaneSquad()
    {
        Formation kado = RowOf(RowKado), src = RowOf(RowHane);
        var inKado = new HashSet<string>(kado.Occupied().Select(o => o.Def.Id), StringComparer.Ordinal);
        var f = new Formation();
        foreach ((int slot, UnitDef def) in src.Occupied())
            f[slot] = inKado.Contains(def.Id) ? UnitCatalog.Yomi : def;
        return f;
    }

    /// <summary>
    /// 3 隊。先頭 2 隊が最初から拠点にいて、3 番目が控え。
    /// <b>第172期に「ハネ隊」を「かき回し隊」へ改名した</b>（指示書 §3）——隊を動かしているのは
    /// バサ・ヨミ・ウツ・ドルガで、<b>看板に偽りがあった</b>（`Id` は `hane` のまま。
    /// 器具（第168期）の行名と突き合わせるときに追えなくなるため）。
    /// </summary>
    public static SquadDef[] Squads { get; } =
    {
        new("kado", "カド隊", "反撃・棘", RowOf(RowKado)),
        new("hane", "かき回し隊", "突き返し", HaneSquad()),
        new("hold", "控え隊", "死軸・火選り", RowOf(RowHold)),
    };

    // ---------------- 控えの駒（第172期 §1-2） ----------------

    /// <summary>控えの駒の枚数（指示書 §1-2）。</summary>
    public const int ReserveCount = 6;

    /// <summary>
    /// 名指しの2枚（指示書 §1-2）。<b>ポンが「ヒヨの代わり」を試せるようにするための指定。</b>
    /// </summary>
    private static readonly UnitDef[] NamedReserves = { UnitCatalog.Kubi, UnitCatalog.Sekki };

    /// <summary>候補1枚ぶんの素性（<c>--map11-phase172</c> がそのまま表にする）。</summary>
    public sealed record ReserveRow(UnitDef Def, int Seats, int[] WithSquad, bool AllThree, int Total);

    /// <summary>
    /// 残り4枚の候補を、<b>規則で</b>並べる（指示書 §1-2）。手で選ばない。
    ///
    /// <para>線は3つ——(1) 3 隊 15 枚と重ならない ／ (2) `Presets.Compare` の在席枠が 3 以上 ／
    /// (3) <b>カド隊・かき回し隊・控え隊それぞれの駒と同じ行に入った実績がある</b>。
    /// 通った駒を「同席した (行, 相手) の組の数」の多い順に並べ、同数なら
    /// <see cref="UnitCatalog.All"/> の並び順で割る。</para>
    /// </summary>
    public static ReserveRow[] ReserveRanking()
    {
        var rows = Presets.Compare;
        var squadIds = Squads.Select(s => s.F.Occupied().Select(o => o.Def.Id).ToHashSet(StringComparer.Ordinal))
                             .ToArray();
        var taken = new HashSet<string>(squadIds.SelectMany(x => x), StringComparer.Ordinal);
        foreach (UnitDef d in NamedReserves) taken.Add(d.Id);

        var list = new List<ReserveRow>();
        foreach (UnitDef def in UnitCatalog.All)
        {
            if (taken.Contains(def.Id)) continue;
            int seats = rows.Sum(r => r.F.Occupied().Count(o => o.Def.Id == def.Id));
            var with = new int[Squads.Length];
            foreach (var r in rows)
            {
                var ids = r.F.Occupied().Select(o => o.Def.Id).ToHashSet(StringComparer.Ordinal);
                if (!ids.Contains(def.Id)) continue;
                for (int s = 0; s < Squads.Length; s++)
                    with[s] += ids.Count(x => squadIds[s].Contains(x));
            }
            bool all3 = with.All(x => x > 0);
            list.Add(new ReserveRow(def, seats, with, all3 && seats >= 3, with.Sum()));
        }
        return list
            .OrderByDescending(x => x.AllThree)
            .ThenByDescending(x => x.Total)
            .ThenBy(x => UnitCatalog.All.ToList().FindIndex(u => u.Id == x.Def.Id))
            .ToArray();
    }

    /// <summary>
    /// 控えの駒 6 枚。<b>名指しの2枚 ＋ 規則で選んだ4枚</b>（<see cref="ReserveRanking"/>）。
    /// <b>ポンが差し替える前提</b>——差し替えるなら <see cref="NamedReserves"/> に足すだけでよい。
    /// </summary>
    public static UnitDef[] Reserves { get; } = NamedReserves
        .Concat(ReserveRanking().Where(x => x.AllThree).Select(x => x.Def))
        .Take(ReserveCount)
        .ToArray();

    // ---------------- 駒カードの一行（指示書 §3） ----------------

    /// <summary>
    /// 第164〜168期で<b>一行が書けた駒だけ</b>（4 枚）。書けなかった駒には何も出さない
    /// ——第164期は 25 体中 11 体しか書けず、残りは「不明」と書いた。画面では出さない。
    /// </summary>
    private static readonly Dictionary<string, string> Lines = new(StringComparer.Ordinal)
    {
        [UnitCatalog.Kado.Id] = "粛の敵が揃っていると何もできない。粛のいない道へ",
        [UnitCatalog.Hane.Id] = "重い敵が後に来る道で働く",
        [UnitCatalog.Golm.Id] = "重い敵が先に来る道で働く",
        [UnitCatalog.Basa.Id] = "戦う回数が多い道で働く",
    };

    public static string? LineOf(string unitId) => Lines.GetValueOrDefault(unitId);

    /// <summary>この部隊が持つ盤面ルール／敵側の札のうち、名前を出す価値のあるもの。</summary>
    public static string RuleLineOf(Formation f)
    {
        var names = new List<string>();
        foreach ((int _, UnitDef def) in f.Occupied())
            foreach (TraitId t in def.Traits)
            {
                string? label = t switch
                {
                    TraitId.Hush => "粛（ターン外の行動が止まる）",
                    TraitId.Drought => "渇き（回復が通らない）",
                    TraitId.Yoke => "軛（1発が25で切られる）",
                    TraitId.Inversion => "逆位（速さの向きが反転する）",
                    TraitId.Condemn => "断罪（反撃すると痺れる）",
                    _ => null,
                };
                if (label is not null && !names.Contains(label)) names.Add(label);
            }
        return names.Count == 0 ? "盤面ルールなし" : string.Join(" / ", names);
    }

    /// <summary>
    /// 各戦闘の seed を親 seed から決定的に導く。
    /// <b><c>EngagementEngine</c> の private な <c>DeriveSeed</c> と同じ式</b>——第168期の器具が
    /// この式で回しているので、自己検査 (c) を成立させるにはここも同じでなければならない。
    /// </summary>
    public static int DeriveSeed(int seed, int battleIndex) => unchecked(seed * 1000003 + battleIndex);
}

/// <summary>
/// マップ 1-1 の進行。<b>盤面の規則は <see cref="BattleEngine"/> と
/// <see cref="EngagementEngine.CrossBoundary"/> しか呼ばない</b>（判定はこのクラスに1つも無い）。
///
/// <para>遊ぶ側（<c>Map11Main</c>）と頭なしの自己検査（<c>Map11Verify</c>）が
/// <b>同じこのクラスを回す</b>。違うのは「どの隊をどの道へ出すか」を誰が決めるかだけ。</para>
/// </summary>
public sealed class Map11State
{
    public sealed class Squad
    {
        public required Map11.SquadDef Def { get; init; }
        public required int Index { get; init; }
        /// <summary>盤上に出ている駒。<c>null</c> ＝ まだ出ていない（控え）／全滅した。</summary>
        public List<UnitState>? Units { get; set; }
        /// <summary>向かっている道。-1 ＝ 拠点で待機。</summary>
        public int Road { get; set; } = -1;
        /// <summary>
        /// <b>最初に送り出された道</b>（-1 ＝ まだ出していない）。
        /// <c>Cleared</c> はこの道で抜いた数だけを数える——第168期 <c>BandOnce</c> の
        /// <c>clearedBy</c> が「担当の道」で数えているのと同じ（2 本抜き率の分子）。
        /// </summary>
        public int Home { get; set; } = -1;
        public bool Deployed { get; set; }
        public bool Lost { get; set; }
        /// <summary>この隊が<b>自分の担当の道で</b>抜いた敵部隊の数。</summary>
        public int Cleared { get; set; }
        public bool OnMap => Units is not null;
    }

    public sealed class Node
    {
        public required Map11.RoadNode Def { get; init; }
        public List<UnitState>? Units { get; set; }
        public bool Cleared { get; set; }
        public int DefMaxHp { get; init; }
        /// <summary>いまの残 HP（まだ当たっていない部隊は満タン）。</summary>
        public int HpNow { get; set; }
    }

    public int Seed { get; }
    public int Battles { get; private set; }
    public Squad[] Squads { get; }
    public Node[][] Nodes { get; }

    /// <summary>
    /// 控えの駒（第172期 §1-2）。<b>席を持たない駒の置き場</b>で、拠点の隊とだけ行き来できる。
    /// <see cref="Map11.Reserves"/> をそのまま実体化したもの——<b>乱数を1つも引かない</b>ので、
    /// 組み直しを1度もしない通しは第169期と1ビットも違わない（自己検査 (a)）。
    /// </summary>
    public List<UnitState> Bench { get; }

    public Map11State(int seed)
    {
        Seed = seed;
        Bench = Map11.Reserves
            .Select(d => BattleEngine.Materialize(Formation.Build(front1: d), BattleContext.PlayerTeam)[0])
            .ToList();
        Squads = Map11.Squads.Select((d, i) => new Squad { Def = d, Index = i }).ToArray();
        Nodes = Map11.Roads.Select(r => r.Select(n =>
        {
            int hp = n.Enemy.Occupied().Sum(o => o.Def.MaxHp);
            return new Node { Def = n, DefMaxHp = hp, HpNow = hp };
        }).ToArray()).ToArray();
    }

    public int ClearedCount => Nodes.Sum(r => r.Count(n => n.Cleared));
    public bool AllCleared => ClearedCount >= Map11.TotalNodes;

    /// <summary>出せる隊が1つも残っていない（盤上に居らず、かつ出していないわけでもない）。</summary>
    public bool NoSquadsLeft => Squads.All(s => s.Units is null && (s.Lost || s.Deployed));

    public bool Finished => AllCleared || NoSquadsLeft || Battles >= Map11.BattleCap;
    public bool Won => AllCleared;

    /// <summary>道 <paramref name="road"/> で次に当たる区画。道が抜け切っていれば null。</summary>
    public Node? NextNode(int road)
    {
        foreach (Node n in Nodes[road]) if (!n.Cleared) return n;
        return null;
    }

    /// <summary>出撃させる（拠点 → 道）。控えはここで初めて盤上に出る。</summary>
    public void Send(int squadIndex, int road)
    {
        Squad s = Squads[squadIndex];
        if (s.Lost) return;
        if (!CanSend(squadIndex)) return;
        s.Road = road;
        if (s.Home < 0) s.Home = road;
        // 組み直しで先に実体化していることがあるので `??=`。**第169期と同値**
        // ——あの版で「`Units` が null でなく `Deployed` が偽」になる道は1本も無かった。
        s.Units ??= BattleEngine.Materialize(s.Def.F, BattleContext.PlayerTeam);
        s.Deployed = true;
    }

    // =============================================================================
    // 拠点での組み直し（第172期 部A）
    //
    // **戦闘・回復・勝敗の規則は1行も触っていない。** ここでするのは
    // 「拠点にいる隊の `UnitState` を、どの席に置くか」の書き換えだけ。
    // 組み直しを1度もしなければ第169期と1ビットも違わない（自己検査 (a)）。
    // =============================================================================

    /// <summary>その隊を組み直せるか（<b>拠点にいる隊だけ</b>。道の上と全滅した隊は不可）。</summary>
    public bool CanReform(int squadIndex)
    {
        Squad s = Squads[squadIndex];
        return !s.Lost && s.Road < 0;
    }

    /// <summary>出せるか。<b>0 枚の隊は出せない</b>（指示書 §1-1）。</summary>
    public bool CanSend(int squadIndex)
    {
        Squad s = Squads[squadIndex];
        if (s.Lost) return false;
        return s.Units is not { } u || u.Any(x => x.IsAlive);
    }

    /// <summary>
    /// 拠点の隊の駒を、動かせる形（<see cref="UnitState"/> のリスト）にする。
    /// <b>触ったときだけ実体化する</b>——触らない隊は <c>Units</c> が null のままで、
    /// <see cref="Send"/> がこれまでどおり定義から作る。
    /// </summary>
    private List<UnitState> Roster(int squadIndex)
    {
        Squad s = Squads[squadIndex];
        return s.Units ??= BattleEngine.Materialize(s.Def.F, BattleContext.PlayerTeam);
    }

    /// <summary>席の昇順に整える。<b>engine は渡した並びで <c>InstanceId</c> を振る</b>ので、
    /// <c>Materialize</c> / <c>CrossBoundary</c> と同じ並びに保つ。</summary>
    private static void SortBySlot(List<UnitState> units) => units.Sort((a, b) => a.Slot.CompareTo(b.Slot));

    /// <summary>
    /// 組み直しのために席の駒をつまむ。<b>拠点の隊なら、まだ実体化していなくてもここで実体化する</b>
    /// ——定義のままの隊（`Units` が null）でも 1 枚目からつまめるようにするため。
    /// </summary>
    public UnitState? PickUp(int squadIndex, int slot)
        => CanReform(squadIndex)
            ? Roster(squadIndex).FirstOrDefault(u => u.Slot == slot)
            : UnitAt(squadIndex, slot);

    /// <summary>その席に立っている駒（空席なら null）。<b>読むだけ</b>——実体化はしない。</summary>
    public UnitState? UnitAt(int squadIndex, int slot)
    {
        Squad s = Squads[squadIndex];
        if (s.Units is { } u) return u.FirstOrDefault(x => x.Slot == slot);
        return null;
    }

    /// <summary>
    /// 席どうしを入れ替える（<b>同じ隊の中も、拠点にいる隊どうしも同じ操作</b>）。
    /// 片方が空席なら移動になる。どちらも拠点にいないと何もしない。
    /// </summary>
    public bool SwapSeats(int squadA, int slotA, int squadB, int slotB)
    {
        if (!CanReform(squadA) || !CanReform(squadB)) return false;
        if (squadA == squadB && slotA == slotB) return false;
        List<UnitState> a = Roster(squadA), b = Roster(squadB);
        UnitState? ua = a.FirstOrDefault(u => u.Slot == slotA);
        UnitState? ub = b.FirstOrDefault(u => u.Slot == slotB);
        if (ua is null && ub is null) return false;
        if (ua is not null) a.Remove(ua);
        if (ub is not null) b.Remove(ub);
        if (ua is not null) { ua.Slot = slotB; b.Add(ua); }
        if (ub is not null) { ub.Slot = slotA; a.Add(ub); }
        SortBySlot(a);
        if (!ReferenceEquals(a, b)) SortBySlot(b);
        return true;
    }

    /// <summary>
    /// 席と控えの駒を入れ替える。<paramref name="benchIndex"/> が範囲外なら
    /// 「その駒を控えへ下げる」（席が空く）。席が空なら「控えの駒をそこへ入れる」。
    /// </summary>
    public bool SwapWithBench(int squadIndex, int slot, int benchIndex)
    {
        if (!CanReform(squadIndex)) return false;
        List<UnitState> a = Roster(squadIndex);
        UnitState? seat = a.FirstOrDefault(u => u.Slot == slot);
        UnitState? bench = benchIndex >= 0 && benchIndex < Bench.Count ? Bench[benchIndex] : null;
        if (seat is null && bench is null) return false;
        int at = bench is null ? Bench.Count : benchIndex;
        if (seat is not null) a.Remove(seat);
        if (bench is not null) { Bench.RemoveAt(benchIndex); bench.Slot = slot; a.Add(bench); }
        if (seat is not null) Bench.Insert(Math.Min(at, Bench.Count), seat);
        SortBySlot(a);
        return true;
    }

    /// <summary>
    /// その隊を既定の編成へ戻す（指示書 §1-3 の「元に戻す」）。
    /// <b>拠点にある駒からしか集められない</b>——道の上・全滅した隊にいる駒は戻せないので、
    /// 1 枚でも欠けていれば何もしない（<c>false</c> を返す）。
    /// 押し出された駒は控えへ下がる。
    /// </summary>
    public bool ResetSquad(int squadIndex)
    {
        if (!CanReform(squadIndex)) return false;
        var want = Squads[squadIndex].Def.F.Occupied().ToArray();

        // 拠点にある駒を全部集める（どの隊のどの席にいるか／控えの何番目か）。
        UnitState? Find(string id)
        {
            for (int s = 0; s < Squads.Length; s++)
                if (CanReform(s) && Squads[s].Units is { } u
                    && u.FirstOrDefault(x => x.Def.Id == id) is { } hit) return hit;
            return Bench.FirstOrDefault(x => x.Def.Id == id);
        }

        var found = want.Select(w => Find(w.Def.Id)).ToArray();
        if (found.Any(f => f is null)) return false;

        // いったん全部を控えへ引き上げてから、定義どおりに置き直す。
        for (int s = 0; s < Squads.Length; s++)
            if (CanReform(s) && Squads[s].Units is { } u)
                foreach (UnitState x in u.ToList())
                    if (found.Contains(x) || s == squadIndex) { u.Remove(x); Bench.Add(x); }
        foreach (UnitState f in found!) Bench.Remove(f!);

        var target = Roster(squadIndex);
        for (int i = 0; i < want.Length; i++) { found[i]!.Slot = want[i].Slot; target.Add(found[i]!); }
        SortBySlot(target);
        for (int s = 0; s < Squads.Length; s++)
            if (Squads[s].Units is { } u) SortBySlot(u);
        return true;
    }

    /// <summary>
    /// 1 戦ぶんの駒を用意する。<b>返すリストはそのまま <see cref="BattleEngine.Run"/> へ渡す</b>
    /// ——戦闘が書き換えた同じ参照を <see cref="Resolve"/> が読む。
    /// </summary>
    public (List<UnitState> Players, List<UnitState> Enemies, int Seed, Node Node)? Prepare(int squadIndex)
    {
        Squad s = Squads[squadIndex];
        if (s.Units is not { } pu || s.Road < 0) return null;
        Node? node = NextNode(s.Road);
        if (node is null) return null;
        node.Units ??= BattleEngine.Materialize(node.Def.Enemy, BattleContext.EnemyTeam);
        return (pu, node.Units, Map11.DeriveSeed(Seed, Battles), node);
    }

    /// <summary>
    /// 1 戦ぶんの後始末。<b>第168期 <c>BandOnce</c> と同じ順序・同じ規則</b>——
    /// 敵の生存者は傷ついたまま残り、味方は生き残れば境界を越えて
    /// <b>勝ったときだけ</b> <see cref="Map11.RecoverPercent"/> ぶん回復する。
    /// </summary>
    public void Resolve(int squadIndex, Node node, bool playerWon)
    {
        Squad s = Squads[squadIndex];
        List<UnitState> pu = s.Units!;
        List<UnitState> eu = node.Units!;
        Battles++;

        var aliveP = pu.Where(u => u.IsAlive).ToList();
        var aliveE = eu.Where(u => u.IsAlive).ToList();
        node.HpNow = aliveE.Sum(u => u.Hp);

        if (aliveE.Count == 0)
        {
            node.Cleared = true;
            node.Units = null;
            node.HpNow = 0;
            if (node.Def.Road == s.Home) s.Cleared++;
        }
        else node.Units = EngagementEngine.CrossBoundary(aliveE);

        if (aliveP.Count == 0)
        {
            s.Units = null;
            s.Lost = true;
            s.Road = -1;
        }
        else
        {
            s.Units = EngagementEngine.CrossBoundary(aliveP, pu,
                playerWon && Map11.RecoverPercent > 0
                    ? new RecoverRule(Map11.RecoverPercent, false) : null);
        }
    }

    /// <summary>部分点（第168期と同じ定義）＝ 抜いた部隊数 ＋ 残った部隊の削り。</summary>
    public double Partial()
    {
        double p = ClearedCount;
        foreach (Node[] road in Nodes)
            foreach (Node n in road)
                if (!n.Cleared && n.DefMaxHp > 0)
                    p += (double)(n.DefMaxHp - n.HpNow) / n.DefMaxHp;
        return p;
    }
}
