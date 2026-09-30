using BattleCore;
using static Common;
using BA = BurnAuditDiag;

// fireburst の版（§4）——札の差し替えだけ。S0 は規定の駒そのもの（＝第242期 R3）。
// 第245期 前段で大技が規定になったので、版はすべて第244期の規定（`UnitCatalog.BorgR3` / `HotaR3` / `HiyoR3`）から組む。
static partial class FireBurstDiag
{
    static UnitDef With(UnitDef g, IEnumerable<TraitId> tr) => new()
    {
        Id = g.Id, Name = g.Name, MaxHp = g.MaxHp, Attack = g.Attack, Speed = g.Speed, Traits = tr.ToArray(), Pattern = g.Pattern,
        Advances = g.Advances, Actions = g.Actions, PlusText = g.PlusText, MinusText = g.MinusText, Flavor = g.Flavor,
    };
    // S1: ② 燃え広がりの上限（ボルグに持たせる・陣営の規則）＋ ③ 攻撃力を倍率の前で比べる（ヒヨ）
    internal static readonly UnitDef BorgS1 = With(UnitCatalog.BorgR3, UnitCatalog.BorgR3.Traits.Append(TraitId.FireSpreadCap));
    internal static readonly UnitDef HiyoS1 = With(UnitCatalog.HiyoR3, UnitCatalog.HiyoR3.Traits.Append(TraitId.StokeBaseAtk));
    // S2: ＋ ① 大技（ボルグ 放つ ／ ホタ 焼き尽くす・残り火・呼び火）
    internal static readonly UnitDef BorgS2 = With(UnitCatalog.BorgR3, UnitCatalog.BorgR3.Traits.Concat(new[] { TraitId.FireSpreadCap, TraitId.FireUnleash }));
    internal static readonly UnitDef HotaS2R = With(UnitCatalog.HotaR3, UnitCatalog.HotaR3.Traits.Concat(new[] { TraitId.PyreBurnout, TraitId.PyreEmbers, TraitId.CallFire }));
    internal static readonly UnitDef HotaS2D = With(UnitCatalog.HotaR3, UnitCatalog.HotaR3.Traits.Concat(new[] { TraitId.PyreBurnout, TraitId.PyreEmbers, TraitId.CallFire, TraitId.FireRainOrdered }));
    internal static readonly UnitDef HiyoS2g4 = With(UnitCatalog.HiyoR3,
        UnitCatalog.HiyoR3.Traits.Select(t => t == TraitId.TurnGift ? TraitId.TurnGiftWait : t).Append(TraitId.StokeBaseAtk));

    internal sealed record Ver(string Name, string What, UnitDef Borg, UnitDef Hota, UnitDef Hiyo);
    internal static readonly Ver[] Versions =
    {
        new("S0", "規定（第242期 R3・対照）", UnitCatalog.BorgR3, UnitCatalog.HotaR3, UnitCatalog.HiyoR3),
        new("S1", "S0 ＋ 燃え広がりの上限 ＋ 攻撃力の比べ方", BorgS1, UnitCatalog.HotaR3, HiyoS1),
        new("S2R", "S1 ＋ 大技・火の雨は乱数", BorgS2, HotaS2R, HiyoS1),
        new("S2D", "S1 ＋ 大技・火の雨は決まった順", BorgS2, HotaS2D, HiyoS1),
        new("S2R-g4", "S2R のギフトを G4 待ち", BorgS2, HotaS2R, HiyoS2g4),
    };
    internal static Ver VerOf(string name) => Versions.First(v => v.Name == name);

    /// <summary>ボルグ・ホタ・ヒヨを版の駒に差し替える（ほかの駒・席はそのまま）。</summary>
    internal static Formation Apply(Formation f, Ver v)
    {
        UnitDef? M(UnitDef? d) => d is null ? null : d.Id switch { "borg" => v.Borg, "hota" => v.Hota, "hiyo" => v.Hiyo, _ => d };
        return Formation.Build(front1: M(f[0]), front3: M(f[1]), center: M(f[2]), back1: M(f[3]), back3: M(f[4]));
    }
    internal static Formation Dec(string enc) => BA.Seat(enc.Split(',').Select(UnitCatalog.ById).ToArray());
    internal static string Enc(Formation f) => string.Join(",", Enumerable.Range(0, 5).Select(i => f[i]!.Id));

    static partial void LogOne(string ver, string seats, int seed, int wave, int sc)
    {
        var f = Apply(Dec(seats), VerOf(ver));
        var (r, _, _) = Fight(f, wave, BA.Scales[sc].Sc, seed);
        Console.WriteLine($"# {ver} × {BA.SeatsNamed(f)} × {BA.WaveNames[wave]} × {BA.Scales[sc].Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }
}
