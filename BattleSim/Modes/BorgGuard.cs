using BattleCore;
using static Common;
using BA = BurnAuditDiag;

// =====================================================================================
// borgguard —— 第234期「ボルグを盾に転生（火の鎧・焼け残り）」。
// 指示書は design/PHASE234_BORG_GUARD_SPEC.md ／ 報告は design/PHASE234_BORG_GUARD.md。
// **版は札の差し替えだけ**（G0 規定 ／ G1 火の鎧 ／ G2 ＋焼け残り ／ G3 G1 ＋HP100 ／ G4 全部）。**線は置かない。**
// 集計は第233期の `BurnAuditDiag.Agg` をそのまま包み、ボルグ固有の量だけを足す。
//
//     dotnet run --project BattleSim -c Release 0 borgguard phase0         # Q0-1〜Q0-5
//     dotnet run --project BattleSim -c Release 0 borgguard pick [TSV]     # 段1（版ごとに 1,081 組 × 席 120 × seed 40）
//     dotnet run --project BattleSim -c Release 0 borgguard run [TSV]      # 段2 ＋ 表A〜G（TSV が無ければ段1 を回し直す）
//     dotnet run --project BattleSim -c Release 0 borgguard check          # 自己検査（受け入れ 1〜5）
//     dotnet run --project BattleSim -c Release 0 borgguard log <版> <前1,前3,中央,後1,後3> [seed] [波] [倍率 0/1/2]
// =====================================================================================
static partial class BorgGuardDiag
{
    public static void Run(string[] args, int stageIndex)
    {
        _args = args;
        string mode = args.Length > 2 ? args[2] : "";
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "pick": PickImpl(); return;
            case "run": RunImpl(); return;
            case "check": CheckImpl(); return;
            case "log":
                LogOne(args.Length > 3 ? args[3] : "G1", args.Length > 4 ? args[4] : "", args.Length > 5 ? int.Parse(args[5]) : 0,
                    args.Length > 6 ? int.Parse(args[6]) : BA.MainWave, args.Length > 7 ? int.Parse(args[7]) : 0);
                return;
            default:
                Console.WriteLine("borgguard: モードは phase0 / pick / run / check / log。");
                return;
        }
    }

    static string[]? _args;
    static partial void Phase0();
    static partial void PickImpl();
    static partial void RunImpl();
    static partial void CheckImpl();

    // ---------------------------------------------------------------------------------
    // 版（§2）——札の差し替えだけ。巻き込み・火の粉・攻撃力・速さ・薙ぎはそのまま。
    // ---------------------------------------------------------------------------------
    static UnitDef BorgWith(int hp, params TraitId[] extra) => new()
    {
        Id = UnitCatalog.BorgF0.Id, Name = UnitCatalog.BorgF0.Name, MaxHp = hp, Attack = UnitCatalog.BorgF0.Attack, Speed = UnitCatalog.BorgF0.Speed,
        Traits = UnitCatalog.BorgF0.Traits.Concat(extra).ToArray(), Pattern = UnitCatalog.BorgF0.Pattern, Advances = UnitCatalog.BorgF0.Advances,
        Actions = UnitCatalog.BorgF0.Actions, PlusText = UnitCatalog.BorgF0.PlusText, MinusText = UnitCatalog.BorgF0.MinusText, Flavor = UnitCatalog.BorgF0.Flavor,
    };

    internal static readonly (string Name, string What, UnitDef Borg)[] Versions =
    {
        ("G0", "第233期の規定（対照）", UnitCatalog.BorgF0),
        ("G1", "火の鎧", BorgWith(60, TraitId.FireArmor)),
        ("G2", "火の鎧 ＋ 焼け残り", BorgWith(60, TraitId.FireArmor, TraitId.Smolder)),
        ("G3", "火の鎧 ＋ HP100", BorgWith(100, TraitId.FireArmor)),
        ("G4", "全部", BorgWith(100, TraitId.FireArmor, TraitId.Smolder)),
    };
    internal static UnitDef VerBorg(string name) => Versions.First(v => v.Name == name).Borg;

    internal static UnitDef[] CoreOf(UnitDef borg) => new[] { borg, UnitCatalog.HotaL0, UnitCatalog.HiyoF0 };

    /// <summary>X 字の編成で、ボルグが前列（前1 ／ 前3）にいるか。</summary>
    internal static bool BorgFront(Formation f) => f[0]?.Id == "borg" || f[1]?.Id == "borg";

    /// <summary>席の並び（駒 Id）の駒を版のボルグで引き直す。</summary>
    internal static Formation Dec(string enc, UnitDef borg)
        => BA.Seat(enc.Split(',').Select(id => id == "borg" ? borg : id == "hota" ? UnitCatalog.HotaL0 : UnitCatalog.ById(id)).ToArray());
    internal static string Enc(Formation f) => string.Join(",", Enumerable.Range(0, 5).Select(i => f[i]!.Id));

    static void LogOne(string ver, string seats, int seed, int wave, int sc)
    {
        var borg = VerBorg(ver);
        var f = Dec(seats, borg);
        var (r, _, _, _) = BA.Fight(f, wave, BA.Scales[sc].Sc, seed);
        Console.WriteLine($"# {ver} × {BA.SeatsNamed(f)} × {BA.WaveNames[wave]} × {BA.Scales[sc].Name} × seed {seed}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }

    // ---------------------------------------------------------------------------------
    // 集計: 第233期の Agg ＋ ボルグ固有の量
    // ---------------------------------------------------------------------------------
    internal sealed class XAgg
    {
        public readonly BA.Agg A = new();
        public long BorgAliveTurns, BorgBurnTurns, BorgFrontTurns, BorgBurnFrontTurns;
        public long BorgEnemyHits;          // ボルグが受けた敵の攻撃の一撃（HP に届いた `Damage`）
        public long FoeLit, SelfLit, GuardHits, Saved;
        public long SmolderUsed, SmolderSurvived, SmolderDiedTurnsAfter, SmolderDied;
        public readonly Dictionary<string, long> FavorDullTo = new();   // ヒヨの −2 の受け手（量）
        public long HiyoToBorg;
        public long BorgDealtSweep;         // ボルグの攻撃の型が薙ぎの一撃で敵に与えた量

        public void Merge(XAgg o)
        {
            A.Merge(o.A);
            BorgAliveTurns += o.BorgAliveTurns; BorgBurnTurns += o.BorgBurnTurns; BorgFrontTurns += o.BorgFrontTurns; BorgBurnFrontTurns += o.BorgBurnFrontTurns;
            BorgEnemyHits += o.BorgEnemyHits;
            FoeLit += o.FoeLit; SelfLit += o.SelfLit; GuardHits += o.GuardHits; Saved += o.Saved;
            SmolderUsed += o.SmolderUsed; SmolderSurvived += o.SmolderSurvived; SmolderDiedTurnsAfter += o.SmolderDiedTurnsAfter; SmolderDied += o.SmolderDied;
            foreach (var (k, v) in o.FavorDullTo) FavorDullTo[k] = FavorDullTo.GetValueOrDefault(k) + v;
            HiyoToBorg += o.HiyoToBorg; BorgDealtSweep += o.BorgDealtSweep;
        }

        static readonly string BurnLabel = StatusKeys.LabelOf(StatusKeys.Burn);

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e, Dictionary<int, int> slot0)
        {
            A.Take(r, p, e, slot0);
            var borgU = p.FirstOrDefault(u => u.Def.Id == "borg");
            var hiyoU = p.FirstOrDefault(u => u.Def.Id == "hiyo");
            int borg = borgU?.InstanceId ?? -1, hiyo = hiyoU?.InstanceId ?? -1;
            var players = p.Select(u => u.InstanceId).ToHashSet();
            var nameOf = p.Concat(e).ToDictionary(u => u.InstanceId, u => u.Def.Name);
            var b = new GaleDiag.Board(p.Concat(e), slot0);
            bool burnSeen = false; int smolderTurn = 0;
            foreach (BattleEvent ev in r.Events)
            {
                switch (ev.Kind)
                {
                    case BattleEventKind.TurnStart:
                        burnSeen = false;
                        if (borg >= 0 && b.Alive.Contains(borg))
                        {
                            BorgAliveTurns++;
                            if (b.RowOfUnit(borg) == Row.Front) BorgFrontTurns++;
                        }
                        break;
                    case BattleEventKind.StatusSnapshot when ev.TargetId == borg && ev.Text == BurnLabel:
                        if (!burnSeen && b.Alive.Contains(borg))
                        {
                            BorgBurnTurns++;
                            if (b.RowOfUnit(borg) == Row.Front) BorgBurnFrontTurns++;
                        }
                        burnSeen = true;
                        break;
                    case BattleEventKind.Damage when ev.TargetId == borg && ev.ActorId is int ea && !players.Contains(ea) && ev.Pattern is not null:
                        BorgEnemyHits++;
                        break;
                    case BattleEventKind.Damage when ev.ActorId == borg && ev.TargetId is int dt && !players.Contains(dt) && ev.Pattern == AttackPattern.Sweep && b.Hp.ContainsKey(dt):
                        BorgDealtSweep += Math.Max(0, b.Hp[dt] - ev.HpAfter);
                        break;
                    case BattleEventKind.StatusGain when ev.ActorId == hiyo && hiyo >= 0 && ev.Text == BattleContext.DullKey && ev.TargetId is int dv:
                        FavorDullTo[nameOf[dv]] = FavorDullTo.GetValueOrDefault(nameOf[dv]) + ev.Amount;
                        break;
                    case BattleEventKind.FireArmor when ev.Text == FireArmorLabels.Smolder:
                        smolderTurn = ev.Turn;
                        break;
                    case BattleEventKind.Death when ev.TargetId == borg && smolderTurn > 0:
                        SmolderDied++; SmolderDiedTurnsAfter += ev.Turn - smolderTurn;
                        break;
                }
                b.Apply(ev);
            }
            if (borgU is not null && r.TallyByUnit.TryGetValue("borg", out var t))
            {
                FoeLit += t.FireArmorFoeLit; SelfLit += t.FireArmorSelfLit; GuardHits += t.FireArmorGuardHits; Saved += t.FireArmorSaved;
                SmolderUsed += t.SmolderUsed;
                if (t.SmolderUsed > 0 && !r.PlayerStarterFallen.Contains("borg")) SmolderSurvived++;
            }
            if (hiyoU is not null && borgU is not null) HiyoToBorg += r.FavorWhetTo.GetValueOrDefault(borgU.Def.Name);
        }
    }

    internal static XAgg Measure(Formation f, int w, EnemyScaleRule sc, int seed0 = 0, int seeds = BA.Seeds)
    {
        var parts = new XAgg[seeds];
        Parallel.For(0, seeds, i =>
        {
            var a = new XAgg();
            var (r, p, e, slot0) = BA.Fight(f, w, sc, seed0 + i);
            a.Take(r, p, e, slot0);
            parts[i] = a;
        });
        var all = new XAgg();
        foreach (var a in parts) all.Merge(a);
        return all;
    }
}
