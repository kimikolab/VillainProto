using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using BF = BorgFrontDiag;

// =====================================================================================
// fireward —— 第238期「燃えている味方を守る（ボルグの盾の配り・ヒヨの火の変換）」。
// 指示書は design/PHASE236_FIRE_WARD_SPEC.md（第236・237期はハネで使われたので第238期として回した）／ 報告は design/PHASE238_FIRE_WARD.md。
// **版は札の差し替えだけ**（G0 規定 ／ A ＝ 第235期「全部」／ D1・D2（ボルグ）× V1・V2（ヒヨ））。**線は置かない。**
// 集計は第235期の `BorgFrontDiag.YAgg`（＝第234期 XAgg ＝ 第233期 Agg）をそのまま包み、この期の量だけを足す。
//
//     dotnet run --project BattleSim -c Release 0 fireward digest [path]     # 受け入れ 1 の台本の指紋（G0 ／ A）
//     dotnet run --project BattleSim -c Release 0 fireward phase0            # Q0-1〜Q0-4
//     dotnet run --project BattleSim -c Release 0 fireward pick [dir]        # 段1（版ごとに T3 1,081 組 × 席 120 × seed 40・並びは 200/200 → 400/300 → 落 → 決着T）
//     dotnet run --project BattleSim -c Release 0 fireward run [dir]         # 段2 ＋ 表A〜H（段1 の TSV が無ければ回し直す）
//     dotnet run --project BattleSim -c Release 0 fireward check             # 自己検査（受け入れ 2〜5）
//     dotnet run --project BattleSim -c Release 0 fireward log <版> <前1,前3,中央,後1,後3> [seed] [波] [倍率 0/1/2]
// =====================================================================================
static partial class FireWardDiag
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
                LogOne(args.Length > 3 ? args[3] : "A+D1+V1", args.Length > 4 ? args[4] : "", args.Length > 5 ? int.Parse(args[5]) : 0,
                    args.Length > 6 ? int.Parse(args[6]) : BA.MainWave, args.Length > 7 ? int.Parse(args[7]) : 0);
                return;
            default:
                Console.WriteLine("fireward: モードは digest / phase0 / pick / run / check / log。");
                return;
        }
    }
    static string[]? _args;
    static partial void Phase0();
    static partial void PickImpl();
    static partial void RunImpl();
    static partial void CheckImpl();

    // ---------------------------------------------------------------------------------
    // 版（§4）——札の差し替えだけ。ボルグは第235期「全部」＋ D、ヒヨは規定 ＋ V。
    // ---------------------------------------------------------------------------------
    static UnitDef With(UnitDef g, IEnumerable<TraitId> tr) => new()
    {
        Id = g.Id, Name = g.Name, MaxHp = g.MaxHp, Attack = g.Attack, Speed = g.Speed, Traits = tr.ToArray(), Pattern = g.Pattern,
        Advances = g.Advances, Actions = g.Actions, PlusText = g.PlusText, MinusText = g.MinusText, Flavor = g.Flavor,
    };
    internal static readonly UnitDef BorgA = BF.VerBorg("全部");
    internal static UnitDef BorgD(TraitId d) => With(BorgA, BorgA.Traits.Append(d));
    internal static UnitDef HiyoV(TraitId v, bool dry = false)
        => With(UnitCatalog.HiyoF0, dry ? UnitCatalog.HiyoF0.Traits.Append(v).Append(TraitId.FireConvertDry) : UnitCatalog.HiyoF0.Traits.Append(v));

    internal sealed record Ver(string Name, string Key, string What, UnitDef Borg, UnitDef Hiyo);

    internal static readonly UnitDef D1 = BorgD(TraitId.FireWard), D2 = BorgD(TraitId.FireWardAll);
    internal static readonly UnitDef V1 = HiyoV(TraitId.FireConvert), V2 = HiyoV(TraitId.FireConvertHalf);
    /// <summary>版。<c>Key</c> はファイル名に使う（ASCII）。A は第235期「全部」の駒そのもの（受け入れ 1）。</summary>
    internal static readonly Ver[] Versions =
    {
        new("G0", "G0", "規定（対照）", UnitCatalog.BorgF0, UnitCatalog.HiyoF0),
        new("A", "A", "第235期「全部」", BorgA, UnitCatalog.HiyoF0),
        new("A+D1", "AD1", "＋盾の配り（隣）", D1, UnitCatalog.HiyoF0),
        new("A+D2", "AD2", "＋盾の配り（全員）", D2, UnitCatalog.HiyoF0),
        new("A+V1", "AV1", "＋火の変換（全量）", BorgA, V1),
        new("A+V2", "AV2", "＋火の変換（半分）", BorgA, V2),
        new("A+D1+V1", "AD1V1", "隣 ＋ 全量", D1, V1),
        new("A+D1+V2", "AD1V2", "隣 ＋ 半分", D1, V2),
        new("A+D2+V1", "AD2V1", "全員 ＋ 全量", D2, V1),
        new("A+D2+V2", "AD2V2", "全員 ＋ 半分", D2, V2),
    };
    internal static Ver VerOf(string name) => Versions.First(v => v.Name == name || v.Key == name);
    /// <summary>V の b（火の変換の回復が渇きに封じられる）——V を持つヒヨに `FireConvertDry` を足す。</summary>
    internal static UnitDef DryOf(UnitDef hiyo) => hiyo.Traits.Contains(TraitId.FireConvert) ? HiyoV(TraitId.FireConvert, true)
        : hiyo.Traits.Contains(TraitId.FireConvertHalf) ? HiyoV(TraitId.FireConvertHalf, true) : hiyo;

    internal static UnitDef[] CoreOf(Ver v) => new[] { v.Borg, UnitCatalog.Hota, v.Hiyo };
    internal static Formation Dec(string enc, Ver v) => Dec(enc, v.Borg, v.Hiyo);
    internal static Formation Dec(string enc, UnitDef borg, UnitDef hiyo)
        => BA.Seat(enc.Split(',').Select(id => id == "borg" ? borg : id == "hiyo" ? hiyo : UnitCatalog.ById(id)).ToArray());
    internal static string Enc(Formation f) => string.Join(",", Enumerable.Range(0, 5).Select(i => f[i]!.Id));

    static void LogOne(string ver, string seats, int seed, int wave, int sc)
    {
        var v = VerOf(ver);
        var f = Dec(seats, v);
        var (r, _, _, _) = BA.Fight(f, wave, BA.Scales[sc].Sc, seed);
        Console.WriteLine($"# {ver} × {BA.SeatsNamed(f)} × {BA.WaveNames[wave]} × {BA.Scales[sc].Name} × seed {seed}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }

    // ---------------------------------------------------------------------------------
    // 集計: 第235期の YAgg ＋ この期の量
    // ---------------------------------------------------------------------------------
    internal sealed class ZAgg
    {
        public readonly BF.YAgg Y = new();
        public long WardHits, WardSaved, CensusTurns, BurnAllies, BurnAdj;
        public readonly Dictionary<string, long> WardTakenBy = new();      // 受け手（Id）→ 切ってもらった量
        public long ConvTickNom, ConvTickHealed, ConvSplashNom, ConvSplashHealed, ConvDry, PrecPyre, PrecMend, PrecBeni, PrecV;
        public readonly Dictionary<string, long> FellBy = new();           // 役（ボルグ／ホタ／ヒヨ／相方）→ 倒れた数
        public readonly Dictionary<string, long[]> CauseByRole = new();    // 役 → 死因

        public BA.Agg A => Y.X.A;

        public void Merge(ZAgg o)
        {
            Y.Merge(o.Y);
            WardHits += o.WardHits; WardSaved += o.WardSaved; CensusTurns += o.CensusTurns; BurnAllies += o.BurnAllies; BurnAdj += o.BurnAdj;
            foreach (var (k, x) in o.WardTakenBy) WardTakenBy[k] = WardTakenBy.GetValueOrDefault(k) + x;
            ConvTickNom += o.ConvTickNom; ConvTickHealed += o.ConvTickHealed; ConvSplashNom += o.ConvSplashNom; ConvSplashHealed += o.ConvSplashHealed; ConvDry += o.ConvDry;
            PrecPyre += o.PrecPyre; PrecMend += o.PrecMend; PrecBeni += o.PrecBeni; PrecV += o.PrecV;
            foreach (var (k, x) in o.FellBy) FellBy[k] = FellBy.GetValueOrDefault(k) + x;
            foreach (var (k, x) in o.CauseByRole)
            {
                if (!CauseByRole.TryGetValue(k, out var a)) CauseByRole[k] = a = new long[BA.Causes.Length];
                for (int i = 0; i < a.Length; i++) a[i] += x[i];
            }
        }

        internal static string RoleOf(string id) => id switch { "borg" => "ボルグ", "hota" => "ホタ", "hiyo" => "ヒヨ", _ => "相方" };
        internal static readonly string[] Roles = { "ボルグ", "ホタ", "ヒヨ", "相方" };

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e, Dictionary<int, int> slot0)
        {
            var before = Y.X.A.CauseBy.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
            var fellBefore = Y.X.A.Fell.ToDictionary(kv => kv.Key, kv => kv.Value);
            Y.Take(r, p, e, slot0);
            foreach (var (id, n) in Y.X.A.Fell)
            {
                long d = n - fellBefore.GetValueOrDefault(id);
                if (d != 0) FellBy[RoleOf(id)] = FellBy.GetValueOrDefault(RoleOf(id)) + d;
            }
            foreach (var (nm, arr) in Y.X.A.CauseBy)
            {
                var b0 = before.GetValueOrDefault(nm);
                string? id = p.FirstOrDefault(u => u.Def.Name == nm)?.Def.Id;
                if (id is null) continue;
                string role = RoleOf(id);
                if (!CauseByRole.TryGetValue(role, out var cr)) CauseByRole[role] = cr = new long[BA.Causes.Length];
                for (int i = 0; i < arr.Length; i++) cr[i] += arr[i] - (b0 is null ? 0 : b0[i]);
            }
            foreach (var u in p)
            {
                if (!r.TallyByUnit.TryGetValue(u.Def.Id, out var t)) continue;
                if (u.Def.Id == "borg")
                {
                    WardHits += t.FireWardHits; WardSaved += t.FireWardSaved;
                    CensusTurns += t.WardCensusTurns; BurnAllies += t.WardBurnAllies; BurnAdj += t.WardBurnAdj;
                }
                if (u.Def.Id == "hiyo")
                {
                    ConvTickNom += t.FireConvTickNominal; ConvTickHealed += t.FireConvTickHealed;
                    ConvSplashNom += t.FireConvSplashNominal; ConvSplashHealed += t.FireConvSplashHealed; ConvDry += t.FireConvDry;
                    PrecPyre += t.FireConvPrecPyre; PrecMend += t.FireConvPrecMend; PrecBeni += t.FireConvPrecBeni; PrecV += t.FireConvPrecV;
                }
                if (t.FireWardTaken > 0) WardTakenBy[u.Def.Id] = WardTakenBy.GetValueOrDefault(u.Def.Id) + t.FireWardTaken;
            }
        }
    }

    internal static ZAgg Measure(Formation f, int w, EnemyScaleRule sc, int seed0 = 0, int seeds = BA.Seeds)
    {
        var parts = new ZAgg[seeds];
        Parallel.For(0, seeds, i =>
        {
            var a = new ZAgg();
            var (r, p, e, slot0) = BA.Fight(f, w, sc, seed0 + i);
            a.Take(r, p, e, slot0);
            parts[i] = a;
        });
        var all = new ZAgg();
        foreach (var a in parts) all.Merge(a);
        return all;
    }
}
