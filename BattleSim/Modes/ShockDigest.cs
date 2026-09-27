using System.Reflection;
using System.Text;
using BattleCore;

// =====================================================================================
// shockdigest（第214期）—— 台本の指紋を並べるだけの器具（受け入れ 1・4）。
//
//     dotnet run --project BattleSim -c Release 0 shockdigest k0    # 旧カタ（起爆）の台
//     dotnet run --project BattleSim -c Release 0 shockdigest aka   # スス／アカの台
//     dotnet run --project BattleSim -c Release 0 shockdigest t216  # 第216期の台（O0・S0 の台本が第215期と一致すること）
//     dotnet run --project BattleSim -c Release 0 shockdigest w217  # 第217期の台（G0＝今のシガの台本が実装の前後で一致すること）
//     dotnet run --project BattleSim -c Release 0 shockdigest m218  # 第218期の台（M0＝今のミオの台本が実装の前後で一致すること）
//                                                                    ——第219期からミオの規定は M5 なので、この台は `MireDiag.VerOf("M0")` を引く
//     dotnet run --project BattleSim -c Release 0 shockdigest d222  # 第222期の台（V0＝今のシオ・ヨミの台本が実装の前後で一致すること）
//     dotnet run --project BattleSim -c Release 0 shockdigest m219 cat|m5  # 第219期の台（cat ＝ 規定のミオ ／ m5 ＝ 第218期の診断の版 M5）
//                                                                    ——**第218期の worktree で `m5`、第219期で `cat` を回して全行一致**なら M5 の規定化が台本を変えていない
//
// **後の期に足した出来事の種類と `BattleEvent` の欄は指紋から外す**（`SkipKinds` / `SkipProps`・名前で持つので古い worktree でも回る）
// ——足した期の前後で同じ台本が同じ指紋になるように。
//
// **同じファイルを第213期の worktree に置いても回る**ように書いてある（旧カタは `KataOld`、無ければ `Kata` を引く）。
// 指紋は `BattleEvent` の公開プロパティを宣言順に並べた文字列の FNV-1a（**見せ場 `Highlight` の `Text` だけは除く**
// ——ログの1行がそのまま載るので名前が入る）。`Openings`（名前を持つ）とログは見ない。
// =====================================================================================

static class ShockDigestDiag
{
    /// <summary>後の期に足した出来事の種類（名前で持つ・古い worktree でも回る）。</summary>
    static Formation D222(Formation f) => DriftDiag.Apply(f, DriftDiag.ShioV0, DriftDiag.YomiV0);

    static readonly HashSet<string> SkipKinds = new() { "MireCarried", "MireHandedOff", "MireBurst", "Regroup" };
    /// <summary>後の期に足した <c>BattleEvent</c> の欄。</summary>
    static readonly HashSet<string> SkipProps = new() { "BrittleExtra", "PartnerId" };

    static (string, Formation)[] MireBenches(UnitDef mio) => new (string, Formation)[]
    {
        ("M台1 X", Formation.Build(front1: mio, front3: UnitCatalog.Kubi, center: UnitCatalog.Beni, back1: UnitCatalog.Kata, back3: UnitCatalog.Tou)),
        ("M台2 X", Formation.Build(front1: UnitCatalog.Beni, front3: UnitCatalog.Kubi, center: mio, back1: UnitCatalog.Kata, back3: UnitCatalog.Sid)),
        ("M台2 P2", Formation.BuildDiamond(a: UnitCatalog.Beni, b: mio, c: UnitCatalog.Kata, d: UnitCatalog.Sid, e: UnitCatalog.Kubi)),
        ("M台3 ポンX", MireDiag.PonX(mio)),
        ("M台4 ポンP2", MireDiag.PonP2(mio)),
        ("M台5 毒", MireDiag.WithMio(Common.CompareBuilds().First(r => r.Name == "毒 (グザ×ミオ×ラウ)").F, mio)),
        ("M台5 毒+耐久", MireDiag.WithMio(Common.CompareBuilds().First(r => r.Name == "毒+耐久 (ベニ×トウ)").F, mio)),
        ("M台5 刻み×澱み", MireDiag.WithMio(Common.CompareBuilds().First(r => r.Name == "刻み×澱み (ノミ×ミオ)").F, mio)),
        ("M台5 追撃×毒", MireDiag.WithMio(Common.CompareBuilds().First(r => r.Name.StartsWith("追撃×毒")).F, mio)),
        ("M台5 澱み喰い", MireDiag.WithMio(Common.CompareBuilds().First(r => r.Name.StartsWith("澱み喰い")).F, mio)),
    };

