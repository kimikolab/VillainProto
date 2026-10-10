using BattleCore;
using static Common;

// =====================================================================================
// rally296 —— 第296期「ヒサ HK-b の規定化 ＋ 標軸の試遊の準備」。指示書は design/PHASE296_HISA_RALLY_PLAYTEST_SPEC.md ／ 報告は design/PHASE296_HISA_RALLY_PLAYTEST.md。
//
//     dotnet run --project BattleSim -c Release 0 rally296 rates        # Phase 0 §2: 試遊・標の5台 × 本編（第1〜5波）／ 近衛 ／ 大隊 ／ ボス規定形 × seed 0..49 の勝率（目安）
//     dotnet run --project BattleSim -c Release 0 rally296 events       # §3: 同じ台 × 試遊の波 × seed 0..49 の出来事の件数（見切り・あいつを狙え・仇討ち・返り血・味方の刃）
//     dotnet run --project BattleSim -c Release 0 rally296 memo <行名の一部> <boss|guard|bat> <seed> [T から] [T まで]   # §3: 台本の並び（全件）
//     dotnet run --project BattleSim -c Release 0 rally296 check        # 自己検査
//
// 駒・札・数値・波は段0（規定のヒサ ＝ HK-b）のほかは1つも変えない（器具だけ）。試遊の波と行は `EnemyCatalog.PlaytestStages` ／ `Presets.Playtest` を引くだけ。
// =====================================================================================
static class Rally296Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "rates";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        switch (mode)
        {
            case "rates": Rates(); return;
            case "events": Events(); return;
            case "memo": Memo(A(3, "循環"), A(4, "boss"), int.Parse(A(5, "0")), int.Parse(A(6, "0")), int.Parse(A(7, "99"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("rally296: モードは rates / events / memo / check。"); return;
        }
    }

    /// <summary>試遊・標の行（既存の2行 ＋ 第296期の3行）。規定の駒のまま（固定しない）。</summary>
    static (string Name, Formation F)[] MarkRows => Presets.Playtest.Where(r => r.Name.StartsWith("試遊・標", StringComparison.Ordinal)).Select(r => (r.Name, Pin299(FvSwap(r.F, UnitCatalog.Doha, UnitCatalog.DohaD0)))).ToArray();   // 第298期: ドハは旧（`DohaD0`）に固定・第299期: ミサ ／ ザン ／ ヒサも（`Pin299`）
    static readonly string[] NewRows = { "試遊・標 循環", "試遊・標 三人組", "試遊・標 守り型" };
    static EnemyCatalog.PlaytestStage WaveOf(string n) => EnemyCatalog.PlaytestStages[n switch { "boss" => 0, "guard" => 1, "bat" => 2, _ => int.Parse(n) }];
    static string Short(UnitDef d) { var m = System.Text.RegularExpressions.Regex.Match(d.Name, @"[ァ-ヴー]+$"); return m.Success ? m.Value : d.Name; }
    static string Seats(Formation f) => string.Join("・", Enumerable.Range(0, 5).Select(i => f[i] is { } d ? Short(d) : "—"));
    const int Seeds = 50;

    /// <summary>波の一覧（本編 第1〜5波 ＝ `compare` と同じ倍率 ／ 試遊の波 ＝ 波ごとの倍率）。</summary>
    static (string Name, Func<List<UnitState>> Enemy)[] Waves()
    {
        var l = new List<(string, Func<List<UnitState>>)>();
        for (int i = 0; i < EnemyCatalog.Stages.Count; i++) { int ii = i; l.Add(($"第{i + 1}波", () => BattleEngine.Materialize(EnemyCatalog.Stages[ii].Enemy, BattleContext.EnemyTeam))); }
        foreach (int i in new[] { 1, 2, 0 }) { var w = EnemyCatalog.PlaytestStages[i]; l.Add((w.Name, () => BattleEngine.MaterializeEnemy(w.Enemy, w.Scale))); }
        return l.ToArray();
    }

    // ---------------------------------------------------------------------------------
    // Phase 0 §2: 目安の勝率
    // ---------------------------------------------------------------------------------
    static void Rates()
    {
        var waves = Waves();
        Console.WriteLine($"# 第296期 試遊・標の目安 —— 試遊・標の {MarkRows.Length} 台 × 本編 ／ 精鋭 ／ ボス（seed 0..{Seeds - 1}・規定の駒・ヒサは HK-b）");
        Console.WriteLine();
        Console.WriteLine("勝率（倒しT ＝ 勝った戦の平均の決着ターン）。本編は `compare` と同じ倍率（115 ／ 115）、試遊の波は波ごとの倍率（精鋭 1000 ／ 300・ボス 素の値）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 席（前1・前3・中央・後1・後3） | " + string.Join(" | ", waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(waves.Select(_ => "--:|")));
        foreach (var (name, f) in MarkRows)
        {
            var cells = waves.Select(w =>
            {
                var res = new (bool W, int T)[Seeds];
                Parallel.For(0, Seeds, s =>
                {
                    var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Enemy(), s, verbose: false);
                    res[s] = (r.PlayerWon, r.Turns);
                });
                int wins = res.Count(x => x.W);
                return $"{100.0 * wins / Seeds:F0}%（{(wins == 0 ? "—" : res.Where(x => x.W).Average(x => x.T).ToString("F1"))}）";
            }).ToArray();
            Console.WriteLine($"| {name}{(NewRows.Contains(name) ? "（新）" : "")} | {Seats(f)} | {string.Join(" | ", cells)} |");
        }
    }

    // ---------------------------------------------------------------------------------
    // §3: 出来事の件数（1戦あたり）
    // ---------------------------------------------------------------------------------
    static void Events()
    {
        Console.WriteLine($"# 第296期 出来事の件数 —— 試遊・標の {MarkRows.Length} 台 × 試遊の波（seed 0..{Seeds - 1}・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("- 見切り ＝ `Insight`・叫び ＝ 攻撃のひとまとまりごとの `MarkRally` の束の数（同じ `PartnerId`・同じターンで続く件を1つに）・回復 ＝ `MarkRally` の件数（うち癒えた量 0 ＝ `Heal` を伴わない件）");
        Console.WriteLine("- 仇討ち ＝ ザンが敵に入れた `Reaction` の `Damage`（**仇討ちは `Attack` を出さない**）・返り血 ＝ ザンが自分に入れた `Damage`（`ActorId` ＝ `TargetId` ＝ ザン・`Reaction`・`FriendlyFire`）");
        Console.WriteLine("- 味方の刃 ＝ ミサが味方に入れた `Damage`（`FriendlyFire`・中継を除く）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | 勝率 | 見切り | 叫び | 回復（うち 0） | 回復量 | 仇討ち | 返り血 | 味方の刃（回 ／ 量） |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach (var (name, f) in MarkRows)
            foreach (var w in EnemyCatalog.PlaytestStages)
            {
                long wins = 0, insight = 0, shout = 0, rally = 0, zeroRally = 0, rallyAmt = 0, vend = 0, self = 0, ff = 0, ffAmt = 0;
                for (int s = 0; s < Seeds; s++)
                {
                    var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                    var r = BattleEngine.Run(p, BattleEngine.MaterializeEnemy(w.Enemy, w.Scale), s, verbose: true);
                    if (r.PlayerWon) wins++;
                    var zan = p.FirstOrDefault(u => u.Def.Id == "zan")?.InstanceId;
                    var misa = p.FirstOrDefault(u => u.Def.Id == "tome")?.InstanceId;
                    int lastRally = -10;
                    for (int i = 0; i < r.Events.Count; i++)
                    {
                        var x = r.Events[i];
                        switch (x.Kind)
                        {
                            case BattleEventKind.Insight: insight++; break;
                            case BattleEventKind.MarkRally:
                                rally++; rallyAmt += x.Amount;
                                // 叫びの切れ目: 2件前までに同じ攻撃の主・同じターンの `MarkRally` が無ければ新しい叫び（癒えた量 0 の `MarkRally` は `Heal` を伴わない）
                                bool cont = lastRally >= i - 2 && r.Events[lastRally].PartnerId == x.PartnerId && r.Events[lastRally].Turn == x.Turn;
                                if (!cont) shout++;
                                if (x.Amount == 0) zeroRally++;
                                lastRally = i;
                                break;
                            case BattleEventKind.Damage when x.ActorId == zan && x.Reaction && x.TargetId != zan && !x.FriendlyFire: vend++; break;   // 仇討ちは `Attack` を出さない（`Reaction` の `Damage` だけ）
                            case BattleEventKind.Damage when x.ActorId == zan && x.TargetId == zan: self++; break;
                            case BattleEventKind.Damage when x.ActorId == misa && x.FriendlyFire && !x.Relayed: ff++; ffAmt += x.Amount; break;
                        }
                    }
                }
                double P(long n) => (double)n / Seeds;
                Console.WriteLine($"| {name} | {w.Name} | {100.0 * wins / Seeds:F0}% | {P(insight):F2} | {P(shout):F2} | {P(rally):F2}（{P(zeroRally):F2}） | {P(rallyAmt):F1} | {P(vend):F2} | {P(self):F2} | {P(ff):F2} ／ {P(ffAmt):F1} |");
            }
    }

    // ---------------------------------------------------------------------------------
    // §3: 台本の並び（全件）
    // ---------------------------------------------------------------------------------
    static void Memo(string rowPart, string wave, int seed, int fromT, int toT)
    {
        var (name, f) = MarkRows.First(r => r.Name.Contains(rowPart, StringComparison.Ordinal));
        var w = WaveOf(wave);
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = BattleEngine.MaterializeEnemy(w.Enemy, w.Scale);
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"#{i}") : "—";
        Console.WriteLine($"# rally296 memo —— {name}（{Seats(f)}）× {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}"
            + $"（`Insight` {r.Events.Count(x => x.Kind == BattleEventKind.Insight)} 件・`MarkRally` {r.Events.Count(x => x.Kind == BattleEventKind.MarkRally)} 件）");
        Console.WriteLine();
        Console.WriteLine("```");
        int lastT = -1;
        for (int j = 0; j < r.Events.Count; j++)
        {
            var x = r.Events[j];
            if (x.Turn < fromT || x.Turn > toT) continue;
            if (x.Turn != lastT) { Console.WriteLine($"--- T{x.Turn}"); lastT = x.Turn; }
            string other = string.Join("  ", new[]
            {
                x.PartnerId is int pa ? $"Partner={N(pa)}" : null, x.StatusRemaining is int sr ? $"rem={sr}" : null, x.Reaction ? "Reaction" : null,
                x.FriendlyFire ? "ff" : null, x.Relayed ? "relayed" : null, x.Pattern is { } pt ? $"{pt}" : null, x.SourceTrait is { } st ? $"src={st}" : null,
                x.Kind is BattleEventKind.Damage or BattleEventKind.Heal or BattleEventKind.MarkRally ? $"hp={x.HpAfter}" : null,
            }.Where(q => q is not null));
            Console.WriteLine($"#{j,-4} {x.Kind} {x.Text}  {N(x.ActorId)} → {N(x.TargetId)}  Amount {x.Amount}  Slot {x.Slot}  {other}".TrimEnd());
        }
        Console.WriteLine("```");
        Console.WriteLine();
        Console.WriteLine("ログ（同じ範囲）:");
        Console.WriteLine();
        Console.WriteLine("```");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
        Console.WriteLine("```");
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
    // ---------------------------------------------------------------------------------
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# rally296 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        // 第300期: 規定のヒサに `BeckonFeather`（矢面は羽も半分）を足した——ここでは第299期の規定（`HisaHKs`）を見る。
        var hisa = UnitCatalog.HisaHKs; var hk0 = UnitCatalog.HisaHK0;
        // 第299期: 規定のヒサは HK-b ＋ `MarkRallySelf`（叫びは自分も癒す）になった。HK-b そのものは `HisaHKb`。
        Expect("(a) 規定のヒサ ＝ HK-b の札（旧の規定 `HisaHK0` ＋ `MarkRallyWide`）＋ 第299期の `MarkRallySelf`・`HisaHKb` ＋ `MarkRallySelf` と同じ札・数値と手番は旧と同じ",
            hisa.Traits.SequenceEqual(hk0.Traits.Append(TraitId.MarkRallyWide).Append(TraitId.MarkRallySelf)) && hisa.Traits.SequenceEqual(UnitCatalog.HisaHKb.Traits.Append(TraitId.MarkRallySelf))
            && hk0.Traits.SequenceEqual(new[] { TraitId.Beckon, TraitId.Flee })
            && hisa.MaxHp == hk0.MaxHp && hisa.Attack == hk0.Attack && hisa.Speed == hk0.Speed && hisa.Pattern == hk0.Pattern && ReferenceEquals(hisa.Actions, hk0.Actions));
        Expect("(b) 文面: プラスの末尾が指示書の文・フレーバーは今のまま・マイナスは旧と同じ",
            hisa.PlusText == hk0.PlusText + "。指差された敵が攻撃されるたび、『あいつを狙え！ まだ倒れるな！』と叫んで、攻撃した味方と最も傷ついた味方を癒す"
            && hisa.Flavor == "味方を矢面に立たせて生き延びた男。誰も隣に立ちたがらない。" && hisa.MinusText == hk0.MinusText);
        Expect("(c) `HisaHK0` と版（HS ／ HK）は `All` ／ `Retired` の外・規定のヒサは `All`・版は旧の規定から作る",
            !new[] { hk0, UnitCatalog.HisaHKa, UnitCatalog.HisaHKb, UnitCatalog.HisaHSa, UnitCatalog.HisaHSa1, UnitCatalog.HisaHSc, UnitCatalog.HisaHSd }.Any(UnitCatalog.Everyone.Contains)
            && UnitCatalog.All.Contains(UnitCatalog.Hisa)
            && UnitCatalog.HisaHKa.Traits.SequenceEqual(hk0.Traits.Append(TraitId.MarkRally)) && UnitCatalog.HisaHSa.Traits.SequenceEqual(hk0.Traits.Append(TraitId.BeckonHold))
            && UnitCatalog.HisaHSa.PlusText.StartsWith(hk0.PlusText + "。標を付けられた", StringComparison.Ordinal));
        var heal = Boss283Diag.HealPool306; var heal0 = Boss283Diag.HealPool295;   // 第308期: 規定のソム（SH-a）を数えない第307期までの一覧に固定
        Expect("(d) ヒーラーの一覧（`Boss283Diag.HealPool`）に規定のヒサが入る・旧の一覧（`HealPool295`）は入らない・ほかの顔ぶれは同じ",
            heal.Contains(UnitCatalog.Hisa) && !heal0.Contains(UnitCatalog.Hisa) && heal.Where(d => !ReferenceEquals(d, UnitCatalog.Hisa)).SequenceEqual(heal0), $"{heal.Length} 枚 ／ 旧 {heal0.Length} 枚");
        var pl = Presets.Playtest.Take(8).ToArray();   // 第309期: 試遊の末尾に2行（光の盾）を足した——第308期までの8行で見る
        var compareNames = Presets.Compare.Select(r => r.Name).ToHashSet();
        Expect("(e) 試遊の行は8行（第291期の5行 ＋ 3行・既存の5行は名前も並びもそのまま）・`Compare` ／ `Cross` に入っていない・駒はすべて規定（`All`）・5枠",
            pl.Length == 8 && pl.Take(5).Select(r => r.Name).SequenceEqual(new[] { "試遊・感電 火の型", "試遊・感電 雷の型", "試遊・感電 糸", "試遊・標 ボス台", "試遊・標 道中" })
            && pl.Skip(5).Select(r => r.Name).SequenceEqual(NewRows) && Presets.Compare.Length == 64 && Presets.Cross.Length == 12
            && pl.All(r => !compareNames.Contains(r.Name) && !Presets.Cross.Any(c => c.Name == r.Name))
            && pl.All(r => r.F.Occupied().Count() == 5 && r.F.Occupied().All(o => UnitCatalog.All.Contains(o.Def))));
        Formation RowRaw(string n) => FvSwap(pl.First(r => r.Name == n).F, UnitCatalog.Doha, UnitCatalog.DohaD0);   // 第298期: ドハは旧に固定
        Formation Row(string n) => Pin299(RowRaw(n));   // 第299期: ミサ ／ ザン ／ ヒサも第298期の規定に
        var boss = Row("試遊・標 ボス台");
        Expect("(f) 循環 ＝ 試遊・標 ボス台の 後1 バン → ソラ（席はそのまま）・三人組 ＝ 循環の ソラ → ドルガ・守り型 ＝ ゴルム・ザン・ドハ・ミサ・ヒサ",
            Row("試遊・標 循環").Occupied().SequenceEqual(FvSwap(boss, UnitCatalog.Ban, UnitCatalog.SoraSRs).Occupied())   // 第301期: `Pin299` がソラを `SoraSRs` に替える
            && Row("試遊・標 三人組").Occupied().SequenceEqual(FvSwap(Row("試遊・標 循環"), UnitCatalog.SoraSRs, UnitCatalog.Dolga).Occupied())
            && Seats(Row("試遊・標 守り型")) == "ゴルム・ザン・ドハ・ミサ・ヒサ", $"{Seats(Row("試遊・標 循環"))} ／ {Seats(Row("試遊・標 三人組"))} ／ {Seats(Row("試遊・標 守り型"))}");
        // (g) 第295期の測定との一致: 循環の台 × ボス × seed 0..199 が HK-b で 100%（第295期 §4-2）・守り型 × 近衛が 100%・倒しT 3.0（§4-4）
        int Wins(Formation f, EnemyCatalog.PlaytestStage w, out double t)
        {
            var res = new (bool W, int T)[200];
            Parallel.For(0, 200, s => { var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), BattleEngine.MaterializeEnemy(w.Enemy, w.Scale), s, verbose: false); res[s] = (r.PlayerWon, r.Turns); });
            t = res.Where(x => x.W).Select(x => (double)x.T).DefaultIfEmpty(0).Average();
            return res.Count(x => x.W);
        }
        int cyc = Wins(Row("試遊・標 循環"), EnemyCatalog.PlaytestStages[0], out _);
        int cyc0 = Wins(MarkHeal295Diag.Pin296(RowRaw("試遊・標 循環")), EnemyCatalog.PlaytestStages[0], out _);
        int trio = Wins(Row("試遊・標 三人組"), EnemyCatalog.PlaytestStages[0], out _);
        int guard = Wins(Row("試遊・標 守り型"), EnemyCatalog.PlaytestStages[1], out double gt);
        Expect("(g) 第295期の測定と一致: 循環 × ボス（seed 0..199）HK-b 100.0% ／ 旧の規定 47.0%・三人組 × ボス 100.0%（§4-2 の対照）・守り型 × 近衛 100.0%・倒しT 3.0（§4-4）",
            cyc == 200 && cyc0 == 94 && trio == 200 && guard == 200 && Math.Abs(gt - 3.0) < 1e-9, $"{cyc / 2.0:F1} ／ {cyc0 / 2.0:F1} ／ {trio / 2.0:F1} ／ {guard / 2.0:F1}（{gt:F2}）");
        // (h) 表示専用の出来事は台本の外の盤面を変えない: verbose の有無で勝敗とターンが同じ（新しい3台 × 試遊の波 × seed 0..19）
        int diff = 0, n = 0;
        foreach (var name in NewRows) foreach (var w in EnemyCatalog.PlaytestStages) for (int s = 0; s < 20; s++)
                {
                    var a = BattleEngine.Run(BattleEngine.Materialize(Row(name), BattleContext.PlayerTeam), BattleEngine.MaterializeEnemy(w.Enemy, w.Scale), s, verbose: false);
                    var b = BattleEngine.Run(BattleEngine.Materialize(Row(name), BattleContext.PlayerTeam), BattleEngine.MaterializeEnemy(w.Enemy, w.Scale), s, verbose: true);
                    n++; if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns) diff++;
                }
        Expect("(h) verbose の有無で勝敗・決着T が同じ（新しい3台 × 試遊の波 × seed 0..19）", diff == 0, $"{n} 戦・違い {diff}");
        // (i) 新しい台で `MarkRally` と `Insight` が出る（演出の対象）・三人組（ソラなし）では `Insight` が出ない
        long Count(string name, int wi, BattleEventKind k) { long c = 0; for (int s = 0; s < 10; s++) { var w = EnemyCatalog.PlaytestStages[wi]; c += BattleEngine.Run(BattleEngine.Materialize(Row(name), BattleContext.PlayerTeam), BattleEngine.MaterializeEnemy(w.Enemy, w.Scale), s, verbose: true).Events.Count(x => x.Kind == k); } return c; }
        long cr = Count("試遊・標 循環", 0, BattleEventKind.MarkRally), ci = Count("試遊・標 循環", 0, BattleEventKind.Insight), tr = Count("試遊・標 三人組", 1, BattleEventKind.MarkRally), ti = Count("試遊・標 三人組", 1, BattleEventKind.Insight);
        Expect("(i) 循環 × ボスで `MarkRally` ／ `Insight` が出る・三人組 × 近衛で `MarkRally` が出て `Insight` は 0（seed 0..9）", cr > 0 && ci > 0 && tr > 0 && ti == 0, $"{cr} ／ {ci} ／ {tr} ／ {ti}");
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "すべて ○" : $"**× {_fail} 件**");
    }
}
