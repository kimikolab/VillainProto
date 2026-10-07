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
//     dotnet run --project BattleSim -c Release 0 shockdigest f224  # 第224期の台（F0＝第223期 E2・H0＝規定のシオの台本が実装の前後で一致すること）
//     dotnet run --project BattleSim -c Release 0 shockdigest r225  # 第225期の台（J0＝規定のシオ・規定のセロの台本が実装の前後で一致すること）
//     dotnet run --project BattleSim -c Release 0 shockdigest k226  # 第226期の台（K0＝前段の規定の台本が実装の前後で一致すること）
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

    static readonly HashSet<string> SkipKinds = new() { "MireCarried", "MireHandedOff", "MireBurst", "Regroup", "Evade", "EvadeRiposte", "EvadeStage", "Barrage", "StatusArrow", "Retreat", "ShioStage", "Decoy", "Disarray", "DisarrayStage", "Squall", "LastDodge", "Blast", "Spring", "Tailwind", "StaggerBreach", "KillImpact", "Overflow" };
    /// <summary>第231期以後に足した出来事の種類（<b>どのモードでも外す</b>——挑発の表示は規定のセロにも出るので、前の期の台本と揃えるには必ず外す）。</summary>
    static readonly HashSet<string> SkipAlways = new() { "DecoyShow", "SpringGuard", "MoveShot", "Landing" };
    /// <summary>後の期に足した <c>BattleEvent</c> の欄。</summary>
    static readonly HashSet<string> SkipProps = new() { "BrittleExtra", "PartnerId" };

    /// <summary>
    /// 第287期: `compare` の行の規定のトウ（第287期から T3）を旧の規定 `TouT0` に戻した行。指紋の台は規定化の前（第286期まで）の台本を写すので、
    /// トウの在席する行（責め苦・毒+耐久）を旧に固定する（第282〜283期のミサの `CompareT0` と同じ作法）。トウのいない行は1ビットも変わらない。
    /// </summary>
    static (string Name, Formation F)[] CompareTou0() => Common.CompareBuilds().Select(r => (r.Name, Common.FvSwap(Common.FvSwap(Common.FvSwap(r.F, UnitCatalog.Tou, UnitCatalog.TouT0), UnitCatalog.Shiga, UnitCatalog.ShigaG3K), UnitCatalog.Kata, UnitCatalog.KataS3))).ToArray();

    static (string, Formation)[] MireBenches(UnitDef mio) => new (string, Formation)[]
    {
        ("M台1 X", Formation.Build(front1: mio, front3: UnitCatalog.Kubi, center: UnitCatalog.Beni, back1: UnitCatalog.KataS3, back3: UnitCatalog.TouT0)),
        ("M台2 X", Formation.Build(front1: UnitCatalog.Beni, front3: UnitCatalog.Kubi, center: mio, back1: UnitCatalog.KataS3, back3: UnitCatalog.Sid)),
        ("M台2 P2", Formation.BuildDiamond(a: UnitCatalog.Beni, b: mio, c: UnitCatalog.KataS3, d: UnitCatalog.Sid, e: UnitCatalog.Kubi)),
        ("M台3 ポンX", MireDiag.PonX(mio)),
        ("M台4 ポンP2", MireDiag.PonP2(mio)),
        ("M台5 毒", MireDiag.WithMio(CompareTou0().First(r => r.Name == "毒 (グザ×ミオ×ラウ)").F, mio)),
        ("M台5 毒+耐久", MireDiag.WithMio(CompareTou0().First(r => r.Name == "毒+耐久 (ベニ×トウ)").F, mio)),
        ("M台5 刻み×澱み", MireDiag.WithMio(CompareTou0().First(r => r.Name == "刻み×澱み (ノミ×ミオ)").F, mio)),
        ("M台5 追撃×毒", MireDiag.WithMio(CompareTou0().First(r => r.Name.StartsWith("追撃×毒")).F, mio)),
        ("M台5 澱み喰い", MireDiag.WithMio(CompareTou0().First(r => r.Name.StartsWith("澱み喰い")).F, mio)),
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
                ("D1 ポン", D222(Formation.Build(front1: UnitCatalog.Yomi, front3: UnitCatalog.Gald, center: UnitCatalog.ShioV3, back1: UnitCatalog.SeroOld, back3: UnitCatalog.BasaK0))),
                ("D2 移動改", D222(CompareTou0().First(r => r.Name.StartsWith("移動改 (")).F)),
                ("D3 隊列崩し", D222(CompareTou0().First(r => r.Name.StartsWith("隊列崩し")).F)),
                ("D3 突き出し", D222(CompareTou0().First(r => r.Name.StartsWith("突き出し")).F)),
            }
            : mode == "e223"
            // 第223期（受け入れ 2）: E0（今のセロ）の台本が実装の前後で一致すること。S1 ポン・S2（ネル）・S3 仮の並び・S4 compare のセロの12行。
            ? new (string, Formation)[]
            {
                ("S1 ポン", SeroDiag.BenchS1Pon),
                ("S2 ネル", SeroDiag.BenchS2Raw),
                ("S3 仮", SeroDiag.BenchS3Raw),
            }.Concat(SeroDiag.CompareRowsWithSero().Select(r => ("S4 " + r.Name, r.F))).ToArray()
            : mode == "f224"
            // 第224期（受け入れ 1）: F0（＝第223期 E2）・H0（規定のシオ）の台本が実装の前後で一致すること。
            // **この台だけは第223期の出来事（回避・乱れ撃ちほか）も指紋に入れる**（F0 はそれを出す版なので）。
            ? new (string, Formation)[]
            {
                ("S1 ポン", SeroShioDiag.Apply(SeroShioDiag.S1, SeroShioDiag.F0, SeroShioDiag.H0)),
                ("S2 ネル", SeroShioDiag.Apply(SeroShioDiag.S2, SeroShioDiag.F0, SeroShioDiag.H0)),
                ("K-リリ", SeroShioDiag.Apply(SeroShioDiag.K(UnitCatalog.Lili), SeroShioDiag.F0, SeroShioDiag.H0)),
                ("K-ツギ", SeroShioDiag.Apply(SeroShioDiag.K(UnitCatalog.Tsugi), SeroShioDiag.F0, SeroShioDiag.H0)),
            }.Concat(SeroShioDiag.S4Rows().Select(r => ("S4 " + r.Name, SeroShioDiag.Apply(r.F, SeroShioDiag.F0, SeroShioDiag.H0)))).ToArray()
            : mode == "r225"
            // 第225期（受け入れ 2）: J0（規定のシオ）の台本が実装の前後で一致すること。台は Phase 0 の仮の席 ＋ シオのいる compare の行。
            // **第223期以降の出来事と `PartnerId` も指紋に入れる**（前段で規定になったセロが出すので）。
            ? new (string, Formation)[]
            {
                ("M-ハネ（仮）", RetreatDiag.RawHane),
                ("M-カド（仮）", RetreatDiag.RawKado),
                ("参考 ガルド", RetreatDiag.RefGald),
            }.Concat(CompareTou0().Where(r => r.F.Occupied().Any(o => o.Def.Id == "shio")).Select(r => ("compare " + r.Name, RetreatDiag.WithShio(r.F, RetreatDiag.J0)))).ToArray()
            : mode == "k226"
            // 第226期（受け入れ 2）: K0（前段の規定）の台本が実装の前後で一致すること。M-ハネ（第225期の席）・参考の雷（仮の席）・セロ／バサ／ハネのいる compare の行。
            ? new (string, Formation)[]
            {
                ("M-ハネ（225）", DecoyDiag.MHane225),
                ("参考 雷（ポンの席）", DecoyDiag.Thunder),
            }.Concat(CompareTou0().Where(r => r.F.Occupied().Any(o => o.Def.Id is "sero" or "basa" or "hane")).Select(r => ("compare " + r.Name, r.F))).ToArray()
            : mode == "l227"
            // 第227期（受け入れ 2）: L0（前段の規定・バサ／ハネは敵の乱れ＋突風）の台本が必死の逃げ足の実装の前後で一致すること。
            ? new (string, Formation)[]
            {
                ("M-ハネ（225）", LastDodgeDiag.MHane225),
                ("参考 雷（ポンの席）", LastDodgeDiag.Thunder),
            }.Concat(CompareTou0().Where(r => r.F.Occupied().Any(o => o.Def.Id is "sero" or "basa" or "hane")).Select(r => ("compare " + r.Name, r.F))).ToArray()
            : mode == "s232"
            // 第232期（受け入れ 2）: S0（前段の規定＝退避5割・移動の追撃）の台本が、S2（入れ替わらない弾き返し）の実装の前後で一致すること。
            ? new (string, Formation)[]
            {
                ("M-ハネ（228 H3・規定）", Spring2Diag.MHane228),
                ("参考 雷（ポンの席）", Spring2Diag.Thunder),
            }.Concat(CompareTou0().Where(r => r.F.Occupied().Any(o => o.Def.Id is "sero" or "basa" or "hane" or "shio" or "yomi")).Select(r => ("compare " + r.Name, r.F))).ToArray()
            : mode == "a231"
            // 第231期（受け入れ 2）: V0（前段の規定＝第230期の追記の規定）の台本が、退避の線・隣の弾き返し・移動の追撃・挑発の表示の実装の前後で一致すること。
            ? new (string, Formation)[]
            {
                ("M-ハネ（228 H3・規定）", Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Sero, center: UnitCatalog.Yomi, back1: UnitCatalog.Shio, back3: UnitCatalog.Hane)),
                ("参考 雷（ポンの席）", CycleDiag.Thunder),
            }.Concat(CompareTou0().Where(r => r.F.Occupied().Any(o => o.Def.Id is "sero" or "basa" or "hane" or "shio" or "yomi")).Select(r => ("compare " + r.Name, r.F))).ToArray()
            : mode == "w230"
            // 第230期（受け入れ 2）: W0（前段の規定・嵐・追い風・転倒の穴）の台本が追い風の攻撃力順・撃破の衝撃・溢れの実装の前後で一致すること。
            ? new (string, Formation)[]
            {
                ("M-ハネ（228 H3）", CycleDiag.MHane228),
                ("M-ハネ（229 G4）", CycleDiag.MHane229),
                ("参考 雷（ポンの席）", CycleDiag.Thunder),
            }.Concat(CompareTou0().Where(r => r.F.Occupied().Any(o => o.Def.Id is "sero" or "basa" or "hane" or "shio" or "yomi")).Select(r => ("compare " + r.Name, r.F))).ToArray()
            : mode == "g229"
            // 第229期（受け入れ 2）: G0（前段の規定・ハネは H3）の台本が嵐・追い風・転倒の穴の実装の前後で一致すること。
            ? new (string, Formation)[]
            {
                ("M-ハネ（228 H3）", GaleDiag.MHane228),
                ("参考 雷（ポンの席）", GaleDiag.Thunder),
            }.Concat(CompareTou0().Where(r => r.F.Occupied().Any(o => o.Def.Id is "sero" or "basa" or "hane" or "shio" or "sasa")).Select(r => ("compare " + r.Name, r.F))).ToArray()
            : mode == "h228"
            // 第228期（受け入れ 2）: H0（前段の規定・セロは L2）の台本が吹っ飛ばし・弾き返しの実装の前後で一致すること。
            ? new (string, Formation)[]
            {
                ("M-ハネ（227 L2）", SpringDiag.MHane227),
                ("参考 雷（ポンの席）", SpringDiag.Thunder),
            }.Concat(CompareTou0().Where(r => r.F.Occupied().Any(o => o.Def.Id is "sero" or "basa" or "hane")).Select(r => ("compare " + r.Name, r.F))).ToArray()
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
                ("W1 X", Formation.Build(front1: UnitCatalog.Mio, front3: WhipDiag.G0Def, center: UnitCatalog.Beni, back1: UnitCatalog.KataS3, back3: UnitCatalog.TouT0)),
                ("W1 P2", Formation.BuildDiamond(a: UnitCatalog.Beni, b: UnitCatalog.KataS3, c: WhipDiag.G0Def, d: UnitCatalog.TouT0, e: UnitCatalog.Mio)),
                ("W2 X", Formation.Build(front1: UnitCatalog.Beni, front3: WhipDiag.G0Def, center: UnitCatalog.Mio, back1: UnitCatalog.KataS3, back3: UnitCatalog.Kugu)),
                ("W2 P2", Formation.BuildDiamond(a: UnitCatalog.Beni, b: UnitCatalog.Mio, c: UnitCatalog.KataS3, d: WhipDiag.G0Def, e: UnitCatalog.Kugu)),
                ("W3 X", Formation.Build(front1: UnitCatalog.Beni, front3: UnitCatalog.Mio, center: UnitCatalog.KataS3, back1: WhipDiag.G0Def, back3: UnitCatalog.Guza)),
                ("W3 P2", Formation.BuildDiamond(a: UnitCatalog.Beni, b: UnitCatalog.Mio, c: UnitCatalog.KataS3, d: WhipDiag.G0Def, e: UnitCatalog.Guza)),
                ("W4 責め苦", WhipDiag.AsG0(CompareTou0().First(r => r.Name == "責め苦 (トウ×シガ)").F)),
                ("W4 裂き×責め苦", WhipDiag.AsG0(CompareTou0().First(r => r.Name == "裂き×責め苦 (キリ×エグ×シガ)").F)),
            }
            : mode == "t216"
            ? new (string, Formation)[]
            {
                // 第216期（受け入れ 1）: O0・S0 の台本が第215期と一致すること。台C は Phase 0 で O0 に選んだ参考の席。
                ("台A", ShockDiag.TableA216(UnitCatalog.Beni, UnitCatalog.KataS3)),
                ("台B", ShockDiag.TableB216(UnitCatalog.Beni, UnitCatalog.KataS3)),
                ("台C X", Formation.Build(front1: UnitCatalog.Mio, front3: UnitCatalog.Kubi, center: UnitCatalog.Beni, back1: UnitCatalog.TouT0, back3: UnitCatalog.KataS3)),
                ("台C P2", Formation.BuildDiamond(a: UnitCatalog.Mio, b: UnitCatalog.KataS3, c: UnitCatalog.Beni, d: UnitCatalog.TouT0, e: UnitCatalog.Kubi)),
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
        // 第230期の追記: 第230期 W4 が規定になったので、全モードの台の規定のバサ・ハネ・ヨミ・シオを第230期の前段の姿へ戻す（以下の固定はその上に掛かる）。
        if (mode is not ("a231" or "s232")) benches = benches.Select(b => (b.Item1, Common.OldCycle(b.Item2))).ToArray();   // 第231期の台は今の規定のまま
        // 第227期 前段: バサ・ハネが規定で変わったので、第222〜226期の台は旧のバサ・ハネに戻す（台本が前段の前と一致すること）。
        if (mode is "d222" or "e223" or "f224" or "r225" or "k226")
            benches = benches.Select(b => (b.Item1, Common.OldBasaHane(b.Item2))).ToArray();
        // 第228期 前段: セロが規定で変わったので、第227期の台は旧のセロに戻す（バサ・ハネは第227期の規定のまま）。
        if (mode is "l227")
            benches = benches.Select(b => (b.Item1, Common.OldSero(b.Item2))).ToArray();
        // 第229期 前段: ハネが規定で変わったので、第227・228期の台は旧のハネに戻す。
        if (mode is "l227" or "h228")
            benches = benches.Select(b => (b.Item1, Common.OldHane(b.Item2))).ToArray();
        // 第230期 前段: バサ・ハネが嵐・追い風で規定に（転倒の穴も既定）なったので、第227〜229期の台は旧のバサ・ハネに戻す。
        if (mode is "l227" or "h228" or "g229")
            benches = benches.Select(b => (b.Item1, Common.OldGale(b.Item2))).ToArray();
        // 第232期 前段: セロ（移動の追撃）・シオ（退避5割）が規定で変わったので、全モードの台の規定のセロ・シオを第231期の姿へ戻す（上の固定の後に掛ける）。
        if (mode != "s232") benches = benches.Select(b => (b.Item1, Common.OldTune(b.Item2))).ToArray();
        benches = benches.Select(b => (b.Item1, Common.OldSpring2(b.Item2))).ToArray();   // 第232期（S2 の規定化）: 全モードの規定のハネを第232期の S0 へ
        benches = benches.Select(b => (b.Item1, Common.OldFire(b.Item2))).ToArray();   // 第239期（ボルグ・ヒヨの規定化）: 全モードの規定のボルグ・ヒヨを第238期の規定へ
        benches = benches.Select(b => (b.Item1, Common.OldArrow(b.Item2))).ToArray();   // 第256期（セロの状態の矢を外した）: 全モードの規定のセロを第255期の規定へ
        // 第230期 前段: 転倒の穴が既定になったので、それより前の期の台は穴なしで回す（w230 だけが今の既定）。
        ShufflerRule? digestRule = mode is "w230" or "a231" or "s232" ? null : ShufflerRule.PreHole;
        PropertyInfo[] props = typeof(BattleEvent).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        long total = 0;
        foreach (var (name, f) in benches)
            for (int st = 0; st < 5; st++)
                for (int s = 0; s < 20; s++)
                {
                    BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, s, verbose: true, shuffler: digestRule, ember: EmberRule.Pre256);
                    ulong h = 1469598103934665603UL;
                    int counted = 0;
                    foreach (BattleEvent e in r.Events)
                    {
                        if (SkipAlways.Contains(e.Kind.ToString())) continue;
                        if (mode is not ("f224" or "r225" or "k226" or "l227" or "h228" or "g229" or "w230") && SkipKinds.Contains(e.Kind.ToString())) continue;
                        counted++;
                        var sb = new StringBuilder();
                        foreach (PropertyInfo p in props)
                        {
                            if (SkipProps.Contains(p.Name) && !(mode is ("f224" or "r225" or "k226" or "l227" or "h228" or "g229" or "w230") && p.Name == "PartnerId")) continue;
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
