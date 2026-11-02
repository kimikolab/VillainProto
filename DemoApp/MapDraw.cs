using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 途中到着の引き（第441期 §2）
//
// **規則は持たない。** 手札 15 枚（既定の 3 隊の中身）から「開始時に配る 10 枚」と「後から届く 5 枚の順」を
// seed で決めるだけ。**保証 3 条件**（回復役 ≥1・前に立てる駒 ≥1・同じ軸の芯 1 組）を満たすまで引き直す（20 回まで）。
// **保証の表は `Presets.Compare` の行から機械で引く**（手書きしない）:
//
//   回復役       ── Compare の行（15 枚のうち誰かを含む行）を第二波と seed 0 で 1 戦ずつ回し、味方を回復した駒（`BattleEventKind.Heal` の出し手）
//   前に立てる駒 ── Compare の行で、その駒の席が前列（前1・前3）である割合が半分以上
//   芯 1 組      ── Compare の行名の括弧（「反撃 (ヒサ×カド)」の「ヒサ×カド」）の名前が全部 15 枚の中の駒に当たる組
//
// 乱数は `Map11State` の戦闘（`Map11.DeriveSeed`）とは別の `System.Random` で引く——戦闘の seed は 1 つも動かない。
// **ここには Godot の型を1つも使わない**（`Map11.cs` と同じ扱い）。
// =====================================================================================

public static class MapDraw
{
    /// <summary>引き直しの上限（指示書 §2）。超えたら最後の引きで始め、結果画面に印を出す。</summary>
    public const int MaxRerolls = 20;

    /// <summary>保証の表（15 枚の手札に対して 1 度だけ作る）。</summary>
    public sealed record GuaranteeTable(IReadOnlySet<string> Healers, IReadOnlySet<string> Front,
                                        IReadOnlyList<(string Row, string[] Ids)> Cores);

    /// <summary>手札 15 枚（既定の 3 隊の中身・隊の順 × 席の順）。</summary>
    public static IReadOnlyList<UnitDef> Pool { get; } =
        Map11.Squads.SelectMany(s => s.F.Occupied().Select(o => o.Def)).ToArray();

    private static readonly Lazy<GuaranteeTable> _table = new(Build);
    public static GuaranteeTable Table => _table.Value;

    private static GuaranteeTable Build()
    {
        var pool = Pool.Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
        var rows = Presets.Compare.Where(r => r.F.Occupied().Any(o => pool.Contains(o.Def.Id))).ToArray();

        // 回復役: 行を 1 戦ずつ回し、回復の出し手を数える（相手は第二波・seed 0・倍率なし）。
        var healers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, f) in rows)
        {
            var players = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var enemies = BattleEngine.Materialize(EnemyCatalog.Stages[1].Enemy, BattleContext.EnemyTeam);
            BattleResult r = BattleEngine.Run(players, enemies, 0, verbose: true);
            var byId = players.ToDictionary(u => u.InstanceId, u => u.Def.Id);
            foreach (BattleEvent e in r.Events)
                if (e.Kind == BattleEventKind.Heal && e.Amount > 0 && e.ActorId is int a
                    && byId.TryGetValue(a, out string? id) && pool.Contains(id))
                    healers.Add(id);
        }

        // 前に立てる駒: 前列（席 0・1）に座る割合が半分以上。
        var front = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in pool)
        {
            var seats = rows.SelectMany(r => r.F.Occupied().Where(o => o.Def.Id == id).Select(o => o.Slot)).ToArray();
            if (seats.Length > 0 && seats.Count(s => s is 0 or 1) * 2 >= seats.Length) front.Add(id);
        }

        // 芯: 行名の括弧の中の名前（× 区切り）が、全部 15 枚の中の駒の名前に含まれる組。
        var cores = new List<(string, string[])>();
        foreach (var (name, _) in rows)
        {
            int open = name.IndexOf('('), close = name.LastIndexOf(')');
            if (open < 0 || close <= open) continue;
            string[] tokens = name[(open + 1)..close].Split('×', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var ids = new List<string>();
            foreach (string t in tokens)
                if (Pool.FirstOrDefault(d => d.Name.Contains(t, StringComparison.Ordinal)) is { } hit) ids.Add(hit.Id);
            if (tokens.Length >= 2 && ids.Count == tokens.Length && ids.Distinct().Count() == ids.Count
                && !cores.Any(c => c.Item2.ToHashSet().SetEquals(ids)))
                cores.Add((name, ids.ToArray()));
        }
        return new GuaranteeTable(healers, front, cores);
    }

    /// <summary>その 10 枚が保証 3 条件を満たすか（足りないものを返す・空なら満たす）。</summary>
    public static List<string> Missing(IEnumerable<UnitDef> hand)
    {
        var ids = hand.Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
        var miss = new List<string>();
        if (!ids.Overlaps(Table.Healers)) miss.Add("回復役");
        if (!ids.Overlaps(Table.Front)) miss.Add("前に立てる駒");
        if (!Table.Cores.Any(c => c.Ids.All(ids.Contains))) miss.Add("芯");
        return miss;
    }

    /// <summary>開始の手札と到着の列（届く順・1 回に届く枚数の並び）。</summary>
    public sealed record Opening(UnitDef[] Start, UnitDef[] Queue, int[] Batches, int Rerolls, bool Ok);

    /// <summary>
    /// 開始の <paramref name="start"/> 枚と、残りの到着の列を seed で引く（指示書 §2）。
    /// 開始の手札は<b>手札の元の並び</b>（隊の順 × 席の順）に並べ直す——画面の一覧が引きのたびに散らからないように。
    /// 1 回に届く枚数は <paramref name="minPer"/>〜<paramref name="maxPer"/> を同じ乱数で引く。
    /// </summary>
    public static Opening Draw(int seed, int start, int minPer, int maxPer)
    {
        var rng = new Random(unchecked(seed * 7919 + 441));
        int n = Pool.Count;
        int[] order = Enumerable.Range(0, n).ToArray();
        int tries = 0;
        bool ok = false;
        while (true)
        {
            for (int i = n - 1; i > 0; i--) { int j = rng.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
            ok = Missing(order.Take(start).Select(i => Pool[i])).Count == 0;
            if (ok || tries >= MaxRerolls) break;
            tries++;
        }
        UnitDef[] hand = order.Take(start).OrderBy(i => i).Select(i => Pool[i]).ToArray();
        UnitDef[] queue = order.Skip(start).Select(i => Pool[i]).ToArray();
        var batches = new List<int>();
        for (int left = queue.Length; left > 0;)
        {
            int k = Math.Min(left, rng.Next(Math.Max(1, minPer), Math.Max(minPer, maxPer) + 1));
            batches.Add(k);
            left -= k;
        }
        return new Opening(hand, queue, batches.ToArray(), tries, ok);
    }
}
