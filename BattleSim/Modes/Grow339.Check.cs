using System.Reflection;
using BattleCore;
using static Common;
using T323 = Tou323Diag;

// grow339 の自己検査（第339期）。盤を直に組み、ミサの羽の一振り（`FeatherVolley`・非公開）を反射で呼ぶ（`T323.Ctx`）。
static partial class Grow339Diag
{
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static readonly UnitDef Dummy = new()
    {
        Id = "dummy339", Name = "木偶", MaxHp = 5000, Attack = 1, Speed = 1, Pattern = AttackPattern.Single, Traits = Array.Empty<TraitId>(),
        PlusText = "", MinusText = "", Flavor = "",
    };
    static readonly Formation Foes = Formation.Build(front1: Dummy, front3: Dummy, center: Dummy, back1: Dummy, back3: Dummy);
    static readonly MethodInfo VolleyM = typeof(BattleContext).GetMethod("FeatherVolley", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("FeatherVolley が見つからない");

    /// <summary>ミサ（後1）＋ 壁（前1・乱射の的になる味方）。敵は木偶 5 体。</summary>
    static (BattleContext Ctx, UnitState Misa, List<UnitState> E) Board(UnitDef misa)
    {
        var ctx = T323.Ctx(Formation.Build(front1: T323.Wall, back1: misa), Foes, out var p, out var e);
        return (ctx, T323.Seat(p, 3), Enumerable.Range(0, 5).Select(i => T323.Seat(e, i)).ToList());
    }
    static void Swing(BattleContext ctx, UnitState u, int turn) { T323.TurnProp.SetValue(ctx, turn); VolleyM.Invoke(ctx, new object[] { u }); }
    static int Fe(UnitState u) => FeathersTrait.Count(u);

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# grow339 check —— 第339期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 版の定義と試遊の台
        {
            UnitDef M = UnitCatalog.TomeH339, A = UnitCatalog.TomeFGa, B = UnitCatalog.TomeFGb;   // 第340期: 版は第334〜339期の規定のミサから作り直した
            var baseT = M.Traits.Where(t => t != TraitId.FeatherLoss).ToArray();
            bool def = M.Traits.Contains(TraitId.FeatherLoss) && A.Traits.SequenceEqual(baseT.Append(TraitId.FeatherGrow)) && B.Traits.SequenceEqual(baseT.Append(TraitId.FeatherGrowUnmarked))
                && A.PlusText == M.PlusText && B.PlusText == M.PlusText
                && A.MinusText == "羽は毎ターン3枚ずつ増えていく。追う標が無い羽は、敵味方構わず飛ぶ。味方が指差されても、撃つ"
                && B.MinusText == "標を追えない間、羽は毎ターン3枚ずつ増えていく。追う標が無い羽は、敵味方構わず飛ぶ。味方が指差されても、撃つ"
                && !UnitCatalog.Everyone.Contains(A) && !UnitCatalog.Everyone.Contains(B)
                && UnitCatalog.Everyone.All(d => !d.Traits.Contains(TraitId.FeatherGrow))
                && UnitCatalog.Everyone.Where(d => d.Traits.Contains(TraitId.FeatherGrowUnmarked)).SequenceEqual(new[] { UnitCatalog.Tome });   // 第340期: FG-b は規定のミサの札
            Expect("版の定義: FG-a ／ FG-b ＝ 規定のミサ − `FeatherLoss` ＋ 札1枚（マイナスの文面は叩き台・「戻ってこない」を外した）・`Everyone` の外・保持者 0", def);
            // 第340期: FG-b を規定にしたので「版・FG」の3台を外した——1行目（ポンの台）と、FG の版が試遊の台に残っていないことだけを見る
            var v = Presets.Versions;
            bool rows = v.Length >= 1 && v[0].Name == "試遊・感電 ポンの台" && Presets.Playtest.Length == 17
                && v.SelectMany(r => r.F.Occupied()).All(o => !ReferenceEquals(o.Def, A) && !ReferenceEquals(o.Def, B));
            Expect("試遊の台: `Presets.Versions` の1行目は「試遊・感電 ポンの台」・第339期の「版・FG」の3台は第340期に外した・`Playtest` は 17 台のまま", rows);
        }

        // ---- FG-a: 手番の頭に 3 枚・標の有無を問わない・乱射しても減らない・1ラウンドに1度
        {
            var (ctx, m, e) = Board(UnitCatalog.TomeFGa);
            int f0 = Fe(m);
            Swing(ctx, m, 1); int f1 = Fe(m);
            Swing(ctx, m, 1); int f1b = Fe(m);   // 同じラウンドの2回目（再行動）
            long sprayed = T323.Tal(ctx, "tome").FeatherSprayed;
            e[0].SetCounter(StatusKeys.Marked, 2);
            Swing(ctx, m, 2); int f2 = Fe(m);
            Expect("FG-a: 手番の頭に羽が 3 枚増える（標が無くても ／ 有っても）・乱射しても減らない・同じラウンドの2回目の一振りでは増えない", f0 == 1 && f1 == 4 && f1b == 4 && f2 == 7 && sprayed >= 4 + 4 && T323.Tal(ctx, "tome").Fg339Grows == 2,
                $"羽 {f0} → {f1} → {f1b}（同じラウンド）→ {f2}（標あり）・乱射 {sprayed}");
        }

        // ---- FG-b: 敵に標持ちがいないときだけ 3 枚・乱射しても減らない
        {
            var (ctx, m, e) = Board(UnitCatalog.TomeFGb);
            Swing(ctx, m, 1); int f1 = Fe(m);
            e[0].SetCounter(StatusKeys.Marked, 3);
            Swing(ctx, m, 2); int f2 = Fe(m);
            e[0].SetCounter(StatusKeys.Marked, 0);
            Swing(ctx, m, 3); int f3 = Fe(m);
            Expect("FG-b: 敵に標持ちがいないときだけ 3 枚増える（標があるターンは増えない）・乱射しても減らない", f1 == 4 && f2 == 4 && f3 == 7 && T323.Tal(ctx, "tome").Fg339Grows == 2,
                $"羽 1 → {f1}（標なし）→ {f2}（標あり）→ {f3}（標なし）");
        }

        // ---- 規定のミサ（対照）: 増えない・乱射した数だけ減る（下限 1）
        {
            var (ctx, m, _) = Board(UnitCatalog.TomeH339);   // 第340期: 第334〜339期の規定のミサ
            m.SetCounter(FeathersTrait.ExtraKey, 3);
            Swing(ctx, m, 1);
            Expect("規定のミサ（対照）: 羽は増えず、乱射した数だけ失う（4 → 1）", Fe(m) == 1 && T323.Tal(ctx, "tome").Fg339Grows == 0, $"羽 4 → {Fe(m)}");
        }

        // ---- 部隊戦の境界で初期に戻る（各札の OnCarryOver）
        {
            bool ok = true; string note = "";
            foreach (var d in new[] { UnitCatalog.TomeFGa, UnitCatalog.TomeFGb })
            {
                var (ctx, m, _) = Board(d);
                Swing(ctx, m, 1); Swing(ctx, m, 2);
                int before = Fe(m);
                foreach (var t in m.Traits) t.OnCarryOver(m);
                ok &= before == 7 && Fe(m) == 1 && m.RawCounter(FeatherGrowTrait.RoundKey) == 0;
                note += $"{S307Short(d)} {before} → {Fe(m)}・";
            }
            Expect("部隊戦の境界: 羽は初期（1 枚）に戻り、ラウンドの帳簿も捨てる", ok, note.TrimEnd('・'));
        }

        // ---- 1戦の台本（書き手なしの台 × FG-a × 近衛 × seed 0）: 一振りの見出しの羽の数が毎ラウンド +3
        {
            var f = Apply(NoWriterBoard(), Vers[1]);
            var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), Waves[1].Make(), 0, verbose: true);
            var grows = r.Events.Where(x => x.Kind == BattleEventKind.Feather && x.Text == FeatherLabels.Gain && x.Slot == FeatherGrowTrait.Gain).ToList();
            var heads = r.Events.Where(x => x.Kind == BattleEventKind.Feather && x.Text == FeatherLabels.Volley).Select(x => x.Amount).ToList();
            bool ok = grows.Count > 0 && heads.Count > 0 && heads.Zip(heads.Skip(1)).All(p => p.Second == p.First + FeatherGrowTrait.Gain || p.Second == p.First) && heads[0] == 1 + FeatherGrowTrait.Gain;
            Expect("1戦の台本: 一振りの見出しの羽の数は 4 → 7 → 10 …（毎ラウンド +3・同じラウンドの再行動は据え置き・乱射で減らない）・増えた瞬間は `Feather`「増えた」（Slot 3）", ok, $"見出し {string.Join(",", heads.Take(6))}・増えた {grows.Count} 件");
        }

        // ---- verbose の有無で勝敗 ／ 決着T が同じ（代表台 5 × ボス ／ 近衛 ／ 大隊 × seed 0..9 × 規定 ＋ 2 版）
        {
            int diff = 0, n = 0;
            foreach (var v in Vers)
                foreach (var (_, f) in Boards())
                    foreach (var w in new[] { Waves[0], Waves[1], Waves[2] })
                        for (int sd = 0; sd < 10; sd++)
                        {
                            var fv = Apply(f, v);
                            var r1 = BattleEngine.Run(BattleEngine.Materialize(fv, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                            var r0 = BattleEngine.Run(BattleEngine.Materialize(fv, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                            n++;
                            if (r1.PlayerWon != r0.PlayerWon || r1.Turns != r0.Turns) diff++;
                        }
            Expect($"代表台 5 × ボス ／ 近衛 ／ 大隊 × seed 0..9 × 規定 ＋ 2 版（{n} 戦）: verbose の有無で勝敗 ／ 決着T が同じ", diff == 0, $"違い {diff}");
        }

        // ---- 乱数を新たに引いていない
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            int engine = Now("BattleCore/BattleEngine.cs"), traits = Now("BattleCore/Traits.cs");
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show b9ee80c7:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int he = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, ht = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第338期（b9ee80c7）と同じ）", engine == he && traits == ht && he > 0, $"engine {he} → {engine}・traits {ht} → {traits}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }

    static string S307Short(UnitDef d) => d == UnitCatalog.TomeFGa ? "FG-a" : "FG-b";
}
