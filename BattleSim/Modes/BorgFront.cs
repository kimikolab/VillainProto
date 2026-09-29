using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using BG = BorgGuardDiag;

// =====================================================================================
// borgfront —— 第235期「ボルグを前に出す理由を揃える（燃える巻き込み・くすぶり・火の癒し・焼き返し）」。
// 指示書は design/PHASE235_BORG_FRONT_SPEC.md ／ 報告は design/PHASE235_BORG_FRONT.md。
// **版は札の差し替えだけ**（G0 規定 ／ B ＝ 第234期 G3 ／ S・O・H1・H2 を1本ずつ積む・抜く）。**線は置かない。**
// 集計は第234期の `BorgGuardDiag.XAgg`（＝第233期の `BurnAuditDiag.Agg`）をそのまま包み、この期の量だけを足す。
//
//     dotnet run --project BattleSim -c Release 0 borgfront digest [path]     # 受け入れ 1 の台本の指紋（G0 ／ B）
//     dotnet run --project BattleSim -c Release 0 borgfront phase0            # Q0-1〜Q0-5
//     dotnet run --project BattleSim -c Release 0 borgfront pick [dir]        # 段1（版ごとに T3 1,081 組 ＋ 雷＋ボルグ 44 枚 × 席 120 × seed 40）
//     dotnet run --project BattleSim -c Release 0 borgfront run [dir]         # 段2 ＋ 表A〜R（段1 の TSV が無ければ回し直す）
//     dotnet run --project BattleSim -c Release 0 borgfront check             # 自己検査（受け入れ 2〜5）
//     dotnet run --project BattleSim -c Release 0 borgfront log <版> <前1,前3,中央,後1,後3> [seed] [波] [倍率 0/1/2]
// =====================================================================================
static partial class BorgFrontDiag
{
    public static void Run(string[] args, int stageIndex)
    {
        _args = args;
        string mode = args.Length > 2 ? args[2] : "";
        switch (mode)
        {
            case "digest": Digest(); return;
            case "phase0": Phase0(); return;
            case "pick": PickImpl(); return;
            case "run": RunImpl(); return;
            case "check": CheckImpl(); return;
            case "log":
                LogOne(args.Length > 3 ? args[3] : "ALL", args.Length > 4 ? args[4] : "", args.Length > 5 ? int.Parse(args[5]) : 0,
                    args.Length > 6 ? int.Parse(args[6]) : BA.MainWave, args.Length > 7 ? int.Parse(args[7]) : 0);
                return;
            default:
                Console.WriteLine("borgfront: モードは digest / phase0 / pick / run / check / log。");
                return;
        }
    }

    static string[]? _args;
    static partial void Phase0();
    static partial void PickImpl();
    static partial void RunImpl();
    static partial void CheckImpl();

    // ---------------------------------------------------------------------------------
    // 版（§4）——札の差し替えだけ。焼き返し（H2）は札の並びで火の粉（Cinder）より前に置く（殴る前に燃えていたかを読むため）。
    // ---------------------------------------------------------------------------------
    internal static UnitDef Mk(bool s, bool o, bool h1, bool h2, bool dry = false)
    {
        var tr = new List<TraitId> { TraitId.Splash };
        if (h2) tr.Add(TraitId.FireFeed);
        tr.Add(TraitId.Cinder);
        tr.Add(TraitId.FireArmor);
        if (s) tr.Add(TraitId.FireSplash);
        if (o) tr.Add(TraitId.SelfKindle);
        if (h1) tr.Add(TraitId.FireMend);
        if (dry) tr.Add(TraitId.FireMendDry);
        var g = UnitCatalog.Borg;
        return new UnitDef
        {
            Id = g.Id, Name = g.Name, MaxHp = 100, Attack = g.Attack, Speed = g.Speed, Traits = tr.ToArray(), Pattern = g.Pattern,
            Advances = g.Advances, Actions = g.Actions, PlusText = g.PlusText, MinusText = g.MinusText, Flavor = g.Flavor,
        };
    }

    /// <summary>版。<c>Key</c> はファイル名に使う（ASCII）。B は第234期 G3 の駒そのもの（受け入れ 1）。</summary>
    internal static readonly (string Name, string Key, string What, UnitDef Borg)[] Versions =
    {
        ("G0", "G0", "規定（対照）", UnitCatalog.Borg),
        ("B", "B", "火の鎧 ＋ HP100（第234期 G3）", BG.VerBorg("G3")),
        ("B+S", "BS", "＋燃える巻き込み", Mk(true, false, false, false)),
        ("B+S+O", "BSO", "＋くすぶり", Mk(true, true, false, false)),
        ("B+S+O+H1", "BSOH1", "＋火の癒し", Mk(true, true, true, false)),
        ("全部", "ALL", "＋焼き返し", Mk(true, true, true, true)),
        ("全部−S", "ALL-S", "燃える巻き込みだけ抜く", Mk(false, true, true, true)),
        ("全部−O", "ALL-O", "くすぶりだけ抜く", Mk(true, false, true, true)),
        ("全部−H1", "ALL-H1", "火の癒しだけ抜く（火に焼かれないに戻る）", Mk(true, true, false, true)),
    };
    /// <summary>H1b（火の回復が渇きに封じられる）——全部の台で第三波だけ並べる。</summary>
    internal static readonly UnitDef H1b = Mk(true, true, true, true, dry: true);
    internal static UnitDef VerBorg(string name) => name == "H1b" ? H1b : Versions.First(v => v.Name == name || v.Key == name).Borg;

