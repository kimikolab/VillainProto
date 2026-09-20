using BattleCore;
using static Common;

// =====================================================================================
// stage map（第166期） —— 検証用マップ 1-1 を頭なしで回す
//
// 指示書は design/PHASE166_MAP11_SPEC.md。前提は PHASE163 / PHASE164 / PHASE165。
//
// **新しい敵・新しい駒は1体も作らない。** 列は `EnemyCatalog.Stages` の部分順列
// （`P24` / `P53`）で、編成は `UnitCatalog.All` の 12 枚から組む。
// engine で足したのは `EngagementEngine.CrossBoundary`（private `CarryOver` の公開の包み）1本だけ。
//
//     dotnet run --project BattleSim -c Release 0 stage map phase0  # 前提（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 stage map rank    # Q0-1 / Q0-2（順位を引き直す）
//     dotnet run --project BattleSim -c Release 0 stage map seats   # 席を決める
//     dotnet run --project BattleSim -c Release 0 stage map         # 段A の本体
// =====================================================================================

static partial class StageDiag
{
    // ---- 道（**列は `Stages` の部分順列**。`ColOf` で引く） ----
    public sealed record Road(string Name, string Col, string Note);

    /// <summary>
    /// <b>第166期 Phase 0 で差し替えた</b>（指示書 §2-1 の但し書き・ポンの判断）。
    /// 指示書の案（`P24` / `P53`）は Q0-2 が **×**（5 枚中 2 枚）——
    /// <b>どちらの道も「軽い波 1 つ ＋ 重い波 1 つ」で、道としての性格が分かれていなかった。</b>
    /// 選び直した組は `stage map pick` の 2 位で、<b>隊ごとに向きが揃う</b>ほう。
    /// </summary>
    public static readonly Road[] MapRoads =
    {
        new("北の道", "P23", "第二波（粛）→ 第三波（渇き）。**軽い2波・ターン外と回復を封じる道**"),
        new("南の道", "P45", "第四波（軛）→ 第五波（断罪＋殉教者）。**重い2波・1発の上限と単体封じの道**"),
    };

    // ---- 手札 12 枚 ----
    // 必ず入れる5枚（第164〜165期で一行が書けた駒）。
    public static readonly string[] MapCore = { "kado", "hane", "kubi", "golm", "basa" };

    /// <summary>
    /// 埋め草 7 枚。<b>第165期 部B（seed 800・R0）の「どこでも同じ」群から
    /// <c>checkup</c> の単独が高い順に 7 枚</b>（指示書 §2-2）。
    /// 選び方は `stage map phase0` が実測から引き直して印字する（この配列との一致が自己検査）。
    /// </summary>
    public static string[] MapFillerA => MapFillerPick(0);

    /// <summary>第2候補 7 枚（同じ群の 8〜14 位）。依存変数の確認用（指示書 §段A の4点セット）。</summary>
    public static string[] MapFillerB => MapFillerPick(7);

    // ---- 線（**測る前に固定する**。結果を見てから緩めない・第64期） ----
    const double MapDiffLine = 15.0;   // 線A: 踏破率の差（pt）
    const double MapNoiseMul = 3.0;    // 線A: ノイズの何倍か
    const double MapCeil = 95.0;       // 両方これ以上なら「易しすぎる」
    const double MapFloor = 5.0;       // 両方これ以下なら「難しすぎる」
    const int MapSeeds = 800;          // 1 帯あたりの seed 本数
    const int MapBattleCap = 24;       // 1 会戦の戦闘回数の上限（膠着で止まらないため）
    const int SeatSeeds = 100;         // 席の探索に使う seed 本数

    /// <summary>
    /// <b>拠点の手当て</b>（指示書 §2-2 が名前だけ置いたノブ。<b>既定は回復なし</b>）。
    /// 負けて拠点へ戻った部隊にだけ効く——<b>勝って道を進む部隊には効かない</b>
    /// （道の途中では組み直せない、と同じ理由）。
    /// <c>Percent</c> は <c>MaxHp</c> に対する割合、<c>Revive</c> は死者を戻すか。
    /// <b>engine の <see cref="RecoverRule"/> をそのまま渡すだけで、新しい規則は書いていない。</b>
    /// </summary>
    public readonly record struct BaseRecover(int Percent, bool Revive)
    {
        public static BaseRecover Default => new(0, false);
        public bool Active => Percent > 0 || Revive;
        public override string ToString() => Percent == 0 && !Revive ? "なし"
            : (Percent > 0 ? $"{Percent}%" : "") + (Revive ? "＋死者復帰" : "");
    }

    public static readonly (string Key, string Text)[] MapPredictions =
    {
        ("C1", "**線A は ○ だが、差は W1 の 20〜35pt より小さい**（15〜25pt）"
             + "——再出撃で挽回できるので、1回の割り当て違いが丸ごと踏破率に出ない"),
        ("C2", "**カド隊が北（粛）に入ると、第二波で止まって何度も戻る**"
             + "——R251 どおり。戦闘回数が正より 1.5 回以上増える"),
        ("C3", "**ゴルムの `与えた総害` は正（南）のほうが大きい**"
             + "——第165期の一行「重い敵が先に来る道で働く」がそのまま出る"),
        ("C4", "**踏破率はどちらの割り当てでも天井にも床にも張り付かない**"
             + "（正 55〜85% / 逆 30〜60%）——手札 12 枚のうち 10 枚を出すので、素の 61 行より強い"),
        ("C5", "**W3 の「2割以上が挽回」は逆の割り当ての踏破率そのもの**"
             + "——逆でも 20% 以上踏破するなら ○。C4 が当たれば自動的に通る"),
    };

    // =================================================================================
    // 埋め草の選定（**第165期 部B の群の定義をそのまま再現する**）
    //
    // 群の定義（第164期 Q0-3・`CatBandNoise` / `CatColBig` / `CatRatio`）と
    // `checkup` の単独（`design/PHASE152_CHECKUP.md` の表A）は、どちらも既存の器具から引く。
    // **ここで新しい線は1本も引いていない。**
    // =================================================================================

    /// <summary>
    /// <b>第165期 部B（`stage catalog seeds=800`・R0）の「どこでも同じ」21 枚</b>を、
    /// <c>checkup</c>（第152期）の単独の降順に並べたもの。<b>走らせた出力をそのまま写してある</b>
    /// ——`stage map phase0` が `design/PHASE152_CHECKUP.md` から単独を引き直して並びを検算する。
    /// <c>抉りのエグ</c> は <c>Retired</c> なので `checkup` の表Aに無く、末尾に置く。
    /// </summary>
    static readonly string[] MapSteadyBySolo =
    {
        "gan",   // 鬨の号令ガン    +20.85
        "tomo",  // 尾灯のトモ      +17.75
        "mudo",  // 泥人形ムド      +16.35
        "gald",  // 廃棄聖騎士ガルド +15.59
        "vel",   // 継ぎ接ぎのヴェル +10.99
        "yomi",  // 軋みのヨミ      +10.38
        "rica",  // 墓守リィカ       +9.81
        "shio",  // 移り木のシオ     +8.54
        "uro",   // 鱗のウロ         +4.52
        "utsu",  // 逆しまのウツ     +3.89
        "vio",   // 澱み喰いのヴィオ +2.62
        "beni",  // 毒喰らいのベニ   +2.11
        "kari",  // 駆り立てのカリ   +1.94
        "rau",   // 疫みのラウ       +1.47
        "hota",  // 熾のホタ         +1.44
        "sero",  // 逃亡兵セロ       +1.42
        "nata",  // 断ちのナタ       +0.65
        "borg",  // 焼け残りのボルグ −4.51
        "dolga", // のろまの巨兵ドルガ −11.99
        "hagi",  // 追い打ちのハギ  −33.36
        "egu",   // 抉りのエグ      （Retired・表Aに無い）
    };

    static string[] MapFillerPick(int skip) => MapSteadyBySolo.Skip(skip).Take(7).ToArray();

    // =================================================================================
    // マップの進行（**1 戦ずつ**。`EngagementEngine.Run` の列ループには乗らない）
    // =================================================================================

    sealed class MapState
    {
        public List<UnitState>?[] P = null!;      // 味方部隊（null ＝ 全滅して消えた）
        public int[] Assign = null!;              // 部隊 → 割り当てられた道
        public List<UnitState>?[][] E = null!;    // 敵部隊（null ＝ まだ出していない）
        public bool[][] ECleared = null!;
    }

    /// <summary>
    /// 1 マップぶん回す。戻りは（抜いた敵部隊数・戦闘回数・終了時の味方生存枚数）と
    /// 駒ごとの与えた総害（生の値）。
    /// </summary>
    static (int Cleared, int Battles, int Alive, int[] PerRoad, int LastRoad, bool Wiped) MapOnce(
        Formation[] squads, int[] assign, IReadOnlyList<Formation>[] roads, int seed,
        Dictionary<string, double>? harm, BaseRecover bas = default)
    {
        var st = new MapState
        {
            P = squads.Select(f => (List<UnitState>?)BattleEngine.Materialize(f, BattleContext.PlayerTeam)).ToArray(),
            Assign = (int[])assign.Clone(),
            E = roads.Select(r => new List<UnitState>?[r.Count]).ToArray(),
            ECleared = roads.Select(r => new bool[r.Count]).ToArray(),
        };

        int total = roads.Sum(r => r.Count);
        int battles = 0;
        int last = -1;

        while (battles < MapBattleCap)
        {
            bool acted = false;
            for (int s = 0; s < st.P.Length; s++)
            {
                if (st.P[s] is not { } pu) continue;

                // 送り先: **割り当てられた道が未踏破ならそこ。踏破済みならもう一方**
                // （§段A-3 の「固定の規則」。勝っても負けてもこの1本で決まる）。
                int road = NextRoad(st, st.Assign[s]);
                if (road < 0) continue;
                last = road;
                int idx = Array.FindIndex(st.ECleared[road], c => !c);

                st.E[road][idx] ??= BattleEngine.Materialize(roads[road][idx], BattleContext.EnemyTeam);
                var eu = st.E[road][idx]!;

                BattleResult r = BattleEngine.Run(pu, eu, DeriveSeed(seed, battles), verbose: false);
                battles++; acted = true;

                if (harm is not null)
                    foreach ((string id, UnitTally t) in r.TallyByUnit)
                        if (harm.ContainsKey(id)) harm[id] += t.DamageToEnemy;

                var aliveP = pu.Where(u => u.IsAlive).ToList();
                var aliveE = eu.Where(u => u.IsAlive).ToList();

                if (aliveE.Count == 0) { st.ECleared[road][idx] = true; st.E[road][idx] = null; }
                else st.E[road][idx] = EngagementEngine.CrossBoundary(aliveE);

                // 勝っても負けても、生存者は傷を抱えたまま残る（§2-3）。
                // **負けた部隊の生存者を捨てない**のが `EngagementEngine.Run` との唯一の違い。
                // 拠点の手当ては**負けて戻った部隊にだけ**掛ける（`BaseRecover` 既定は効かない）。
                bool lostP = aliveP.Count == 0 || !r.PlayerWon;
                st.P[s] = aliveP.Count == 0 ? null
                    : EngagementEngine.CrossBoundary(aliveP, pu,
                        lostP && bas.Active ? new RecoverRule(bas.Percent, bas.Revive) : null);

                int cleared0 = st.ECleared.Sum(a => a.Count(x => x));
                if (cleared0 >= total || st.P.All(x => x is null))
                    return (cleared0, battles, st.P.Sum(x => x?.Count(u => u.IsAlive) ?? 0),
                            st.ECleared.Select(a => a.Count(x => x)).ToArray(), last,
                            st.P.All(x => x is null));
            }
            if (!acted) break;
        }

        return (st.ECleared.Sum(a => a.Count(x => x)), battles,
                st.P.Sum(x => x?.Count(u => u.IsAlive) ?? 0),
                st.ECleared.Select(a => a.Count(x => x)).ToArray(), last,
                st.P.All(x => x is null));
    }

