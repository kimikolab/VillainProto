using BattleCore;
using static Common;

// =====================================================================================
// mire モード（第218期） —— 澱みのミオ：叩きつけ・通電・澱みのデバフ・印を運ぶ
//
// 指示書は design/PHASE218_MIO_CONDUCT_SPEC.md ／ 報告は design/PHASE218_MIO_CONDUCT.md。
// **線は置かない**（採否はポンが遊んで決める）。台は `Presets` に足さない（この診断のローカル）。
//
//     dotnet run --project BattleSim -c Release 0 mire phase0   # Q0-1〜Q0-6（実装前・盤面は第217期の追記のまま）
//     dotnet run --project BattleSim -c Release 0 mire run      # 表A〜F（実装後）
//     dotnet run --project BattleSim -c Release 0 mire check    # 自己検査（受け入れ 1〜3・6）
// =====================================================================================

static partial class MireDiag
{
    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": RunImpl(arg); return;
            case "check": CheckImpl(arg); return;
            case "log": LogImpl(arg); return;
            default:
                Console.WriteLine("mire: モードは phase0 / run / check / log。");
                return;
        }
    }

    static partial void RunImpl(string arg);
    static partial void CheckImpl(string arg);
    static partial void LogImpl(string arg);

    // =================================================================================
    // 台（指示書 §6.1）
    // =================================================================================

    /// <summary>M台1〜M台4 の顔ぶれ（席は総当たりで選ぶ）。<paramref name="mio"/> に版のミオを渡す。</summary>
    internal static readonly (string Tag, string Aim, Func<UnitDef, List<UnitDef>> Members)[] Rigs =
    {
        ("M台1", "トウが殴る", m => new() { UnitCatalog.Beni, m, UnitCatalog.KataS3, UnitCatalog.Kubi, UnitCatalog.TouT0 }),
        ("M台2", "殴る駒が少ない（スィド）", m => new() { UnitCatalog.Beni, m, UnitCatalog.KataS3, UnitCatalog.Kubi, UnitCatalog.Sid }),
        ("M台3", "ポンの X字（グザ・クビ）", m => new() { UnitCatalog.Beni, m, UnitCatalog.KataS3, UnitCatalog.Guza, UnitCatalog.Kubi }),
        ("M台4", "ポンのパターン2（グザ・スィド）", m => new() { UnitCatalog.Beni, m, UnitCatalog.KataS3, UnitCatalog.Guza, UnitCatalog.Sid }),
    };

    /// <summary>ポンの席（第216期の台A・台B）。ミオを差し替える。</summary>
    internal static Formation PonX(UnitDef mio) => Formation.Build(
        front1: UnitCatalog.Kubi, front3: UnitCatalog.Guza, center: UnitCatalog.Beni, back1: mio, back3: UnitCatalog.KataS3);
    internal static Formation PonP2(UnitDef mio) => Formation.BuildDiamond(
        a: UnitCatalog.KataS3, b: UnitCatalog.Guza, c: UnitCatalog.Beni, d: UnitCatalog.Sid, e: mio);

    /// <summary>M台5 ＝ `compare` でミオのいる行（元の席のまま）。</summary>
    internal static List<(string Name, Formation F)> M5Rows()
        => CompareBuilds().Where(r => r.F.Occupied().Any(o => o.Def.Id == "mio")).Select(r => (r.Name, r.F)).ToList();

    internal static Formation WithMio(Formation f, UnitDef mio)
    {
        var g = new Formation { Shape = f.Shape };
        foreach ((int slot, UnitDef d) in f.Occupied()) g[slot] = d.Id == "mio" ? mio : d;
        return g;
    }

    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string F2(double x) => double.IsNaN(x) ? "—" : x.ToString("F2");
    static string P1(long a, long b) => b == 0 ? "—" : (100.0 * a / b).ToString("F1");
    static string Short(UnitDef d) => d.Name.Split('の').Last();
    static string ShapeName(FormationShape s) => s == FormationShape.X ? "X字" : "P2";
    internal static string SeatsNamed(Formation f) => string.Join(" ／ ", f.Occupied().Select(o => f.Shape.FrameNames[o.Slot] + ":" + Short(o.Def)));

    static string FindRoot()
    {
        string d = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(d, "CLAUDE.md"))) d = Path.GetDirectoryName(d) ?? throw new InvalidOperationException("CLAUDE.md が見つからない");
        return d;
    }

    static int LineOf(string src, string key)
    {
        int at = src.IndexOf(key, StringComparison.Ordinal);
        return at < 0 ? -1 : src.Take(at).Count(c => c == '\n') + 1;
    }

    // =================================================================================
    // 台本から読む（Phase 0・M0）
    // =================================================================================

    /// <summary>1 台ぶんの台本の読み（Q0-1・Q0-4）。</summary>
    sealed class Scan
    {
        public long Battles, MioActs, MioActsWithShock, ShockedAtMioSum, ShockedSlowerAtMio;
        public long KataGains, GainPoppedBeforeMio, GainDiedBeforeMio, GainAliveAtMio, GainEnded;
        public long PopByMio, PopByAlly, PopByFoe, PopByTick;
        public long Centers, CentersFresh, AroundFresh, Around;
        public long MarksDiedFoe, MarksDiedAlly, FoeDeathsMarked, FoeDeaths;
    }

    static Scan ScanBoard(Formation f, EnemyScaleRule scale, int seeds)
    {
        var sc = new Scan();
        var gate = new object();
        Parallel.For(0, 4 * seeds, () => new Scan(), (j, _, loc) =>
        {
            int st = 1 + j / seeds, s = j % seeds;
            var pl = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var en = BattleEngine.Materialize(EnemyCatalog.Stages[st].Enemy, BattleContext.EnemyTeam, scale);
            BattleResult r = BattleEngine.Run(pl, en, s, verbose: true);
            ReadOne(r, pl, en, loc);
            return loc;
        }, loc =>
        {
            lock (gate)
                foreach (var fi in typeof(Scan).GetFields()) fi.SetValue(sc, (long)fi.GetValue(sc)! + (long)fi.GetValue(loc)!);
        });
        return sc;
    }

    static void ReadOne(BattleResult r, List<UnitState> pl, List<UnitState> en, Scan sc)
    {
        sc.Battles++;
        UnitState? mio = pl.FirstOrDefault(u => u.Def.Id == "mio");
        UnitState? kata = pl.FirstOrDefault(u => u.Def.Id == "kata");
        if (mio is null) return;
        var foe = en.Select(u => u.InstanceId).ToHashSet();
        var ally = pl.Select(u => u.InstanceId).ToHashSet();
        var speed = en.ToDictionary(u => u.InstanceId, u => u.Def.Speed);
        var shocked = new HashSet<int>();
        var pending = new Dictionary<int, int>();   // 敵 → カタが付けた感電（ミオの次の手番を待っている）
        var marks = new Dictionary<int, int>();
        var dead = new HashSet<int>();
        foreach (BattleEvent e in r.Events)
        {
            int t = e.TargetId ?? -1;
            switch (e.Kind)
            {
                case BattleEventKind.StatusGain when e.Text == StatusKeys.Shock:
                    if (foe.Contains(t))
                    {
                        shocked.Add(t);
                        if (kata is not null && e.ActorId == kata.InstanceId) { sc.KataGains++; pending[t] = 1; }
                    }
                    break;
                case BattleEventKind.ShockSpent:
                    if (foe.Contains(t))
                    {
                        shocked.Remove(t);
                        if (pending.Remove(t)) sc.GainPoppedBeforeMio++;
                        if (e.ActorId is null) sc.PopByTick++;
                        else if (e.ActorId == mio.InstanceId) sc.PopByMio++;
                        else if (ally.Contains(e.ActorId.Value)) sc.PopByAlly++;
                        else sc.PopByFoe++;
                    }
                    break;
                case BattleEventKind.Death:
                    dead.Add(t);
                    if (foe.Contains(t))
                    {
                        sc.FoeDeaths++;
                        shocked.Remove(t);
                        if (pending.Remove(t)) sc.GainDiedBeforeMio++;
                        int m = marks.GetValueOrDefault(t);
                        if (m > 0) { sc.FoeDeathsMarked++; sc.MarksDiedFoe += m; }
                    }
                    else sc.MarksDiedAlly += marks.GetValueOrDefault(t);
                    break;
                case BattleEventKind.ConcentrateMark:
                    marks[t] = e.Amount;
                    if (e.Text == ConcentrateTrait.CenterLabel) { sc.Centers++; if (e.Amount == 1) sc.CentersFresh++; }
                    else if (e.Text == ConcentrateTrait.AroundLabel) { sc.Around++; if (e.Amount == 1) sc.AroundFresh++; }
                    break;
                case BattleEventKind.Skill when e.ActorId == mio.InstanceId:
                    sc.MioActs++;
                    int live = shocked.Count(x => !dead.Contains(x));
                    if (live > 0) sc.MioActsWithShock++;
                    sc.ShockedAtMioSum += live;
                    sc.ShockedSlowerAtMio += shocked.Count(x => !dead.Contains(x) && speed[x] <= mio.Def.Speed);
                    sc.GainAliveAtMio += pending.Count;
                    pending.Clear();
                    break;
            }
        }
        sc.GainEnded += pending.Count;
    }

    // =================================================================================
    // Phase 0（実装前）
    // =================================================================================

    static void Phase0()
    {
        var t0 = DateTime.Now;
        Console.WriteLine("# 第218期 `mire phase0` —— Q0-1〜Q0-6（実装前・盤面は第217期の追記のまま）");
        Console.WriteLine();
        string root = FindRoot();
        string engine = File.ReadAllText(Path.Combine(root, "BattleCore", "BattleEngine.cs"));

        // ---- 前提 ----
        Console.WriteLine("## 前提（シガ G3K・ベニ O4・カタ S2 が規定か）");
        Console.WriteLine();
        UnitDef mio = UnitCatalog.Mio;
        Console.WriteLine("- ミオ: HP" + mio.MaxHp + "・攻" + mio.Attack + "・速" + mio.Speed + "・札 " + string.Join(", ", mio.Traits) + "・手番 " + string.Join(", ", mio.Actions!.Select(a => a.Kind + "「" + a.Label + "」")));
        Console.WriteLine("- シガ: " + UnitCatalog.ShigaG3K.Name + "・" + UnitCatalog.ShigaG3K.Pattern + "・札 " + string.Join(", ", UnitCatalog.ShigaG3K.Traits));
        Console.WriteLine("- ベニの札: " + string.Join(", ", UnitCatalog.Beni.Traits) + "（O4 " + (UnitCatalog.Beni.Traits.Contains(TraitId.GurenOpeningBurn) ? "**あり**" : "なし") + "）");
        Console.WriteLine("- カタの札: " + string.Join(", ", UnitCatalog.KataS3.Traits) + "（S2 " + (UnitCatalog.KataS3.Traits.Contains(TraitId.ShockStunAll) ? "**あり**" : "なし") + "）");
        Console.WriteLine();

        // ---- Q0-6（先に席を決める：Q0-1・Q0-4 はこの席で読む） ----
        Console.WriteLine("## Q0-6 台と駒（**仮の席** ＝ M0 × 倍率 150 × 第2〜5波 × seed 1000..1049 の勝ち数最大・同値は列挙順で最初）");
        Console.WriteLine();
        Console.WriteLine("本番の席は実装の後に **M4** で選び直す（指示書 §6.1）。ここでは Q0-1・Q0-4 を読むための仮の席。");
        Console.WriteLine();
        var boards = new List<(string Tag, Formation F)>();
        foreach (var (tag, aim, mem) in Rigs)
        {
            Console.WriteLine("- " + tag + "（" + aim + "）: " + string.Join("・", mem(mio).Select(d => Short(d) + "(速" + d.Speed + (d.Actions is { Count: > 0 } a && a.All(x => x.Kind == ActionKind.Skill) ? "・殴らない" : "・殴る") + ")")));
            foreach (FormationShape sh in ShockDiag.Shapes)
            {
                Formation f = ShockDiag.PickSeats216(mem(mio), sh, ShockDiag.Scale150);
                boards.Add((tag + " " + ShapeName(sh), f));
                Console.WriteLine("  - " + ShapeName(sh) + ": " + SeatsNamed(f));
            }
        }
        boards.Add(("M台3 ポンX", PonX(mio)));
        boards.Add(("M台4 ポンP2", PonP2(mio)));
        Console.WriteLine("- ポンの席: X字 " + SeatsNamed(PonX(mio)) + " ／ P2 " + SeatsNamed(PonP2(mio)));
        var m5 = M5Rows();
        Console.WriteLine("- M台5（`compare` でミオのいる行）: " + m5.Count + " 行 —— " + string.Join(" ／ ", m5.Select(r => "`" + r.Name + "`")));
        Console.WriteLine();

        // ---- Q0-1 ----
        Console.WriteLine("## Q0-1 感電の残り方（台本・M0・第2〜5波 × seed 0..49）");
        Console.WriteLine();
        Console.WriteLine("「カタの感電」＝ カタが敵に新しく付けた感電。**ミオの次の手番**（`Skill`）まで残ったか、その前に弾けた／倒れたか。"
                          + "「手番の頭の感電」＝ ミオの手番の瞬間に生きていて感電している敵の数（うち速さ ≤ 8 ＝ ミオより後に動く敵）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | ミオの手番/戦 | 手番の頭に感電の敵がいた % | その数/手番 | うちミオより遅い | カタの感電/戦 | ミオまで残った % | 先に弾けた % | 先に倒れた % | 戦が終わった % | 弾いた主: ミオ/味方/敵/刻み（/戦） |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        var scans = new Dictionary<string, Scan>();
        foreach (var (tag, f) in boards)
            foreach (var (st, rule) in ShockDiag.Scales216)
            {
                Scan s = ScanBoard(f, rule, 50);
                if (st == "115") scans[tag] = s;
                double b = s.Battles;
                Console.WriteLine("| " + tag + " | " + st + " | " + F2(s.MioActs / b) + " | " + P1(s.MioActsWithShock, s.MioActs) + " | " + F2((double)s.ShockedAtMioSum / Math.Max(1, s.MioActs))
                                  + " | " + F2((double)s.ShockedSlowerAtMio / Math.Max(1, s.MioActs)) + " | " + F2(s.KataGains / b) + " | " + P1(s.GainAliveAtMio, s.KataGains)
                                  + " | " + P1(s.GainPoppedBeforeMio, s.KataGains) + " | " + P1(s.GainDiedBeforeMio, s.KataGains) + " | " + P1(s.GainEnded, s.KataGains)
                                  + " | " + F2(s.PopByMio / b) + " / " + F2(s.PopByAlly / b) + " / " + F2(s.PopByFoe / b) + " / " + F2(s.PopByTick / b) + " |");
            }
        Console.WriteLine();

        // ---- Q0-2 ----
        Console.WriteLine("## Q0-2 ミオ（速" + mio.Speed + "）より遅い敵");
        Console.WriteLine();
        Console.WriteLine("同速は陣営の番号の順（味方が先）なので、**速さ ≤ 8 の敵はミオの後に動く**。ミオの一撃で弾けて痺れれば、その敵は**そのターンの手番**を失う。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 敵（速さ） | ≤ 8 の割合 | ≤ 6（カタより後）の割合 |");
        Console.WriteLine("|---|---|--:|--:|");
        for (int st = 1; st < 5; st++)
        {
            var ds = EnemyCatalog.Stages[st].Enemy.Occupied().Select(o => o.Def).ToList();
            Console.WriteLine("| 第" + (st + 1) + "波 | " + string.Join("・", ds.Select(d => d.Name + "(" + d.Speed + ")")) + " | "
                              + ds.Count(d => d.Speed <= mio.Speed) + " / " + ds.Count + " | " + ds.Count(d => d.Speed <= 6) + " / " + ds.Count + " |");
        }
        Console.WriteLine();

        // ---- Q0-3 ----
        Console.WriteLine("## Q0-3 与ダメを下げる既存の口");
        Console.WriteLine();
        Console.WriteLine("| 口 | 場所 | 何に掛かる | 重ね方 |");
        Console.WriteLine("|---|---|---|---|");
        Console.WriteLine("| 萎縮（クビ・`Daunted`） | `BattleEngine.cs:" + LineOf(engine, "if (_dauntLive && actor.RawCounter(StatusKeys.Daunted) > 0)") + "`（`PerformAttackBody`・`atk` を作り終えた直後） | `PerformAttack` を通る一撃（反撃・割り込み・追い打ち・再行動も） | 次の1回だけ ×50%（切り捨て）して消える |");
        Console.WriteLine("| 痺れ毒（スィド・`Numbed`） | `BattleEngine.cs:" + LineOf(engine, "if (_numbLive && actor.RawCounter(StatusKeys.Numbed) > 0)") + "`（萎縮の直後） | 同上 | 層 × 3%（上限 60%）・消えない。萎縮と重なれば 半分 → さらに割合（切り捨て2回） |");
        Console.WriteLine("| 身を縮める（クビ・`Huddle`／旧 `Cower`） | `BattleEngine.cs:" + LineOf(engine, "teammates.Any(u => u.HasTrait(TraitId.Cower) || u.HasTrait(TraitId.Huddle))") + "`（`ApplyDamage` の軽減の族） | **被ダメ側**（受ける駒の味方にクビがいれば −30%） | 与ダメ側ではない |");
        Console.WriteLine("| なまり（`AtkBonus` を負に・窓口 `Dull`） | `ctx.Dull`（ネルの呪い −4・呪いの漏れ −2 ほか） | `CurrentAttack` そのもの（攻撃・起爆以外の攻撃力を読む全部） | 加算（割合ではない）。倍率を上げた敵には効きが薄まる |");
        Console.WriteLine();
        Console.WriteLine("- **雷（`StrikeThunder`）と放電（`Discharge`）は `PerformAttack` を通らない**（`ApplyDamage` を直に呼ぶ）。萎縮・痺れ毒は掛からない。");
        Console.WriteLine("- **敵の与ダメージは全部 `PerformAttack` を通る**（第195期 Q0-1。5波の敵の札で `ApplyDamage` を直に呼ぶものは 0 本）——敵だけに効く版（M3）は `PerformAttackBody` の1箇所で全部に掛かる。");
        Console.WriteLine("- **挟む場所の案**: 与ダメの量を作る3箇所 ——(1) `PerformAttackBody` の**痺れ毒の直後**（通常攻撃・反撃）／(2) `StrikeThunder` の量（雷）／(3) `Discharge` の量（放電）——と、叩きつけ・通電の量。"
                          + "割合（印 × 10%・上限 40%）を**切り捨て**で引く（萎縮・痺れ毒と同じ形）。**刻みは出どころが null なので自然に外れる**。棘の反射・板の反射・リリの吸い取り・灰は指示書の一覧（通常攻撃・雷・放電・反撃）に無いので掛けない。");
        Console.WriteLine("- **重ね順**: 萎縮 → 痺れ毒 → 澱み（どれも切り捨て）。§1 の敵の標 +50%・身を縮める −30%・層・軛は `ApplyDamage` の中なので、**軽くなった量に今までどおり掛かる**。");
        Console.WriteLine("- 放電は `isFriendlyFire` で**出どころが放電した駒**——敵だけに効く版でも、印のある敵が放電すれば**敵どうしの放電が軽くなる**（指示書の一覧どおりに掛けると、こちらには損の側に働く）。表D で別に数える。");
        Console.WriteLine();

        // ---- Q0-4 ----
        Console.WriteLine("## Q0-4 失速の実測（台本・M0・倍率 115・第2〜5波 × seed 0..49）");
        Console.WriteLine();
        Console.WriteLine("「一から重ね直した」＝ 中心の印が付いた後に 1（それまで 0）。「印を持って倒れた」＝ 倒れた瞬間に印が 1 以上。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 中心/戦 | 一から重ね直した % | 周り/戦 | 周りが 0 から % | 倒れた敵/戦 | 印を持って倒れた % | 倒れて消えた印（敵）/戦 | 倒れて消えた印（味方）/戦 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (tag, s) in scans)
        {
            double b = s.Battles;
            Console.WriteLine("| " + tag + " | " + F2(s.Centers / b) + " | " + P1(s.CentersFresh, s.Centers) + " | " + F2(s.Around / b) + " | " + P1(s.AroundFresh, s.Around)
                              + " | " + F2(s.FoeDeaths / b) + " | " + P1(s.FoeDeathsMarked, s.FoeDeaths) + " | " + F2(s.MarksDiedFoe / b) + " | " + F2(s.MarksDiedAlly / b) + " |");
        }
        foreach (var (name, f) in m5)
        {
            Scan s = ScanBoard(f, EnemyScaleRule.Adopted, 50);
            double b = s.Battles;
            Console.WriteLine("| M台5 `" + name + "` | " + F2(s.Centers / b) + " | " + P1(s.CentersFresh, s.Centers) + " | " + F2(s.Around / b) + " | " + P1(s.AroundFresh, s.Around)
                              + " | " + F2(s.FoeDeaths / b) + " | " + P1(s.FoeDeathsMarked, s.FoeDeaths) + " | " + F2(s.MarksDiedFoe / b) + " | " + F2(s.MarksDiedAlly / b) + " |");
        }
        Console.WriteLine();

        // ---- Q0-5 ----
        Console.WriteLine("## Q0-5 過去の測定");
        Console.WriteLine();
        Console.WriteLine("- 第194期（ミオの転生）: 印（刻みの追加回数）で在席行は上がった（`毒` 59.2 → 99.8 ほか・差し替え表 4 / 4 が正）。**第四波の上げ幅がいちばん大きい**（層を2倍にした初版は軛に切られ、回数は切られない＝R284）。"
                          + "代金はベニが隣の `毒+耐久` で最大（結界がミオの隣4枚のうち1枚しか覆わない）。**1回の刻みの量は旧と変わらない**——速くなったのは「倒れるまで」。");
        Console.WriteLine("- 第214期（感電）: **弾く役は攻撃型ではなく行動順で決まる**（R317）——感電は付いた手番のうちに弾かれるので、カタより速い駒は弾けない。**ミオ（速8）はカタ（速6）より速い**ので、"
                          + "ミオが弾くのは**前のターンに付いて残った感電**になる（同じターンのカタの感電はミオの後に付く）。");
        Console.WriteLine("- 第216期（S2）: 味方の連鎖は小さく多い・敵の連鎖は大きい。**弾く役が自陣で痺れると連鎖ごと消える**（R319・台C の P2 で唯一の弾く役トウが止まって 67.0 → 28.0）。");
        Console.WriteLine("- 第217期（電気鞭）: 1振りで弾かせる感電は増えない（主目標を弾けば放電で連鎖が敵陣を一周する）。");
        Console.WriteLine("- **ミオが殴る版・通電・印を運ぶ版を測った期は 0**（`design/` を「叩きつけ」「通電」で grep ——当たるのは第138期の指示書の「破片を消費して敵全体へ叩きつける」だけで、別の駒の話）。");
        Console.WriteLine();
        Console.WriteLine("所要 " + (DateTime.Now - t0).TotalSeconds.ToString("F0") + " 秒");
    }
}