    /// <summary>雷＋ボルグの芯（ベニ・ミオ・カタ・ボルグ）と、相方の候補（T3 と同じ 47 枚からベニ・ミオ・カタを除く 44 枚）。</summary>
    internal static UnitDef[] ThunderCore(UnitDef borg) => new[] { UnitCatalog.Beni, UnitCatalog.Mio, UnitCatalog.Kata, borg };
    internal static List<UnitDef> ThunderCandidates => BA.Candidates.Where(u => u.Id is not ("beni" or "mio" or "kata")).ToList();
    /// <summary>雷＋ボルグ（置き換え）: ポンの席の前1 シガ → ボルグ ／ 前3 ツギ → ボルグ。</summary>
    internal static Formation ThunderSwap(UnitDef borg, bool front1) => front1
        ? Formation.Build(front1: borg, front3: UnitCatalog.Tsugi, center: UnitCatalog.Beni, back1: UnitCatalog.Kata, back3: UnitCatalog.Mio)
        : Formation.Build(front1: UnitCatalog.Shiga, front3: borg, center: UnitCatalog.Beni, back1: UnitCatalog.Kata, back3: UnitCatalog.Mio);

    static void LogOne(string ver, string seats, int seed, int wave, int sc)
    {
        var borg = VerBorg(ver);
        var f = BG.Dec(seats, borg);
        var (r, _, _, _) = BA.Fight(f, wave, BA.Scales[sc].Sc, seed);
        Console.WriteLine($"# {ver} × {BA.SeatsNamed(f)} × {BA.WaveNames[wave]} × {BA.Scales[sc].Name} × seed {seed}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }

    // ---------------------------------------------------------------------------------
    // 集計: 第234期の XAgg ＋ この期の量
    // ---------------------------------------------------------------------------------
    internal sealed class YAgg
    {
        public readonly BG.XAgg X = new();
        public long SplashPhysDeaths, SplashBurnDeaths;          // 表B: ボルグの巻き込みで倒れた（物理 ／ 燃える巻き込み）
        public long BorgT1Alive, BorgT1Burn, BorgT1Taken, BorgT1Hits;   // 表D: 1ターン目
        public long SplashHits, SplashNominal, SplashImmune, SplashInverted, SplashInvHealed, SplashTaken;
        public long KindleLit, MendNominal, MendHealed, MendDry, MendSplash, FeedFires, FeedNominal, FeedHealed, FeedDry;
        public long BorgHealed, BorgTaken;
        public long KataCasts, KataHits, KataDealt, KindsSum, KindsN;
        public long BorgFell;

        public void Merge(YAgg o)
        {
            X.Merge(o.X);
            SplashPhysDeaths += o.SplashPhysDeaths; SplashBurnDeaths += o.SplashBurnDeaths;
            BorgT1Alive += o.BorgT1Alive; BorgT1Burn += o.BorgT1Burn; BorgT1Taken += o.BorgT1Taken; BorgT1Hits += o.BorgT1Hits;
            SplashHits += o.SplashHits; SplashNominal += o.SplashNominal; SplashImmune += o.SplashImmune; SplashInverted += o.SplashInverted;
            SplashInvHealed += o.SplashInvHealed; SplashTaken += o.SplashTaken;
            KindleLit += o.KindleLit; MendNominal += o.MendNominal; MendHealed += o.MendHealed; MendDry += o.MendDry; MendSplash += o.MendSplash;
            FeedFires += o.FeedFires; FeedNominal += o.FeedNominal; FeedHealed += o.FeedHealed; FeedDry += o.FeedDry;
            BorgHealed += o.BorgHealed; BorgTaken += o.BorgTaken;
            KataCasts += o.KataCasts; KataHits += o.KataHits; KataDealt += o.KataDealt; KindsSum += o.KindsSum; KindsN += o.KindsN;
            BorgFell += o.BorgFell;
        }

        static readonly string BurnLabel = StatusKeys.LabelOf(StatusKeys.Burn);

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e, Dictionary<int, int> slot0)
        {
            X.Take(r, p, e, slot0);
            var borgU = p.FirstOrDefault(u => u.Def.Id == "borg");
            int borg = borgU?.InstanceId ?? -1;
            var players = p.Select(u => u.InstanceId).ToHashSet();
            var hp = p.Concat(e).ToDictionary(u => u.InstanceId, u => u.Def.MaxHp);
            var pending = new HashSet<int>();                 // 燃える巻き込みの札が出て、まだ Damage が来ていない駒
            var fatalSplash = new Dictionary<int, int>();     // 0 なし ／ 1 物理 ／ 2 燃焼
            int turn = 0;
            bool t1Alive = false, t1Burn = false;
            foreach (BattleEvent ev in r.Events)
            {
                switch (ev.Kind)
                {
                    case BattleEventKind.TurnStart:
                        turn = ev.Turn;
                        if (turn == 1 && borg >= 0) t1Alive = true;
                        break;
                    case BattleEventKind.Attack:
                        pending.Clear();
                        break;
                    case BattleEventKind.StatusSnapshot when turn == 1 && ev.TargetId == borg && ev.Text == BurnLabel:
                        t1Burn = true;
                        break;
                    case BattleEventKind.FireArmor when ev.Text == FireArmorLabels.Splash && ev.TargetId is int st:
                        pending.Add(st);
                        break;
                    case BattleEventKind.Damage when ev.TargetId is int dt && hp.ContainsKey(dt):
                        {
                            int loss = Math.Max(0, hp[dt] - ev.HpAfter);
                            if (dt == borg && turn == 1) { BorgT1Taken += loss; if (ev.ActorId is int ea && !players.Contains(ea)) BorgT1Hits++; }
                            if (players.Contains(dt))
                            {
                                bool splash = ev.ActorId == borg && borg >= 0 && ev.FriendlyFire && ev.Pattern is null && !ev.Relayed;
                                bool burn = splash && pending.Remove(dt);
                                if (ev.HpAfter <= 0) fatalSplash[dt] = splash ? (burn ? 2 : 1) : 0;
                            }
                            break;
                        }
                    case BattleEventKind.Death when ev.TargetId is int dd && players.Contains(dd):
                        if (fatalSplash.TryGetValue(dd, out int k)) { if (k == 1) SplashPhysDeaths++; else if (k == 2) SplashBurnDeaths++; }
                        fatalSplash.Remove(dd);
                        break;
                }
                if (ev.Kind is BattleEventKind.Damage or BattleEventKind.Heal && ev.TargetId is int ht && hp.ContainsKey(ht)) hp[ht] = ev.HpAfter;
                if (ev.Kind == BattleEventKind.Revive && ev.TargetId is int rv && ev.HpAfter > 0 && hp.ContainsKey(rv)) hp[rv] = ev.HpAfter;
            }
            if (t1Alive) { BorgT1Alive++; if (t1Burn) BorgT1Burn++; }
            if (borgU is not null && r.TallyByUnit.TryGetValue("borg", out var t))
            {
                SplashHits += t.FireSplashHits; SplashNominal += t.FireSplashNominal; SplashImmune += t.FireSplashImmune;
                SplashInverted += t.FireSplashInverted; SplashInvHealed += t.FireSplashInvHealed; SplashTaken += t.FireSplashTaken;
                KindleLit += t.SelfKindleLit; MendNominal += t.FireMendNominal; MendHealed += t.FireMendHealed; MendDry += t.FireMendDry; MendSplash += t.FireMendSplash;
                FeedFires += t.FireFeedFires; FeedNominal += t.FireFeedNominal; FeedHealed += t.FireFeedHealed; FeedDry += t.FireFeedDry;
                BorgHealed += t.Healed;
                if (r.PlayerStarterFallen.Contains("borg")) BorgFell++;
            }
            if (borgU is not null) BorgTaken += X.A.Taken.GetValueOrDefault(UnitCatalog.Borg.Name)?.Sum() ?? 0;
            if (r.TallyByUnit.TryGetValue("kata", out var kt) && p.Any(u => u.Def.Id == "kata"))
            {
                KataCasts += kt.ThunderCasts; KataHits += kt.ThunderHits; KataDealt += kt.ThunderDealt;
                if (kt.ThunderKindsHist is { } h) for (int i = 0; i < h.Length; i++) { KindsSum += i * h[i]; KindsN += h[i]; }
            }
        }
    }

    internal static YAgg Measure(Formation f, int w, EnemyScaleRule sc, int seed0 = 0, int seeds = BA.Seeds)
    {
        var parts = new YAgg[seeds];
        Parallel.For(0, seeds, i =>
        {
            var a = new YAgg();
            var (r, p, e, slot0) = BA.Fight(f, w, sc, seed0 + i);
            a.Take(r, p, e, slot0);
            parts[i] = a;
        });
        var all = new YAgg();
        foreach (var a in parts) all.Merge(a);
        return all;
    }
}
