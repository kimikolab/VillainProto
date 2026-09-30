using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;

// enemyfire の版（指示書 §4）——札の差し替えだけ（ボルグに持たせる・保持者 0 枚）。E0 は前段の規定の駒そのもの。
static partial class EnemyFireDiag
{
    static UnitDef BorgWith(params TraitId[] tr) => With(UnitCatalog.BorgE0, UnitCatalog.BorgE0.Traits.Concat(tr));   // 第246期: E2 が規定になったので第245期の規定（`BorgE0`）から組む
    internal static readonly UnitDef BorgTick = BorgWith(TraitId.FoeFireLevel, TraitId.FoeFireTick);
    internal static readonly UnitDef BorgBrittle = BorgWith(TraitId.FoeFireLevel, TraitId.FoeFireBrittle);
    internal static readonly UnitDef BorgE1 = BorgWith(TraitId.FoeFireLevel, TraitId.FoeFireTick, TraitId.FoeFireBrittle, TraitId.FoeFireSpread);
    internal static readonly UnitDef BorgE1U = BorgWith(TraitId.FoeFireLevel, TraitId.FoeFireTick, TraitId.FoeFireBrittle, TraitId.FoeFireSpread, TraitId.UnleashStoke);
    internal static readonly UnitDef BorgE2 = BorgWith(TraitId.FoeFireLevel, TraitId.FoeFireTick, TraitId.FoeFireBrittle, TraitId.FoeFireSpread, TraitId.AllyFireTick);

    internal static readonly FB.Ver[] Versions =
    {
        new("E0", "前段の規定（対照）", UnitCatalog.BorgE0, UnitCatalog.Hota, UnitCatalog.Hiyo),
        new("E-刻み", "敵の火勢（育つ・萎む）＋ 刻みの回数", BorgTick, UnitCatalog.Hota, UnitCatalog.Hiyo),
        new("E-脆さ", "敵の火勢 ＋ 脆さの上昇", BorgBrittle, UnitCatalog.Hota, UnitCatalog.Hiyo),
        new("E1", "敵の火勢 ＋ 刻みの回数 ＋ 脆さの上昇 ＋ 延焼（味方の刻みはそのまま）", BorgE1, UnitCatalog.Hota, UnitCatalog.Hiyo),
        new("E2", "E1 ＋ 味方の刻みも回数", BorgE2, UnitCatalog.Hota, UnitCatalog.Hiyo),
        new("E1+放", "E1 ＋ 放つで当てた敵はさらに +1（燃えていなければ火勢2・追記 A）", BorgE1U, UnitCatalog.Hota, UnitCatalog.Hiyo),
    };
    internal static FB.Ver VerOf(string name) => Versions.First(v => v.Name == name);
    internal static Formation Apply(Formation f, FB.Ver v) => Apply(f, v.Borg, v.Hota, v.Hiyo);

    static partial void LogOne(string ver, string seats, int seed, int wave, int sc)
    {
        var f = Apply(FB.Dec(seats), VerOf(ver));
        var (r, _, _) = FB.Fight(f, wave, BA.Scales[sc].Sc, seed);
        Console.WriteLine($"# {ver} × {BA.SeatsNamed(f)} × {BA.WaveNames[wave]} × {BA.Scales[sc].Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }
}
