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
}
