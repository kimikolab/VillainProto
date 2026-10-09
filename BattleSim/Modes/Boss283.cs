using System.Text.RegularExpressions;
using BattleCore;
using static Common;
using BA = BurnAuditDiag;

// =====================================================================================
// boss283 —— 第283期「A2 の確定 ＋ ボスの標台の寿命探索（ヒーラーは1枚まで）」。
// 指示書は design/PHASE283_BOSS_MARK_SQUAD_SPEC.md ／ 報告は design/PHASE283_BOSS_MARK_SQUAD.md。
//
//     dotnet run --project BattleSim -c Release 0 boss283 p0      # Phase 0: 回復・破片・味方の標の書き手（ソースを機械で走査）と保持者・ボスで実際に働くか・1戦の所要
//     dotnet run --project BattleSim -c Release 0 boss283 grid    # 段1: 固定枠（トメ T1n ＋ ザン）＋ 探索枠3（候補 × 席 120 通り）× ボス・足切り → seed 0..199 → 届いた台の帯B とドルガ対照
//     dotnet run --project BattleSim -c Release 0 boss283 ctl     # 段1 の後: 代表の台 × トメの版（T1n ／ T1 ／ T1-s ／ T0 ／ ドルガ）と余裕（隊の全員の最大HP × k）
//     dotnet run --project BattleSim -c Release 0 boss283 check   # 自己検査
//     dotnet run --project BattleSim -c Release 0 boss283 log <席の並び（5 名・前1,前3,中央,後1,後3 の Id をカンマ）> [seed]
// =====================================================================================
static class Boss283Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "p0";
        switch (mode)
        {
            case "p0": P0(); return;
            case "grid": Grid(); return;
            case "ctl": Ctl(); return;
            case "check": Check(); return;
            case "log": LogOne(args.Length > 3 ? args[3] : "", args.Length > 4 ? int.Parse(args[4]) : 0); return;
            default: Console.WriteLine("boss283: モードは p0 / grid / ctl / check / log。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 候補（Phase 0 で決めた・測る前に固定）
    // ---------------------------------------------------------------------------------
    /// <summary>固定枠（指示書 §2-1）。トメは規定（第283期 T1n）。</summary>
    static readonly UnitDef[] Fixed = { UnitCatalog.Tome, UnitCatalog.ZanZNb };   // 第300期: ザンは第299期の規定（`ZanZNb`）に固定（規定は ZM-a になった）

    /// <summary>
    /// 寿命側の候補（ヒーラー以外）。Phase 0 §2 の棚卸し: 被ダメを減らす・肩代わりする・逸らす駒 ＋ 味方に標を書く駒（ザンの供給の入口）。
    /// ボスは全体攻撃1体なので、単体攻撃への介入（庇う・後備え・引き寄せ）は効かないが、**ヒサ・ソラは「味方に標を書く」ので候補に残す**。
    /// </summary>
    internal static readonly UnitDef[] LifePool =
    {
        UnitCatalog.HisaHK0, UnitCatalog.Sora, UnitCatalog.DohaD0, UnitCatalog.Golm, UnitCatalog.Ban, UnitCatalog.Kubi,
        UnitCatalog.Sekki, UnitCatalog.Gald, UnitCatalog.Kado, UnitCatalog.Uke, UnitCatalog.Gan,
    };

    /// <summary>ヒーラー（Phase 0 §1 の機械的定義 ＝ 味方に回復または破片を2回以上書ける駒）。台に最大1枚。</summary>
    internal static UnitDef[] HealPool => Healers().Select(h => h.Def).ToArray();

    /// <summary>
    /// 第296期より前の一覧（規定のヒサを数えない）。<b>過去の器具（第283〜295期）はこちらを読む</b>——規定のヒサを HK-b にして
    /// <see cref="HealPool"/> にヒサを足したので、過去の器具の出力が動かないよう固定する。
    /// </summary>
    internal static UnitDef[] HealPool295 => Healers(hisa: false).Select(h => h.Def).ToArray();

    /// <summary>
    /// Phase 0 §1 の境界の判定（ソースの走査結果に人が付ける列）。
    /// 「味方へ」「繰り返し」の両方が立つ書き手だけをヒーラーに数える。自分だけ・敵へ・倒れたとき1回は数えない。
    /// </summary>
    static readonly Dictionary<string, (string To, bool Repeat, string Note)> WriterKind = new()
    {
        ["DrainTrait"] = ("自分", true, "与えた分を自分に吸う"),
        ["NecroTrait"] = ("自分", true, "味方の死で自分が癒える"),
        ["ColossusTrait"] = ("後ろの味方", false, "倒れたとき1回の還し（指示書の境界の例）"),
        ["MenderTrait"] = ("味方", true, ""),
        ["AlmsTrait"] = ("味方", true, ""),
        ["SutureTrait"] = ("味方", true, ""),
        ["DevourTrait"] = ("味方", true, ""),
        ["RegenTrait"] = ("自分", true, ""),
        ["DrifterTrait"] = ("味方", true, "動かされた味方を癒す"),
        ["ForsakeTrait"] = ("味方", true, ""),
        ["WardTrait"] = ("味方", true, ""),
        ["ForfeitTrait"] = ("敵", true, "敵を癒す（代金）"),
        ["IndulgenceTrait"] = ("味方", true, ""),
        ["EruptTrait"] = ("自分", true, "暴発の1発ごとに自分"),
        ["StitchTrait"] = ("味方", true, ""),
        ["RegroupTrait"] = ("味方", true, ""),
        ["PartyMendTrait"] = ("味方", true, "敵の札（勇者パーティー）"),
        ["PartyMendPctTrait"] = ("味方", true, "敵の札（勇者パーティー）"),
        ["BossMendTrait"] = ("自分", true, "ボスの札"),
        ["HeroMendTrait"] = ("味方", true, "敵の札（勇者）"),
        ["KissTrait"] = ("味方", true, ""),
        // 破片（`StatusKeys.Armor`・HP の前に削られる別資源）の書き手
        ["ScaleTrait"] = ("自分", true, "破片"),
        ["ShatterTrait"] = ("味方", true, "破片（範囲を受けると味方全員へ）"),
        ["ShrapnelTrait"] = ("—", false, "破片を消す側"),
        ["BraceTrait"] = ("味方", true, "破片"),
        ["PlankTrait"] = ("味方", true, "破片（板）"),
    };

    sealed record Writer(string Cls, int Line, string Kind, TraitId[] Ids);
    sealed record HealerRow(UnitDef Def, string Classes);

    static readonly Regex ClassRe = new(@"^\s*(?:public |internal )?(?:sealed )?(?:abstract )?class (\w+)", RegexOptions.Compiled);

    /// <summary>`BattleCore/Traits.cs` を走査して、<paramref name="hit"/> が真になる行の書き手（囲むクラス）を拾う（コメント行は除く）。</summary>
    static List<Writer> Scan(string kind, Func<string, bool> hit)
    {
        var l = new List<Writer>();
        string cls = "";
        var lines = File.ReadAllLines(Path.Combine("BattleCore", "Traits.cs"));
        for (int i = 0; i < lines.Length; i++)
        {
            var m = ClassRe.Match(lines[i]);
            if (m.Success) cls = m.Groups[1].Value;
            string t = lines[i].TrimStart();
            if (t.StartsWith("//") || t.StartsWith("///")) continue;
            if (hit(lines[i])) l.Add(new Writer(cls, i + 1, kind, IdsOf(cls)));
        }
        return l;
    }

    static readonly Dictionary<string, TraitId[]> _ids = BuildIds();
    static Dictionary<string, TraitId[]> BuildIds()
    {
        var d = new Dictionary<string, List<TraitId>>();
        foreach (TraitId id in Enum.GetValues<TraitId>())
        {
            Trait t;
            try { t = TraitCatalog.Get(id); } catch { continue; }
            string n = t.GetType().Name;
            if (!d.TryGetValue(n, out var l)) d[n] = l = new List<TraitId>();
            if (!l.Contains(id)) l.Add(id);
        }
        return d.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
    }
    static TraitId[] IdsOf(string cls) => _ids.TryGetValue(cls, out var a) ? a : Array.Empty<TraitId>();

    static List<Writer> HealWriters() => Scan("回復", s => s.Contains("ctx.Heal("));
    static List<Writer> ArmorWriters() => Scan("破片", s => s.Contains("StatusKeys.Armor") && s.Contains("SetCounter("));
    static List<Writer> AllyMarkWriters() => Scan("標", s => s.Contains("SetCounter(StatusKeys.Marked, 1)"));

    /// <summary>ヒーラーの機械的定義（Phase 0 §1）: 「味方へ」かつ「繰り返し」の回復・破片の書き手を持つ `All` の駒。</summary>
    static List<HealerRow> Healers(bool hisa = true)
    {
        var w = HealWriters().Concat(ArmorWriters())
            .Where(x => WriterKind.TryGetValue(x.Cls, out var k) && k.To == "味方" && k.Repeat).ToList();
        var rows = new List<HealerRow>();
        foreach (var d in UnitCatalog.All)
        {
            var cls = w.Where(x => x.Ids.Any(d.Traits.Contains)).Select(x => x.Cls).Distinct().ToList();
            if (cls.Count > 0) rows.Add(new HealerRow(d, string.Join(" ／ ", cls)));
        }
        // engine 側の書き手（`ctx.Heal` を通らない・Traits.cs の外）: ベニの反転（毒・燃焼を癒しに変える）は隣の味方を癒すので、ここで足す。
        if (!rows.Any(r => ReferenceEquals(r.Def, UnitCatalog.Beni)))
            rows.Add(new HealerRow(UnitCatalog.Beni, "（engine）`InverseHeal`——隣の味方の毒・燃焼を癒しに変える"));
        // 第296期: 規定のヒサ（HK-b）の「あいつを狙え！」も engine 側の書き手（`MarkRallyWide`・Traits.cs の外）。ポンの決めでヒーラーに数える。
        if (hisa && UnitCatalog.Hisa.Traits.Contains(TraitId.MarkRallyWide) && !rows.Any(r => ReferenceEquals(r.Def, UnitCatalog.Hisa)))
            rows.Add(new HealerRow(UnitCatalog.Hisa, "（engine）`MarkRallyWide`——標の敵が攻撃されるたび、攻撃した味方と最も傷ついた味方を癒す"));
        return rows;
    }

    // ---------------------------------------------------------------------------------
    // 台と1戦
    // ---------------------------------------------------------------------------------
    static List<UnitState> Boss() => BattleEngine.MaterializeEnemy(EnemyCatalog.BossRegularWave, EnemyScaleRule.None);

    readonly record struct One(bool Won, int Turns, int Scar, int Vend, int Layers, int Fires);

    static One FightLite(Formation f, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, Boss(), seed, verbose: false);
        int scar = 0, fires = 0, layers = 0, vend = 0;
        if (r.TallyByUnit.TryGetValue(UnitCatalog.Tome.Id, out var tm)) { scar = tm.RuptureScar; fires = tm.RuptureFires; layers = tm.RuptureLayerSum; }
        if (r.TallyByUnit.TryGetValue(UnitCatalog.Zan.Id, out var zt)) vend = (int)zt.VendettaFires;
        return new One(r.PlayerWon, r.Turns, scar, vend, layers, fires);
    }

    sealed class Cell
    {
        public int N, Wins; public long WinT, Scar, Vend, Turns, Layers, Fires;
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public double ScarAvg => N == 0 ? 0 : (double)Scar / N;
        public void Add(One o) { N++; if (o.Won) { Wins++; WinT += o.Turns; } Scar += o.Scar; Vend += o.Vend; Turns += o.Turns; Layers += o.Layers; Fires += o.Fires; }
        public void Merge(Cell c) { N += c.N; Wins += c.Wins; WinT += c.WinT; Scar += c.Scar; Vend += c.Vend; Turns += c.Turns; Layers += c.Layers; Fires += c.Fires; }
    }

    static Cell MeasureLite(Formation f, int from, int n)
    {
        var parts = new One[n];
        Parallel.For(0, n, i => parts[i] = FightLite(f, from + i));
        var c = new Cell();
        foreach (var o in parts) c.Add(o);
        return c;
    }

    /// <summary>詳しい計数（verbose）: 崩れ始め・トメの死亡T・勇者の回復・ヒーラーの回復と破片・ヒサの標の半減が掛かった被弾。</summary>
    sealed class Deep
    {
        public int N, Wins; public long WinT, Scar, Vend, FirstDeath, FirstDeathT, TomeDied, TomeDeathT, BossHeal, AllyHeal, AllyArmor, Turns;
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public void Merge(Deep o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Scar += o.Scar; Vend += o.Vend; FirstDeath += o.FirstDeath; FirstDeathT += o.FirstDeathT;
            TomeDied += o.TomeDied; TomeDeathT += o.TomeDeathT; BossHeal += o.BossHeal; AllyHeal += o.AllyHeal; AllyArmor += o.AllyArmor; Turns += o.Turns;
        }
    }

    static Deep FightDeep(Formation f, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = Boss();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var d = new Deep { N = 1, Turns = r.Turns };
        if (r.PlayerWon) { d.Wins = 1; d.WinT = r.Turns; }
        var mine = p.Select(u => u.InstanceId).ToHashSet();
        var foe = e.Select(u => u.InstanceId).ToHashSet();
        UnitState? tome = p.FirstOrDefault(u => u.Def.Id == UnitCatalog.Tome.Id);
        int first = 0, tomeDeath = 0;
        foreach (var ev in r.Events)
        {
            if (ev.Kind == BattleEventKind.Heal && ev.TargetId is int h)
            {
                if (foe.Contains(h)) d.BossHeal += ev.Amount;
                else if (mine.Contains(h)) d.AllyHeal += ev.Amount;
            }
            else if (ev.Kind == BattleEventKind.Death && ev.TargetId is int x && mine.Contains(x))
            {
                if (first == 0) first = ev.Turn;
                if (tome is not null && x == tome.InstanceId && tomeDeath == 0) tomeDeath = ev.Turn;
            }
        }
        if (first > 0) { d.FirstDeath = 1; d.FirstDeathT = first; }
        if (tomeDeath > 0) { d.TomeDied = 1; d.TomeDeathT = tomeDeath; }
        if (r.TallyByUnit.TryGetValue(UnitCatalog.Tome.Id, out var tm)) d.Scar = tm.RuptureScar;
        if (r.TallyByUnit.TryGetValue(UnitCatalog.Zan.Id, out var zt)) d.Vend = zt.VendettaFires;
        d.AllyArmor = r.Armor.StockSum[BattleContext.PlayerTeam];   // ターン頭の味方の破片の在庫の和
        return d;
    }

    static Deep MeasureDeep(Formation f, int from, int n)
    {
        var parts = new Deep[n];
        Parallel.For(0, n, i => parts[i] = FightDeep(f, from + i));
        var all = new Deep();
        foreach (var d in parts) all.Merge(d);
        return all;
    }

    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string Per1(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");

    static string Names(IEnumerable<UnitDef> ds) => string.Join("・", ds.Select(Short));
    static readonly Regex KanaTail = new(@"[ァ-ヴー]+$", RegexOptions.Compiled);
    static string Short(UnitDef d) => KanaTail.Match(d.Name) is { Success: true } m ? m.Value : d.Name;

    /// <summary>5 枚を 0..4 の席へ並べた全 120 通り。</summary>
    internal static IEnumerable<UnitDef[]> Perms(UnitDef[] a)
    {
        if (a.Length <= 1) { yield return a; yield break; }
        for (int i = 0; i < a.Length; i++)
        {
            var rest = a.Where((_, j) => j != i).ToArray();
            foreach (var p in Perms(rest)) yield return new[] { a[i] }.Concat(p).ToArray();
        }
    }

    internal static Formation Seat(UnitDef[] order)
    {
        var f = new Formation();
        for (int i = 0; i < order.Length; i++) f[i] = order[i];
        return f;
    }

    static Formation Swap(Formation f, UnitDef from, UnitDef to) => FvSwap(f, from, to);

    /// <summary>探索枠3 の組（ヒーラーは 0 ／ 1 枚。2 枚は作らない＝ポンの制約）。</summary>
    static List<UnitDef[]> Lineups()
    {
        var heal = HealPool295;
        var pool = LifePool.Concat(heal.Where(h => !LifePool.Contains(h))).ToArray();
        var l = new List<UnitDef[]>();
        for (int a = 0; a < pool.Length; a++)
            for (int b = a + 1; b < pool.Length; b++)
                for (int c = b + 1; c < pool.Length; c++)
                {
                    var trio = new[] { pool[a], pool[b], pool[c] };
                    if (trio.Count(heal.Contains) <= 1) l.Add(trio);
                }
        return l;
    }

    // ---------------------------------------------------------------------------------
    // Phase 0
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第283期 Phase 0 —— ボスの標台の寿命探索（ヒーラーは1枚まで）");
        Console.WriteLine();
        Console.WriteLine("## §1 回復・破片の書き手（`BattleCore/Traits.cs` を走査・コメント行を除く）");
        Console.WriteLine();
        Console.WriteLine("| 種 | 書き手（クラス） | 行 | 札 | 宛先 | 繰り返し | `All` の保持者 | 注 |");
        Console.WriteLine("|---|---|--:|---|---|---|---|---|");
        foreach (var w in HealWriters().Concat(ArmorWriters()))
        {
            var k = WriterKind.TryGetValue(w.Cls, out var kk) ? kk : ("**未分類**", false, "");
            var holders = UnitCatalog.All.Where(d => w.Ids.Any(d.Traits.Contains)).Select(d => d.Name);
            Console.WriteLine($"| {w.Kind} | {w.Cls} | {w.Line} | {string.Join("・", w.Ids)} | {k.Item1} | {(k.Item2 ? "○" : "1回")} | {string.Join("・", holders)} | {k.Item3} |");
        }
        Console.WriteLine();
        Console.WriteLine("**ヒーラーの機械的定義**: 宛先が「味方」かつ繰り返し書ける回復（`ctx.Heal`）・破片（`StatusKeys.Armor`）の書き手を持つ `All` の駒。");
        Console.WriteLine("**境界**: ゴルムの還し（倒れたとき1回）は数えない。ベニの反転（engine の `InverseHeal`・隣の味方の毒・燃焼を癒しに変える）は数える。破片は HP の前に削られる別資源なので回復と同じに数える（ツギの板）。");
        Console.WriteLine();
        Console.WriteLine("| ヒーラー | HP | 書き手 |");
        Console.WriteLine("|---|--:|---|");
        foreach (var h in Healers(hisa: false)) Console.WriteLine($"| {h.Def.Name} | {h.Def.MaxHp} | {h.Classes} |");
        Console.WriteLine();

        Console.WriteLine("## §2 味方に標を書く札（ザンの仇指しの入口）");
        Console.WriteLine();
        Console.WriteLine("| 書き手 | 行 | 札 | `All` の保持者 |");
        Console.WriteLine("|---|--:|---|---|");
        foreach (var w in AllyMarkWriters())
            Console.WriteLine($"| {w.Cls} | {w.Line} | {string.Join("・", w.Ids)} | {string.Join("・", UnitCatalog.All.Where(d => w.Ids.Any(d.Traits.Contains)).Select(d => d.Name))} |");
        Console.WriteLine();

        // §3 ボスで実際に働くか: 第282期の新台（前1 トメ ／ 前3 ドハ ／ 中央 ザン ／ 後1 ガン ／ 後3 ヒサ）のガンを各候補に替える
        Console.WriteLine("## §3 ボスで働くか（第282期の新台の後1 ガン → 候補・seed 0..99・verbose）");
        Console.WriteLine();
        Console.WriteLine("勝率 ／ 崩れ ＝ 味方の最初の死亡T ／ ト死 ＝ トメの死亡T ／ 仇 ＝ ザンの仇指し／戦 ／ 爪 ＝ 爪痕累計 ／ 味方回復 ＝ 味方に入った回復の量／戦（破片は回復の event に乗らないので出ない）／ 倒しT ＝ 勝った戦の決着T ／ ドルガ ＝ トメ → ドルガの勝率");
        Console.WriteLine();
        Console.WriteLine("| 後1 | 勝率 | 倒しT | 崩れ | ト死 | 仇 | 爪 | 味方回復 | 勇者の回復 | ドルガ |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var basis = Formation.Build(front1: UnitCatalog.Tome, front3: UnitCatalog.DohaD0, center: UnitCatalog.ZanZNb, back1: UnitCatalog.Gan, back3: UnitCatalog.HisaHK0);
        foreach (var c in new[] { UnitCatalog.Gan }.Concat(LifePool.Where(d => d != UnitCatalog.Gan && d != UnitCatalog.DohaD0 && d != UnitCatalog.HisaHK0)).Concat(HealPool295).Distinct())
        {
            var f = Swap(basis, UnitCatalog.Gan, c);
            var d = MeasureDeep(f, 0, 100);
            var dg = MeasureLite(Swap(f, UnitCatalog.Tome, UnitCatalog.Dolga), 0, 100);
            Console.WriteLine($"| {c.Name}{(HealPool295.Contains(c) ? "（癒）" : "")} | {F1(d.Win)} | {Per2(d.WinT, d.Wins)} | {Per1(d.FirstDeathT, d.FirstDeath)} | {Per1(d.TomeDeathT, d.TomeDied)} | {Per1(d.Vend, d.N)} | {Per1(d.Scar, d.N)} | {Per1(d.AllyHeal, d.N)} | {Per1(d.BossHeal, d.N)} | {F1(dg.Win)} |");
        }
        Console.WriteLine();

        // §5 所要の見積もり
        var lu = Lineups();
        var t0 = System.Diagnostics.Stopwatch.StartNew();
        long fights = 0;
        foreach (var trio in lu.Take(4))
            foreach (var order in Perms(Fixed.Concat(trio).ToArray()).Take(30)) { MeasureLite(Seat(order), 0, 20); fights += 20; }
        double perFight = t0.Elapsed.TotalSeconds / fights;
        long gridFights = (long)lu.Count * 120 * 20;
        Console.WriteLine("## §5 格子の大きさと所要");
        Console.WriteLine();
        Console.WriteLine($"- 候補: 寿命側 {LifePool.Length} 枚（{Names(LifePool)}）＋ ヒーラー {HealPool295.Length} 枚（{Names(HealPool295)}）");
        Console.WriteLine($"- 探索枠3 の組（ヒーラー ≦ 1）: {lu.Count} 組（ヒーラー 0 枚 {lu.Count(t => !t.Any(HealPool295.Contains))} ／ 1 枚 {lu.Count(t => t.Any(HealPool295.Contains))}）× 席 120 通り ＝ {lu.Count * 120:N0} 台");
        Console.WriteLine($"- 1戦（ボス・verbose なし・並列）: {perFight * 1e6:F1} µs → 足切り段（seed 0..19）{gridFights:N0} 戦 ≈ {gridFights * perFight:F0} 秒");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 段1: 格子
    // ---------------------------------------------------------------------------------
    /// <summary>足切り段の seed 数（測る前に固定）。</summary>
    const int CutSeeds = 20;

    sealed record Board(UnitDef[] Trio, UnitDef[] Order, Cell Cut);

    static List<Board> CutStage(List<UnitDef[]> lu)
    {
        var jobs = new List<(UnitDef[] Trio, UnitDef[] Order)>();
        foreach (var trio in lu)
            foreach (var order in Perms(Fixed.Concat(trio).ToArray())) jobs.Add((trio, order));
        var res = new Board[jobs.Count];
        Parallel.For(0, jobs.Count, i =>
        {
            var f = Seat(jobs[i].Order);
            var c = new Cell();
            for (int s = 0; s < CutSeeds; s++) c.Add(FightLite(f, s));
            res[i] = new Board(jobs[i].Trio, jobs[i].Order, c);
        });
        return res.ToList();
    }

    static string SeatText(UnitDef[] order) => $"前1 {Short(order[0])} ／ 前3 {Short(order[1])} ／ 中 {Short(order[2])} ／ 後1 {Short(order[3])} ／ 後3 {Short(order[4])}";
    static string HealOf(UnitDef[] trio) => trio.FirstOrDefault(HealPool295.Contains) is { } h ? Short(h) : "—";

    static void Grid()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var lu = Lineups();
        Console.WriteLine("# 第283期 段1 —— ボスの標台の寿命探索（固定枠 トメ T1n ＋ ザン・探索枠3・ヒーラー ≦ 1）");
        Console.WriteLine();
        Console.WriteLine($"- 寿命側 {LifePool.Length} 枚: {Names(LifePool)}");
        Console.WriteLine($"- ヒーラー {HealPool295.Length} 枚: {Names(HealPool295)}");
        Console.WriteLine($"- 組 {lu.Count} × 席 120 ＝ {lu.Count * 120:N0} 台。ボスは規定形（勇者 HP {EnemyCatalog.BossRegular.MaxHp}・倍率なし）");
        Console.WriteLine();
        Console.WriteLine("**足切りの基準（測る前に固定）**: 段A は全台 × seed 0..19。段B（seed 0..199）へ送るのは");
        Console.WriteLine("(1) 段A で1勝でもした台すべて ＋ (2) 各組の段A の最良の席（勝ち数 → 爪痕累計の順）。");
        Console.WriteLine("段C（届いた台 ＝ 段B で 0% を離れた台すべて）: 帯B（seed 200..599）とドルガ対照（トメ → ドルガ・同じ席・帯A ／ 帯B）。");
        Console.WriteLine();

        var cut = CutStage(lu);
        Console.WriteLine($"段A: {cut.Count:N0} 台 × {CutSeeds} seed（{sw.Elapsed.TotalSeconds:F0} 秒）。1勝以上の台 {cut.Count(b => b.Cut.Wins > 0)}（組 {cut.Where(b => b.Cut.Wins > 0).Select(b => Names(b.Trio)).Distinct().Count()}）。");
        Console.WriteLine();

        var send = new Dictionary<string, Board>();
        foreach (var b in cut.Where(b => b.Cut.Wins > 0)) send[SeatText(b.Order)] = b;
        foreach (var g in cut.GroupBy(b => Names(b.Trio)))
        {
            var best = g.OrderByDescending(b => b.Cut.Wins).ThenByDescending(b => b.Cut.Scar).First();
            send[SeatText(best.Order)] = best;
        }
        var stageB = send.Values.AsParallel().Select(b => (B: b, C: MeasureLiteSeq(Seat(b.Order), 0, 200))).ToList();
        Console.WriteLine($"段B: {stageB.Count} 台 × seed 0..199（{sw.Elapsed.TotalSeconds:F0} 秒）。");
        Console.WriteLine();

        // 表1: ヒーラー別の最良（爪痕 → 勝率）
        Console.WriteLine("## 表1 ヒーラー別の最良の台（段B・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("| ヒーラー | 探索枠 | 席 | 勝率 | 倒しT | 爪痕 | 仇／戦 | 決着T | 段A 勝 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var g in stageB.GroupBy(x => HealOf(x.B.Trio)).OrderBy(g => g.Key == "—" ? "" : g.Key))
        {
            foreach (var x in g.OrderByDescending(x => x.C.Wins).ThenByDescending(x => x.C.Scar).Take(3))
                Console.WriteLine($"| {g.Key} | {Names(x.B.Trio)} | {SeatText(x.B.Order)} | {F1(x.C.Win)} | {Per2(x.C.WinT, x.C.Wins)} | {F1(x.C.ScarAvg)} | {Per1(x.C.Vend, x.C.N)} | {Per2(x.C.Turns, x.C.N)} | {x.B.Cut.Wins} |");
        }
        Console.WriteLine();

        // 表2: 爪痕の上位 15 台（全体）
        Console.WriteLine("## 表2 爪痕累計の上位 15 台（段B）");
        Console.WriteLine();
        Console.WriteLine("| 順 | ヒーラー | 探索枠 | 席 | 勝率 | 爪痕 | 仇／戦 | 平均層 | 決着T |");
        Console.WriteLine("|--:|---|---|---|--:|--:|--:|--:|--:|");
        int rank = 0;
        foreach (var x in stageB.OrderByDescending(x => x.C.Wins).ThenByDescending(x => x.C.Scar).Take(15))
            Console.WriteLine($"| {++rank} | {HealOf(x.B.Trio)} | {Names(x.B.Trio)} | {SeatText(x.B.Order)} | {F1(x.C.Win)} | {F1(x.C.ScarAvg)} | {Per1(x.C.Vend, x.C.N)} | {Per2(x.C.Layers, x.C.Fires)} | {Per2(x.C.Turns, x.C.N)} |");
        Console.WriteLine();

        // 表3: 届いた台の検死（段C）——台ごとに測り、組ごとに集計して出す（台の一覧は 2,000 行を超えるので出さない）
        var reached = stageB.Where(x => x.C.Wins > 0).ToList();
        var verdicts = reached.AsParallel().Select(x =>
        {
            var f = Seat(x.B.Order);
            var bb = MeasureLiteSeq(f, 200, 400);
            var df = Swap(f, UnitCatalog.Tome, UnitCatalog.Dolga);
            var da = MeasureLiteSeq(df, 0, 200);
            var db = MeasureLiteSeq(df, 200, 400);
            return (x.B, A: x.C, Bb: bb, Da: da, Db: db, Scar: da.Wins == 0 && db.Wins == 0);
        }).ToList();
        Console.WriteLine($"## 表3 届いた台（段B で 0% を離れた {reached.Count} 台）の帯B とドルガ対照");
        Console.WriteLine();
        Console.WriteLine("爪痕由来 ＝ トメ → ドルガ（同じ席）が帯A・帯B とも 0%。そうでない台は「回復・守りの到達」で、到達に数えない（指示書 §2-2）。");
        Console.WriteLine();
        Console.WriteLine("### 3-1 ヒーラー別");
        Console.WriteLine();
        Console.WriteLine("| ヒーラー | 届いた台 | うち爪痕由来 | 爪痕由来の組 | 爪痕由来で帯A・帯B とも 100% の台 | 組の数（全体） |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        foreach (var h in new[] { "—" }.Concat(HealPool295.Select(Short)))
        {
            var v = verdicts.Where(x => HealOf(x.B.Trio) == h).ToList();
            int trios = lu.Count(t => HealOf(t) == h);
            Console.WriteLine($"| {h} | {v.Count} | {v.Count(x => x.Scar)} | {v.Where(x => x.Scar).Select(x => Names(x.B.Trio)).Distinct().Count()} | {v.Count(x => x.Scar && x.A.Wins == x.A.N && x.Bb.Wins == x.Bb.N)} | {trios} |");
        }
        Console.WriteLine($"| **計** | {verdicts.Count} | {verdicts.Count(x => x.Scar)} | {verdicts.Where(x => x.Scar).Select(x => Names(x.B.Trio)).Distinct().Count()} | {verdicts.Count(x => x.Scar && x.A.Wins == x.A.N && x.Bb.Wins == x.Bb.N)} | {lu.Count} |");
        Console.WriteLine();
        Console.WriteLine("### 3-2 組別（届いた台が1つでもある組・爪痕由来の席の数の多い順）");
        Console.WriteLine();
        Console.WriteLine("席の数は 120 通りのうち（段A で1勝もしなかった席は段B に送っていないので、数は下限）。最良 ＝ 爪痕由来の席のうち 帯B → 帯A → 決着T の短い順。");
        Console.WriteLine();
        Console.WriteLine("| ヒーラー | 探索枠 | 届いた席 | 爪痕由来の席 | 両帯 100% の席 | ドルガが勝つ席（最大の勝率） | 最良の席 | 帯A | 帯B | 倒しT | 爪痕 | 仇／戦 |");
        Console.WriteLine("|---|---|--:|--:|--:|---|---|--:|--:|--:|--:|--:|");
        foreach (var g in verdicts.GroupBy(x => Names(x.B.Trio)).OrderByDescending(g => g.Count(x => x.Scar)).ThenByDescending(g => g.Count()))
        {
            var dol = g.Where(x => !x.Scar).ToList();
            var best = g.Where(x => x.Scar).OrderByDescending(x => x.Bb.Wins).ThenByDescending(x => x.A.Wins).ThenBy(x => x.A.Wins == 0 ? 99 : (double)x.A.WinT / x.A.Wins).FirstOrDefault();
            string dolText = dol.Count == 0 ? "0" : $"{dol.Count}（{F1(dol.Max(x => Math.Max(x.Da.Win, x.Db.Win)))}）";
            string bestText = best.B is null ? "—" : $"{SeatText(best.B.Order)} | {F1(best.A.Win)} | {F1(best.Bb.Win)} | {Per2(best.A.WinT, best.A.Wins)} | {F1(best.A.ScarAvg)} | {Per1(best.A.Vend, best.A.N)}";
            if (best.B is null) bestText += " | | | | |";
            Console.WriteLine($"| {HealOf(g.First().B.Trio)} | {g.Key} | {g.Count()} | {g.Count(x => x.Scar)} | {g.Count(x => x.Scar && x.A.Wins == x.A.N && x.Bb.Wins == x.Bb.N)} | {dolText} | {bestText} |");
        }
        Console.WriteLine();
        Console.WriteLine("### 3-3 届かなかった組（爪痕由来の席が 0）のうち、爪痕の最良");
        Console.WriteLine();
        var reachedTrios = verdicts.Where(x => x.Scar).Select(x => Names(x.B.Trio)).ToHashSet();
        var miss = stageB.Where(x => !reachedTrios.Contains(Names(x.B.Trio))).GroupBy(x => Names(x.B.Trio))
            .Select(g => g.OrderByDescending(x => x.C.Wins).ThenByDescending(x => x.C.Scar).First()).OrderByDescending(x => x.C.Scar).ToList();
        Console.WriteLine($"届かなかった組 {miss.Count}（ヒーラー 0 枚 {miss.Count(x => HealOf(x.B.Trio) == "—")}）。爪痕の上位 10:");
        Console.WriteLine();
        Console.WriteLine("| ヒーラー | 探索枠 | 席 | 勝率 | 爪痕 | 仇／戦 | 決着T |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|");
        foreach (var x in miss.Take(10))
            Console.WriteLine($"| {HealOf(x.B.Trio)} | {Names(x.B.Trio)} | {SeatText(x.B.Order)} | {F1(x.C.Win)} | {F1(x.C.ScarAvg)} | {Per1(x.C.Vend, x.C.N)} | {Per2(x.C.Turns, x.C.N)} |");
        Console.WriteLine();
        Console.WriteLine($"供給の入口（ヒサ ／ ソラ）を持たない組 {lu.Count(t => !t.Contains(UnitCatalog.HisaHK0) && !t.Contains(UnitCatalog.Sora))} のうち、爪痕由来で届いた組: {reachedTrios.Count(n => !n.Contains("ヒサ") && !n.Contains("ソラ"))}");
        Console.WriteLine();

        // 表4: ヒサ・ドハの在不在と、組の全体分布
        Console.WriteLine("## 表4 寄与の切り分け（段B の最良を、探索枠に含む駒で集計）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 含む組の数 | 含む組の最良の爪痕 | 含む組の爪痕の中央値 | 含まない組の最良の爪痕 | 最良の勝率 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        var bestPerTrio = stageB.GroupBy(x => Names(x.B.Trio)).Select(g => g.OrderByDescending(x => x.C.Wins).ThenByDescending(x => x.C.Scar).First()).ToList();
        foreach (var d in LifePool.Concat(HealPool295))
        {
            var with = bestPerTrio.Where(x => x.B.Trio.Contains(d)).ToList();
            var without = bestPerTrio.Where(x => !x.B.Trio.Contains(d)).ToList();
            if (with.Count == 0) continue;
            var sc = with.Select(x => x.C.ScarAvg).OrderBy(v => v).ToList();
            Console.WriteLine($"| {d.Name}{(HealPool295.Contains(d) ? "（癒）" : "")} | {with.Count} | {F1(sc[^1])} | {F1(sc[sc.Count / 2])} | {F1(without.Count == 0 ? double.NaN : without.Max(x => x.C.ScarAvg))} | {F1(with.Max(x => x.C.Win))} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    /// <summary>外側を並列にしたときの内側（逐次）。</summary>
    static Cell MeasureLiteSeq(Formation f, int from, int n)
    {
        var c = new Cell();
        for (int i = 0; i < n; i++) c.Add(FightLite(f, from + i));
        return c;
    }

    // ---------------------------------------------------------------------------------
    // 段1 の後: 代表の台の対照（版）と余裕（寿命を削る）
    // ---------------------------------------------------------------------------------
    /// <summary>
    /// 代表の台（`grid` の表3-2 を見て選んだ・選び方は報告 §4-1）。先頭が第282期の新台（届かなかった基準）。
    /// </summary>
    internal static readonly (string Label, UnitDef[] Order)[] CtlBoards =
    {
        ("第282期の新台", new[] { UnitCatalog.Tome, UnitCatalog.DohaD0, UnitCatalog.ZanZNb, UnitCatalog.Gan, UnitCatalog.HisaHK0 }),
        ("新台のガン → バン（席はそのまま）", new[] { UnitCatalog.Tome, UnitCatalog.DohaD0, UnitCatalog.ZanZNb, UnitCatalog.Ban, UnitCatalog.HisaHK0 }),
        ("0枚: ヒサ・ドハ・バン（新台の組み替え）", new[] { UnitCatalog.HisaHK0, UnitCatalog.DohaD0, UnitCatalog.Ban, UnitCatalog.ZanZNb, UnitCatalog.Tome }),
        ("0枚: ヒサ・ドハ・ゴルム（標準の候補）", new[] { UnitCatalog.Golm, UnitCatalog.Tome, UnitCatalog.DohaD0, UnitCatalog.ZanZNb, UnitCatalog.HisaHK0 }),
        ("0枚: ヒサ・ゴルム・バン", new[] { UnitCatalog.ZanZNb, UnitCatalog.Golm, UnitCatalog.Tome, UnitCatalog.Ban, UnitCatalog.HisaHK0 }),
        ("シオ: ヒサ・ドハ・シオ", new[] { UnitCatalog.DohaD0, UnitCatalog.ZanZNb, UnitCatalog.HisaHK0, UnitCatalog.Tome, UnitCatalog.Shio }),
        ("シオ: ヒサ・クビ・シオ（120 席すべて届く）", new[] { UnitCatalog.HisaHK0, UnitCatalog.Shio, UnitCatalog.Kubi, UnitCatalog.Tome, UnitCatalog.ZanZNb }),
        ("ツギ: ヒサ・バン・ツギ", new[] { UnitCatalog.Tome, UnitCatalog.HisaHK0, UnitCatalog.Ban, UnitCatalog.Tsugi, UnitCatalog.ZanZNb }),
    };

    static Formation Tough(Formation f, int x10)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied()) g[slot] = d.WithStats(Math.Max(1, d.MaxHp * x10 / 10), d.Attack);
        return g;
    }

    static void Ctl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var vers = new (string Name, UnitDef D)[]
        {
            ("T1n（第283期の規定）", UnitCatalog.TomeT1n), ("T1（層を消す）", UnitCatalog.TomeT1), ("T1-s（爪痕なし）", UnitCatalog.TomeT1s),
            ("T0（旧トメ）", UnitCatalog.TomeT0), ("ドルガ", UnitCatalog.Dolga),
        };
        Console.WriteLine("# 第283期 —— 代表の台の対照（トメの版を同じ席で差し替え）と余裕（隊の全員の最大HP × k）");
        Console.WriteLine();
        Console.WriteLine("## 表1 版の対照（セル ＝ 帯A 勝率 ／ 帯B 勝率 ／ 爪痕累計（帯A））");
        Console.WriteLine();
        Console.WriteLine("| 台 | 席 | " + string.Join(" | ", vers.Select(v => v.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(vers.Select(_ => "--:|")));
        foreach (var (label, order) in CtlBoards)
        {
            var cells = vers.Select(v =>
            {
                var f = Swap(Seat(order), UnitCatalog.Tome, v.D);
                var a = MeasureLite(f, 0, 200); var b = MeasureLite(f, 200, 400);
                return $"{F1(a.Win)} ／ {F1(b.Win)} ／ {a.ScarAvg:F0}";
            });
            Console.WriteLine($"| {label} | {SeatText(order)} | " + string.Join(" | ", cells) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("## 表1b 中身（規定 T1n・帯A・verbose）——崩れ ＝ 味方の最初の死亡T ／ ト死 ＝ トメの死亡T（死んだ戦だけ）／ ト死率 ／ 仇 ＝ 仇指し／戦 ／ 味方回復／戦 ／ 勇者の回復／戦");
        Console.WriteLine();
        Console.WriteLine("| 台 | 勝率 | 倒しT | 崩れ | ト死 | ト死率 | 仇 | 爪痕 | 味方回復 | 勇者の回復 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (label, order) in CtlBoards)
        {
            var d = MeasureDeep(Seat(order), 0, 200);
            Console.WriteLine($"| {label} | {F1(d.Win)} | {Per2(d.WinT, d.Wins)} | {Per1(d.FirstDeathT, d.FirstDeath)} | {Per1(d.TomeDeathT, d.TomeDied)} | {F1(100.0 * d.TomeDied / d.N)} | {Per1(d.Vend, d.N)} | {Per1(d.Scar, d.N)} | {Per1(d.AllyHeal, d.N)} | {Per1(d.BossHeal, d.N)} |");
        }
        Console.WriteLine();
        int[] ks = { 5, 6, 7, 8, 9, 10, 15, 20 };
        Console.WriteLine("## 表2 余裕と不足（隊の全員の最大HP × k・規定 T1n・セル ＝ 勝率 ／ 爪痕・seed 0..199・診断の仮想の版）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", ks.Select(k => $"×{k / 10.0:0.0}")) + " | 0% を離れる最小の k |");
        Console.WriteLine("|---|" + string.Concat(ks.Select(_ => "--:|")) + "--:|");
        foreach (var (label, order) in CtlBoards)
        {
            var cells = new List<string>();
            double? first = null;
            foreach (int k in ks)
            {
                var c = MeasureLite(Tough(Seat(order), k), 0, 200);
                cells.Add($"{F1(c.Win)} ／ {c.ScarAvg:F0}");
                if (first is null && c.Wins > 0) first = k / 10.0;
            }
            Console.WriteLine($"| {label} | " + string.Join(" | ", cells) + $" | {(first is null ? "> ×2" : $"×{first:0.0}")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
    // ---------------------------------------------------------------------------------
    static void Check()
    {
        int fail = 0;
        void Expect(string what, bool ok, string detail = "")
        {
            Console.WriteLine($"- {(ok ? "○" : "×")} {what}{(detail.Length > 0 ? "（" + detail + "）" : "")}");
            if (!ok) fail++;
        }
        Console.WriteLine("# 第283期 boss283 check —— 自己検査");
        Console.WriteLine();
        var unk = HealWriters().Concat(ArmorWriters()).Where(w => !WriterKind.ContainsKey(w.Cls)).Select(w => w.Cls).Distinct().ToList();
        Expect("(a) 回復・破片の書き手はすべて境界の表に載っている（未分類 0）", unk.Count == 0, string.Join("・", unk));
        var lu = Lineups();
        Expect("(b) 探索枠の組にヒーラー 2 枚以上の組は 0（ポンの制約）", lu.All(t => t.Count(HealPool295.Contains) <= 1), $"{lu.Count} 組");
        Expect("(c) ツギとリリはどちらもヒーラーに数えられている（同時編成の禁止が組に効く）",
            HealPool295.Contains(UnitCatalog.Tsugi) && HealPool295.Contains(UnitCatalog.Lili) && !lu.Any(t => t.Contains(UnitCatalog.Tsugi) && t.Contains(UnitCatalog.Lili)));
        Expect("(d) 固定枠と探索枠は重ならない・ドルガは候補に無い（対照の駒）",
            !LifePool.Concat(HealPool295).Any(Fixed.Contains) && !LifePool.Concat(HealPool295).Contains(UnitCatalog.Dolga));
        Expect("(e) 席の並べ方は 120 通りで重複なし", Perms(Fixed.Concat(lu[0]).ToArray()).Select(o => string.Join(",", o.Select(d => d.Id))).Distinct().Count() == 120);
        // 第286期に M-b を規定にしたので、(f)(h) は T1n を `TomeT1n` で明示して読む（第283期の測定は T1n で行った）。
        Expect("(f) `TomeT1n` は層を残す札を持つ・規定（第286期から M-b）も層を残す", UnitCatalog.TomeT1n.Traits.Contains(TraitId.RuptureKeep) && UnitCatalog.Tome.Traits.Contains(TraitId.RuptureKeep));
        // (g) 口の一致: 軽い口（verbose なし）と詳しい口（verbose）で勝敗・爪痕が一致（第282期の新台 × seed 0..49）
        var basis = Seat(CtlBoards[0].Order);
        int bad = 0;
        for (int s = 0; s < 50; s++)
        {
            var a = FightLite(basis, s); var b = FightDeep(basis, s);
            if (a.Won != (b.Wins == 1) || a.Scar != b.Scar || a.Vend != b.Vend) bad++;
        }
        Expect("(g) 軽い口と詳しい口で勝敗・爪痕・仇指しが一致（第282期の新台 × seed 0..49）", bad == 0, $"{bad} 件");
        // (h) 第282期の新台 × T1n の爪痕が第282期の報告（1,400.?）と一致
        var c = MeasureLite(Swap(basis, UnitCatalog.Tome, UnitCatalog.TomeT1n), 0, 200);
        Expect("(h) 第282期の新台 × T1n（`TomeT1n`）の爪痕累計が第282期の報告（1,400）と同じ桁", Math.Abs(c.ScarAvg - 1400) < 1.0, $"{c.ScarAvg:F1}");
        // (i) 決定性
        int nd = 0;
        for (int s = 0; s < 30; s++) if (FightLite(basis, s) != FightLite(basis, s)) nd++;
        Expect("(i) seed 決定的", nd == 0, $"{nd} 件");
        Console.WriteLine();
        Console.WriteLine(fail == 0 ? "すべて ○。" : $"× が {fail} 件。");
    }

    static void LogOne(string ids, int seed)
    {
        var order = ids.Split(',').Select(UnitCatalog.ById).ToArray();
        var f = Seat(order);
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = Boss();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        Console.WriteLine($"# {BA.SeatsNamed(f)} × ボス × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
        var boss = e.First(u => u.Def.Id == EnemyCatalog.BossRegular.Id);
        Console.WriteLine($"（勇者の最後: HP {boss.Hp} ／ 最大HP {boss.MaxHp}）");
    }
}
