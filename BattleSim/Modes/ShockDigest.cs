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
//
// **同じファイルを第213期の worktree に置いても回る**ように書いてある（旧カタは `KataOld`、無ければ `Kata` を引く）。
// 指紋は `BattleEvent` の公開プロパティを宣言順に並べた文字列の FNV-1a（**見せ場 `Highlight` の `Text` だけは除く**
// ——ログの1行がそのまま載るので名前が入る）。`Openings`（名前を持つ）とログは見ない。
// =====================================================================================

static class ShockDigestDiag
{
    public static void Run(string mode)
    {
        var fKataOld = typeof(UnitCatalog).GetField("KataOld");
        UnitDef kata = (UnitDef)(fKataOld ?? typeof(UnitCatalog).GetField("Kata")!).GetValue(null)!;
        var benches = mode == "w217"
            ? new (string, Formation)[]
            {
                // 第217期（受け入れ 1）: G0（今のシガ）の台本が実装の前後で一致すること。席は Phase 0 で G0 に選んだ参考の席。
                ("W1 X", Formation.Build(front1: UnitCatalog.Mio, front3: UnitCatalog.Shiga, center: UnitCatalog.Beni, back1: UnitCatalog.Kata, back3: UnitCatalog.Tou)),
                ("W1 P2", Formation.BuildDiamond(a: UnitCatalog.Beni, b: UnitCatalog.Kata, c: UnitCatalog.Shiga, d: UnitCatalog.Tou, e: UnitCatalog.Mio)),
                ("W2 X", Formation.Build(front1: UnitCatalog.Beni, front3: UnitCatalog.Shiga, center: UnitCatalog.Mio, back1: UnitCatalog.Kata, back3: UnitCatalog.Kugu)),
                ("W2 P2", Formation.BuildDiamond(a: UnitCatalog.Beni, b: UnitCatalog.Mio, c: UnitCatalog.Kata, d: UnitCatalog.Shiga, e: UnitCatalog.Kugu)),
                ("W3 X", Formation.Build(front1: UnitCatalog.Beni, front3: UnitCatalog.Mio, center: UnitCatalog.Kata, back1: UnitCatalog.Shiga, back3: UnitCatalog.Guza)),
                ("W3 P2", Formation.BuildDiamond(a: UnitCatalog.Beni, b: UnitCatalog.Mio, c: UnitCatalog.Kata, d: UnitCatalog.Shiga, e: UnitCatalog.Guza)),
                ("W4 責め苦", Common.CompareBuilds().First(r => r.Name == "責め苦 (トウ×シガ)").F),
                ("W4 裂き×責め苦", Common.CompareBuilds().First(r => r.Name == "裂き×責め苦 (キリ×エグ×シガ)").F),
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
                    foreach (BattleEvent e in r.Events)
                    {
                        var sb = new StringBuilder();
                        foreach (PropertyInfo p in props)
                        {
                            if (p.Name == "Text" && e.Kind == BattleEventKind.Highlight) continue;
                            object? v = p.GetValue(e);
                            sb.Append(p.Name).Append('=').Append(v is System.Collections.IEnumerable en && v is not string ? string.Join(",", en.Cast<object>()) : v).Append('|');
                        }
                        foreach (byte b in Encoding.UTF8.GetBytes(sb.ToString())) { h ^= b; h *= 1099511628211UL; }
                    }
                    total += r.Events.Count;
                    Console.WriteLine(name + "\t" + (st + 1) + "\t" + s + "\t" + r.PlayerWon + "\t" + r.Turns + "\t" + r.Events.Count + "\t" + h.ToString("x16"));
                }
        Console.WriteLine("events\t" + total);
    }
}