    static int NextRoad(MapState st, int want)
    {
        if (st.ECleared[want].Any(c => !c)) return want;
        for (int r = 0; r < st.ECleared.Length; r++) if (st.ECleared[r].Any(c => !c)) return r;
        return -1;
    }

    /// <summary>
    /// 踏破に失敗した会戦の内訳。<b>どちらの道で止まったか</b>を 2 通りの数え方で持つ
    /// ——(1) 終了時に未踏破の敵が残っていた道 ／ (2) 最後に戦った道。
    /// <b>どちらも読んで分岐しない計数である。</b>
    /// </summary>
    sealed record MapFail(int Total, int NorthOnly, int SouthOnly, int Both,
                          int LastNorth, int LastSouth, int Wiped, int Capped, double Cleared);

    /// <summary>seed 帯 1 本ぶんの（踏破率・平均戦闘回数・平均生存枚数・与えた総害・失敗の内訳）。</summary>
    static (double Rate, double Deg, double Battles, double Alive, Dictionary<string, double> Harm, MapFail Fail) MapBand(
        Formation[] squads, int[] assign, IReadOnlyList<Formation>[] roads, int seed0, string[] watch,
        BaseRecover bas = default)
    {
        int total = roads.Sum(r => r.Count);
        var full = new int[MapSeeds];
        var bt = new int[MapSeeds];
        var al = new int[MapSeeds];
        var left = new (int N, int S, int Last, bool Wiped, int Bat, int Cl)[MapSeeds];
        var hs = new Dictionary<string, double>[MapSeeds];
        Parallel.For(0, MapSeeds, i =>
        {
            var h = watch.ToDictionary(x => x, _ => 0.0);
            var (c, b, a, per, lastR, wiped) = MapOnce(squads, assign, roads, seed0 + i, h, bas);
            full[i] = c >= total ? 1 : 0; bt[i] = b; al[i] = a; hs[i] = h;
            left[i] = (roads[0].Count - per[0], roads[1].Count - per[1], lastR, wiped, b, c);
        });
        var sum = watch.ToDictionary(x => x, x => hs.Sum(h => h[x]) / MapSeeds);
        var f = Enumerable.Range(0, MapSeeds).Where(i => full[i] == 0).ToArray();
        var fail = new MapFail(
            f.Length,
            f.Count(i => left[i].N > 0 && left[i].S == 0),
            f.Count(i => left[i].S > 0 && left[i].N == 0),
            f.Count(i => left[i].N > 0 && left[i].S > 0),
            f.Count(i => left[i].Last == 0),
            f.Count(i => left[i].Last == 1),
            f.Count(i => left[i].Wiped),
            f.Count(i => left[i].Bat >= MapBattleCap),
            f.Length == 0 ? 0 : f.Average(i => (double)left[i].Cl));
        // **部分点（抜いた敵部隊数）**も併せて返す——踏破率が床に張り付いたとき、
        // 「差が無い」のか「測っていない」のかを分けるのはこちらである（R197 / 第61期）。
        return (100.0 * full.Average(), left.Average(x => (double)x.Cl),
                bt.Average(), al.Average(), sum, fail);
    }

    // =================================================================================
    // 席（**道に依らない規則で決める**。§段A-1）
    // =================================================================================

    /// <summary>
    /// 5 枚の並べ方 120 通りを、**4 つの敵部隊それぞれに対する単発の勝率の合計**で採点し、
    /// 最良を採る（同点は辞書順で先のもの）。
    /// <b>道の割り当てに依らない</b>のが要点——片方の道で測ると、席が割り当ての結論を先取りする。
    /// </summary>
    static (Formation F, double Score, (int[] P, double S)[] Top) MapSeat(UnitDef[] five,
                                                                          IReadOnlyList<Formation> foes)
    {
        var perms = AllPerms(5).ToArray();
        var score = new double[perms.Length];
        Parallel.For(0, perms.Length, i =>
        {
            var f = new Formation();
            for (int k = 0; k < 5; k++) f[k] = five[perms[i][k]];
            double s = 0;
            foreach (var e in foes)
                for (int seed = 0; seed < SeatSeeds; seed++)
                    if (BattleEngine.Run(f, e, seed, verbose: false).PlayerWon) s++;
            score[i] = s / (foes.Count * SeatSeeds) * 100.0;
        });
        int best = 0;
        for (int i = 1; i < perms.Length; i++) if (score[i] > score[best]) best = i;
        var bf = new Formation();
        for (int k = 0; k < 5; k++) bf[k] = five[perms[best][k]];
        var top = Enumerable.Range(0, perms.Length).OrderByDescending(i => score[i]).ThenBy(i => i)
            .Take(10).Select(i => (perms[i], score[i])).ToArray();
        return (bf, score[best], top);
    }

    static readonly string[] SlotName = { "前1", "前3", "中央", "後1", "後3" };

    static string Show(Formation f) => string.Join(" / ",
        f.Occupied().Select(o => $"{SlotName[o.Slot]}:{o.Def.Name}"));

    static UnitDef Def(string id) => UnitCatalog.Everyone.First(u => u.Id == id);

    // =================================================================================
    // 入口
    // =================================================================================

    static void MapRun(string arg)
    {
        string a = arg.Trim();
        if (a.StartsWith("phase0")) { MapPhase0(); return; }
        if (a.StartsWith("rank")) { MapRank(); return; }
        if (a.StartsWith("pick")) { MapPick(); return; }
        if (a.StartsWith("sweep")) { MapSweep(); return; }
        if (a.StartsWith("diag")) { MapDiag(); return; }
        if (a.StartsWith("seats")) { MapMain(seatsOnly: true, fillerB: false); return; }
        MapMain(seatsOnly: false, fillerB: a.StartsWith("b"));
    }

    // ---------------- Q0-1 / Q0-2 ----------------

    /// <summary>名指しの5枚（**指示書 §2-2 の「必ず入れる5枚」**と同じ並び）。</summary>
    static readonly string[] MapFive =
        { "棘鎧のカド", "萎縮のクビ", "突き返しのハネ", "大喰らいゴルム", "喧噪のバサ" };

    const double MapContrastLine = 15.0;   // Q0-2: 2 つの道での順位差
    const int MapContrastUnits = 3;        // Q0-2: 5 枚のうち何枚

    /// <summary>
    /// 列ごとに「52 体の帰属（`与えた総害`）の平均順位」を返す。
    /// <b>第165期 `ShortRun` の `Point` の逐語の写し</b>——新しい量も新しい線も足していない。
    /// </summary>
    static (string[] Ids, Dictionary<string, string> Name, Dictionary<string, int> Slots,
            Func<string, double[]> Rank) MapRankTable(string ver = "R0")
    {
        var rows = CompareBuilds();
        var jobs = new List<(int Row, int Slot, UnitDef Def)>();
        for (int i = 0; i < rows.Length; i++)
            foreach ((int sl, UnitDef d) in rows[i].F.Occupied()) jobs.Add((i, sl, d));
        var ids = jobs.Select(j => j.Def.Id).Distinct().ToArray();
        var uname = ids.ToDictionary(id => id, id => jobs.First(j => j.Def.Id == id).Def.Name);
        var slotsOf = ids.ToDictionary(id => id,
            id => Enumerable.Range(0, jobs.Count).Where(j => jobs[j].Def.Id == id).ToArray());
        int H = (int)Obj.Harm;
        var cache = new Dictionary<string, double[]>();

        double[] Rank(string cn)
        {
            if (cache.TryGetValue(cn, out var got)) return got;
            var col = ColOf(cn);
            var fE = new double[rows.Length][];
            Parallel.For(0, rows.Length, i => fE[i] = EngageObjAvg(rows[i].F, col, VerOf(ver)));
            var gE = new double[jobs.Count][];
            Parallel.For(0, jobs.Count, j =>
                gE[j] = EngageObjAvg(SwapOne(rows[jobs[j].Row].F, jobs[j].Slot), col, VerOf(ver)));
            var sv = new double[jobs.Count];
            for (int j = 0; j < jobs.Count; j++) sv[j] = fE[jobs[j].Row][H] - gE[j][H];
            var r = AverageRanksDesc(ids.Select(id => slotsOf[id].Average(j => sv[j])).ToArray());
            cache[cn] = r;
            return r;
        }

        return (ids, uname, slotsOf.ToDictionary(kv => kv.Key, kv => kv.Value.Length), Rank);
    }

    /// <summary>
    /// <b>Q0-2 が落ちたときの選び直し</b>（指示書 §2-1・Q0-2）。
    /// 候補は <b>第一波を含まない長さ2 の列</b>（波 2..5 の順列 12 本）で、
    /// <b>2 本の道が波を共有しない</b>組（＝ {2,3,4,5} の2分割 3 通り × 並び 4 通り ＝ 12 組）。
    /// 採点は「5 枚のうち何枚が <see cref="MapContrastLine"/> 位以上違うか」、同点は差の合計。
    /// <b>線は Q0-2 のものをそのまま使う。新しい線は引いていない。</b>
    /// </summary>
    static void MapPick()
    {
        var (ids, uname, _, Rank) = MapRankTable();
        int[] u5 = MapFive.Select(nm => Array.FindIndex(ids, id => uname[id] == nm)).ToArray();

        var waves = new[] { 1, 2, 3, 4 };   // 0 始まり ＝ 第二波〜第五波
        var colNames = new List<string>();
        foreach (int a in waves)
            foreach (int b in waves)
                if (a != b) colNames.Add(PermCol(new[] { a, b }).Name);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        foreach (string cn in colNames) _ = Rank(cn);
        double secs = sw.Elapsed.TotalSeconds;

        Console.WriteLine("# 第166期 Q0-2 の選び直し —— 対比が最大になる長さ2 の 2 本");
        Console.WriteLine();
        Console.WriteLine($"候補は**第一波を含まない長さ2 の列 {colNames.Count} 本**（波 2..5 の順列）。"
            + "**2 本の道が波を共有しない**組だけを見る（＝ {2,3,4,5} の2分割 3 通り × 並び 4 通り）。");
        Console.WriteLine();
        Console.WriteLine($"採点: **5 枚のうち順位差が {MapContrastLine:F0} 位以上ある枚数**"
            + "（同点は差の合計）。**線は Q0-2 のものをそのまま使う。**");
        Console.WriteLine();

        Console.WriteLine("## 12 本の列での 5 枚の順位");
        Console.WriteLine();
        Console.WriteLine("| 列 | 並び | " + string.Join(" | ", MapFive) + " |");
        Console.WriteLine("|---|---|" + string.Concat(MapFive.Select(_ => "--:|")));
        foreach (string cn in colNames)
        {
            var r = Rank(cn);
            Console.WriteLine($"| `{cn}` | "
                + string.Join(" → ", ColOf(cn).Squads.Select(sq => $"第{WaveIdxOf(sq) + 1}波")) + " | "
                + string.Join(" | ", u5.Select(u => u < 0 ? "—" : r[u].ToString("F0"))) + " |");
        }
        Console.WriteLine();

        var parts = new[]
        {
            (new[] { 1, 2 }, new[] { 3, 4 }),
            (new[] { 1, 3 }, new[] { 2, 4 }),
            (new[] { 1, 4 }, new[] { 2, 3 }),
        };
        var cand = new List<(string A, string B, int Hit, double Sum, double[] D)>();
        foreach (var (x, y) in parts)
            foreach (var ax in new[] { x, x.Reverse().ToArray() })
                foreach (var by in new[] { y, y.Reverse().ToArray() })
                {
                    string ca = PermCol(ax).Name, cb = PermCol(by).Name;
                    var ra = Rank(ca); var rb = Rank(cb);
                    var d = u5.Select(u => u < 0 ? 0 : Math.Abs(ra[u] - rb[u])).ToArray();
                    cand.Add((ca, cb, d.Count(x2 => x2 >= MapContrastLine), d.Sum(), d));
                }

        Console.WriteLine("## 組（**波を共有しない 2 本**）");
        Console.WriteLine();
        Console.WriteLine("| 道A | 道B | **>= 15 位の枚数** | 差の合計 | "
            + string.Join(" | ", MapFive) + " | Q0-2 |");
        Console.WriteLine("|---|---|--:|--:|" + string.Concat(MapFive.Select(_ => "--:|")) + ":-:|");
        foreach (var c in cand.OrderByDescending(c => c.Hit).ThenByDescending(c => c.Sum))
            Console.WriteLine($"| `{c.A}` | `{c.B}` | **{c.Hit}** | {c.Sum:F0} | "
                + string.Join(" | ", c.D.Select(x => x.ToString("F0"))) + " | "
                + (c.Hit >= MapContrastUnits ? "**○**" : "×") + " |");
        Console.WriteLine();

        var best = cand.OrderByDescending(c => c.Hit).ThenByDescending(c => c.Sum).First();
        Console.WriteLine(best.Hit >= MapContrastUnits
            ? $"**最良は `{best.A}` × `{best.B}`（{best.Hit} 枚・差の合計 {best.Sum:F0}）。"
              + "この組を提案して段A へ進む。**"
            : $"**最良でも {best.Hit} 枚で線に届かない。長さ2 の組では対比が作れない**"
              + "——長さ3 の組を提案するか、指示書 §2-1 の但し書きどおり止まる。");
        Console.WriteLine();
        Console.WriteLine($"所要 {secs:F1} 秒。");
    }

