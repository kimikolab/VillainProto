using System.Reflection;
using System.Text;
using BattleCore;

// =====================================================================================
// shockdigest（第214期）—— 台本の指紋を並べるだけの器具（受け入れ 1・4）。
//
//     dotnet run --project BattleSim -c Release 0 shockdigest k0    # 旧カタ（起爆）の台
//     dotnet run --project BattleSim -c Release 0 shockdigest aka   # スス／アカの台
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
        var benches = mode == "aka"
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