    public static void Run(string mode, string arg = "")
    {
        var fKataOld = typeof(UnitCatalog).GetField("KataOld");
        UnitDef kata = (UnitDef)(fKataOld ?? typeof(UnitCatalog).GetField("Kata")!).GetValue(null)!;
        var benches = mode == "d222"
            // 第222期（受け入れ 1）: V0（今のシオ・ヨミ）の台本が実装の前後で一致すること。D1＝ポンの台・D2/D3＝compare の行。
            ? new (string, Formation)[]
            {
                // 第223期 前段で V3 が規定になったので、シオ・ヨミは `DriftDiag` の V0（版の札を抜いた駒）へ差し替える。
                ("D1 ポン", D222(Formation.Build(front1: UnitCatalog.Yomi, front3: UnitCatalog.Gald, center: UnitCatalog.Shio, back1: UnitCatalog.Sero, back3: UnitCatalog.Basa))),
                ("D2 移動改", D222(Common.CompareBuilds().First(r => r.Name.StartsWith("移動改 (")).F)),
                ("D3 隊列崩し", D222(Common.CompareBuilds().First(r => r.Name.StartsWith("隊列崩し")).F)),
                ("D3 突き出し", D222(Common.CompareBuilds().First(r => r.Name.StartsWith("突き出し")).F)),
            }
            : mode == "m219"
            // 第219期（受け入れ 1）: 規定のミオ（cat）と第218期の M5（m5）の台本が一致すること。
            ? MireBenches(arg == "m5" ? MireDiag.VerOf("M5") : UnitCatalog.Mio)
            : mode == "m218"
            // 第218期（受け入れ 1）: M0 の台本が実装の前後で一致すること。席は Phase 0 で M0 に選んだ仮の席。
            // 第219期からは規定が M5 なので M0 を診断の版から引く（第218期の worktree でも `VerOf("M0")` は同じ札）。
            ? MireBenches(MireDiag.VerOf("M0")).Take(8).ToArray()
            : mode == "w217"
            ? new (string, Formation)[]
            {
                // 第217期（受け入れ 1）: G0（今のシガ）の台本が実装の前後で一致すること。席は Phase 0 で G0 に選んだ参考の席。
                ("W1 X", Formation.Build(front1: UnitCatalog.Mio, front3: WhipDiag.G0Def, center: UnitCatalog.Beni, back1: UnitCatalog.Kata, back3: UnitCatalog.Tou)),
                ("W1 P2", Formation.BuildDiamond(a: UnitCatalog.Beni, b: UnitCatalog.Kata, c: WhipDiag.G0Def, d: UnitCatalog.Tou, e: UnitCatalog.Mio)),
                ("W2 X", Formation.Build(front1: UnitCatalog.Beni, front3: WhipDiag.G0Def, center: UnitCatalog.Mio, back1: UnitCatalog.Kata, back3: UnitCatalog.Kugu)),
                ("W2 P2", Formation.BuildDiamond(a: UnitCatalog.Beni, b: UnitCatalog.Mio, c: UnitCatalog.Kata, d: WhipDiag.G0Def, e: UnitCatalog.Kugu)),
                ("W3 X", Formation.Build(front1: UnitCatalog.Beni, front3: UnitCatalog.Mio, center: UnitCatalog.Kata, back1: WhipDiag.G0Def, back3: UnitCatalog.Guza)),
                ("W3 P2", Formation.BuildDiamond(a: UnitCatalog.Beni, b: UnitCatalog.Mio, c: UnitCatalog.Kata, d: WhipDiag.G0Def, e: UnitCatalog.Guza)),
                ("W4 責め苦", WhipDiag.AsG0(Common.CompareBuilds().First(r => r.Name == "責め苦 (トウ×シガ)").F)),
                ("W4 裂き×責め苦", WhipDiag.AsG0(Common.CompareBuilds().First(r => r.Name == "裂き×責め苦 (キリ×エグ×シガ)").F)),
            }
            : mode == "t216"
            ? new (string, Formation)[]
            {
                // 第216期（受け入れ 1）: O0・S0 の台本が第215期と一致すること。台C は Phase 0 で O0 に選んだ参考の席。
                ("台A", ShockDiag.TableA216(UnitCatalog.Beni, UnitCatalog.Kata)),
                ("台B", ShockDiag.TableB216(UnitCatalog.Beni, UnitCatalog.Kata)),
                ("台C X", Formation.Build(front1: UnitCatalog.Mio, front3: UnitCatalog.Kubi, center: UnitCatalog.Beni, back1: UnitCatalog.Tou, back3: UnitCatalog.Kata)),
                ("台C P2", Formation.BuildDiamond(a: UnitCatalog.Mio, b: UnitCatalog.Kata, c: UnitCatalog.Beni, d: UnitCatalog.Tou, e: UnitCatalog.Kubi)),
                ("台1 X", ShockDiag.Tables()[0].Seats[FormationShape.X]),
                ("台3 X", ShockDiag.Tables()[2].Seats[FormationShape.X]),
            }
            : mode == "aka"
            ? new (string, Formation)[]
            {
                ("惨禍×死の連鎖（ゴルム→スス）", Formation.Build(front1: UnitCatalog.Susu, front3: UnitCatalog.Zoto, center: UnitCatalog.Kado, back1: UnitCatalog.Rica, back3: UnitCatalog.Vel)),
                ("燃焼（ガルド→スス）", Formation.Build(front1: UnitCatalog.Susu, front3: UnitCatalog.Borg, center: UnitCatalog.Lili, back1: UnitCatalog.Mudo, back3: UnitCatalog.Hota)),
                ("速攻（セロ→スス）", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Mudo, center: UnitCatalog.Nel, back1: UnitCatalog.Susu, back3: UnitCatalog.Borg)),
            }
            : new (string, Formation)[]
            {
                ("毒の台", Formation.Build(front1: UnitCatalog.Beni, front3: UnitCatalog.Vio, center: UnitCatalog.Guza, back1: UnitCatalog.Mio, back3: kata)),
                ("毒×燃焼の台", Formation.Build(front1: UnitCatalog.Borg, front3: UnitCatalog.Hota, center: UnitCatalog.Guza, back1: UnitCatalog.Mio, back3: kata)),
                ("リィカの台", Formation.Build(front1: UnitCatalog.Mug, front3: UnitCatalog.Zoto, center: UnitCatalog.Rica, back1: UnitCatalog.Guza, back3: kata)),
                ("毒+ベニ+ラウ（ラウ→旧カタ）", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Sid, center: UnitCatalog.Guza, back1: kata, back3: UnitCatalog.Beni)),
            };
        PropertyInfo[] props = typeof(BattleEvent).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        long total = 0;
        foreach (var (name, f) in benches)
            for (int st = 0; st < 5; st++)
                for (int s = 0; s < 20; s++)
                {
                    BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, s, verbose: true);
                    ulong h = 1469598103934665603UL;
                    int counted = 0;
                    foreach (BattleEvent e in r.Events)
                    {
                        if (SkipKinds.Contains(e.Kind.ToString())) continue;
                        counted++;
                        var sb = new StringBuilder();
                        foreach (PropertyInfo p in props)
                        {
                            if (SkipProps.Contains(p.Name)) continue;
                            if (p.Name == "Text" && e.Kind == BattleEventKind.Highlight) continue;
                            object? v = p.GetValue(e);
                            sb.Append(p.Name).Append('=').Append(v is System.Collections.IEnumerable en && v is not string ? string.Join(",", en.Cast<object>()) : v).Append('|');
                        }
                        foreach (byte b in Encoding.UTF8.GetBytes(sb.ToString())) { h ^= b; h *= 1099511628211UL; }
                    }
                    total += counted;
                    Console.WriteLine(name + "\t" + (st + 1) + "\t" + s + "\t" + r.PlayerWon + "\t" + r.Turns + "\t" + counted + "\t" + h.ToString("x16"));
                }
        Console.WriteLine("events\t" + total);
    }
}