    static void MapRank()
    {
        string[] five = MapFive;
        string[] cols = { "P24", "P42", "P53", "P35" };
        const string Ver = "R0";

        var rows = CompareBuilds();
        var jobs = new List<(int Row, int Slot, UnitDef Def)>();
        for (int i = 0; i < rows.Length; i++)
            foreach ((int sl, UnitDef d) in rows[i].F.Occupied()) jobs.Add((i, sl, d));
        var ids = jobs.Select(j => j.Def.Id).Distinct().ToArray();
        var uname = ids.ToDictionary(id => id, id => jobs.First(j => j.Def.Id == id).Def.Name);
        var slotsOf = ids.ToDictionary(id => id,
            id => Enumerable.Range(0, jobs.Count).Where(j => jobs[j].Def.Id == id).ToArray());
        int H = (int)Obj.Harm;

        double[] Point(Col col)
        {
            var fE = new double[rows.Length][];
            Parallel.For(0, rows.Length, i => fE[i] = EngageObjAvg(rows[i].F, col, VerOf(Ver)));
            var gE = new double[jobs.Count][];
            Parallel.For(0, jobs.Count, j =>
                gE[j] = EngageObjAvg(SwapOne(rows[jobs[j].Row].F, jobs[j].Slot), col, VerOf(Ver)));
            var s = new double[jobs.Count];
            for (int j = 0; j < jobs.Count; j++) s[j] = fE[jobs[j].Row][H] - gE[j][H];
            return ids.Select(id => slotsOf[id].Average(j => s[j])).ToArray();
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rank = cols.ToDictionary(cn => cn, cn => AverageRanksDesc(Point(ColOf(cn))));

        Console.WriteLine("# 第166期 Q0-1 / Q0-2 —— 2 つの道での 5 枚の順位");
        Console.WriteLine();
        Console.WriteLine("**第165期 §7-2 の S-6 表は 5 体のうち `礫のガレ` しか載せておらず、"
            + "`大喰らいゴルム` の順位が報告書に残っていない。**"
            + $"そこで**同じ器具・同じ線**（`与えた総害` / {Ver} / seed 0..{Seeds - 1}）で "
            + $"{cols.Length} 列だけ引き直した——**新しい線は1本も引いていない**。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 在席枠 | **P24**（北・粛が先頭） | **P53**（南・重い敵が先頭） "
            + "| **\\|差\\|** | 15 位以上 | 参考 P42 | 参考 P35 |");
        Console.WriteLine("|---|--:|--:|--:|--:|:-:|--:|--:|");
        int hit = 0;
        foreach (string nm in five)
        {
            int u = Array.FindIndex(ids, id => uname[id] == nm);
            if (u < 0) { Console.WriteLine($"| {nm} | 0 | 在席 0 | — | — | — | — | — |"); continue; }
            double r24 = rank["P24"][u], r53 = rank["P53"][u];
            double d = Math.Abs(r24 - r53);
            bool ok = d >= 15.0;
            if (ok) hit++;
            Console.WriteLine($"| {nm} | {slotsOf[ids[u]].Length} | {r24:F0} | {r53:F0} "
                + $"| **{d:F0}** | {(ok ? "**○**" : "×")} | {rank["P42"][u]:F0} | {rank["P35"][u]:F0} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**Q0-2: 5 枚のうち {hit} 枚が 15 位以上違う"
            + $"（線 3 枚以上 → {(hit >= 3 ? "**○**。この組で進む" : "**×**。列を選び直す")}）。**");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒。");
    }

    // ---------------- Phase 0 ----------------
    static void MapPhase0()
    {
        Console.WriteLine("# 第166期 Phase 0 —— 前提を実装から引き直す（**戦闘0回**）");
        Console.WriteLine();

        // ---- Q0-3 ----
        Console.WriteLine("## Q0-3. 手札 12 枚");
        Console.WriteLine();
        var chk = CheckupTable(out string cpath);
        Console.WriteLine($"必ず入れる5枚: {string.Join(" / ", MapCore.Select(id => Def(id).Name))}");
        Console.WriteLine();
        Console.WriteLine($"埋め草は**第165期 部B（seed 800・R0）の「どこでも同じ」群**から "
            + $"`{cpath}` の単独の降順に 7 枚（第2候補は 8〜14 位）。");
        Console.WriteLine();
        Console.WriteLine("| # | 駒 | id | checkup 単独 | checkup 群 | 採否 |");
        Console.WriteLine("|--:|---|---|--:|---|---|");
        for (int i = 0; i < MapSteadyBySolo.Length; i++)
        {
            var d = Def(MapSteadyBySolo[i]);
            bool has = chk.TryGetValue(d.Name, out var c);
            string use = i < 7 ? "**埋め草**" : i < 14 ? "第2候補" : "—";
            Console.WriteLine($"| {i + 1} | {d.Name} | {d.Id} | {(has ? c.Solo.ToString("+0.00;-0.00") : "—")} "
                + $"| {(has ? c.Grp : "—")} | {use} |");
        }
        Console.WriteLine();
        var dup = MapFillerA.Intersect(MapCore).ToArray();
        Console.WriteLine(dup.Length == 0
            ? "**必ず入れる5枚と埋め草に重複は無い。**"
            : $"**重複がある: {string.Join(" / ", dup)}**（選び直すこと）。");
        Console.WriteLine();

        // ---- Q0-4 / Q0-5 ----
        Console.WriteLine("## Q0-4 / Q0-5. 境界を外から呼ぶために触った範囲");
        Console.WriteLine();
        string src = ReadRepo("BattleCore/Engagement.cs");
        var privates = src.Split('\n').Where(l => l.Contains("private static")).Select(l => l.Trim()).ToArray();
        Console.WriteLine("`BattleCore/Engagement.cs` の private メンバ:");
        Console.WriteLine();
        foreach (string p in privates) Console.WriteLine($"- `{p.Split('(')[0].Trim()}(`…");
        Console.WriteLine();
        Console.WriteLine("**足したのは `EngagementEngine.CrossBoundary` 1 本だけ**"
            + "——`CarryOver` を public にはせず、**新しいリストへ写してから private を呼ぶ包み**にした。"
            + $"`Snapshot` / `DeriveSeed` は**触っていない**（`DeriveSeed` は診断側に同じ式の写しが既にある）。"
            + $"`Run` の本体は 1 文字も変えていない（{(src.Contains("? BattleEngine.Materialize(playerSquads[pi]") ? "確認: 投入の分岐はそのまま" : "**要確認**")}）。");
        Console.WriteLine();
        Console.WriteLine("### Q0-5. 「全員生存のはず」に当たる前提");
        Console.WriteLine();
        bool inSnapshot = src.Contains("投入直後（Materialize / CarryOver の直後）に呼ぶので全員生存のはずだが");
        Console.WriteLine(inSnapshot
            ? "唯一当たるのは **`Snapshot` の doc**（「全員生存のはずだが、**生存数は仮定せず数える**」）で、"
              + "**`CarryOver` 自身には勝敗を読む行も、全員生存を仮定する行も無い**"
              + "——読むのは生存リストと `RecoverRule` / `BoundaryChoice` だけ。"
              + "**負けた部隊の生存者を持ち帰っても前提は崩れない。**"
            : "**`Snapshot` の doc が見つからない**（Engagement.cs が変わっている。読み直すこと）。");
        Console.WriteLine();
        int win = src.Split('\n').Count(l => l.Contains("PlayerWon") && l.Contains("CarryOver"));
        Console.WriteLine($"`CarryOver` の定義から `return` までに `PlayerWon` を読む行: **{win} 行**。");
        Console.WriteLine();

        // ---- Q0-7 ----
        Console.WriteLine("## Q0-7. 敵側の召喚・蘇生");
        Console.WriteLine();
        var spawn = new[] { TraitId.Splitter, TraitId.Reviver, TraitId.Betrayed };
        Console.WriteLine("湧かす／戻す札（`ctx.Summon` と蘇生の呼び出し元から引いた）: "
            + string.Join(" / ", spawn.Select(t => $"`{t}`")));
        Console.WriteLine();
        Console.WriteLine("| 道 | 列 | 敵部隊 | 駒 | 湧かす札 |");
        Console.WriteLine("|---|---|---|---|---|");
        int bad = 0;
        foreach (var rd in MapRoads)
        {
            var col = ColOf(rd.Col);
            for (int i = 0; i < col.Squads.Count; i++)
                foreach ((int _, UnitDef d) in col.Squads[i].Occupied())
                {
                    var t = d.Traits.Where(x => Array.IndexOf(spawn, x) >= 0).ToArray();
                    if (t.Length == 0) continue;
                    bad++;
                    Console.WriteLine($"| {rd.Name} | {rd.Col} | {i + 1} 番目 | {d.Name} "
                        + $"| **{string.Join(" / ", t)}** |");
                }
        }
        if (bad == 0) Console.WriteLine("| — | — | — | — | **0 件** |");
        Console.WriteLine();
        Console.WriteLine(bad == 0
            ? "**4 部隊に湧かす札は1枚も無い。** 敵を持ち越しても湧き駒は残らない"
            + "（`CarryOver` は投入した駒だけを持つので、そもそも湧き駒は持ち越されない）。"
            : "**湧かす札を持つ敵がいる。** 持ち越しの前提を書き直すこと。");
        Console.WriteLine();

        // ---- 道 ----
        Console.WriteLine("## 道と列");
        Console.WriteLine();
        Console.WriteLine("| 道 | 列 | 並び（波番号） | 性格 |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var rd in MapRoads)
            Console.WriteLine($"| {rd.Name} | `{rd.Col}` | "
                + string.Join(" → ", ColOf(rd.Col).Squads.Select(s => $"第{WaveIdxOf(s) + 1}波")) + $" | {rd.Note} |");
        Console.WriteLine();

        Console.WriteLine("## 線（**結果を見る前に固定した**・R238 の4点セット）");
        Console.WriteLine();
        Console.WriteLine("| 点 | 中身 |");
        Console.WriteLine("|---|---|");
        Console.WriteLine("| 量 | 踏破率の差 ＝（正の割り当てで4部隊すべて抜いた割合）−（逆の割り当ての同じ割合） |");
        Console.WriteLine($"| 単位 | pt（seed {MapSeeds} 本の割合） |");
        Console.WriteLine($"| 対照 | 同じ割り当てを seed 帯2本（0..{MapSeeds - 1} / {MapSeeds}..{2 * MapSeeds - 1}）で回した差 |");
        Console.WriteLine("| 依存変数 | 埋め草の選び方（第2候補7枚）／再出撃の順番の規則 |");
        Console.WriteLine();
        Console.WriteLine($"**線A: 踏破率の差 >= {MapDiffLine:F1}pt、かつノイズの {MapNoiseMul:F0} 倍以上。**"
            + $"両方 {MapCeil:F0}% 以上 または {MapFloor:F0}% 以下なら **×**。");
        Console.WriteLine();

        Console.WriteLine("## 再出撃の規則（§段A-3）");
        Console.WriteLine();
        Console.WriteLine("- **割り当てられた道が未踏破ならそこへ。踏破済みなら、残っているもう一方の道へ。**"
            + "勝っても負けてもこの1本で決まる（勝った部隊はそのまま次の敵へ進む、と同じ規則になる）");
        Console.WriteLine("- 1 巡で 部隊0 → 部隊1 の順に 1 戦ずつ。全滅した部隊は消える");
        Console.WriteLine($"- 戦闘回数の上限は **{MapBattleCap} 回**（引き分けの堂々巡りで止まらないため）。"
            + "上限に当たった会戦は未踏破として数える");
        Console.WriteLine();

        Console.WriteLine("## 予測（**実装前に書いた。外れても消さない**）");
        Console.WriteLine();
        Console.WriteLine("| # | 予測 |");
        Console.WriteLine("|---|---|");
        foreach (var (k, t) in MapPredictions) Console.WriteLine($"| {k} | {t} |");
    }

    static string ReadRepo(string rel)
    {
        string[] cand = { rel, "../" + rel, "../../" + rel };
        string p = cand.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException($"{rel} が引けない（cwd をリポジトリ直下に）。");
        return File.ReadAllText(p);
    }

    /// <summary>
    /// <b>床に張り付いた原因の切り分け</b>——隊 × 敵部隊の単発勝率と、
    /// 隊 1 つで道を 1 本だけ歩いたときの到達点、そして<b>負け方の内訳</b>
    /// （全滅 ／ 引き分けで生存者ごと退く）を出す。
    /// <b>採否には使わない。盤面は 1 ビットも動かない。</b>
    /// </summary>
    static void MapDiag()
    {
        var (fA, fB, roads) = MapSetup(fillerB: false);
        var named = new[] { ("カド隊", fA), ("ハネ隊", fB) };
        const int N = 400;

        Console.WriteLine("# 第166期 段A 追補 —— 床の原因の切り分け（**測定だけ**）");
        Console.WriteLine();

        Console.WriteLine("## D-1. 隊 × 敵部隊 の単発勝率（新品どうし・seed 0..399）");
        Console.WriteLine();
        Console.WriteLine("| 隊 | 編成 | " + string.Join(" | ",
            MapRoads.SelectMany((rd, r) => roads[r].Select((sq, i) =>
                $"{rd.Name} {i + 1}（第{WaveIdxOf(sq) + 1}波）"))) + " |");
        Console.WriteLine("|---|---|" + string.Concat(roads.Sum(r => r.Count) is var nn
            ? Enumerable.Repeat("--:|", nn) : Array.Empty<string>()));
        foreach (var (nm, f) in named)
        {
            var cells = new List<string>();
            for (int r = 0; r < roads.Length; r++)
                foreach (var e in roads[r])
                {
                    int win = 0;
                    for (int seed = 0; seed < N; seed++)
                        if (BattleEngine.Run(f, e, seed, verbose: false).PlayerWon) win++;
                    cells.Add($"{100.0 * win / N:F1}%");
                }
            Console.WriteLine($"| {nm} | {Show(f)} | " + string.Join(" | ", cells) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## D-2. 負け方の内訳（**全滅か、生存者を残して退くか**）");
        Console.WriteLine();
        Console.WriteLine("`BattleEngine.Run` が偽を返す負けには 2 種類ある"
            + "——**味方が全員倒れた**（部隊が消える）と、**上限ターンの引き分け**（生存者が残る）。"
            + "**指示書 §2-3 の「傷を抱えて拠点へ戻る」が起きるのは後者だけ**である。");
        Console.WriteLine();
        Console.WriteLine("| 隊 | 敵部隊 | 負け | うち全滅 | うち生存者あり | 生存者ありの割合 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        foreach (var (nm, f) in named)
            for (int r = 0; r < roads.Length; r++)
                for (int i = 0; i < roads[r].Count; i++)
                {
                    int lose = 0, wipe = 0;
                    for (int seed = 0; seed < N; seed++)
                    {
                        var pu = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                        var eu = BattleEngine.Materialize(roads[r][i], BattleContext.EnemyTeam);
                        var res = BattleEngine.Run(pu, eu, seed, verbose: false);
                        if (res.PlayerWon) continue;
                        lose++;
                        if (pu.All(u => !u.IsAlive)) wipe++;
                    }
                    Console.WriteLine($"| {nm} | {MapRoads[r].Name} {i + 1}（第{WaveIdxOf(roads[r][i]) + 1}波） "
                        + $"| {lose} | {wipe} | **{lose - wipe}** | {(lose == 0 ? 0 : 100.0 * (lose - wipe) / lose):F1}% |");
                }
        Console.WriteLine();

        Console.WriteLine("## D-3. 隊 1 つで道を 1 本だけ歩いたとき（**持ち越しあり**・seed 0..399）");
        Console.WriteLine();
        Console.WriteLine("| 隊 | 道 | 1 本抜いた割合 | 2 本抜いた割合（＝道の踏破） |");
        Console.WriteLine("|---|---|--:|--:|");
        foreach (var (nm, f) in named)
            for (int r = 0; r < roads.Length; r++)
            {
                int one = 0, two = 0;
                for (int seed = 0; seed < N; seed++)
                {
                    var res = EngagementEngine.Run(new[] { f }, roads[r], seed, verbose: false);
                    if (res.EnemySquadsCleared >= 1) one++;
                    if (res.EnemySquadsCleared >= 2) two++;
                }
                Console.WriteLine($"| {nm} | {MapRoads[r].Name}（`{MapRoads[r].Col}`） "
                    + $"| {100.0 * one / N:F1}% | **{100.0 * two / N:F1}%** |");
            }
        Console.WriteLine();
        Console.WriteLine("**マップの勝利条件は 4 部隊すべてを抜くこと**なので、"
            + "この表の右端が両方とも低ければ、割り当てを何にしても踏破率は床に張り付く。");
    }

    /// <summary>
    /// <b>段A が床に張り付いたときの診断</b>——拠点の手当て（<see cref="BaseRecover"/>）を掃引して、
    /// 踏破率が帯（5〜95%）へ入る点があるかを見る。
    /// <b>採否には使わない。</b> 既定（回復なし）は指示書 §2-2 のまま動かしていない。
    /// </summary>
    static void MapSweep()
    {
        var (fA, fB, roads) = MapSetup(fillerB: false);
        var squads = new[] { fA, fB };
        int[] straight = { 1, 0 }, cross = { 0, 1 };
        var knobs = new[]
        {
            BaseRecover.Default, new BaseRecover(25, false), new BaseRecover(50, false),
            new BaseRecover(100, false), new BaseRecover(0, true), new BaseRecover(100, true),
        };

        Console.WriteLine("# 第166期 段A 追補 —— 拠点の手当てを掃引する");
        Console.WriteLine();
        Console.WriteLine("**負けて拠点へ戻った部隊にだけ**掛かる（勝って道を進む部隊には掛からない）。"
            + "engine の `RecoverRule` をそのまま渡すだけで、**新しい規則は 1 本も書いていない**。");
        Console.WriteLine();
        Console.WriteLine("| 拠点の手当て | 正 踏破率 | 逆 踏破率 | **差** | 正 部分点 | 逆 部分点 | 正 戦闘回数 | 逆 戦闘回数 | 帯(5〜95%) |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|:-:|");
        foreach (var k in knobs)
        {
            var p = MapBand(squads, straight, roads, 0, MapCore, k);
            var q = MapBand(squads, cross, roads, 0, MapCore, k);
            bool inBand = Math.Max(p.Rate, q.Rate) > MapFloor && Math.Min(p.Rate, q.Rate) < MapCeil;
            Console.WriteLine($"| {k} | {p.Rate:F1}% | {q.Rate:F1}% | **{p.Rate - q.Rate:+0.0;-0.0}pt** "
                + $"| {p.Deg:F2} | {q.Deg:F2} | {p.Battles:F2} | {q.Battles:F2} "
                + $"| {(inBand ? "**○**" : "×")} |");
        }
        Console.WriteLine();
        Console.WriteLine("**既定（なし）は動かしていない。**"
            + "この表は「マップが難しすぎる原因が手当ての有無かどうか」を切り分けるためだけにある。");
    }

    /// <summary>席の探索まで済ませた段A の土台（<see cref="MapMain"/> と <see cref="MapSweep"/> で共有）。</summary>
    static (Formation A, Formation B, IReadOnlyList<Formation>[] Roads) MapSetup(bool fillerB)
    {
        string[] filler = fillerB ? MapFillerB : MapFillerA;
        var roads = MapRoads.Select(r => (IReadOnlyList<Formation>)ColOf(r.Col).Squads).ToArray();
        var allFoes = roads.SelectMany(r => r).ToList();
        var fiveA = new[] { "kado", "golm" }.Concat(filler.Take(3)).Select(Def).ToArray();
        var fiveB = new[] { "hane", "kubi", "basa" }.Concat(filler.Skip(3).Take(2)).Select(Def).ToArray();
        return (MapSeat(fiveA, allFoes).F, MapSeat(fiveB, allFoes).F, roads);
    }

    // ---------------- 段A 本体 ----------------
    static void MapMain(bool seatsOnly, bool fillerB)
    {
        string[] filler = fillerB ? MapFillerB : MapFillerA;
        var roads = MapRoads.Select(r => (IReadOnlyList<Formation>)ColOf(r.Col).Squads).ToArray();
        var allFoes = roads.SelectMany(r => r).ToList();

        // カド隊 ＝ カド ＋ ゴルム ＋ 埋め草3 ／ ハネ隊 ＝ ハネ ＋ クビ ＋ バサ ＋ 埋め草2
        var fiveA = new[] { "kado", "golm" }.Concat(filler.Take(3)).Select(Def).ToArray();
        var fiveB = new[] { "hane", "kubi", "basa" }.Concat(filler.Skip(3).Take(2)).Select(Def).ToArray();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var (fA, sA, topA) = MapSeat(fiveA, allFoes);
        var (fB, sB, topB) = MapSeat(fiveB, allFoes);
        double seatSecs = sw.Elapsed.TotalSeconds;

        Console.WriteLine("# 第166期 段A —— 送り先を間違えると本当に損をするか");
        Console.WriteLine();
        Console.WriteLine($"埋め草は **{(fillerB ? "第2候補7枚" : "本命7枚")}**"
            + $"（{string.Join(" / ", filler.Select(x => Def(x).Name))}）。控え 2 枚 ＝ "
            + $"{string.Join(" / ", filler.Skip(5).Select(x => Def(x).Name))}。");
        Console.WriteLine();

        Console.WriteLine("## A-0. 席（**道に依らない規則で決めた**）");
        Console.WriteLine();
        Console.WriteLine($"5 枚の並べ方 120 通りを、**4 つの敵部隊それぞれに対する単発の勝率の合計**"
            + $"（seed 0..{SeatSeeds - 1}）で採点して最良を採る。"
            + "**片方の道で測ると席が割り当ての結論を先取りする**ので、4 部隊すべてを等しく見る。");
        Console.WriteLine();
        Console.WriteLine($"- **カド隊**: {Show(fA)}  —— 平均勝率 {sA:F1}%");
        Console.WriteLine($"- **ハネ隊**: {Show(fB)}  —— 平均勝率 {sB:F1}%");
        Console.WriteLine();
        Console.WriteLine("| 隊 | 順 | 並び | 平均勝率 |");
        Console.WriteLine("|---|--:|---|--:|");
        for (int i = 0; i < 5; i++)
        {
            Console.WriteLine($"| カド隊 | {i + 1} | {string.Join(" / ", topA[i].P.Select((k, sl) => $"{SlotName[sl]}:{fiveA[k].Name}"))} | {topA[i].S:F1}% |");
        }
        for (int i = 0; i < 5; i++)
        {
            Console.WriteLine($"| ハネ隊 | {i + 1} | {string.Join(" / ", topB[i].P.Select((k, sl) => $"{SlotName[sl]}:{fiveB[k].Name}"))} | {topB[i].S:F1}% |");
        }
        Console.WriteLine();
        Console.WriteLine($"（席の探索 {seatSecs:F1} 秒）");
        Console.WriteLine();
        if (seatsOnly) return;

        // ---- 走らせる ----
        var squads = new[] { fA, fB };
        var watch = MapCore;
        int[] straight = { 1, 0 };   // 部隊0（カド隊）→ 道1（南）／部隊1（ハネ隊）→ 道0（北）
        int[] cross = { 0, 1 };      // 逆

        sw.Restart();
        var res = new Dictionary<(string Asg, int Band), (double Rate, double Deg, double Battles, double Alive, Dictionary<string, double> Harm, MapFail Fail)>();
        foreach (int band in new[] { 0, 1 })
        {
            res[("正", band)] = MapBand(squads, straight, roads, band * MapSeeds, watch);
            res[("逆", band)] = MapBand(squads, cross, roads, band * MapSeeds, watch);
        }
        double secs = sw.Elapsed.TotalSeconds;

        // ---- 道ごとの重さ（**対比の出どころが「重さ」か「並び」か**を後から切り分けるため） ----
        var w = WaveWeights();
        Console.WriteLine("## A-0b. 道の重さ（**対比の出どころの切り分け**）");
        Console.WriteLine();
        Console.WriteLine("`w(k) = 100 − docs/balance.md の第k波の 61 行平均勝率`（第165期 §1-6 と同じ定義）。");
        Console.WriteLine("**総重量 Σw は並びに依らない**ので、2 本の道で総重量が近ければ"
            + "「差は並びから来ている」・離れていれば「重さから来ている」と読める。");
        Console.WriteLine();
        Console.WriteLine("| 道 | 列 | 並び | w の内訳 | **総重量 Σw** | 重心 |");
        Console.WriteLine("|---|---|---|---|--:|--:|");
        for (int r = 0; r < MapRoads.Length; r++)
        {
            var c = ColOf(MapRoads[r].Col);
            var ws = c.Squads.Select(sq => w[WaveIdxOf(sq)]).ToArray();
            double bary = ws.Select((x, i) => (double)i / (ws.Length - 1) * x).Sum() / ws.Sum();
            Console.WriteLine($"| {MapRoads[r].Name} | `{MapRoads[r].Col}` | "
                + string.Join(" → ", c.Squads.Select(sq => $"第{WaveIdxOf(sq) + 1}波")) + " | "
                + string.Join(" ＋ ", ws.Select(x => x.ToString("F1")))
                + $" | **{ws.Sum():F1}** | {bary:F3} |");
        }
        {
            double a0 = ColOf(MapRoads[0].Col).Squads.Sum(sq => w[WaveIdxOf(sq)]);
            double a1 = ColOf(MapRoads[1].Col).Squads.Sum(sq => w[WaveIdxOf(sq)]);
            Console.WriteLine();
            Console.WriteLine($"**総重量の差 {Math.Abs(a0 - a1):F1}**"
                + $"（比 {Math.Max(a0, a1) / Math.Max(Math.Min(a0, a1), 0.01):F2}）"
                + "——**この期の 2 本は中身が違う列なので、第165期の 40 列のように"
                + "「並びだけが違う対」にはなっていない。** 差がこの重さの偏りで説明できるかは A-3 の内訳で見る。");
        }
        Console.WriteLine();

        Console.WriteLine("## A-1. 踏破率");
        Console.WriteLine();
        Console.WriteLine($"- **正**: カド隊 → {MapRoads[1].Name}（`{MapRoads[1].Col}`）／"
            + $"ハネ隊 → {MapRoads[0].Name}（`{MapRoads[0].Col}`）");
        Console.WriteLine("- **逆**: 入れ替える");
        Console.WriteLine();
        Console.WriteLine("| 割り当て | 帯 | **踏破率** | **抜いた部隊数（部分点）** | 平均戦闘回数 | 終了時の生存枚数 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        foreach (string asg in new[] { "正", "逆" })
            foreach (int band in new[] { 0, 1 })
            {
                var v = res[(asg, band)];
                Console.WriteLine($"| {asg} | seed {band * MapSeeds}..{(band + 1) * MapSeeds - 1} "
                    + $"| **{v.Rate:F1}%** | **{v.Deg:F2} / 4** | {v.Battles:F2} | {v.Alive:F2} |");
            }
        Console.WriteLine();

        double d0 = res[("正", 0)].Rate - res[("逆", 0)].Rate;
        double d1 = res[("正", 1)].Rate - res[("逆", 1)].Rate;
        double nS = Math.Abs(res[("正", 0)].Rate - res[("正", 1)].Rate);
        double nC = Math.Abs(res[("逆", 0)].Rate - res[("逆", 1)].Rate);
        double noise = (nS + nC) / 2;

        Console.WriteLine("## A-2. 線A");
        Console.WriteLine();
        Console.WriteLine("| 量 | 値 |");
        Console.WriteLine("|---|--:|");
        Console.WriteLine($"| **踏破率の差（帯0）** | **{d0:+0.0;-0.0}pt** |");
        Console.WriteLine($"| 踏破率の差（帯1・再現） | {d1:+0.0;-0.0}pt |");
        Console.WriteLine($"| ノイズ（正の帯差） | {nS:F1}pt |");
        Console.WriteLine($"| ノイズ（逆の帯差） | {nC:F1}pt |");
        Console.WriteLine($"| **ノイズ（平均）** | **{noise:F1}pt** |");
        Console.WriteLine($"| 比（差 ÷ ノイズ） | {(noise > 0 ? (d0 / noise).ToString("F1") : "∞")} |");
        Console.WriteLine();
        bool big = d0 >= MapDiffLine;
        bool overNoise = noise <= 0 ? d0 > 0 : d0 >= MapNoiseMul * noise;
        double hi = Math.Max(res[("正", 0)].Rate, res[("逆", 0)].Rate);
        double lo = Math.Min(res[("正", 0)].Rate, res[("逆", 0)].Rate);
        bool band95 = lo >= MapCeil, band5 = hi <= MapFloor;
        Console.WriteLine($"- 差 >= {MapDiffLine:F1}pt: **{(big ? "○" : "×")}**");
        Console.WriteLine($"- 差 >= ノイズの {MapNoiseMul:F0} 倍: **{(overNoise ? "○" : "×")}**");
        Console.WriteLine($"- 天井／床（両方 {MapCeil:F0}% 以上 または {MapFloor:F0}% 以下）: "
            + $"**{(band95 || band5 ? (band95 ? "易しすぎる（×）" : "難しすぎる（×）") : "該当なし")}**");
        Console.WriteLine();
        bool ok = big && overNoise && !band95 && !band5;
        Console.WriteLine($"**線A: {(ok ? "○ —— 段B へ進む" : "× —— 止まる")}**");
        Console.WriteLine();
        if (band5 || band95)
        {
            double g0 = res[("正", 0)].Deg - res[("逆", 0)].Deg;
            double g1 = res[("正", 1)].Deg - res[("逆", 1)].Deg;
            double gn = (Math.Abs(res[("正", 0)].Deg - res[("正", 1)].Deg)
                       + Math.Abs(res[("逆", 0)].Deg - res[("逆", 1)].Deg)) / 2;
            Console.WriteLine("### 参考 —— 部分点で見た場合（**判定には使わない**）");
            Console.WriteLine();
            Console.WriteLine("踏破率が床（または天井）に張り付いたときの「差が無い」は、"
                + "**「差が無い」ではなく「測っていない」**（第61期）。同じ走行の部分点を並べる。");
            Console.WriteLine();
            Console.WriteLine("| 量 | 値 |");
            Console.WriteLine("|---|--:|");
            Console.WriteLine($"| 抜いた部隊数の差（帯0） | **{g0:+0.00;-0.00}** / 4 |");
            Console.WriteLine($"| 同（帯1・再現） | {g1:+0.00;-0.00} / 4 |");
            Console.WriteLine($"| ノイズ（帯差の平均） | {gn:F3} |");
            Console.WriteLine($"| 比 | {(gn > 0 ? (g0 / gn).ToString("F1") : "∞")} |");
            Console.WriteLine();
        }

        Console.WriteLine("## A-3. 踏破に失敗した会戦の内訳（**どちらの道で止まったか**）");
        Console.WriteLine();
        Console.WriteLine("**「残った道」は終了時に未踏破の敵が残っていた道**（両方残る＝早い段階で詰んだ）。"
            + "**「最後に戦った道」**は別の数え方で、止まった場所そのものを指す。");
        Console.WriteLine();
        Console.WriteLine($"| 割り当て | 帯 | 失敗数 | 北(`{MapRoads[0].Col}`)だけ残る | 南(`{MapRoads[1].Col}`)だけ残る "
            + "| 両方残る | 最後は北 | 最後は南 | 全滅で終了 | 上限 24 戦 | 抜いた部隊数 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (string asg in new[] { "正", "逆" })
            foreach (int band in new[] { 0, 1 })
            {
                var f = res[(asg, band)].Fail;
                Console.WriteLine($"| {asg} | seed {band * MapSeeds}..{(band + 1) * MapSeeds - 1} | {f.Total} "
                    + $"| **{f.NorthOnly}** | **{f.SouthOnly}** | {f.Both} | {f.LastNorth} | {f.LastSouth} "
                    + $"| {f.Wiped} | {f.Capped} | {f.Cleared:F2} / 4 |");
            }
        Console.WriteLine();

        Console.WriteLine("## A-4. 5 枚の `与えた総害`（**参考**・1 会戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 正 | 逆 | 差（正 − 逆） |");
        Console.WriteLine("|---|--:|--:|--:|");
        foreach (string id in watch)
        {
            double p = res[("正", 0)].Harm[id], q = res[("逆", 0)].Harm[id];
            Console.WriteLine($"| {Def(id).Name} | {p:F1} | {q:F1} | **{p - q:+0.0;-0.0}** |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {secs:F1} 秒（{4 * MapSeeds} 会戦）。");
        Console.WriteLine();
        Console.WriteLine("## A-5. 予測の当落");
        Console.WriteLine();
        Console.WriteLine("| # | 予測 |");
        Console.WriteLine("|---|---|");
        foreach (var (k, t) in MapPredictions) Console.WriteLine($"| {k} | {t} |");
    }

    // =================================================================================
    // stage band（第167期） —— 道中の回復で、2〜3戦のマップを遊べる難度帯に入れる
    //
    // 指示書は design/PHASE167_MAPBAND_SPEC.md。前提は第166期（F1〜F3）。
    //
    // **触るのはこのファイルだけ。** 道は第166期の組（北 `P23` / 南 `P45`）をそのまま使い、
    // 隊は `CompareBuilds()` の行をそのまま取る（第166期 F1 ＝ 隊割りのミスを直すため）。
    // **敵も駒も1体も作らない。engine には1行も足さない。**
    //
    //     dotnet run --project BattleSim -c Release 0 stage band phase0
    //     dotnet run --project BattleSim -c Release 0 stage band
    //     dotnet run --project BattleSim -c Release 0 stage band b     # 第3隊を第2候補にして確かめる
    // =================================================================================

    const int BandSeeds = 800;            // 1 帯あたりの seed 本数（指示書 §3-1）
    const int BandCap = 24;               // 1 マップの戦闘回数の上限
    const double BandLo = 30.0;           // 線1: 2本抜き率の帯（下）
    const double BandHi = 70.0;           // 線1: 同（上）
    const double BandContrastLine = 0.5;  // 線2: 対比（点）
    const double BandNoiseMul = 3.0;      // 線2: ノイズの何倍か
    const double BandRallyLo = 20.0;      // 線3: 挽回率の帯（下）
    const double BandRallyHi = 80.0;      // 線3: 同（上）
    const int BandProbeSeeds = 200;       // 第3隊の選定に使う seed 本数

    /// <summary>振るつまみ（指示書 §2）。<b>勝った隊が次の戦闘へ入る前</b>に効く割合回復。</summary>
    static readonly int[] BandRecover = { 0, 25, 50, 75, 100 };

    const string BandRowA = "反撃 (ヒサ×カド)";
    const string BandRowB = "突き返し (ハネ×ウツ)";

    static Formation FormOf(string row) =>
        CompareBuilds().FirstOrDefault(r => r.Name == row).F
        ?? throw new ArgumentException("その行が `CompareBuilds()` に無い: " + row);

    /// <summary>
    /// 2 隊を組む。<b>席はその行のまま</b>（指示書 §1-2）。
    /// 駒が重なったら、重なった駒はカド隊に残し、ハネ隊の席には
    /// 第165期 部B の「どこでも同じ」群から <c>checkup</c> 単独が高い順に空いている駒を入れる。
    /// </summary>
    static (Formation A, Formation B, (string Slot, string From, string To)[] Sub) BandSquads()
    {
        Formation a = FormOf(BandRowA), b0 = FormOf(BandRowB);
        var inA = new HashSet<string>(a.Occupied().Select(o => o.Def.Id));
        var used = new HashSet<string>(inA);
        foreach (var o in b0.Occupied()) used.Add(o.Def.Id);

        var b = new Formation();
        var subs = new List<(string, string, string)>();
        foreach (var o in b0.Occupied())
        {
            if (!inA.Contains(o.Def.Id)) { b[o.Slot] = o.Def; continue; }
            string pick = MapSteadyBySolo.First(x => !used.Contains(x));
            used.Add(pick);
            b[o.Slot] = Def(pick);
            subs.Add((SlotName[o.Slot], o.Def.Name, Def(pick).Name));
        }
        return (a, b, subs.ToArray());
    }

    /// <summary>ある編成の、第2〜5波に対する単発勝率（新品どうし）。</summary>
    static double[] BandSolo(Formation f, int seeds)
    {
        var res = new double[4];
        Parallel.For(0, 4, k =>
        {
            int win = 0;
            for (int seed = 0; seed < seeds; seed++)
                if (BattleEngine.Run(f, EnemyCatalog.Stages[k + 1].Enemy, seed, verbose: false).PlayerWon) win++;
            res[k] = 100.0 * win / seeds;
        });
        return res;
    }

    /// <summary>
    /// 第3隊の候補（指示書 §1-3）——<b>上の2隊と駒が重ならず、
    /// 第2〜5波の単発勝率の平均が 61 行の中央に最も近い行</b>。
    /// 戻りは（候補の並び・61 行の中央値）で、先頭が第1候補・2 番目が第2候補。
    /// </summary>
    static ((string Name, Formation F, double Mean, double[] Solo)[] Cand, double Median61) BandReserve(
        Formation a, Formation b, int seeds)
    {
        var rows = CompareBuilds();
        var means = new double[rows.Length];
        var solos = new double[rows.Length][];
        for (int i = 0; i < rows.Length; i++)
        {
            solos[i] = BandSolo(rows[i].F, seeds);
            means[i] = solos[i].Average();
        }
        double med = Median(means.ToArray());
        var taken = new HashSet<string>(a.Occupied().Select(o => o.Def.Id)
            .Concat(b.Occupied().Select(o => o.Def.Id)));
        var cand = Enumerable.Range(0, rows.Length)
            .Where(i => rows[i].F.Occupied().All(o => !taken.Contains(o.Def.Id)))
            .OrderBy(i => Math.Abs(means[i] - med)).ThenBy(i => i)
            .Select(i => (rows[i].Name, rows[i].F, means[i], solos[i]))
            .ToArray();
        return (cand, med);
    }

    // ---------------- マップの進行 ----------------

    /// <summary>1 マップぶんの結果。<b>どれも読んで分岐しない計数である。</b></summary>
    readonly record struct BandOut(
        int Cleared, double Partial, bool TwoRunA, bool TwoRunB,
        bool ReserveUsed, bool ReserveCleared, int LastRoad, int LastIdx, int Battles);

    /// <summary>
    /// 1 マップぶん回す。第166期の <see cref="MapOnce"/> との違いは 2 つだけ——
    /// <b>(1) 勝った隊が次の戦闘へ入る前に割合ぶん回復する</b>（指示書 §2）と、
    /// <b>(2) 隊が全滅したら控えの第3隊がその道の続きへ 1 回だけ出る</b>（§1-3）。
    /// </summary>
    static BandOut BandOnce(Formation[] starters, Formation? reserve, int[] assign,
                            IReadOnlyList<Formation>[] roads, int seed, int pct)
    {
        var P = new List<UnitState>?[3];
        var asg = new int[3];
        for (int s = 0; s < 2; s++)
        {
            P[s] = BattleEngine.Materialize(starters[s], BattleContext.PlayerTeam);
            asg[s] = assign[s];
        }
        asg[2] = -1;   // 控え（まだ出ていない）

        var E = roads.Select(r => new List<UnitState>?[r.Count]).ToArray();
        var cleared = roads.Select(r => new bool[r.Count]).ToArray();
        var maxHp = roads.Select(r => r.Select(f => f.Occupied().Sum(o => o.Def.MaxHp)).ToArray()).ToArray();
        var hpNow = roads.Select((r, i) => maxHp[i].ToArray()).ToArray();

        int total = roads.Sum(r => r.Count);
        var clearedBy = new int[3];          // 隊ごとの、自分の担当の道で抜いた数
        bool usedReserve = false;
        int reserveRoad = -1;
        int battles = 0, lastRoad = -1, lastIdx = -1;

        while (battles < BandCap)
        {
            bool acted = false;
            for (int s = 0; s < 3; s++)
            {
                if (P[s] is not { } pu || asg[s] < 0) continue;
                int road = asg[s];
                if (!cleared[road].Any(c => !c))
                {
                    road = Array.FindIndex(cleared, r => r.Any(c => !c));
                    if (road < 0) break;
                }
                int idx = Array.FindIndex(cleared[road], c => !c);
                lastRoad = road; lastIdx = idx;

                E[road][idx] ??= BattleEngine.Materialize(roads[road][idx], BattleContext.EnemyTeam);
                var eu = E[road][idx]!;

                BattleResult r = BattleEngine.Run(pu, eu, DeriveSeed(seed, battles), verbose: false);
                battles++; acted = true;

                var aliveP = pu.Where(u => u.IsAlive).ToList();
                var aliveE = eu.Where(u => u.IsAlive).ToList();
                hpNow[road][idx] = aliveE.Sum(u => u.Hp);

                if (aliveE.Count == 0)
                {
                    cleared[road][idx] = true; E[road][idx] = null; hpNow[road][idx] = 0;
                    if (road == (s == 2 ? reserveRoad : assign[s])) clearedBy[s]++;
                }
                else E[road][idx] = EngagementEngine.CrossBoundary(aliveE);

                // **勝った隊だけが、次の戦闘へ入る前に回復する**（指示書 §2）。
                // 負けて生存者が残った隊（第166期 F2 の実測で 0.04%）には掛けない。
                if (aliveP.Count == 0)
                {
                    P[s] = null;
                    if (!usedReserve && reserve is not null)
                    {
                        usedReserve = true; reserveRoad = road; asg[2] = road;
                        P[2] = BattleEngine.Materialize(reserve, BattleContext.PlayerTeam);
                    }
                }
                else
                {
                    P[s] = EngagementEngine.CrossBoundary(aliveP, pu,
                        r.PlayerWon && pct > 0 ? new RecoverRule(pct, false) : null);
                }

                int done = cleared.Sum(x => x.Count(c => c));
                if (done >= total || P.All(x => x is null)) goto fin;
            }
            if (!acted) break;
        }
    fin:
        int cl = cleared.Sum(x => x.Count(c => c));
        double partial = cl;
        for (int r0 = 0; r0 < roads.Length; r0++)
            for (int i = 0; i < roads[r0].Count; i++)
                if (!cleared[r0][i] && maxHp[r0][i] > 0)
                    partial += (double)(maxHp[r0][i] - hpNow[r0][i]) / maxHp[r0][i];

        bool rallied = usedReserve && reserveRoad >= 0 && cleared[reserveRoad].All(c => c);
        return new BandOut(cl, partial, clearedBy[0] >= 2, clearedBy[1] >= 2,
                           usedReserve, rallied, lastRoad, lastIdx, battles);
    }

    /// <summary>seed 帯 1 本ぶんの集計。</summary>
    sealed record BandStat(double Full, double Partial, double TwoA, double TwoB,
                           int ReserveN, double Rally, double Battles, int[,] StopAt,
                           double ClearedAvg);

    static BandStat BandBand(Formation[] starters, Formation? reserve, int[] assign,
                             IReadOnlyList<Formation>[] roads, int seed0, int pct)
    {
        var o = new BandOut[BandSeeds];
        Parallel.For(0, BandSeeds, i => o[i] = BandOnce(starters, reserve, assign, roads, seed0 + i, pct));
        int total = roads.Sum(r => r.Count);
        var stop = new int[roads.Length, roads.Max(r => r.Count)];
        foreach (var x in o)
            if (x.Cleared < total && x.LastRoad >= 0) stop[x.LastRoad, x.LastIdx]++;
        int rn = o.Count(x => x.ReserveUsed);
        return new BandStat(
            100.0 * o.Count(x => x.Cleared >= total) / BandSeeds,
            o.Average(x => x.Partial),
            100.0 * o.Count(x => x.TwoRunA) / BandSeeds,
            100.0 * o.Count(x => x.TwoRunB) / BandSeeds,
            rn, rn == 0 ? 0 : 100.0 * o.Count(x => x.ReserveCleared) / rn,
            o.Average(x => (double)x.Battles), stop, o.Average(x => (double)x.Cleared));
    }

    /// <summary>
    /// 1 戦目の勝率と、1 戦目を抜いた時点の平均残り HP 割合・平均生存枚数
    /// （指示書 §5-1 の併記）。<b>マップとは独立に、新品どうしで 1 戦だけ測る。</b>
    /// </summary>
    static (double Win, double Hp, double Alive) BandFirst(Formation f, Formation foe, int seeds)
    {
        int win = 0; double hp = 0, al = 0;
        for (int seed = 0; seed < seeds; seed++)
        {
            var pu = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var eu = BattleEngine.Materialize(foe, BattleContext.EnemyTeam);
            if (!BattleEngine.Run(pu, eu, seed, verbose: false).PlayerWon) continue;
            win++;
            hp += 100.0 * pu.Where(u => u.IsAlive).Sum(u => u.Hp) / pu.Sum(u => u.Def.MaxHp);
            al += pu.Count(u => u.IsAlive);
        }
        return (100.0 * win / seeds, win == 0 ? 0 : hp / win, win == 0 ? 0 : al / win);
    }

    // ---------------- 入口 ----------------

    static void BandEntry(string arg)
    {
        string a = arg.Trim();
        if (a.StartsWith("phase0")) { BandPhase0(); return; }
        BandMain(a.StartsWith("b"));
    }

    // ---------------- Phase 0（戦闘は下見のぶんだけ） ----------------

    static void BandPhase0()
    {
        Console.WriteLine("# 第167期 Phase 0 —— 道中の回復で 2〜3戦のマップを帯に入れる");
        Console.WriteLine();

        var (a, b, subs) = BandSquads();

        Console.WriteLine("## Q0-1. 2 行の駒の一覧と重なり");
        Console.WriteLine();
        Console.WriteLine("| 隊 | 行 | 席（その行のまま） |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine($"| カド隊 | `{BandRowA}` | {Show(FormOf(BandRowA))} |");
        Console.WriteLine($"| ハネ隊 | `{BandRowB}` | {Show(FormOf(BandRowB))} |");
        Console.WriteLine();
        if (subs.Length == 0)
            Console.WriteLine("**重なりは 0 件。** 入れ替えは行っていない。");
        else
        {
            Console.WriteLine($"**重なりが {subs.Length} 件。** §1-2 の規則どおり、"
                + "重なった駒はカド隊に残し、ハネ隊の席を"
                + "「どこでも同じ」群（`checkup` 単独の降順）の空いている駒で埋めた。");
            Console.WriteLine();
            Console.WriteLine("| 席 | もとの駒（カド隊に残す） | 入れた駒 |");
            Console.WriteLine("|---|---|---|");
            foreach (var (sl, f, t) in subs) Console.WriteLine($"| {sl} | {f} | **{t}** |");
            Console.WriteLine();
            Console.WriteLine($"**入れ替え後のハネ隊**: {Show(b)}");
        }
        Console.WriteLine();
        Console.WriteLine($"### 入れ替え後の単発勝率（新品どうし・seed 0..{BandProbeSeeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("| 隊 | 第二波 | 第三波 | 第四波 | 第五波 | 平均 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        foreach (var (nm, f) in new[] { ("カド隊", a), ("ハネ隊", b) })
        {
            var s = BandSolo(f, BandProbeSeeds);
            Console.WriteLine($"| {nm} | " + string.Join(" | ", s.Select(x => $"{x:F1}%"))
                + $" | **{s.Average():F1}%** |");
        }
        Console.WriteLine();

        Console.WriteLine("## Q0-2. 第3隊の候補");
        Console.WriteLine();
        var (cand, med) = BandReserve(a, b, BandProbeSeeds);
        Console.WriteLine($"`CompareBuilds()` {CompareBuilds().Length} 行の"
            + $"「第2〜5波の単発勝率の平均」の**中央値は {med:F1}%**。"
            + $"上の 2 隊と駒が重ならない行は **{cand.Length} 行**で、中央値に近い順に:");
        Console.WriteLine();
        Console.WriteLine("| # | 行 | 第二波 | 第三波 | 第四波 | 第五波 | 平均 | 中央値との差 | 採否 |");
        Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|--:|---|");
        for (int i = 0; i < Math.Min(6, cand.Length); i++)
        {
            var c = cand[i];
            string mark = i == 0 ? "**第1候補**" : i == 1 ? "第2候補" : "—";
            Console.WriteLine($"| {i + 1} | `{c.Name}` | "
                + string.Join(" | ", c.Solo.Select(x => $"{x:F1}%"))
                + $" | {c.Mean:F1}% | {Math.Abs(c.Mean - med):F1}pt | {mark} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**第1候補の席**: {Show(cand[0].F)}");
        Console.WriteLine();

        Console.WriteLine("## Q0-3. 境界の包みは回復の規則を受けられるか");
        Console.WriteLine();
        string eng = ReadRepo("BattleCore/Engagement.cs");
        var el = eng.Split('\n');
        int cb = Array.FindIndex(el, l => l.Contains("public static List<UnitState> CrossBoundary"));
        for (int i = cb; i >= 0 && i < cb + 6; i++)
            Console.WriteLine($"    Engagement.cs:{i + 1}  {el[i].TrimEnd()}");
        Console.WriteLine();
        bool ok3 = cb >= 0 && el.Skip(cb).Take(6).Any(l => l.Contains("? recover"));
        Console.WriteLine(ok3
            ? "**○ —— 受けられる。** 第166期が足した包みが省略可能な引数として持っているので、"
              + "**`BattleCore` には 1 行も足さない。**"
            : "**× —— 受けられない。** 指示書 §2 の但し書きどおり、ここで止まって報告する。");
        Console.WriteLine();

        Console.WriteLine("## Q0-4. 渇き（第三波の回復封じ）は境界の回復に効くか");
        Console.WriteLine();
        int at = Array.FindIndex(el, l => l.Contains("rec.HpPercent > 0"));
        if (at >= 0)
            foreach (var l in el.Skip(Math.Max(0, at - 4)).Take(6))
                Console.WriteLine("    " + l.TrimEnd());
        Console.WriteLine();
        bool heal = at >= 0 && el.Skip(at).Take(2).Any(l => l.Contains("u.Hp = Math.Min"));
        Console.WriteLine(heal
            ? "**効かない。** 境界の回復は **`Hp` を直接足していて、回復の窓口を通らない**"
              + "——渇きも支援拒否も「戦闘中に味方が配るもの」に対する規則なので、"
              + "**北の道の第三波は、道中の回復を 1 点も削らない。**"
            : "**要確認**（走査で該当行が引けなかった）。");
        Console.WriteLine();

        Console.WriteLine("## Q0-5. 第101期（境界の回復を入れた期）との重なり");
        Console.WriteLine();
        string p101 = ReadRepo("design/PHASE101_RECOVER.md");
        foreach (var l in p101.Split('\n').Where(l => l.TrimStart().StartsWith("> ")).Take(3))
            Console.WriteLine(l.TrimEnd());
        Console.WriteLine();
        Console.WriteLine("**重なる**——第101期は**同じつまみ（境界ごとの割合回復）を 0 / 25 / 50 / 100 で**"
            + "73 行 × 地点3波に当てている。ただし**測った量が違う**"
            + "（あちらは突破率と 0% の行の数・こちらは 1 隊が道 1 本を抜く率）ので、"
            + "**この期は「HP だけの回復では足りない」という第101期の結論を、"
            + "2〜3戦のマップの尺度で引き直すことになる。**");
        Console.WriteLine();

        Console.WriteLine("## Q0-6. 走行時間の見積もり");
        Console.WriteLine();
        var roads = MapRoads.Select(r => (IReadOnlyList<Formation>)ColOf(r.Col).Squads).ToArray();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var probe = BandBand(new[] { a, b }, cand[0].F, new[] { 1, 0 }, roads, 0, 50);
        sw.Stop();
        double per = sw.Elapsed.TotalSeconds;
        int points = BandRecover.Length * 2 * 2 + 4;   // 5 回復量 × 2 割り当て × 2 帯 ＋ 第2候補
        Console.WriteLine($"1 点（seed {BandSeeds} 本）が **{per:F1} 秒**。"
            + $"本体は **{points} 点**なので **約 {per * points:F0} 秒**"
            + $"（{(per * points > 600 ? "**10 分を超える。止まって報告する**" : "10 分の内側")}）。");
        Console.WriteLine();
        Console.WriteLine($"下見の 1 点（正・回復 50%）: 踏破 {probe.Full:F1}% ／ 部分点 {probe.Partial:F2} ／ "
            + $"2本抜き カド隊 {probe.TwoA:F1}% ・ ハネ隊 {probe.TwoB:F1}% ／ 挽回 {probe.Rally:F1}%（分母 {probe.ReserveN}）。");
        Console.WriteLine();

        Console.WriteLine("## 予測（**本体を回す前に書いた。外れても消さない**）");
        Console.WriteLine();
        Console.WriteLine("| # | 予測 |");
        Console.WriteLine("|---|---|");
        foreach (var (k, t) in BandPredictions) Console.WriteLine($"| {k} | {t} |");
    }

    public static readonly (string Key, string Text)[] BandPredictions =
    {
        ("K1", "**線1 は ×**——第101期が同じつまみ（0/25/50/100）で"
             + "「持ち越しの代金は HP ではなく体」を出している。HP だけ戻しても帯に入らない"),
        ("K2", "**カド隊の北（粛）は回復 100 でも 30% に届かない**"
             + "——R251 どおり、第二波の 1 戦目で落ちる。回復は 1 戦目には効かない"),
        ("K3", "**ハネ隊は 2 隊のうち先に帯を抜けて 70% を超える**"
             + "——`compare` の行なので殴る駒を持ち、第2〜4波の単発が 95% 以上ある"),
        ("K4", "**挽回率は回復量にほとんど依らない**——第3隊は新品で出るので、"
             + "道中の回復は第3隊の 1 戦目に 1 点も効かない"),
        ("K5", "**対比は回復量が増えるほど小さくなる**——回復は弱い側の道を救うので、"
             + "割り当ての違いが埋まる"),
    };

    // ---------------- 本体 ----------------

    static void BandMain(bool second)
    {
        var (a, b, subs) = BandSquads();
        var roads = MapRoads.Select(r => (IReadOnlyList<Formation>)ColOf(r.Col).Squads).ToArray();
        var (cand, med) = BandReserve(a, b, BandProbeSeeds);
        Formation res = cand[second ? 1 : 0].F;
        string resName = cand[second ? 1 : 0].Name;
        var squads = new[] { a, b };
        int[] straight = { 1, 0 };   // 正: カド隊 → 南(1) ／ ハネ隊 → 北(0)
        int[] cross = { 0, 1 };      // 逆

        Console.WriteLine("# 第167期 —— 道中の回復で、2〜3戦のマップを遊べる難度帯に入れる");
        Console.WriteLine();
        Console.WriteLine($"**第3隊は{(second ? "第2候補" : "第1候補")} `{resName}`**（中央値 {med:F1}%）。");
        Console.WriteLine($"カド隊 {Show(a)}／ハネ隊 {Show(b)}"
            + (subs.Length == 0 ? "" : $"（{subs.Length} 枠を入れ替えた）") + "。");
        Console.WriteLine($"道: 北 `{MapRoads[0].Col}` ／ 南 `{MapRoads[1].Col}`。"
            + $"seed 帯 2 本（0..{BandSeeds - 1} / {BandSeeds}..{2 * BandSeeds - 1}）。");
        Console.WriteLine();

        // 5 回復量 × 2 割り当て × 2 帯
        var P = new BandStat[BandRecover.Length, 2];   // [pct, band]
        var Q = new BandStat[BandRecover.Length, 2];
        for (int p = 0; p < BandRecover.Length; p++)
            for (int bd = 0; bd < 2; bd++)
            {
                P[p, bd] = BandBand(squads, res, straight, roads, bd * BandSeeds, BandRecover[p]);
                Q[p, bd] = BandBand(squads, res, cross, roads, bd * BandSeeds, BandRecover[p]);
            }

        // ---- 表1 ----
        Console.WriteLine("## 1. 回復量 × 隊 × 道 の 2本抜き率（線1 の本体）");
        Console.WriteLine();
        Console.WriteLine("**2本抜き率 ＝ 1 隊が自分の道の 2 部隊を続けて抜いた割合**（第3隊の加勢なし）。"
            + "正の割り当ては カド隊 → 南 ／ ハネ隊 → 北、逆はその入れ替え。");
        Console.WriteLine();
        Console.WriteLine("| 回復 | カド隊×南（正） | ハネ隊×北（正） | 線1（正） | カド隊×北（逆） | ハネ隊×南（逆） | 踏破率 正 | 踏破率 逆 |");
        Console.WriteLine("|--:|--:|--:|:-:|--:|--:|--:|--:|");
        var pass1 = new bool[BandRecover.Length];
        for (int p = 0; p < BandRecover.Length; p++)
        {
            double ka = P[p, 0].TwoA, ha = P[p, 0].TwoB;
            bool inA = ka >= BandLo && ka <= BandHi, inB = ha >= BandLo && ha <= BandHi;
            pass1[p] = inA && inB;
            string mark = inA && inB ? "**○**" : (inA || inB ? "△" : "×");
            Console.WriteLine($"| {BandRecover[p]}% | {ka:F1}% | {ha:F1}% | {mark} "
                + $"| {Q[p, 0].TwoA:F1}% | {Q[p, 0].TwoB:F1}% "
                + $"| {P[p, 0].Full:F1}% | {Q[p, 0].Full:F1}% |");
        }
        Console.WriteLine();
        for (int p = 0; p < BandRecover.Length; p++)
        {
            double ka = P[p, 0].TwoA, ha = P[p, 0].TwoB;
            bool inA = ka >= BandLo && ka <= BandHi, inB = ha >= BandLo && ha <= BandHi;
            if (inA ^ inB)
                Console.WriteLine($"- 回復 {BandRecover[p]}%: **△** —— "
                    + $"{(inA ? "ハネ隊×北" : "カド隊×南")} が "
                    + $"{((inA ? ha : ka) < BandLo ? "下" : "上")}へ外れた"
                    + $"（{(inA ? ha : ka):F1}%）");
        }
        Console.WriteLine();
        Console.WriteLine("### 1 戦目の勝率と、抜いた時点の残り（**マップとは独立に新品どうしで 1 戦だけ**）");
        Console.WriteLine();
        Console.WriteLine("| 隊 | 道 | 1 戦目 | 勝率 | 抜いた時点の残り HP 割合 | 同 生存枚数 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|");
        foreach (var (nm, f) in new[] { ("カド隊", a), ("ハネ隊", b) })
            for (int r = 0; r < roads.Length; r++)
            {
                var (w, h, al) = BandFirst(f, roads[r][0], BandProbeSeeds);
                Console.WriteLine($"| {nm} | {MapRoads[r].Name} | 第{WaveIdxOf(roads[r][0]) + 1}波 "
                    + $"| {w:F1}% | {h:F1}% | {al:F2} 枚 |");
            }
        Console.WriteLine();

        // ---- 表2 ----
        Console.WriteLine("## 2. 部分点と対比（線2）");
        Console.WriteLine();
        Console.WriteLine("**部分点 ＝ 抜いた敵部隊の数（0〜4）＋ 残った敵部隊の削り**"
            + "（失った HP ÷ 定義上の総最大 HP）。**対比 ＝ 正 − 逆。**");
        Console.WriteLine();
        Console.WriteLine("| 回復 | 正 部分点 | 逆 部分点 | **対比** | 帯1 の再現（正/逆） | ノイズ | 対比 ÷ ノイズ | 線2 |");
        Console.WriteLine("|--:|--:|--:|--:|--:|--:|--:|:-:|");
        var pass2 = new bool[BandRecover.Length];
        for (int p = 0; p < BandRecover.Length; p++)
        {
            double c0 = P[p, 0].Partial - Q[p, 0].Partial;
            double c1 = P[p, 1].Partial - Q[p, 1].Partial;
            double noise = (Math.Abs(P[p, 0].Partial - P[p, 1].Partial)
                          + Math.Abs(Q[p, 0].Partial - Q[p, 1].Partial)) / 2.0;
            double ratio = noise <= 0 ? double.PositiveInfinity : Math.Abs(c0) / noise;
            pass2[p] = c0 >= BandContrastLine && ratio >= BandNoiseMul;
            Console.WriteLine($"| {BandRecover[p]}% | {P[p, 0].Partial:F3} | {Q[p, 0].Partial:F3} "
                + $"| **{c0:+0.000;-0.000}** | {P[p, 1].Partial:F3} / {Q[p, 1].Partial:F3} "
                + $"| {noise:F3} | {(double.IsInfinity(ratio) ? "∞" : ratio.ToString("F1"))} "
                + $"| {(pass2[p] ? "**○**" : "×")} |（対比の再現 {c1:+0.000;-0.000}）");
        }
        Console.WriteLine();

        // ---- 表3 ----
        Console.WriteLine("## 3. 挽回率（線3）");
        Console.WriteLine();
        Console.WriteLine("**挽回率 ＝ 第3隊が出た seed のうち、第3隊がその道を抜き切った割合。**");
        Console.WriteLine();
        Console.WriteLine("| 回復 | 正 第3隊が出た | 正 挽回率 | 逆 第3隊が出た | 逆 挽回率 | 線3（逆） |");
        Console.WriteLine("|--:|--:|--:|--:|--:|:-:|");
        var pass3 = new bool[BandRecover.Length];
        for (int p = 0; p < BandRecover.Length; p++)
        {
            double rq = Q[p, 0].Rally;
            pass3[p] = rq >= BandRallyLo && rq <= BandRallyHi;
            Console.WriteLine($"| {BandRecover[p]}% | {P[p, 0].ReserveN} / {BandSeeds} | {P[p, 0].Rally:F1}% "
                + $"| {Q[p, 0].ReserveN} / {BandSeeds} | **{rq:F1}%** | {(pass3[p] ? "**○**" : "×")} |");
        }
        Console.WriteLine();

        // ---- 表4 ----
        Console.WriteLine("## 4. 失敗の内訳（どの道の何戦目で止まったか）");
        Console.WriteLine();
        Console.WriteLine("| 回復 | 割り当て | 失敗数 | " + string.Join(" | ",
            MapRoads.SelectMany((rd, r) => Enumerable.Range(0, roads[r].Count)
                .Select(i => $"{rd.Name} {i + 1}戦目"))) + " | 平均戦闘回数 |");
        Console.WriteLine("|--:|---|--:|" + string.Concat(Enumerable.Repeat("--:|", roads.Sum(r => r.Count) + 1)));
        for (int p = 0; p < BandRecover.Length; p++)
            foreach (var (nm, st) in new[] { ("正", P[p, 0]), ("逆", Q[p, 0]) })
            {
                var cells = new List<string>();
                for (int r = 0; r < roads.Length; r++)
                    for (int i = 0; i < roads[r].Count; i++) cells.Add(st.StopAt[r, i].ToString());
                int fail = (int)Math.Round(BandSeeds * (100.0 - st.Full) / 100.0);
                Console.WriteLine($"| {BandRecover[p]}% | {nm} | {fail} | "
                    + string.Join(" | ", cells) + $" | {st.Battles:F2} |");
            }
        Console.WriteLine();

        // ---- 表5 ----
        Console.WriteLine("## 5. 回復 100% でも 2本抜き率が 30% に届かない隊×道");
        Console.WriteLine();
        int last = BandRecover.Length - 1;
        var bad = new List<string>();
        if (P[last, 0].TwoA < BandLo) bad.Add($"**カド隊 × 南**（{P[last, 0].TwoA:F1}%）");
        if (P[last, 0].TwoB < BandLo) bad.Add($"**ハネ隊 × 北**（{P[last, 0].TwoB:F1}%）");
        if (Q[last, 0].TwoA < BandLo) bad.Add($"**カド隊 × 北**（{Q[last, 0].TwoA:F1}%）");
        if (Q[last, 0].TwoB < BandLo) bad.Add($"**ハネ隊 × 南**（{Q[last, 0].TwoB:F1}%）");
        Console.WriteLine(bad.Count == 0
            ? "**該当なし。** 回復だけで 4 通りすべてが 30% を超える。"
            : "**回復では解けない＝敵を軽くする必要がある箇所**: " + string.Join(" ／ ", bad) + "。");
        Console.WriteLine();

        // ---- 表6 ----
        Console.WriteLine("## 6. 自己検査 —— 回復 0 が第166期と一致するか");
        Console.WriteLine();
        Console.WriteLine("**第166期の隊（`stage map` の `MapSetup`）で、第3隊なし・回復 0 を 1 回だけ回す。**");
        Console.WriteLine();
        var (m1, m2, mroads) = MapSetup(fillerB: false);
        var msq = new[] { m1, m2 };
        Console.WriteLine("| 割り当て | 帯 | 踏破率 | 抜いた部隊数 | 第166期の値 | 一致 |");
        Console.WriteLine("|---|---|--:|--:|--:|:-:|");
        var want = new[] { (1.00, 1.00), (0.04, 0.03) };
        for (int k = 0; k < 2; k++)
            for (int bd = 0; bd < 2; bd++)
            {
                var st = BandBand(msq, null, k == 0 ? straight : cross, mroads, bd * BandSeeds, 0);
                double w = bd == 0 ? want[k].Item1 : want[k].Item2;
                Console.WriteLine($"| {(k == 0 ? "正" : "逆")} | seed {bd * BandSeeds}.. | {st.Full:F1}% "
                    + $"| **{st.ClearedAvg:F2}** | {w:F2} | {(Math.Abs(st.ClearedAvg - w) < 0.005 ? "**○**" : "×")} |");
            }
        Console.WriteLine();

        // ---- 判定 ----
        Console.WriteLine("## 7. 判定");
        Console.WriteLine();
        Console.WriteLine("| 回復 | 線1 | 線2 | 線3 | 本丸（3 つ同時） |");
        Console.WriteLine("|--:|:-:|:-:|:-:|:-:|");
        bool main = false;
        for (int p = 0; p < BandRecover.Length; p++)
        {
            bool all = pass1[p] && pass2[p] && pass3[p];
            main |= all;
            Console.WriteLine($"| {BandRecover[p]}% | {(pass1[p] ? "○" : "×")} | {(pass2[p] ? "○" : "×")} "
                + $"| {(pass3[p] ? "○" : "×")} | {(all ? "**○**" : "×")} |");
        }
        Console.WriteLine();
        Console.WriteLine(main
            ? "**本丸: ○ —— 回復だけで検証用マップが立つ回復量がある。**"
            : "**本丸: × —— 3 つを同時に満たす回復量は無い。線は動かさない（指示書 §3-2）。**");
        Console.WriteLine();
        Console.WriteLine("## 8. 予測の当落");
        Console.WriteLine();
        Console.WriteLine("| # | 予測 |");
        Console.WriteLine("|---|---|");
        foreach (var (k, t) in BandPredictions) Console.WriteLine($"| {k} | {t} |");
    }
}
